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
/// Scans folders for projects and persists the results to the database.
/// A project is a folder: a git repository is the primary rule (its solutions
/// determine the project type), and a solution file found outside any
/// repository becomes a legacy solution-type project.
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

            var discovered = await DiscoverProjectsAsync(folder.Path, cancellationToken);
            projectsFound += discovered.Count;

            foreach (var project in discovered)
            {
                project.FolderId = folder.Id;
                project.LastSeenUnix = scanUnix;
                project.ScanUnix = scanUnix;

                await _projectRepository.UpsertAsync(project);

                // A discovered repo project takes over any legacy rows keyed
                // by a solution path inside the repo (previously one project
                // per solution), preserving their user state instead of
                // pruning them. Repo-level rows (FolderId of a folder scan of
                // the repo root itself) are taken over as well: their folder
                // is the repo root, not inside it.
                var all = await _projectRepository.GetAllAsync();
                var repoRoot = project.FolderPath;
                await _projectRepository.MergeIntoAsync(
                    project.Id,
                    all
                        .Where(p => p.Id != project.Id && p.SolutionPath is not null
                            && (IsInside(repoRoot, p.FolderPath) || IsInside(repoRoot, p.SolutionPath)))
                        .Select(p => p.Id));
            }
        }

        // Prune projects that were not seen during this scan run. Only the
        // scanned folders are pruned: a single-folder scan must not delete
        // projects in folders it did not look for (their LastSeenUnix is from
        // an earlier run and would otherwise be lower than this run's stamp).
        await _projectRepository.PruneAsync(scanUnix, folders.Select(f => f.Id));

        if (folderId.HasValue)
        {
            await UpdateFolderScanStatsAsync(folderId.Value, projectsFound, scanUnix);
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
    /// Recursively walks a folder and discovers the projects in it: every
    /// repository (a folder containing a .git folder) and every orphan
    /// solution file (a .sln / .slnx not inside any repository).
    /// </summary>
    private async Task<List<Project>> DiscoverProjectsAsync(string root, CancellationToken cancellationToken)
    {
        var discovered = new List<Project>();
        var orphanSolutions = new List<string>();
        var solutionsByRepo = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

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

            // A .git folder makes this directory the root of a repository;
            // the repo itself is not walked (it is in SkipDirectories).
            if (directories.Any(d => string.Equals(
                    System.IO.Path.GetFileName(d), ".git", StringComparison.OrdinalIgnoreCase)))
            {
                var solutions = await ScanForSolutionsAsync(current, cancellationToken);
                solutionsByRepo[current] = solutions;
                continue;
            }

            foreach (var file in files)
            {
                if (SolutionExtensions.Contains(System.IO.Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    orphanSolutions.Add(file);
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
            await Task.Yield();
        }

        // One project per repository, keyed by the repo root path.
        foreach (var (repoRoot, solutions) in solutionsByRepo)
        {
            var name = GetFolderName(repoRoot);
            var solutionsJson = ProjectMetadataDiscovery.DiscoverSolutionsJson(solutions);

            long? solutionLastWriteUnix = null;
            string? projectsJson = null;

            // The type is re-derived on every scan: any solution found makes
            // this a solution type project.
            var isSolutionType = solutions.Count > 0;
            var primarySolution = isSolutionType
                ? await SelectPrimarySolutionAsync(repoRoot, solutions, cancellationToken)
                : null;

            if (primarySolution is not null)
            {
                solutionLastWriteUnix = ProjectMetadataDiscovery.GetSolutionLastWriteUnix(primarySolution);
                projectsJson = await ProjectMetadataDiscovery.DiscoverSolutionProjectsJsonAsync(primarySolution);
            }

            discovered.Add(new Project
            {
                Name = name,
                Type = isSolutionType ? ProjectType.Solution : ProjectType.Folder,
                SolutionPath = primarySolution,
                FolderPath = repoRoot,
                SolutionLastWriteUnix = solutionLastWriteUnix,
                FilesJson = ProjectMetadataDiscovery.DiscoverRootFilesJson(repoRoot),
                GitRepoUrl = ProjectMetadataDiscovery.GetGitRepoUrl(repoRoot),
                ProjectsJson = projectsJson,
                SolutionsJson = solutionsJson
            });
        }

        // One legacy project per orphan solution, with the existing root rules.
        foreach (var solution in orphanSolutions)
        {
            var projectRootPath = GetProjectRootPath(solution);
            discovered.Add(new Project
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(solution),
                Type = ProjectType.Solution,
                SolutionPath = solution,
                FolderPath = projectRootPath,
                SolutionLastWriteUnix = ProjectMetadataDiscovery.GetSolutionLastWriteUnix(solution),
                FilesJson = ProjectMetadataDiscovery.DiscoverRootFilesJson(projectRootPath),
                GitRepoUrl = ProjectMetadataDiscovery.GetGitRepoUrl(projectRootPath),
                ProjectsJson = await ProjectMetadataDiscovery.DiscoverSolutionProjectsJsonAsync(solution),
                SolutionsJson = ProjectMetadataDiscovery.DiscoverSolutionsJson([solution])
            });
        }

        return discovered;
    }

    /// <summary>
    /// Walks a repository root and returns the full paths of all .sln / .slnx
    /// files in it. Resilience rules apply: inaccessible entries are skipped
    /// and the subtree of an inner repository (nested clone/submodule) is
    /// skipped entirely — the outer repo wins and its inner repos are neither
    /// projects nor solution sources.
    /// </summary>
    private async Task<List<string>> ScanForSolutionsAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var found = new List<string>();

        var enumeration = new Stack<string>();
        enumeration.Push(repoRoot);

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
                var name = System.IO.Path.GetFileName(directory);

                if (SkipDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                    continue;

                if (_skipPaths.Contains(directory))
                    continue;

                // The subtree of an inner repository is not part of this repo's
                // solutions; the inner repo is its own project (or none at all
                // when it is a submodule).
                if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                    continue;

                enumeration.Push(directory);
            }

            await Task.Yield();
        }

        return found;
    }

    /// <summary>
    /// Chooses the primary solution of a repository: the most recently opened
    /// one when any was opened, otherwise the shallowest path in the repo,
    /// otherwise alphabetical.
    /// </summary>
    private async Task<string> SelectPrimarySolutionAsync(string repoRoot, List<string> solutions, CancellationToken cancellationToken)
    {
        var existing = (await _projectRepository.GetAllAsync())
            .FirstOrDefault(p => string.Equals(p.FolderPath, repoRoot, StringComparison.OrdinalIgnoreCase));

        // The most recently opened solution, if it still exists.
        if (existing is { SolutionLastUsedUnix: not null, SolutionsJson: { Length: > 0 } })
        {
            var lastOpened = JsonSerializer
                .Deserialize<List<string>>(existing.SolutionsJson)
                ?.FirstOrDefault(s => File.Exists(s));
            if (lastOpened is not null)
            {
                return lastOpened;
            }
        }

        // Otherwise the shallowest path in the repo; ties break alphabetically.
        var primary = solutions
            .MinBy(
                s => (Depth(s), string.CompareOrdinal(s, string.Empty)))
            ?? throw new InvalidOperationException("No solutions found.");
        return primary;
    }

    /// <summary>
    /// Number of directory separators in a path, used for shallowest-first ordering.
    /// </summary>
    private static int Depth(string path) => path.Count(c => c == '\\' || c == '/');

    /// <summary>
    /// True when <paramref name="path"/> is inside (or equal to)
    /// <paramref name="root"/>.
    /// </summary>
    private static bool IsInside(string root, string path)
    {
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && (path.Length == root.Length || path[root.Length] == System.IO.Path.DirectorySeparatorChar);
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
    /// Returns the legacy project's root folder: the folder containing the
    /// solution file, unless that folder is named "src", in which case the
    /// parent folder is returned.
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
