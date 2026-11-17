using System.Text.Json;
using NCockpit.Models;
using NCockpit.Repositories;

namespace NCockpit.Services;

/// <summary>
/// Service for reading project data and recording user actions on projects
/// (opening and favoriting). Projects are ordered by recent usage.
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Returns all projects ordered by recent usage (most recently used first).
    /// Supporting folders are not projects, so no filtering is needed.
    /// </summary>
    Task<List<Project>> GetProjectsAsync();

    /// <summary>
    /// Toggle the favorite flag for a project and return the new state.
    /// </summary>
    Task<bool> ToggleFavoriteAsync(int id);

    /// <summary>
    /// Record that a project was just opened/viewed, used for recent-usage ordering.
    /// </summary>
    Task TouchAsync(int id);

    /// <summary>
    /// Re-read the on-disk metadata for a single project (every solution with
    /// its projects, the orphan project files, the solution last-write time,
    /// the root files and the git remote) and persist it. Stale favorite
    /// choices (a file that no longer exists) are re-guessed so one always
    /// exists. The filesystem is the source of truth; this is a lightweight
    /// refresh that does not walk the folder tree beyond the project root.
    /// </summary>
    Task<Project> RefreshProjectAsync(Project project);

    /// <summary>
    /// Set the user's favorite solution (full path) for a project. The invariant
    /// is that exactly one favorite solution always exists: passing null
    /// re-establishes one via the smart guess. Returns the resulting favorite
    /// solution path.
    /// </summary>
    Task<string> SetFavoriteSolutionAsync(int projectId, string? solutionPath);

    /// <summary>
    /// Set the user's favorite project (full path) for a project. The invariant
    /// is that exactly one favorite project always exists: passing null
    /// re-establishes one via the smart guess. Returns the resulting favorite
    /// project path.
    /// </summary>
    Task<string?> SetFavoriteProjectAsync(int projectId, string? projectPath);
}

public class ProjectService : IProjectService
{
    private readonly IProjectRepository _repository;

    public ProjectService(IProjectRepository repository)
    {
        _repository = repository;
    }

    public Task<List<Project>> GetProjectsAsync()
    {
        return _repository.GetAllAsync();
    }

    public async Task<bool> ToggleFavoriteAsync(int id)
    {
        var projects = await _repository.GetAllAsync();
        var project = projects.FirstOrDefault(p => p.Id == id);
        if (project is null)
        {
            return false;
        }

        var newValue = !project.IsFavorite;
        await _repository.SetFavoriteAsync(id, newValue);
        return newValue;
    }

    public Task TouchAsync(int id)
    {
        return _repository.TouchAsync(id);
    }

    public async Task<Project> RefreshProjectAsync(Project project)
    {
        var solutions = ParseSolutionPaths(project.SolutionsJson);

        if (solutions.Count > 0)
        {
            // Re-read every solution (not just the favorite one) and the orphan
            // project files. The walk is bounded to the project root and is
            // resilient by design.
            var withProjectsJson = await ProjectMetadataDiscovery.DiscoverSolutionProjectsJsonAsync(
                solutions);
            var withProjects = DeserializeSolutionProjects(withProjectsJson);

            var orphanProjectsJson = await ProjectMetadataDiscovery.DiscoverOrphanProjectsJsonAsync(
                project.FolderPath, withProjects);

            // Re-resolve the favorites against the current on-disk state: a
            // stale choice (a file that no longer exists) falls back to the
            // smart guess so one always exists.
            var favoriteSolution = ScanService.ResolveFavoriteSolution(
                project.FavoriteSolutionPath,
                project.Name,
                solutions,
                withProjects,
                project.SolutionLastUsedUnix,
                project.SolutionPath);

            project.SolutionPath = favoriteSolution;
            project.SolutionLastWriteUnix = ProjectMetadataDiscovery.GetSolutionLastWriteUnix(favoriteSolution);
            project.SolutionProjectsJson = withProjectsJson;
            project.OrphanProjectsJson = orphanProjectsJson;

            var favoriteProject = ResolveFavoriteProject(project, withProjects);
            if (favoriteProject is not null)
            {
                project.FavoriteProjectPath = favoriteProject.ProjectPath;
            }
        }
        else
        {
            // Folder type: no solution file to read metadata from.
            project.SolutionLastWriteUnix = null;
            project.SolutionProjectsJson = null;
            project.OrphanProjectsJson = null;
            project.FavoriteProjectPath = null;
        }

        project.FilesJson = ProjectMetadataDiscovery.DiscoverRootFilesJson(project.FolderPath);
        project.GitRepoUrl = ProjectMetadataDiscovery.GetGitRepoUrl(project.FolderPath);

        // The refresh does not change discovery state; keep the original
        // LastSeenUnix/ScanUnix so the project is not pruned by the next scan.
        await _repository.UpsertAsync(project);

        return project;
    }

