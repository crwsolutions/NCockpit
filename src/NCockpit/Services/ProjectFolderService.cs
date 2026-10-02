using NCockpit.Models;
using NCockpit.Repositories;

namespace NCockpit.Services;

/// <summary>
/// Service for managing project folders — the primary folders that NCockpit
/// scans for projects. Supporting (secondary) folders are handled by
/// <see cref="ISupportingFolderService"/>.
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
    /// Add a new folder to the folders list. Primary folders are scanned for
    /// projects; secondary folders are listed but never scanned (their
    /// first-level subfolders are supporting folders).
    /// </summary>
    public async Task<ProjectFolder> AddFolderAsync(string path, bool isPrimary)
    {
        var normalizedPath = NormalizeAndVerifyPath(path, nameof(path));
        return await _repository.UpsertAsync(CreateFolder(normalizedPath, isPrimary));
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

    /// <summary>
    /// Validate a folder path (non-empty, existing on disk) and return it in
    /// full (normalized) form.
    /// </summary>
    private static string NormalizeAndVerifyPath(string path, string parameterName)
    {
        var normalizedPath = NormalizePath(path, parameterName);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {normalizedPath}");
        }

        return normalizedPath;
    }

    private static string NormalizePath(string path, string parameterName = "path")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private static ProjectFolder CreateFolder(string path, bool isPrimary) => new()
    {
        Path = path,
        IsPrimary = isPrimary,
        IsEnabled = true,
        ProjectCount = 0,
        LastScanUnix = null
    };
}
