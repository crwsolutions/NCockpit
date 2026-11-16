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
    /// Re-read the on-disk metadata for a single project (solution last-write
    /// time, root files, git remote, referenced solution projects) and persist
    /// it. The filesystem is the source of truth; this is a lightweight refresh
    /// that does not walk the folder tree.
    /// </summary>
    Task<Project> RefreshProjectAsync(Project project);
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
        project.SolutionLastWriteUnix = ProjectMetadataDiscovery.GetSolutionLastWriteUnix(project.SolutionPath);
        project.FilesJson = ProjectMetadataDiscovery.DiscoverRootFilesJson(project.FolderPath);
        project.GitRepoUrl = ProjectMetadataDiscovery.GetGitRepoUrl(project.FolderPath);
        project.ProjectsJson = await ProjectMetadataDiscovery.DiscoverSolutionProjectsJsonAsync(project.SolutionPath);

        // The refresh does not change discovery state; keep the original
        // LastSeenUnix/ScanUnix so the project is not pruned by the next scan.
        await _repository.UpsertAsync(project);

        return project;
    }
}