    public async Task<string> SetFavoriteSolutionAsync(int projectId, string? solutionPath)
    {
        var project = (await _repository.GetAllAsync()).FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Project not found.");

        var solutions = ParseSolutionPaths(project.SolutionsJson);

        // An invalid choice (a path that is not one of the project's solutions)
        // falls back to the smart guess.
        if (solutionPath is not null && !solutions.Contains(solutionPath, StringComparer.OrdinalIgnoreCase))
        {
            solutionPath = null;
        }

        var withProjects = DeserializeSolutionProjects(project.SolutionProjectsJson);
        var favorite = ScanService.ResolveFavoriteSolution(
            solutionPath, project.Name, solutions, withProjects,
            project.SolutionLastUsedUnix, project.SolutionPath);

        await _repository.SetFavoriteSolutionAsync(projectId, favorite);

        // Keep the in-memory model in sync for the caller.
        project.FavoriteSolutionPath = favorite;
        project.SolutionPath = favorite;

        return favorite;
    }

    public async Task<string?> SetFavoriteProjectAsync(int projectId, string? projectPath)
    {
        var project = (await _repository.GetAllAsync()).FirstOrDefault(p => p.Id == projectId)
            ?? throw new InvalidOperationException("Project not found.");

        var withProjects = DeserializeSolutionProjects(project.SolutionProjectsJson);

        // The favorite project must be one of the projects referenced by a
        // solution (an orphan project can be favorited as well).
        var allProjects = withProjects
            .SelectMany(s => s.Projects)
            .Concat(DeserializeOrphanProjects(project.OrphanProjectsJson))
            .ToList();

        if (projectPath is not null
            && !allProjects.Any(p => string.Equals(p.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase)))
        {
            projectPath = null;
        }

        var favoriteProject = ScanService.ResolveFavoriteProject(
            projectPath, project.Name,
            project.SolutionPath is { Length: > 0 } s
                ? System.IO.Path.GetFileNameWithoutExtension(s)
                : null,
            allProjects);

        var favoritePath = favoriteProject?.ProjectPath;
        await _repository.SetFavoriteProjectAsync(projectId, favoritePath);

        project.FavoriteProjectPath = favoritePath;

        return favoritePath;
    }

    private static SolutionProject? ResolveFavoriteProject(
        Project project,
        IReadOnlyList<SolutionWithProjects> withProjects)
    {
        var allProjects = withProjects
            .SelectMany(s => s.Projects)
            .Concat(DeserializeOrphanProjects(project.OrphanProjectsJson))
            .ToList();

        if (allProjects.Count == 0)
        {
            return null;
        }

        return ScanService.ResolveFavoriteProject(
            project.FavoriteProjectPath,
            project.Name,
            project.SolutionPath is { Length: > 0 } s
                ? System.IO.Path.GetFileNameWithoutExtension(s)
                : null,
            allProjects);
    }

    private static List<string> ParseSolutionPaths(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                ?.Where(p => p.Length > 0)
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<SolutionWithProjects> DeserializeSolutionProjects(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SolutionWithProjects>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static List<SolutionProject> DeserializeOrphanProjects(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SolutionProject>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
