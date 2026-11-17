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

            // The paths written by this run are projects in their own right.
            var discoveredPaths = discovered
                .Select(p => p.FolderPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var project in discovered)
            {
                project.FolderId = folder.Id;
                project.LastSeenUnix = scanUnix;
                project.ScanUnix = scanUnix;

                await _projectRepository.UpsertAsync(project);

                // A discovered project takes over any stale row inside its own
                // folder (previously one project per solution), preserving
                // their user state instead of pruning them. Rows this run
                // discovered are never taken over: they are projects of their
                // own (for example an orphan solution in a subfolder), and
                // taking them over would delete and recreate them on every
                // scan - the flickering this used to cause.
                var all = await _projectRepository.GetAllAsync();
                var repoRoot = project.FolderPath;
                await _projectRepository.MergeIntoAsync(
                    project.Id,
                    all
                        .Where(p => p.Id != project.Id
                            && !string.Equals(p.FolderPath, repoRoot, StringComparison.OrdinalIgnoreCase)
                            && !discoveredPaths.Contains(p.FolderPath)
                            && IsInside(repoRoot, p.FolderPath))
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
            cancellationToken.ThrowIfCancellationRequested();
            discovered.Add(await BuildRepoProjectAsync(repoRoot, solutions, cancellationToken));
        }

        // One legacy project per orphan solution, with the existing root rules.
        foreach (var solution in orphanSolutions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            discovered.Add(await BuildLegacyProjectAsync(solution, cancellationToken));
        }

        return discovered;
    }

    /// <summary>
    /// Builds the <see cref="Project"/> for a repository root: every solution
    /// is parsed (not just one), the orphans are discovered, and the favorite
    /// solution is resolved (the user's choice when it still exists, otherwise
    /// the smart guess). The existing row is looked up once so the scan's
    /// user state (favorite choice, last opened) is reused without extra
    /// queries.
    /// </summary>
    private async Task<Project> BuildRepoProjectAsync(string repoRoot, List<string> solutions, CancellationToken cancellationToken)
    {
        var name = GetFolderName(repoRoot);
        var isSolutionType = solutions.Count > 0;
        var existing = (await _projectRepository.GetAllAsync())
            .FirstOrDefault(p => string.Equals(p.FolderPath, repoRoot, StringComparison.OrdinalIgnoreCase));

        string? solutionsJson = null;
        string? solutionProjectsJson = null;
        string? orphanProjectsJson = null;
        long? solutionLastWriteUnix = null;
        string? favoriteSolution = null;

        if (isSolutionType)
        {
            solutionsJson = ProjectMetadataDiscovery.DiscoverSolutionsJson(solutions);

            // Parse every solution, not just the favorite one. An unreadable
            // or malformed solution file is resilient: it appears with an
            // empty project list.
            solutionProjectsJson = await ProjectMetadataDiscovery.DiscoverSolutionProjectsJsonAsync(
                solutions, cancellationToken);
            var withProjects = DeserializeSolutionProjects(solutionProjectsJson);

            // Orphans: every project file in the repo that no solution references.
            orphanProjectsJson = await ProjectMetadataDiscovery.DiscoverOrphanProjectsJsonAsync(
                repoRoot, withProjects, _skipPaths, cancellationToken);

            favoriteSolution = ResolveFavoriteSolution(
                existing?.FavoriteSolutionPath,
                name,
                solutions,
                withProjects,
                existing?.SolutionLastUsedUnix,
                existing?.SolutionPath);

            solutionLastWriteUnix = ProjectMetadataDiscovery.GetSolutionLastWriteUnix(favoriteSolution);
        }

        return new Project
        {
            Name = name,
            Type = isSolutionType ? ProjectType.Solution : ProjectType.Folder,
            SolutionPath = favoriteSolution,
            FolderPath = repoRoot,
            SolutionLastWriteUnix = solutionLastWriteUnix,
            FilesJson = ProjectMetadataDiscovery.DiscoverRootFilesJson(repoRoot),
            GitRepoUrl = ProjectMetadataDiscovery.GetGitRepoUrl(repoRoot),
            SolutionProjectsJson = solutionProjectsJson,
            OrphanProjectsJson = orphanProjectsJson,
            SolutionsJson = solutionsJson,
            // The user's choice is persisted by the repository's upsert (it is
            // not overwritten on a re-scan); the smart guess stands in through
            // SolutionPath until the user picks one.
            FavoriteSolutionPath = null,
            FavoriteProjectPath = null
        };
    }

    /// <summary>
    /// Builds the legacy <see cref="Project"/> for an orphan solution file
    /// (a solution found outside any repository), with the existing root rules.
    /// </summary>
    private async Task<Project> BuildLegacyProjectAsync(string solution, CancellationToken cancellationToken)
    {
        var projectRootPath = GetProjectRootPath(solution);
        var solutions = new List<string> { solution };
        var legacyJson = await ProjectMetadataDiscovery.DiscoverSolutionProjectsJsonAsync(
            solutions, cancellationToken);
        var withProjects = DeserializeSolutionProjects(legacyJson);
        var name = System.IO.Path.GetFileNameWithoutExtension(solution);

        var favoriteSolution = ResolveFavoriteSolution(null, name, solutions, withProjects, null, null);

        return new Project
        {
            Name = name,
            Type = ProjectType.Solution,
            SolutionPath = favoriteSolution,
            FolderPath = projectRootPath,
            SolutionLastWriteUnix = ProjectMetadataDiscovery.GetSolutionLastWriteUnix(favoriteSolution),
            FilesJson = ProjectMetadataDiscovery.DiscoverRootFilesJson(projectRootPath),
            GitRepoUrl = ProjectMetadataDiscovery.GetGitRepoUrl(projectRootPath),
            SolutionProjectsJson = legacyJson,
            OrphanProjectsJson = null,
            SolutionsJson = ProjectMetadataDiscovery.DiscoverSolutionsJson([solution]),
            FavoriteSolutionPath = null,
            FavoriteProjectPath = null
        };
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
    /// Keeps the user's favorite solution when it still exists, otherwise
    /// falls back to the smart guess: a solution whose name matches the repo
    /// name first, then the most recently opened (feeds from
    /// <see cref="Project.SolutionLastUsedUnix"/> / the last opened path), then
    /// the solution referencing the most projects, then the most recently
    /// modified, then the shallowest path in the repo, with alphabetical as
    /// the final tie-break.
    /// </summary>
    internal static string ResolveFavoriteSolution(
        string? userFavorite,
        string repoName,
        IReadOnlyList<string> solutions,
        IReadOnlyList<SolutionWithProjects> withProjects,
        long? lastOpenedUnix,
        string? lastOpenedPath)
    {
        if (solutions.Count == 0)
        {
            throw new InvalidOperationException("No solutions found.");
        }

        if (!string.IsNullOrEmpty(userFavorite) && solutions.Contains(userFavorite, StringComparer.OrdinalIgnoreCase))
        {
            return userFavorite;
        }

        var byPath = withProjects
            .Where(w => solutions.Contains(w.Path, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(w => w.Path, w => w.Projects, StringComparer.OrdinalIgnoreCase);

        return solutions
            .OrderByDescending(s => string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(s), repoName, StringComparison.OrdinalIgnoreCase)
                ? 1 : 0)
            .ThenByDescending(s => lastOpenedPath is { Length: > 0 }
                && string.Equals(s, lastOpenedPath, StringComparison.OrdinalIgnoreCase)
                ? 1 : 0)
            .ThenByDescending(s => byPath.TryGetValue(s, out var projects) ? projects.Count : 0)
            .ThenByDescending(s => ProjectMetadataDiscovery.GetSolutionLastWriteUnix(s) ?? 0)
            .ThenBy(s => Depth(s))
            .ThenBy(s => string.CompareOrdinal(s, string.Empty))
            .First();
    }

    /// <summary>
    /// Picks the favorite project of a solution: a project whose name matches
    /// the repo name or the solution name first, then the project that looks
    /// like an entry point (an executable output type), then alphabetical.
    /// Returns null when the project list is empty.
    /// </summary>
    internal static SolutionProject? ResolveFavoriteProject(
        string? userFavorite,
        string repoName,
        string? solutionName,
        IReadOnlyList<SolutionProject> projects)
    {
        if (projects.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(userFavorite)
            && projects.Any(p => string.Equals(p.ProjectPath, userFavorite, StringComparison.OrdinalIgnoreCase)))
        {
            return projects.First(p => string.Equals(p.ProjectPath, userFavorite, StringComparison.OrdinalIgnoreCase));
        }

        return projects
            .OrderByDescending(p => MatchesName(p, repoName) ? 2 : 0)
            .ThenByDescending(p => solutionName is not null && MatchesName(p, solutionName) ? 1 : 0)
            .ThenByDescending(p => LooksLikeEntryPoint(p) ? 1 : 0)
            .ThenBy(p => string.CompareOrdinal(p.Name, string.Empty))
            .First();
    }

    private static bool MatchesName(SolutionProject project, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        return string.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(project.ProjectPath), name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeEntryPoint(SolutionProject project)
    {
        try
        {
            if (!File.Exists(project.ProjectPath))
            {
                return false;
            }

            var content = File.ReadAllText(project.ProjectPath);
            return content.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
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
}
