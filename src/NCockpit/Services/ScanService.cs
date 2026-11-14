using System.Text.Json;
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
    private readonly ISupportingFolderRepository _supportingFolderRepository;

    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] SkipDirectories =
    [
        ".git", "bin", "obj", "node_modules", "packages",
        ".vs", ".idea", ".gradle", "dist", "build"
    ];

    /// <summary>
    /// Common root-level files worth quickly opening from the project details page.
    /// Checked in order; first match per name wins.
    /// </summary>
    private static readonly string[] KnownRootFiles =
    [
        "AGENTS.md",
        "README.md",
        "README",
        "README.txt",
        "LICENSE",
        "LICENSE.md",
        "LICENSE.txt",
        "CONTRIBUTING.md",
        "CONTRIBUTING",
        "CHANGELOG.md",
        "CHANGELOG",
        "SECURITY.md",
        "CODE_OF_CONDUCT.md"
    ];

    public ScanService(IProjectFolderRepository folderRepository, IProjectRepository projectRepository, ISupportingFolderRepository supportingFolderRepository)
    {
        _folderRepository = folderRepository;
        _projectRepository = projectRepository;
        _supportingFolderRepository = supportingFolderRepository;
    }

    public async Task ScanAsync(int? folderId, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(new ScanProgress { Status = "Preparing scan...", Percentage = 0 });

        List<ProjectFolder> folders = folderId.HasValue
            ? await GetTargetFolderAsync(folderId.Value)
            : await _folderRepository.GetAllAsync();

        // Only primary folders are scanned for projects; secondary (supporting)
        // containers never are.
        folders = folders.Where(f => f.IsPrimary).ToList();

        var scanUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int totalFolders = Math.Max(folders.Count, 1);
        int projectsFound = 0;

        if (folders.Count == 0)
        {
            // Nothing to scan (no primary folders are configured); do not
            // prune, nothing new was discovered anyway.
            progress?.Report(new ScanProgress
            {
                Status = "Scan complete. No primary folders to scan.",
                Percentage = 100,
                ProjectsFound = 0
            });
            return;
        }

        // Registered supporting folders are not scanned, even when a primary
        // folder contains them.
        _skipPaths = (await _supportingFolderRepository.GetAllAsync())
            .Select(f => System.IO.Path.GetFullPath(f.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                var projectPath = GetProjectRootPath(solution);
                await _projectRepository.UpsertAsync(new Project
                {
                    Name = System.IO.Path.GetFileNameWithoutExtension(solution),
                    SolutionPath = solution,
                    FolderPath = projectPath,
                    FolderId = folder.Id,
                    LastSeenUnix = scanUnix,
                    ScanUnix = scanUnix,
                    SolutionLastWriteUnix = GetSolutionLastWriteUnix(solution),
                    FilesJson = DiscoverRootFilesJson(projectPath)
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

    private HashSet<string> _skipPaths = [];

    /// <summary>
    /// Recursively walks a folder and returns the full paths of all .sln / .slnx files found.
    /// Registered supporting folders are skipped entirely.
    /// </summary>
    private Task<List<string>> ScanFolderAsync(string root, CancellationToken cancellationToken)
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

                if (_skipPaths.Contains(directory))
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

    /// <summary>
    /// Discovers common root files in the project folder and returns them as a JSON
    /// array of file names. Missing or inaccessible files are silently ignored so
    /// a missing optional file never fails the scan.
    /// </summary>
    private static string? DiscoverRootFilesJson(string projectPath)
    {
        try
        {
            var found = new List<string>();
            foreach (var fileName in KnownRootFiles)
            {
                if (found.Count > 0 && SameBaseName(fileName, found[^1]))
                {
                    // Already recorded a variant of this file (e.g. README and README.md);
                    // prefer the first one seen in the priority list.
                    continue;
                }

                if (File.Exists(System.IO.Path.Combine(projectPath, fileName)))
                {
                    found.Add(fileName);
                }
            }

            return found.Count > 0 ? JsonSerializer.Serialize(found) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// True when two file names share the same base name (ignoring extension).
    /// </summary>
    private static bool SameBaseName(string a, string b)
    {
        var baseA = System.IO.Path.GetFileNameWithoutExtension(a) ?? a;
        var baseB = System.IO.Path.GetFileNameWithoutExtension(b) ?? b;
        return string.Equals(baseA, baseB, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the project's root folder: the folder containing the solution
    /// file, unless that folder is named "src", in which case the parent folder
    /// is returned.
    /// </summary>
    private static string GetProjectRootPath(string solutionPath)
    {
        try
        {
            var solutionFolder = System.IO.Path.GetDirectoryName(solutionPath)!;
            var folderName = System.IO.Path.GetFileName(solutionFolder);

            if (string.Equals(folderName, "src", StringComparison.OrdinalIgnoreCase))
            {
                return System.IO.Path.GetDirectoryName(solutionFolder)!;
            }

            return solutionFolder;
        }
        catch
        {
            return System.IO.Path.GetDirectoryName(solutionPath)!;
        }
    }
}

