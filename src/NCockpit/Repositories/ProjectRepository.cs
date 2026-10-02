using Dapper;
using NCockpit.Models;
using Microsoft.Data.Sqlite;

namespace NCockpit.Repositories;

/// <summary>
/// Repository for Project database operations.
/// </summary>
public interface IProjectRepository
{
    /// <summary>
    /// Returns all projects ordered by recent usage (most recently used first).
    /// Projects that have never been opened fall back to their last scan time.
    /// </summary>
    Task<List<Project>> GetAllAsync();

    /// <summary>
    /// Insert or update a project keyed by its solution path (case-insensitive).
    /// The project is linked to the folder it was discovered in; a re-scan from a
    /// different folder re-links the row to that folder.
    /// </summary>
    Task UpsertAsync(Project project);

    /// <summary>
    /// Hard-delete projects that were not seen during the current scan.
    /// Returns the number of deleted rows.
    /// </summary>
    Task<int> PruneAsync(long scanUnix);

    /// <summary>
    /// Number of projects currently linked to the given folder.
    /// </summary>
    Task<int> CountByFolderAsync(int folderId);

    /// <summary>
    /// Stamp the project as "used now", used for recent-usage ordering.
    /// </summary>
    Task TouchAsync(int id);

    /// <summary>
    /// Set or clear the favorite flag for a project.
    /// </summary>
    Task SetFavoriteAsync(int id, bool isFavorite);
}

public class ProjectRepository : IProjectRepository
{
    private readonly string _dbPath;

    public ProjectRepository()
    {
        _dbPath = Database.DbPath;
    }

    public async Task<List<Project>> GetAllAsync()
    {
        using var connection = OpenConnection();

        const string sql = """
            SELECT Id, Name, SolutionPath, FolderPath, FolderId, LastSeenUnix, ScanUnix,
                   IsFavorite, LastUsedUnix, SolutionLastWriteUnix
            FROM Projects
            ORDER BY LastUsedUnix IS NULL, COALESCE(LastUsedUnix, 0) DESC,
                     SolutionLastWriteUnix IS NULL, COALESCE(SolutionLastWriteUnix, 0) DESC,
                     Name
            """;

        return (await connection.QueryAsync<Project>(sql)).ToList();
    }

    public async Task UpsertAsync(Project project)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO Projects (Name, SolutionPath, FolderPath, FolderId, LastSeenUnix, ScanUnix, CreatedAt, SolutionLastWriteUnix)
            VALUES (@Name, @SolutionPath, @FolderPath, @FolderId, @LastSeenUnix, @ScanUnix, @CreatedAt, @SolutionLastWriteUnix)
            ON CONFLICT (SolutionPath) DO UPDATE SET
                Name = excluded.Name,
                FolderPath = excluded.FolderPath,
                FolderId = excluded.FolderId,
                LastSeenUnix = excluded.LastSeenUnix,
                ScanUnix = excluded.ScanUnix,
                SolutionLastWriteUnix = excluded.SolutionLastWriteUnix;
            """;

        await connection.ExecuteAsync(sql, new
        {
            project.Name,
            project.SolutionPath,
            project.FolderPath,
            project.FolderId,
            project.LastSeenUnix,
            project.ScanUnix,
            CreatedAt = project.LastSeenUnix,
            project.SolutionLastWriteUnix
        });
    }

    public async Task<int> PruneAsync(long scanUnix)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM Projects
            WHERE LastSeenUnix < @ScanUnix;
            """;

        return await connection.ExecuteAsync(sql, new { ScanUnix = scanUnix });
    }

    public async Task<int> CountByFolderAsync(int folderId)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT COUNT(*)
            FROM Projects
            WHERE FolderId = @FolderId;
            """;

        return Convert.ToInt32(await connection.ExecuteScalarAsync(sql, new { FolderId = folderId }));
    }

    public async Task TouchAsync(int id)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE Projects
            SET LastUsedUnix = @LastUsedUnix
            WHERE Id = @Id;
            """;

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            LastUsedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }

    public async Task SetFavoriteAsync(int id, bool isFavorite)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE Projects
            SET IsFavorite = @IsFavorite
            WHERE Id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, IsFavorite = isFavorite });
    }

    private SqliteConnection OpenConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
