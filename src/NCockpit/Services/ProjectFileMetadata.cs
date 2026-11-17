using System.Text.RegularExpressions;

namespace NCockpit.Services;

/// <summary>
/// What a project file (.csproj, .fsproj, .vbproj) says about itself: its
/// display name, its kind, its SDK, its target frameworks and the version it
/// sets. Reading is resilient by design: a missing, unreadable or malformed
/// file yields a metadata whose <see cref="Name"/> is the folder name and whose
/// other values are null.
/// </summary>
public sealed partial class ProjectFileMetadata
{
    /// <summary>
    /// MSBuild properties that can hold a version, in the order they are
    /// preferred. A project that sets none of them builds with the version the
    /// SDK derives from the target framework.
    /// </summary>
    private static readonly string[] VersionProperties =
    [
        "Version",
        "VersionPrefix",
        "ApplicationDisplayVersion",
        "AssemblyVersion",
        "FileVersion",
        "InformationalVersion"
    ];

    /// <summary>
    /// Display name of the project: the ProjectName property when the file sets
    /// one, otherwise the name of the folder holding the project file.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable kind of the project, e.g. ".NET MAUI app",
    /// "ASP.NET Core web app", "Console app", "Class library". Null when the
    /// kind cannot be told from the file.
    /// </summary>
    public string? TypeLabel { get; init; }

    /// <summary>
    /// The SDK of the project file, e.g. "Microsoft.NET.Sdk" or
    /// "Microsoft.NET.Sdk.Web". Null when the file declares no Sdk attribute.
    /// </summary>
    public string? Sdk { get; init; }

    /// <summary>
    /// The target framework(s) the project builds for, joined with ", ".
    /// Null when the file names none (they are inherited or computed).
    /// </summary>
    public string? TargetFrameworks { get; init; }

    /// <summary>
    /// The version the project sets. Null when it sets none, in which case the
    /// SDK's derived version applies.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// "Exe" or "Library" when the file sets OutputType; otherwise null.
    /// </summary>
    public string? OutputType { get; init; }

    /// <summary>
    /// Reads and parses a project file, together with the Directory.Build.props
    /// MSBuild would import for it. Never throws: an unreadable file yields a
    /// metadata with only <see cref="Name"/> set.
    /// </summary>
    public static async Task<ProjectFileMetadata> ReadAsync(string projectPath)
    {
        var content = await ReadTextAsync(projectPath);
        if (content is null)
        {
            return FolderNameOnly(projectPath);
        }

        return Parse(projectPath, content, await ReadInheritedPropsAsync(projectPath));
    }

    /// <summary>
    /// Parses the content of a project file. Split from the read so the scan can
    /// read a file once and take every value from it.
    /// </summary>
    /// <param name="inheritedContent">Content of the nearest
    /// <c>Directory.Build.props</c> above the project folder, when there is one.
    /// Its properties only supply values the project file itself does not set,
    /// which is how a repository that keeps <c>TargetFrameworks</c> or
    /// <c>Version</c> in shared props still reports them.</param>
    public static ProjectFileMetadata Parse(string projectPath, string content, string? inheritedContent = null)
    {
        content = StripComments(content);
        inheritedContent = inheritedContent is null ? null : StripComments(inheritedContent);

        var properties = ReadProperties(content);
        if (inheritedContent is { Length: > 0 })
        {
            // Inherited properties first: LastValue lets the project file win,
            // mirroring MSBuild importing props before the project body.
            properties.InsertRange(0, ReadProperties(inheritedContent));
        }

        var sdk = ReadSdk(content);
        var outputType = LastValue(properties, "OutputType");
        var targetFrameworks = ReadTargetFrameworks(properties);

        return new ProjectFileMetadata
        {
            Name = LastValue(properties, "ProjectName") ?? FolderName(projectPath),
            TypeLabel = BuildTypeLabel(properties, sdk, outputType, targetFrameworks, content, inheritedContent),
            Sdk = sdk,
            TargetFrameworks = targetFrameworks,
            Version = ReadVersion(properties),
            OutputType = outputType
        };
    }

