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
