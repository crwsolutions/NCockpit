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
    /// Returns all active projects ordered by recent usage (most recently used first).
    /// Projects that have never been opened fall back to their last scan time.
    /// </summary>
    Task<List<Project>> GetAllAsync();

    /// <summary>
    /// Insert or update a project keyed by its solution path (case-insensitive).
    /// If the project already exists it is re-activated (RemovedAtUnix reset to null).
    /// </summary>
    Task UpsertAsync(Project project);

    /// <summary>
    /// Prune projects that were not seen during the current scan (LastSeenUnix &lt; scanUnix)
    /// and were not already removed.
    /// </summary>
    Task<int> PruneAsync(long scanUnix);

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
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        const string sql = """
            SELECT Id, Name, SolutionPath, FolderPath, LastSeenUnix, ScanUnix, RemovedAtUnix,
                   IsFavorite, LastUsedUnix, SolutionLastWriteUnix
            FROM Projects
            WHERE RemovedAtUnix IS NULL
            ORDER BY LastUsedUnix IS NULL, COALESCE(LastUsedUnix, 0) DESC,
                     SolutionLastWriteUnix IS NULL, COALESCE(SolutionLastWriteUnix, 0) DESC,
                     Name
            """;
        return (await connection.QueryAsync<Project>(sql)).ToList();
    }

    public async Task UpsertAsync(Project project)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO Projects (Name, SolutionPath, FolderPath, LastSeenUnix, ScanUnix, RemovedAtUnix, CreatedAt, SolutionLastWriteUnix)
            VALUES (@Name, @SolutionPath, @FolderPath, @LastSeenUnix, @ScanUnix, NULL, @CreatedAt, @SolutionLastWriteUnix)
            ON CONFLICT (SolutionPath) DO UPDATE SET
                Name = excluded.Name,
                FolderPath = excluded.FolderPath,
                LastSeenUnix = excluded.LastSeenUnix,
                ScanUnix = excluded.ScanUnix,
                RemovedAtUnix = NULL,
                SolutionLastWriteUnix = excluded.SolutionLastWriteUnix;
            """;

        await connection.ExecuteAsync(sql, new
        {
            project.Name,
            project.SolutionPath,
            project.FolderPath,
            project.LastSeenUnix,
            project.ScanUnix,
            CreatedAt = project.LastSeenUnix,
            project.SolutionLastWriteUnix
        });
    }

    public async Task<int> PruneAsync(long scanUnix)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        const string sql = """
            UPDATE Projects
            SET RemovedAtUnix = @RemovedAtUnix
            WHERE (RemovedAtUnix IS NULL OR LastSeenUnix < @ScanUnix)
              AND LastSeenUnix < @ScanUnix;
            """;

        return await connection.ExecuteAsync(sql, new
        {
            ScanUnix = scanUnix,
            RemovedAtUnix = scanUnix
        });
    }

    public async Task TouchAsync(int id)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
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
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        const string sql = """
            UPDATE Projects
            SET IsFavorite = @IsFavorite
            WHERE Id = @Id;
            """;

        await connection.ExecuteAsync(sql, new { Id = id, IsFavorite = isFavorite });
    }
}