    /// <summary>
    /// Reads a file as text, or returns null when it is missing, unreadable or
    /// not decodable as text.
    /// </summary>
    private static async Task<string?> ReadTextAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return await File.ReadAllTextAsync(path);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // A file whose content cannot be decoded as text.
            return null;
        }
    }

    /// <summary>
    /// The content of the <c>Directory.Build.props</c> MSBuild would import for
    /// the project: the nearest one walking up from the project folder. Null
    /// when the folder chain has none.
    /// </summary>
    private static async Task<string?> ReadInheritedPropsAsync(string projectPath)
    {
        var directory = Path.GetDirectoryName(projectPath);
        while (!string.IsNullOrEmpty(directory))
        {
            var content = await ReadTextAsync(Path.Combine(directory, DirectoryBuildPropsFileName));
            if (content is not null)
            {
                return content;
            }

            var parent = Path.GetDirectoryName(directory);
            if (string.IsNullOrEmpty(parent) || parent == directory)
            {
                break;
            }

            directory = parent;
        }

        return null;
    }

    private const string DirectoryBuildPropsFileName = "Directory.Build.props";

    /// <summary>
    /// Removes XML comments, so a commented-out property never reads as a live
    /// one. A project file that comments a property out to disable it is common,
    /// and the last definition wins, so a comment could otherwise decide a
    /// version or a target framework.
    /// </summary>
    private static string StripComments(string content) => CommentRegex().Replace(content, string.Empty);

    /// <summary>
    /// Every simple MSBuild property element in document order. A property with
    /// attributes (a Condition) counts; an element with nested elements does
    /// not, because its value is not plain text.
    /// </summary>
    private static List<MsBuildProperty> ReadProperties(string content)
    {
        var properties = new List<MsBuildProperty>();
        foreach (Match match in PropertyRegex().Matches(content))
        {
            properties.Add(new MsBuildProperty(
                match.Groups["name"].Value,
                match.Groups["value"].Value.Trim()));
        }

        return properties;
    }

    private static string? LastValue(List<MsBuildProperty> properties, string name)
    {
        // The last definition wins, mirroring how MSBuild evaluates a property
        // that is set more than once.
        var match = properties.LastOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return match.Value is { Length: > 0 } value ? value : null;
    }

    private static string? ReadVersion(List<MsBuildProperty> properties)
    {
        foreach (var name in VersionProperties)
        {
            if (LastValue(properties, name) is { } version)
            {
                return version;
            }
        }

        return null;
    }

    /// <summary>
    /// Collects the frameworks of every TargetFramework / TargetFrameworks
    /// element, so a project that adds a framework conditionally still lists
    /// them all. Property references such as <c>$(TargetFrameworks)</c> stand
    /// for values defined elsewhere (Directory.Build.props, the SDK) and are
    /// skipped.
    /// </summary>
    private static string? ReadTargetFrameworks(List<MsBuildProperty> properties)
    {
        var frameworks = new List<string>();
        foreach (var property in properties)
        {
            if (!string.Equals(property.Name, "TargetFramework", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(property.Name, "TargetFrameworks", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var framework in property.Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (framework.StartsWith("$(", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!frameworks.Contains(framework, StringComparer.OrdinalIgnoreCase))
                {
                    frameworks.Add(framework);
                }
            }
        }

        return frameworks.Count > 0 ? string.Join(", ", frameworks) : null;
    }

    private static string? ReadSdk(string content)
    {
        var match = SdkAttributeRegex().Match(content);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Derives the kind of the project from its SDK, its properties and the
    /// packages it references. Deliberately coarse: it names what a developer
    /// recognises at a glance, not every MSBuild variant.
    /// </summary>
    private static string? BuildTypeLabel(
        List<MsBuildProperty> properties,
        string? sdk,
        string? outputType,
        string? targetFrameworks,
        string content,
        string? inheritedContent)
    {
        // A project that sets no OutputType builds as a library, which is how the
        // .NET SDK defaults it (a console template writes Exe explicitly).
        var isLibrary = string.Equals(outputType, "Library", StringComparison.OrdinalIgnoreCase);
        var isExecutable = outputType is not null
            && (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase));

        if (FlagIsTrue(properties, "UseMaui")
            || string.Equals(sdk, "Microsoft.NET.Sdk.Maui", StringComparison.OrdinalIgnoreCase))
        {
            return isLibrary ? ".NET MAUI class library" : ".NET MAUI app";
        }

        if (string.Equals(sdk, "Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase)
            || content.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)
            || inheritedContent?.Contains("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "ASP.NET Core web app";
        }

        if (string.Equals(sdk, "Microsoft.NET.Sdk.BlazorWebAssembly", StringComparison.OrdinalIgnoreCase))
        {
            return "Blazor WebAssembly app";
        }

        if (string.Equals(sdk, "Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
        {
            return "Worker service";
        }

        if (TestSdkPackageRegex().IsMatch(content)
            || (inheritedContent is { Length: > 0 } inherited && TestSdkPackageRegex().IsMatch(inherited)))
        {
            return "Test project";
        }

        if (FlagIsTrue(properties, "UseWPF"))
        {
            return isLibrary ? "WPF class library" : "WPF app";
        }

        if (FlagIsTrue(properties, "UseWindowsForms"))
        {
            return isLibrary ? "Windows Forms class library" : "Windows Forms app";
        }

        if (isExecutable)
        {
            return "Console app";
        }

        if (targetFrameworks is { Length: > 0 } frameworks
            && frameworks.Split(',')
                .Any(f => PlatformFrameworkRegex().IsMatch(f)))
        {
            return ".NET app";
        }

        if (isLibrary || string.Equals(sdk, "Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase))
        {
            return "Class library";
        }

        return null;
    }

    private static bool FlagIsTrue(List<MsBuildProperty> properties, string name)
    {
        return LastValue(properties, name) is { } value
            && bool.TryParse(value, out var flag)
            && flag;
    }

    private static ProjectFileMetadata FolderNameOnly(string projectPath) => new()
    {
        Name = FolderName(projectPath)
    };

    private static string FolderName(string projectPath)
    {
        var folder = Path.GetDirectoryName(projectPath);
        return Path.GetFileName(folder) is { Length: > 0 } name ? name : projectPath;
    }

    private readonly record struct MsBuildProperty(string Name, string Value);

    [GeneratedRegex(
        @"<!--.*?-->",
        RegexOptions.Singleline | RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(
        @"<(?<name>[A-Za-z_][A-Za-z0-9_.]*)\b[^>]*>(?<value>[^<]*)</\k<name>>",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 2000)]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(
        @"<Project\s+[^>]*?Sdk\s*=\s*""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex SdkAttributeRegex();

    [GeneratedRegex(
        @"<PackageReference\b[^>]*Include\s*=\s*""Microsoft\.NET\.Test\.Sdk""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex TestSdkPackageRegex();

    [GeneratedRegex(
        @"\b(android|ios|maccatalyst|macos|tvos|watchos|windows|-browser|wasm|maui)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 500)]
    private static partial Regex PlatformFrameworkRegex();
}
