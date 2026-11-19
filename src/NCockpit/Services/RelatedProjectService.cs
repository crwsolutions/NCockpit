using NCockpit.Models;
using NCockpit.Repositories;

namespace NCockpit.Services;

/// <summary>
/// Service for related projects — the manually configured relationships
/// between two projects shown in the "Related projects" panel of the project
/// details page. A related project is always one that lives in a primary
/// (scanned) folder; the relationship does not have to mirror a real
/// project reference (initial.md), it is purely user-managed. The
/// relationship is symmetric: linking A to B shows up on both panels.
/// </summary>
public interface IRelatedProjectService
{
    /// <summary>
    /// The projects related to the given project, in either direction (the
    /// relationship is symmetric), ordered by name.
    /// </summary>
    Task<List<Project>> GetRelatedAsync(int projectId);

    /// <summary>
    /// All projects that live in a primary folder and are not the current
    /// project itself — the candidates the "Add related project" popup lists,
    /// ordered by name.
    /// </summary>
    Task<List<Project>> GetCandidatesAsync(int currentProjectId);

    /// <summary>
    /// Link two projects. The link shows up on both panels, and an existing
    /// link is kept as is.
    /// </summary>
    Task LinkAsync(int projectId, int relatedProjectId);

    /// <summary>
    /// Remove the link between two projects, whatever direction it was stored
    /// in.
    /// </summary>
    Task UnlinkAsync(int projectId, int relatedProjectId);
}

public class RelatedProjectService : IRelatedProjectService
{
    private readonly IRelatedProjectRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectFolderRepository _folderRepository;

    public RelatedProjectService(
        IRelatedProjectRepository repository,
        IProjectRepository projectRepository,
        IProjectFolderRepository folderRepository)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _folderRepository = folderRepository;
    }

    public Task<List<Project>> GetRelatedAsync(int projectId)
    {
        return _repository.GetRelatedAsync(projectId);
    }

    public async Task<List<Project>> GetCandidatesAsync(int currentProjectId)
    {
        var primaryFolderIds = (await _folderRepository.GetAllAsync())
            .Where(f => f.IsPrimary)
            .Select(f => f.Id)
            .ToHashSet();

        return (await _projectRepository.GetAllAsync())
            .Where(p => p.Id != currentProjectId && primaryFolderIds.Contains(p.FolderId))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task LinkAsync(int projectId, int relatedProjectId)
    {
        return _repository.AddLinkAsync(projectId, relatedProjectId);
    }

    public Task UnlinkAsync(int projectId, int relatedProjectId)
    {
        return _repository.RemoveLinkAsync(projectId, relatedProjectId);
    }
}
