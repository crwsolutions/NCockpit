using Dapper;
using Microsoft.Data.Sqlite;
using NCockpit.Models;

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
    /// Insert or update a project keyed by its folder path. Returns the row id.
    /// The project is a folder (a repo root or a legacy solution folder);
    /// a re-scan from a different folder re-links the row to that folder.
    /// </summary>
    Task<int> UpsertAsync(Project project);

    /// <summary>
    /// Merge projects with the given ids into the target project. The user
    /// state (favorite flag, last used) of the merged rows is preserved when
    /// the target does not already have it, and the merged rows are deleted.
    /// Used when a repo-first scan takes over projects that were previously
    /// keyed by solution path.
    /// </summary>
    Task MergeIntoAsync(int targetId, IEnumerable<int> sourceIds);

    /// <summary>
    /// Record that a specific solution of a project was just opened. Used to
    /// re-derive the primary solution on the next scan.
    /// </summary>
    Task TouchSolutionAsync(int id, long unix);

    /// <summary>
    /// Hard-delete projects in the given folders that were not seen during the
    /// current scan run. Projects in other folders are left alone so that a
    /// partial scan does not delete projects it did not look for.
    /// Returns the number of deleted rows.
    /// </summary>
    Task<int> PruneAsync(long scanUnix, IEnumerable<int> folderIds);

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
            SELECT Id, Name, Type, SolutionPath, FolderPath, FolderId, LastSeenUnix, ScanUnix,
                   IsFavorite, LastUsedUnix, SolutionLastWriteUnix, FilesJson, GitRepoUrl,
                   ProjectsJson, SolutionsJson, SolutionLastUsedUnix
            FROM Projects
            ORDER BY LastUsedUnix IS NULL, COALESCE(LastUsedUnix, 0) DESC,
                     SolutionLastWriteUnix IS NULL, COALESCE(SolutionLastWriteUnix, 0) DESC,
                     Name
            """;

        return (await connection.QueryAsync<Project>(sql)).ToList();
    }

    public async Task<int> UpsertAsync(Project project)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO Projects (Name, Type, SolutionPath, FolderPath, FolderId, LastSeenUnix, ScanUnix, CreatedAt, SolutionLastWriteUnix, FilesJson, GitRepoUrl, ProjectsJson, SolutionsJson, SolutionLastUsedUnix)
            VALUES (@Name, @Type, @SolutionPath, @FolderPath, @FolderId, @LastSeenUnix, @ScanUnix, @CreatedAt, @SolutionLastWriteUnix, @FilesJson, @GitRepoUrl, @ProjectsJson, @SolutionsJson, @SolutionLastUsedUnix)
            ON CONFLICT (FolderPath) DO UPDATE SET
                Name = excluded.Name,
                Type = excluded.Type,
                SolutionPath = excluded.SolutionPath,
                FolderId = excluded.FolderId,
                LastSeenUnix = excluded.LastSeenUnix,
                ScanUnix = excluded.ScanUnix,
                SolutionLastWriteUnix = excluded.SolutionLastWriteUnix,
                FilesJson = excluded.FilesJson,
                GitRepoUrl = excluded.GitRepoUrl,
                ProjectsJson = excluded.ProjectsJson,
                SolutionsJson = excluded.SolutionsJson,
                SolutionLastUsedUnix = COALESCE(excluded.SolutionLastUsedUnix, Projects.SolutionLastUsedUnix);
            """;

        var parameters = new
        {
            project.Name,
            project.Type,
            project.SolutionPath,
            project.FolderPath,
            project.FolderId,
            project.LastSeenUnix,
            project.ScanUnix,
            CreatedAt = project.LastSeenUnix,
            project.SolutionLastWriteUnix,
            project.FilesJson,
            project.GitRepoUrl,
            project.ProjectsJson,
            project.SolutionsJson,
            project.SolutionLastUsedUnix
        };

        // INSERT ... ON CONFLICT returns the row id via last_insert_rowid()
        // for both new and updated rows.
        var id = Convert.ToInt32(await connection.ExecuteScalarAsync(
            $"{sql} SELECT last_insert_rowid();", parameters));

        project.Id = id;
        return id;
    }

    public async Task MergeIntoAsync(int targetId, IEnumerable<int> sourceIds)
    {
        var ids = sourceIds.Where(id => id != targetId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        using var connection = OpenConnection();
        await connection.OpenAsync();

        // Merge user state into the target when it does not have it yet.
        const string mergeSql = """
            UPDATE Projects
            SET IsFavorite = MAX(
                    (SELECT MAX(IsFavorite) FROM Projects WHERE Id = @TargetId),
                    (SELECT MAX(IsFavorite) FROM Projects WHERE Id IN @SourceIds)),
                LastUsedUnix = COALESCE(
                    (SELECT MAX(LastUsedUnix) FROM Projects WHERE Id = @TargetId),
                    (SELECT MAX(LastUsedUnix) FROM Projects WHERE Id IN @SourceIds))
            WHERE Id = @TargetId;
            """;

        await connection.ExecuteAsync(mergeSql, new { TargetId = targetId, SourceIds = ids });

        const string deleteSql = """
            DELETE FROM Projects
            WHERE Id IN @Ids;
            """;

        await connection.ExecuteAsync(deleteSql, new { Ids = ids });
    }

    public async Task TouchSolutionAsync(int id, long unix)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE Projects
            SET SolutionLastUsedUnix = @SolutionLastUsedUnix
            WHERE Id = @Id;
            """;

        await connection.ExecuteAsync(sql, new
        {
            Id = id,
            SolutionLastUsedUnix = unix
        });
    }

    public async Task<int> PruneAsync(long scanUnix, IEnumerable<int> folderIds)
    {
        var ids = folderIds.ToList();
        if (ids.Count == 0)
        {
            return 0;
        }

        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM Projects
            WHERE FolderId IN @FolderIds
              AND LastSeenUnix < @ScanUnix;
            """;

        return await connection.ExecuteAsync(sql, new { FolderIds = ids, ScanUnix = scanUnix });
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
