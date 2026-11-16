using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NCockpit.Services;

/// <summary>
/// A project entry discovered in a solution file (.sln or .slnx).
/// </summary>
public class SolutionProject
{
    /// <summary>
    /// Display name of the project (from the .csproj's ProjectName property
    /// when available, otherwise the folder name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path to the folder containing the project file.
    /// </summary>
    public required string FolderPath { get; init; }

    /// <summary>
    /// Full path to the project file (.csproj, .fsproj, .vbproj, ...).
    /// </summary>
    public required string ProjectPath { get; init; }
}

/// <summary>
/// Reads the projects referenced by a solution file (.sln or .slnx) and
/// resolves them to project files on disk. Parsing is resilient by design:
/// a missing, unreadable or malformed file must never throw — it simply
/// yields no projects.
/// </summary>
public static class SolutionFileParser
{
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    /// <summary>
    /// Returns the projects referenced by the given solution file, each with
    /// its name, folder and full project-file path. Returns an empty list when
    /// the solution file is missing, unreadable, or references no resolvable
    /// project files.
    /// </summary>
    public static async Task<List<SolutionProject>> ParseSolutionAsync(string solutionPath)
    {
        try
        {
            if (!File.Exists(solutionPath))
            {
                return [];
            }

            var extension = Path.GetExtension(solutionPath);
            var paths = string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
                ? ParseSlnxPaths(solutionPath)
                : ParseSlnPaths(solutionPath);

            var solutionDir = Path.GetDirectoryName(solutionPath)!;
            var projects = new List<SolutionProject>();

            foreach (var relativePath in paths)
            {
                var projectPath = Path.GetFullPath(Path.Combine(solutionDir, relativePath));

                if (!ProjectExtensions.Contains(Path.GetExtension(projectPath), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!File.Exists(projectPath))
                {
                    continue;
                }

                var name = await TryGetProjectNameAsync(projectPath);
                projects.Add(new SolutionProject
                {
                    Name = name,
                    FolderPath = Path.GetDirectoryName(projectPath)!,
                    ProjectPath = projectPath
                });
            }

            return projects;
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Reads the ProjectName MSBuild property from a project file. Falls back
    /// to the folder name when the property is absent or the file cannot be
    /// read.
    /// </summary>
    public static async Task<string> TryGetProjectNameAsync(string projectPath)
    {
        try
        {
            if (!File.Exists(projectPath))
            {
                return Path.GetFileName(Path.GetDirectoryName(projectPath)!);
            }

            var content = await File.ReadAllTextAsync(projectPath);

            // Take the last <ProjectName> element in the file so a later
            // definition wins, and skip MSBuild-PropertyGroup entries.
            var matches = Regex.Matches(
                content,
                @"<ProjectName>\s*([^<]+?)\s*</ProjectName>",
                RegexOptions.IgnoreCase);

            var name = matches.Count > 0 ? matches[^1].Groups[1].Value.Trim() : string.Empty;
            return string.IsNullOrEmpty(name)
                ? Path.GetFileName(Path.GetDirectoryName(projectPath)!)
                : name;
        }
        catch
        {
            return Path.GetFileName(Path.GetDirectoryName(projectPath)!);
        }
    }

    /// <summary>
    /// Returns the relative project paths referenced by a classic .sln file.
    /// Paths in .sln files use backslashes even on Windows.
    /// </summary>
    private static List<string> ParseSlnPaths(string solutionPath)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(solutionPath))
        {
            // Project("{GUID}") = "Name", "relative\path.csproj", "{GUID}"
            var match = Regex.Match(
                line,
                @"^Project\s*\(\s*""[^""]*""\s*\)\s*=\s*""[^""]*""\s*,\s*""([^""]+)""",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                continue;
            }

            var relativePath = match.Groups[1].Value.Replace("\\", Path.DirectorySeparatorChar.ToString());
            if (seen.Add(relativePath))
            {
                paths.Add(relativePath);
            }
        }

        return paths;
    }

    /// <summary>
    /// Returns the relative project paths referenced by a .slnx (XML) file.
    /// </summary>
    private static List<string> ParseSlnxPaths(string solutionPath)
    {
        var doc = XDocument.Load(solutionPath);
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc.Descendants().Where(e => e.Name.LocalName == "Project"))
        {
            var relativePath = element.Attribute("path")?.Value;
            if (string.IsNullOrEmpty(relativePath))
            {
                continue;
            }

            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            if (seen.Add(relativePath))
            {
                paths.Add(relativePath);
            }
        }

        return paths;
    }
}
