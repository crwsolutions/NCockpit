using Dapper;
using NCockpit.Models;
using Microsoft.Data.Sqlite;

namespace NCockpit.Repositories;

/// <summary>
/// Repository for ProjectFolder database operations. Primary folders are
/// scanned for projects; secondary (supporting) folders are listed but never
/// scanned.
/// </summary>
public interface IProjectFolderRepository
{
    Task<List<ProjectFolder>> GetAllAsync();
    Task<ProjectFolder?> GetByIdAsync(int id);
    Task<ProjectFolder> UpsertAsync(ProjectFolder folder);
    Task DeleteAsync(int id);
}

public class ProjectFolderRepository : IProjectFolderRepository
{
    private readonly string _dbPath;

    public ProjectFolderRepository()
    {
        _dbPath = Database.DbPath;
    }

    public async Task<List<ProjectFolder>> GetAllAsync()
    {
        using var connection = OpenConnection();
        const string sql = """
            SELECT Id, Path, IsPrimary, IsEnabled, ProjectCount, LastScanUnix
            FROM ProjectFolders
            ORDER BY Path
            """;
        return (await connection.QueryAsync<ProjectFolder>(sql)).ToList();
    }

    public async Task<ProjectFolder?> GetByIdAsync(int id)
    {
        using var connection = OpenConnection();
        const string sql = """
            SELECT Id, Path, IsPrimary, IsEnabled, ProjectCount, LastScanUnix
            FROM ProjectFolders
            WHERE Id = @Id
            """;
        return await connection.QueryFirstOrDefaultAsync<ProjectFolder>(sql, new { Id = id });
    }

    public async Task<ProjectFolder> UpsertAsync(ProjectFolder folder)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        if (folder.Id == 0)
        {
            const string insertSql = """
                INSERT INTO ProjectFolders (Path, IsPrimary, IsEnabled, ProjectCount, LastScanUnix, CreatedAt)
                VALUES (@Path, @IsPrimary, @IsEnabled, @ProjectCount, @LastScanUnix, @CreatedAt)
                ON CONFLICT (Path) DO UPDATE SET
                    IsPrimary = excluded.IsPrimary,
                    IsEnabled = excluded.IsEnabled;
                SELECT last_insert_rowid();
                """;
            folder.Id = (int)(long)await connection.ExecuteScalarAsync(insertSql, new
            {
                folder.Path,
                IsPrimary = folder.IsPrimary ? 1 : 0,
                IsEnabled = folder.IsEnabled ? 1 : 0,
                folder.ProjectCount,
                folder.LastScanUnix,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        else
        {
            const string updateSql = """
                UPDATE ProjectFolders
                SET Path = @Path, IsPrimary = @IsPrimary, IsEnabled = @IsEnabled, 
                    ProjectCount = @ProjectCount, LastScanUnix = @LastScanUnix
                WHERE Id = @Id;
                """;
            await connection.ExecuteAsync(updateSql, new
            {
                folder.Id,
                folder.Path,
                IsPrimary = folder.IsPrimary ? 1 : 0,
                IsEnabled = folder.IsEnabled ? 1 : 0,
                folder.ProjectCount,
                folder.LastScanUnix
            });
        }

        return folder;
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = OpenConnection();
        const string sql = "DELETE FROM ProjectFolders WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    private SqliteConnection OpenConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
