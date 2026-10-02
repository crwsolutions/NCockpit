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
}
