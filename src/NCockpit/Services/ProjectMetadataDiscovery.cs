using System.Text.Json;
using System.Text.RegularExpressions;
using NCockpit.Models;

namespace NCockpit.Services;

/// <summary>
/// Read-only per-project metadata discovery, shared by the folder scan and the
/// single-project refresh. Every method is resilient by design: a missing,
/// unreadable or malformed file yields no value and never throws.
/// </summary>
public static class ProjectMetadataDiscovery
{
    /// <summary>
    /// Common root-level files worth quickly opening from the project details
    /// page, checked in order (first match per base name wins).
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

    private static readonly Regex OriginUrlPattern = new(
        @"\[remote\s+""origin""\][\s\S]*?url\s*=\s*(.+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns the solution file's last write time as Unix seconds, or null if
    /// the file cannot be accessed.
    /// </summary>
    public static long? GetSolutionLastWriteUnix(string solutionPath)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(solutionPath);
            return (long)(lastWrite - DateTimeOffset.UnixEpoch).TotalSeconds;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the project file's last write time as Unix seconds, or null if
    /// the file cannot be accessed.
    /// </summary>
    public static long? GetProjectLastWriteUnix(string projectPath)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(projectPath);
            return (long)(lastWrite - DateTimeOffset.UnixEpoch).TotalSeconds;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Discovers common root files in the project folder and returns them as a
    /// JSON array of file names, or null when none are found.
    /// </summary>
    public static string? DiscoverRootFilesJson(string projectPath)
    {
        try
        {
            var found = new List<string>();
            foreach (var fileName in KnownRootFiles)
            {
                if (found.Count > 0 && SameBaseName(fileName, found[^1]))
                {
                    continue;
                }

                if (File.Exists(Path.Combine(projectPath, fileName)))
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
    /// Reads the git remote URL (origin) from .git/config. Returns null when the
    /// project is not a git repo, has no remote, or the URL cannot be parsed.
    /// </summary>
    public static string? GetGitRepoUrl(string projectPath)
    {
        try
        {
            var configFile = Path.Combine(projectPath, ".git", "config");
            if (!File.Exists(configFile))
            {
                return null;
            }

            var content = File.ReadAllText(configFile);
            var match = OriginUrlPattern.Match(content);
            if (match.Success)
            {
                var url = match.Groups[1].Value.Trim();
                return string.IsNullOrEmpty(url) ? null : url;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses every solution file and returns them together with the projects
    /// they reference, as a JSON array of <see cref="SolutionWithProjects"/>,
    /// or null when there are no solutions. Unreadable or malformed solution
    /// files are resilient: they appear with an empty project list.
    /// </summary>
    public static async Task<string?> DiscoverSolutionProjectsJsonAsync(
        List<string> solutionPaths,
        CancellationToken cancellationToken = default)
    {
        if (solutionPaths.Count == 0)
        {
            return null;
        }

        var withProjects = new List<SolutionWithProjects>();
        foreach (var solutionPath in solutionPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var projects = await SolutionFileParser.ParseSolutionAsync(solutionPath);
            withProjects.Add(new SolutionWithProjects
            {
                Path = solutionPath,
                Projects = projects
            });
        }

        return JsonSerializer.Serialize(withProjects);
    }

    /// <summary>
    /// Discovers every project file under <paramref name="root"/> and returns
    /// the ones not referenced by any solution as a JSON array of
    /// <see cref="SolutionProject"/> ("orphans"), or null when there are none.
    /// A file counts as referenced when its full path matches the full path of
    /// a project of any of the given solutions.
    /// </summary>
    public static async Task<string?> DiscoverOrphanProjectsJsonAsync(
        string root,
        IReadOnlyList<SolutionWithProjects> solutions,
        IEnumerable<string>? skipPaths = null,
        CancellationToken cancellationToken = default)
    {
        var all = await SolutionFileParser.DiscoverProjectFilesAsync(root, skipPaths, cancellationToken);
        if (all.Count == 0)
        {
            return null;
        }

        var referenced = solutions
            .SelectMany(s => s.Projects)
            .Select(p => p.ProjectPath)
            .Where(p => p.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orphans = all.Where(p => !referenced.Contains(p.ProjectPath)).ToList();
        return orphans.Count > 0 ? JsonSerializer.Serialize(orphans) : null;
    }

    /// <summary>
    /// Returns the solution file paths as a JSON array, or null when the list
    /// is empty.
    /// </summary>
    public static string? DiscoverSolutionsJson(List<string> solutionPaths)
    {
        if (solutionPaths.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(solutionPaths);
    }

    /// <summary>
    /// Returns the newest last write time of any file under
    /// <paramref name="folder"/>, or null when none can be read. The walk
    /// prunes the usual noise folders (build output, restored packages, tooling
    /// state) and is bounded: for a huge folder it reports the newest time it
    /// reached within the bound, which is enough to tell how recently the
    /// project was worked on.
    /// </summary>
    public static long? GetFolderLastWriteUnix(string folder)
    {
        var newest = (long?)null;
        var visitedDirectories = 0;
        var visitedFiles = 0;

        var enumeration = new Stack<string>();
        enumeration.Push(folder);

        while (enumeration.Count > 0
               && visitedDirectories < MaxVisitedDirectories
               && visitedFiles < MaxVisitedFiles)
        {
            var current = enumeration.Pop();

            List<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(current).ToList();
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(current).ToArray();
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (IOException) { continue; }

            foreach (var file in files)
            {
                visitedFiles++;
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (newest is null || lastWrite > DateTimeOffset.FromUnixTimeSeconds(newest.Value))
                    {
                        newest = (long)(lastWrite - DateTimeOffset.UnixEpoch).TotalSeconds;
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            foreach (var directory in directories)
            {
                if (SkipDirectoryNames.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                visitedDirectories++;
                enumeration.Push(directory);
            }
        }

        return newest;
    }

    /// <summary>
    /// Bounds of the last-modified walk: enough to cover a real project folder,
    /// small enough to keep the details page responsive.
    /// </summary>
    private const int MaxVisitedDirectories = 3000;
    private const int MaxVisitedFiles = 30000;

    private static readonly string[] SkipDirectoryNames =
    [
        ".git", "bin", "obj", "node_modules", "packages",
        ".vs", ".idea", ".gradle", "dist", "build", "TestResults"
    ];

    /// <summary>
    /// True when two file names share the same base name (ignoring extension).
    /// </summary>
    private static bool SameBaseName(string a, string b)
    {
        var baseA = Path.GetFileNameWithoutExtension(a) ?? a;
        var baseB = Path.GetFileNameWithoutExtension(b) ?? b;
        return string.Equals(baseA, baseB, StringComparison.OrdinalIgnoreCase);
    }
}
