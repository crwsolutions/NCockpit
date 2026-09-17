using NCockpit.Models;
using NCockpit.Repositories;

namespace NCockpit.Services;

/// <summary>
/// Reports progress for a running scan.
/// </summary>
public class ScanProgress
{
    public required string Status { get; init; }
    public int Percentage { get; init; }
    public int ProjectsFound { get; init; }
}

/// <summary>
/// Scans folders for projects (folders containing a .sln or .slnx file) and
/// persists the results to the database.
/// </summary>
public interface IScanService
{
    Task ScanAsync(int? folderId, IProgress<ScanProgress>? progress, CancellationToken cancellationToken);
}

public class ScanService : IScanService
{
    private readonly IProjectFolderRepository _folderRepository;
    private readonly IProjectRepository _projectRepository;

    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] SkipDirectories =
    [
        ".git", "bin", "obj", "node_modules", "packages",
        ".vs", ".idea", ".gradle", "dist", "build"
    ];

    public ScanService(IProjectFolderRepository folderRepository, IProjectRepository projectRepository)
    {
        _folderRepository = folderRepository;
        _projectRepository = projectRepository;
    }

    public async Task ScanAsync(int? folderId, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress { Status = "Preparing scan...", Percentage = 0 });

        List<ProjectFolder> folders = folderId.HasValue
            ? await GetTargetFolderAsync(folderId.Value)
            : await _folderRepository.GetAllAsync();

        var scanUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int totalFolders = Math.Max(folders.Count, 1);
        int projectsFound = 0;

        for (int i = 0; i < folders.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var folder = folders[i];
            progress?.Report(new ScanProgress
            {
                Status = $"Scanning \"{GetFolderName(folder.Path)}\" ({i + 1}/{folders.Count})...",
                Percentage = (int)((i / (double)totalFolders) * 100),
                ProjectsFound = projectsFound
            });

            var solutions = await ScanFolderAsync(folder.Path, cancellationToken);
            projectsFound += solutions.Count;

            foreach (var solution in solutions)
            {
                await _projectRepository.UpsertAsync(new Project
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(solution),
                    SolutionPath = solution,
                    FolderPath = folder.Path,
                    LastSeenUnix = scanUnix,
                    ScanUnix = scanUnix,
                    SolutionLastWriteUnix = GetSolutionLastWriteUnix(solution)
                });
            }
        }

        if (folderId.HasValue)
        {
            await UpdateFolderScanStatsAsync(folderId.Value, projectsFound, scanUnix);
        }
        else
        {
            // Global scan: merge the discovered projects and prune ones that no longer exist.
            await _projectRepository.PruneAsync(scanUnix);
        }

        progress?.Report(new ScanProgress
        {
            Status = "Scan complete.",
            Percentage = 100,
            ProjectsFound = projectsFound
        });
    }

    private async Task<List<ProjectFolder>> GetTargetFolderAsync(int folderId)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId)
            ?? throw new InvalidOperationException("Folder not found.");
        return [folder];
    }

    private async Task UpdateFolderScanStatsAsync(int folderId, int projectCount, long scanUnix)
    {
        var folder = await _folderRepository.GetByIdAsync(folderId);
        if (folder is null)
            return;

        folder.ProjectCount = projectCount;
        folder.LastScanUnix = scanUnix;
        await _folderRepository.UpsertAsync(folder);
    }

    /// <summary>
    /// Recursively walks a folder and returns the full paths of all .sln / .slnx files found.
    /// </summary>
    private static Task<List<string>> ScanFolderAsync(string root, CancellationToken cancellationToken)
    {
        var found = new List<string>();

        var enumeration = new Stack<string>();
        enumeration.Push(root);

        while (enumeration.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = enumeration.Pop();

            List<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToList();
            }
            catch (System.UnauthorizedAccessException) { continue; }
            catch (System.IO.IOException) { continue; }

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
            }
            catch (System.UnauthorizedAccessException) { continue; }
            catch (System.IO.IOException) { continue; }

            foreach (var file in files)
            {
                if (SolutionExtensions.Contains(System.IO.Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    found.Add(file);
                }
            }

            foreach (var directory in directories)
            {
                if (SkipDirectories.Contains(System.IO.Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase))
                    continue;

                enumeration.Push(directory);
            }

            // Yield to keep the progress indicator responsive during long walks.
            Task.Yield();
        }

        return Task.FromResult(found);
    }

    /// <summary>
    /// Returns the solution file's last write time as Unix seconds, or null if the
    /// file cannot be accessed. Missing metadata must not fail the scan.
    /// </summary>
    private static long? GetSolutionLastWriteUnix(string path)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(path);
            return (long)(lastWrite - System.DateTimeOffset.UnixEpoch).TotalSeconds;
        }
        catch (System.UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    private static string GetFolderName(string path)
    {
        try
        {
            var trimmed = path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            return System.IO.Path.GetFileName(trimmed) is { Length: > 0 } name ? name : path;
        }
        catch
        {
            return path;
        }
    }
}

