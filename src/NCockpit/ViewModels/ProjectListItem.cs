using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using NCockpit.Models;

namespace NCockpit.ViewModels;

/// <summary>
/// Lightweight display wrapper around <see cref="Project"/> for the overview
/// project list. Exposes human-friendly, pre-formatted strings so the XAML
/// stays simple, and reuses the underlying model for favorite/usage state.
/// </summary>
public partial class ProjectListItem : ObservableObject
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private ProjectListItem(Project project)
    {
        Project = project;
        Name = project.Name;
        FolderPath = project.FolderPath;
        IsFavorite = project.IsFavorite;
        Initial = Name?.Length > 0 ? Name[0].ToString() : "?";
        LastOpenedDisplay = project.LastUsedUnix is { } lastOpened
            ? FormatRelativeTime(lastOpened)
            : "Never opened";
        StatusText = BuildStatus(project);
        IsFolderType = project.Type == ProjectType.Folder;
        IdeActionText = project.Type == ProjectType.Folder
            ? "Open in VS Code"
            : "Open in Visual Studio";
        _favoriteProjectFile = ResolveFavoriteProject(project);
    }

    /// <summary>
    /// Create a display item from a <see cref="Project"/>.
    /// </summary>
    public static ProjectListItem Create(Project project) => new(project);

    /// <summary>
    /// The underlying project. Commands reference this.
    /// </summary>
    public Project Project { get; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    [ObservableProperty]
    public partial string? FolderPath { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// First letter of the project name, used for the colored icon box.
    /// </summary>
    public string? Initial { get; }

    [ObservableProperty]
    public partial string? LastOpenedDisplay { get; set; }

    [ObservableProperty]
    public partial string? StatusText { get; set; }

    /// <summary>
    /// Whether the project is a folder type (a repo without a solution file),
    /// which opens in VS Code instead of Visual Studio.
    /// </summary>
    public bool IsFolderType { get; }

    /// <summary>
    /// Label for the "open in IDE" quick action, e.g. "Open in VS Code" for
    /// folder type projects and "Open in Visual Studio" for solution type.
    /// </summary>
    public string IdeActionText { get; }

    /// <summary>
    /// The favorite project file's metadata, resolved once from the project's
    /// stored data. Null for folder type projects (no solution) or when the
    /// scan found no project files.
    /// </summary>
    private SolutionProject? _favoriteProjectFile;

    /// <summary>
    /// The type of the favorite project file (".NET MAUI app", "ASP.NET Core
    /// web app", ...), for the type badge of the project row. Null for folder
    /// type projects or when the metadata could not be read.
    /// </summary>
    public string? TypeInfo => _favoriteProjectFile?.Type;

    /// <summary>
    /// The target frameworks of the favorite project file (e.g.
    /// "net11.0-windows10.0.19041.0"), for the target-framework badge of the
    /// project row. Null for folder type projects or when the metadata could
    /// not be read.
    /// </summary>
    public string? FrameworksInfo => _favoriteProjectFile?.TargetFrameworks;

    /// <summary>
    /// The favorite project of a solution type project: the user's choice when
    /// it is stored and still found, otherwise the same smart guess the
    /// details page uses (name match first, then an entry-point project).
    /// </summary>
    private static SolutionProject? ResolveFavoriteProject(Project project)
    {
        var projects = DeserializeSolutionProjects(project.SolutionProjectsJson)
            .SelectMany(w => w.Projects)
            .Concat(DeserializeOrphanProjects(project.OrphanProjectsJson))
            .ToList();

        if (projects.Count == 0)
        {
            return null;
        }

        var favoritePath = project.FavoriteProjectPath;
        if (favoritePath is { Length: > 0 })
        {
            var found = projects.FirstOrDefault(p => string.Equals(p.ProjectPath, favoritePath, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }
        }

        var solutionName = project.SolutionPath is { Length: > 0 } solution
            ? System.IO.Path.GetFileNameWithoutExtension(solution)
            : null;

        return projects
            .OrderByDescending(p => MatchesName(p, project.Name) ? 2 : 0)
            .ThenByDescending(p => solutionName is not null && MatchesName(p, solutionName) ? 1 : 0)
            .ThenBy(p => string.CompareOrdinal(p.Name, string.Empty))
            .First();

        static bool MatchesName(SolutionProject p, string? name) =>
            name is { Length: > 0 }
            && (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(System.IO.Path.GetFileNameWithoutExtension(p.ProjectPath), name, StringComparison.OrdinalIgnoreCase));
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

    private static List<SolutionProject> DeserializeOrphanProjects(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<SolutionProject>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Format a Unix timestamp as a relative time string, e.g. "Today 10:24",
    /// "Yesterday 16:42", or "Sep 12, 14:18".
    /// </summary>
    private static string FormatRelativeTime(long unix)
    {
        var dateTime = UnixEpoch.AddSeconds(unix).ToLocalTime();
        var now = DateTime.Now;

        if (dateTime.Date == now.Date)
        {
            return $"Today {dateTime:HH:mm}";
        }

        if (dateTime.Date == now.Date.AddDays(-1))
        {
            return $"Yesterday {dateTime:HH:mm}";
        }

        return $"{dateTime:MMM d, HH:mm}";
    }

    /// <summary>
    /// Build the status text shown in the project row.
    /// </summary>
    private static string BuildStatus(Project project)
    {
        // No git status yet; show "Active" as default.
        return "Active";
    }
}
