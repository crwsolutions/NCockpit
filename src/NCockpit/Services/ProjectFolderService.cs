using NCockpit.Models;
using NCockpit.Repositories;

namespace NCockpit.Services;

/// <summary>
/// Service for managing project folders.
/// </summary>
public interface IProjectFolderService
{
    Task<List<ProjectFolder>> GetAllFoldersAsync();
    Task<ProjectFolder?> GetFolderByIdAsync(int id);
    Task<ProjectFolder> AddFolderAsync(string path, bool isPrimary);
    Task UpdateFolderAsync(ProjectFolder folder);
    Task DeleteFolderAsync(int id);
    Task<bool> FolderExistsAsync(string path);

    /// <summary>
    /// Number of projects currently linked to the given folder. Deleting the
    /// folder removes these projects as well (cascade delete).
    /// </summary>
    Task<int> CountProjectsAsync(int folderId);
}

public class ProjectFolderService : IProjectFolderService
{
    private readonly IProjectFolderRepository _repository;
    private readonly IProjectRepository _projectRepository;

    public ProjectFolderService(IProjectFolderRepository repository, IProjectRepository projectRepository)
    {
        _repository = repository;
        _projectRepository = projectRepository;
    }

    public async Task<List<ProjectFolder>> GetAllFoldersAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<ProjectFolder?> GetFolderByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    /// <summary>
    /// Add a new folder with the specified path and type (primary/secondary).
    /// </summary>
    public async Task<ProjectFolder> AddFolderAsync(string path, bool isPrimary)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty", nameof(path));
        }

        // Normalize the path
        var normalizedPath = Path.GetFullPath(path);

        // Check if folder exists in filesystem
        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");
        }

        var folder = new ProjectFolder
        {
            Path = normalizedPath,
            IsPrimary = isPrimary,
            IsEnabled = true,
            ProjectCount = 0,
            LastScanUnix = null
        };

        return await _repository.UpsertAsync(folder);
    }

    public async Task UpdateFolderAsync(ProjectFolder folder)
    {
        if (folder.Id == 0)
        {
            throw new ArgumentException("Folder must have an Id", nameof(folder));
        }

        await _repository.UpsertAsync(folder);
    }

    public async Task DeleteFolderAsync(int id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<bool> FolderExistsAsync(string path)
    {
        var folders = await _repository.GetAllAsync();
        var normalizedPath = Path.GetFullPath(path);
        return folders.Any(f => f.Path.Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    public Task<int> CountProjectsAsync(int folderId)
    {
        return _projectRepository.CountByFolderAsync(folderId);
    }
}
