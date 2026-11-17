using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NCockpit.Models;

namespace NCockpit.Services;

/// <summary>
/// Reads the projects referenced by a solution file (.sln or .slnx) and
/// resolves them to project files on disk. Parsing is resilient by design:
/// a missing, unreadable or malformed file must never throw — it simply
/// yields no projects.
/// </summary>
public static class SolutionFileParser
{
    /// <summary>
    /// The project file extensions considered by the parser and the orphan
    /// discovery. Exposed so the orphan walk finds exactly the same file types
    /// the solution parser references.
    /// </summary>
    public static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

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

                var metadata = await ProjectFileMetadata.ReadAsync(projectPath);
                projects.Add(new SolutionProject
                {
                    Name = metadata.Name,
                    FolderPath = Path.GetDirectoryName(projectPath)!,
                    ProjectPath = projectPath,
                    Type = metadata.TypeLabel,
                    Sdk = metadata.Sdk,
                    TargetFrameworks = metadata.TargetFrameworks,
                    Version = metadata.Version,
                    OutputType = metadata.OutputType
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
    /// Recursively collects every project file (<see cref="ProjectExtensions"/>)
    /// under the given root, used to find the orphans (projects not referenced
    /// by any solution). Resilient: inaccessible directories are skipped and
    /// the subtree of an inner repository (.git) is skipped — those belong to
    /// their own project. Returns an empty list when nothing is found.
    /// </summary>
    public static async Task<List<SolutionProject>> DiscoverProjectFilesAsync(
        string root,
        IEnumerable<string>? skipPaths,
        CancellationToken cancellationToken)
    {
        var found = new List<SolutionProject>();
        var skip = skipPaths?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                if (!ProjectExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seen.Add(file))
                {
                    continue;
                }

                var metadata = await ProjectFileMetadata.ReadAsync(file);
                found.Add(new SolutionProject
                {
                    Name = metadata.Name,
                    FolderPath = Path.GetDirectoryName(file)!,
                    ProjectPath = file,
                    Type = metadata.TypeLabel,
                    Sdk = metadata.Sdk,
                    TargetFrameworks = metadata.TargetFrameworks,
                    Version = metadata.Version,
                    OutputType = metadata.OutputType
                });
            }

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);

                // Common noise folders and registered supporting folders.
                if (name is ".git" or "bin" or "obj" or "node_modules" or "packages" or ".vs" or ".idea" or ".gradle" or "dist" or "build")
                {
                    continue;
                }

                if (skip.Contains(directory))
                {
                    continue;
                }

                enumeration.Push(directory);
            }

            await Task.Yield();
        }

        return found;
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
    /// The attribute is named "Path" in the .slnx format (capital P); the
    /// lookup is case-insensitive to tolerate both "Path" and "path".
    /// </summary>
    private static List<string> ParseSlnxPaths(string solutionPath)
    {
        var doc = XDocument.Load(solutionPath);
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var element in doc.Descendants().Where(e => e.Name.LocalName == "Project"))
        {
            // The .slnx format uses "Path" (capital P). Look up case-
            // insensitively so both "Path" and "path" work.
            var relativePath = element.Attributes()
                .FirstOrDefault(a => string.Equals(a.Name.LocalName, "path", StringComparison.OrdinalIgnoreCase))
                ?.Value;

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
