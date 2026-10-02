using NCockpit.Models;
using NCockpit.Repositories;

namespace NCockpit.Services;

/// <summary>
/// Service for managing supporting (secondary) folders — path-only records,
/// e.g. materials that must not end up in a project's source control.
/// Supporting folders are never scanned and never appear in the project
/// overview.
/// </summary>
public interface ISupportingFolderService
{
    Task<List<SupportingFolder>> GetAllAsync();

    Task<SupportingFolder?> GetByIdAsync(int id);

    /// <summary>
    /// Number of supporting folders registered under the given container
    /// folder.
    /// </summary>
    Task<int> CountByFolderAsync(int folderId);

    /// <summary>
    /// All supporting folders that are not linked to a project yet, ordered by
    /// path.
    /// </summary>
    Task<List<SupportingFolder>> GetUnlinkedAsync();

    /// <summary>
    /// All supporting folders linked to the given project, ordered by path.
    /// </summary>
    Task<List<SupportingFolder>> GetLinkedToProjectAsync(int projectId);

    /// <summary>
    /// Add every first-level subdirectory of the given container path as a new
    /// supporting folder (e.g. materials that must not end up in a project's
    /// source control). The container path itself is not added. Subdirectories
    /// that are already configured as supporting folders are skipped.
    /// </summary>
    Task<List<SupportingFolder>> AddFromContainerAsync(int folderId, string containerPath);

    /// <summary>
    /// Link a supporting folder to a project.
    /// </summary>
    Task LinkToProjectAsync(int supportingFolderId, int projectId);

    /// <summary>
    /// Remove the link between a supporting folder and its project. The folder
    /// itself is kept.
    /// </summary>
    Task UnlinkFromProjectAsync(int supportingFolderId);

    /// <summary>
    /// Delete a supporting folder entirely.
    /// </summary>
    Task DeleteAsync(int supportingFolderId);
}

public class SupportingFolderService : ISupportingFolderService
{
    private readonly ISupportingFolderRepository _repository;

    public SupportingFolderService(ISupportingFolderRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<SupportingFolder>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<SupportingFolder?> GetByIdAsync(int id)
    {
        return await _repository.GetByIdAsync(id);
    }

    public async Task<int> CountByFolderAsync(int folderId)
    {
        return await _repository.CountByFolderAsync(folderId);
    }

    public async Task<List<SupportingFolder>> GetUnlinkedAsync()
    {
        return (await _repository.GetAllAsync())
            .Where(f => f.ProjectId is null)
            .ToList();
    }

    public async Task<List<SupportingFolder>> GetLinkedToProjectAsync(int projectId)
    {
        return (await _repository.GetAllAsync())
            .Where(f => f.ProjectId == projectId)
            .ToList();
    }

    public async Task<List<SupportingFolder>> AddFromContainerAsync(int folderId, string containerPath)
    {
        var normalizedPath = NormalizeAndVerifyPath(containerPath, nameof(containerPath));

        string[] subDirectories;
        try
        {
            subDirectories = Directory.GetDirectories(normalizedPath);
        }
        catch (System.UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Cannot read the contents of this folder.", null);
        }

        var existingPaths = (await _repository.GetAllAsync())
            .Select(f => NormalizePath(f.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = new List<SupportingFolder>();
        foreach (var subDirectory in subDirectories)
        {
            var subPath = NormalizePath(subDirectory);
            if (existingPaths.Contains(subPath))
            {
                // Skip folders that are already configured as supporting folders.
                continue;
            }

            added.Add(await _repository.UpsertAsync(new SupportingFolder { Path = subPath, FolderId = folderId }));
        }

        return added;
    }

    public async Task LinkToProjectAsync(int supportingFolderId, int projectId)
    {
        var folder = await _repository.GetByIdAsync(supportingFolderId)
            ?? throw new InvalidOperationException("Supporting folder not found.");

        if (folder.ProjectId == projectId)
        {
            return;
        }

        folder.ProjectId = projectId;
        await _repository.UpsertAsync(folder);
    }

    public async Task UnlinkFromProjectAsync(int supportingFolderId)
    {
        var folder = await _repository.GetByIdAsync(supportingFolderId)
            ?? throw new InvalidOperationException("Supporting folder not found.");

        if (folder.ProjectId is null)
        {
            return;
        }

        await _repository.UnlinkFromProjectAsync(supportingFolderId);
    }

    public async Task DeleteAsync(int supportingFolderId)
    {
        await _repository.DeleteAsync(supportingFolderId);
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
}
