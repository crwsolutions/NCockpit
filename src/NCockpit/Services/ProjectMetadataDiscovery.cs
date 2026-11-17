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
    /// Derives a simple type label from a project file, e.g. ".NET MAUI app",
    /// "ASP.NET Core web app", "Console app", "Class library". Returns "Project"
    /// when the file is missing, unreadable, or none of the known markers are
    /// present.
    /// </summary>
    public static string GetProjectTypeLabel(string projectPath)
    {
        try
        {
            if (!File.Exists(projectPath))
            {
                return "Project";
            }

            var content = File.ReadAllText(projectPath);
            var sdk = GetSdk(content);
            var outputType = GetOutputType(content);
            var targetFrameworkMatches = Regex.Matches(
                content,
                @"<TargetFramework(?:s)?\s*>\s*([^<]+?)\s*</TargetFramework(?:s)?>",
                RegexOptions.IgnoreCase);

            if (content.Contains("UseMaui", StringComparison.OrdinalIgnoreCase))
            {
                return ".NET MAUI app";
            }

            if (string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)
                || content.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase))
            {
                return "ASP.NET Core web app";
            }

            if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase))
            {
                return "Console app";
            }

            if (targetFrameworkMatches.Any(m =>
                    m.Groups[1].Value.Contains("android", StringComparison.OrdinalIgnoreCase)
                    || m.Groups[1].Value.Contains("ios", StringComparison.OrdinalIgnoreCase)
                    || m.Groups[1].Value.Contains("macos", StringComparison.OrdinalIgnoreCase)
                    || m.Groups[1].Value.Contains("tvos", StringComparison.OrdinalIgnoreCase)
                    || m.Groups[1].Value.Contains("maccatalyst", StringComparison.OrdinalIgnoreCase)
                    || m.Groups[1].Value.Contains("windows", StringComparison.OrdinalIgnoreCase)))
            {
                return ".NET app";
            }

            if (string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase)
                || (outputType.Length == 0 && string.Equals(sdk, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase)))
            {
                return "Class library";
            }

            return "Project";
        }
        catch
        {
            return "Project";
        }
    }

    private static string GetSdk(string content)
    {
        var match = Regex.Match(
            content,
            @"<Project\s+[^>]*?Sdk\s*=\s*""([^""]+)""",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string GetOutputType(string content)
    {
        var matches = Regex.Matches(
            content,
            @"<OutputType>\s*([^<]+?)\s*</OutputType>",
            RegexOptions.IgnoreCase);
        return matches.Count > 0 ? matches[^1].Groups[1].Value : string.Empty;
    }

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
