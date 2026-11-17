using System.Text.Json;
using System.Text.RegularExpressions;

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
    /// Parses the solution file and returns the referenced projects as a JSON
    /// array, or null when no projects could be resolved.
    /// </summary>
    public static async Task<string?> DiscoverSolutionProjectsJsonAsync(string solutionPath)
    {
        var projects = await SolutionFileParser.ParseSolutionAsync(solutionPath);
        return projects.Count > 0 ? JsonSerializer.Serialize(projects) : null;
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
    /// True when two file names share the same base name (ignoring extension).
    /// </summary>
    private static bool SameBaseName(string a, string b)
    {
        var baseA = Path.GetFileNameWithoutExtension(a) ?? a;
        var baseB = Path.GetFileNameWithoutExtension(b) ?? b;
        return string.Equals(baseA, baseB, StringComparison.OrdinalIgnoreCase);
    }
}
