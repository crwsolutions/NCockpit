using Dapper;
using Microsoft.Data.Sqlite;
using NCockpit.Models;

namespace NCockpit.Repositories;

/// <summary>
/// Repository for related-project links — the manually configured relationships
/// between two projects shown in the "Related projects" panel of the project
/// details page. A link is an edge from one project to another; deleting either
/// project removes its links (the foreign keys cascade).
/// </summary>
public interface IRelatedProjectRepository
{
    /// <summary>
    /// The related projects linked to the given project, in either direction
    /// (the relationship is symmetric), ordered by name.
    /// </summary>
    Task<List<Project>> GetRelatedAsync(int projectId);

    /// <summary>
    /// Add a link between two projects. A link that already exists is kept as
    /// is.
    /// </summary>
    Task AddLinkAsync(int projectId, int relatedProjectId);

    /// <summary>
    /// Remove the link between two projects, whatever direction it was stored
    /// in.
    /// </summary>
    Task RemoveLinkAsync(int projectId, int relatedProjectId);
}

public class RelatedProjectRepository : IRelatedProjectRepository
{
    private readonly string _dbPath;

    public RelatedProjectRepository()
    {
        _dbPath = Database.DbPath;
    }

    public async Task<List<Project>> GetRelatedAsync(int projectId)
    {
        using var connection = OpenConnection();
        const string sql = """
            SELECT p.Id, p.Name, p.Type, p.SolutionPath, p.FolderPath, p.FolderId, p.LastSeenUnix, p.ScanUnix,
                   p.IsFavorite, p.LastUsedUnix, p.SolutionLastWriteUnix, p.FilesJson, p.GitRepoUrl,
                   p.ProjectsJson, p.SolutionsJson, p.SolutionLastUsedUnix,
                   p.SolutionProjectsJson, p.OrphanProjectsJson,
                   p.FavoriteSolutionPath, p.FavoriteProjectPath
            FROM RelatedProjects r
            JOIN Projects p ON p.Id = CASE WHEN r.ProjectId = @ProjectId THEN r.RelatedProjectId ELSE r.ProjectId END
            WHERE r.ProjectId = @ProjectId OR r.RelatedProjectId = @ProjectId
            ORDER BY p.Name
            """;
        return (await connection.QueryAsync<Project>(sql, new { ProjectId = projectId })).ToList();
    }

    public async Task AddLinkAsync(int projectId, int relatedProjectId)
    {
        if (projectId == relatedProjectId)
        {
            return;
        }

        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            INSERT OR IGNORE INTO RelatedProjects (ProjectId, RelatedProjectId, CreatedAt)
            VALUES (@ProjectId, @RelatedProjectId, @CreatedAt);
            """;
        await connection.ExecuteAsync(sql, new
        {
            ProjectId = projectId,
            RelatedProjectId = relatedProjectId,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
    }

    public async Task RemoveLinkAsync(int projectId, int relatedProjectId)
    {
        using var connection = OpenConnection();
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM RelatedProjects
            WHERE (ProjectId = @ProjectId AND RelatedProjectId = @RelatedProjectId)
               OR (ProjectId = @RelatedProjectId AND RelatedProjectId = @ProjectId);
            """;
        await connection.ExecuteAsync(sql, new
        {
            ProjectId = projectId,
            RelatedProjectId = relatedProjectId
        });
    }

    private SqliteConnection OpenConnection()
    {
        return new SqliteConnection($"Data Source={_dbPath}");
    }
}
