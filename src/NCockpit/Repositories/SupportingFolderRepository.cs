using Dapper;
using NCockpit.Models;
using Microsoft.Data.Sqlite;

namespace NCockpit.Repositories;

/// <summary>
/// Repository for SupportingFolder database operations. Supporting folders are
/// path-only records (materials that must not end up in a project's source
/// control); they are never scanned and never appear in the project overview.
/// </summary>
public interface ISupportingFolderRepository
{
    Task<List<SupportingFolder>> GetAllAsync();

    Task<SupportingFolder?> GetByIdAsync(int id);

    /// <summary>
    /// Number of supporting folders registered under the given container
    /// folder.
    /// </summary>
    Task<int> CountByFolderAsync(int folderId);

    /// <summary>
    /// Inserts a new supporting folder, or returns the existing one when the
    /// path is already configured (case-insensitive). When the folder already
    /// has an id, its ProjectId link is updated in place.
    /// </summary>
    Task<SupportingFolder> UpsertAsync(SupportingFolder folder);

    /// <summary>
    /// Removes the link between a supporting folder and its project. The
    /// folder itself is kept.
    /// </summary>
    Task UnlinkFromProjectAsync(int id);

    /// <summary>
    /// Deletes a supporting folder and, through the foreign key constraint,
    /// unlinks it from any project that referenced it.
    /// </summary>
    Task DeleteAsync(int id);
}

public class SupportingFolderRepository : ISupportingFolderRepository
{
    private readonly string _dbPath;

    public SupportingFolderRepository()
    {
        _dbPath = Database.DbPath;
    }

    public async Task<List<SupportingFolder>> GetAllAsync()
    {
        using var connection = OpenConnection();
        const string sql = """
            SELECT Id, Path, FolderId, ProjectId
            FROM SupportingFolders
            ORDER BY Path COLLATE NOCASE
            """;
        return (await connection.QueryAsync<SupportingFolder>(sql)).ToList();
    }

    public async Task<SupportingFolder?> GetByIdAsync(int id)
    {
        using var connection = OpenConnection();
        const string sql = """
            SELECT Id, Path, FolderId, ProjectId
            FROM SupportingFolders
            WHERE Id = @Id
            """;
        return await connection.QueryFirstOrDefaultAsync<SupportingFolder>(sql, new { Id = id });
    }

    public async Task<int> CountByFolderAsync(int folderId)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT COUNT(*)
            FROM SupportingFolders
            WHERE FolderId = @FolderId;
            """;

        return Convert.ToInt32(await connection.ExecuteScalarAsync(sql, new { FolderId = folderId }));
    }

    public async Task<SupportingFolder> UpsertAsync(SupportingFolder folder)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        if (folder.Id == 0)
        {
            // Insert; when the path is already configured (case-insensitive),
            // the insert is skipped and the existing row is returned instead.
            const string insertSql = """
                INSERT INTO SupportingFolders (Path, FolderId, ProjectId, CreatedAt)
                VALUES (@Path, @FolderId, @ProjectId, @CreatedAt)
                ON CONFLICT (Path) DO NOTHING;
                SELECT Id, Path, FolderId, ProjectId FROM SupportingFolders WHERE Path = @Path COLLATE NOCASE;
                """;
            folder = await connection.QuerySingleAsync<SupportingFolder>(insertSql, new
            {
                folder.Path,
                folder.FolderId,
                folder.ProjectId,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
        }
        else
        {
            const string updateSql = """
                UPDATE SupportingFolders
                SET ProjectId = @ProjectId
                WHERE Id = @Id;
                """;
            await connection.ExecuteAsync(updateSql, new { folder.Id, folder.ProjectId });
        }

        return folder;
    }

    public async Task UnlinkFromProjectAsync(int id)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE SupportingFolders
            SET ProjectId = NULL
            WHERE Id = @Id;
            """;
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task DeleteAsync(int id)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        // No cleanup of other tables is needed: the supporting folder's link
        // (SupportingFolders.ProjectId) lives in this row and disappears with it.
        const string sql = "DELETE FROM SupportingFolders WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    private SqliteConnection OpenConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
