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
        LastUsedDisplay = FormatUsed(project.LastUsedUnix ?? project.LastSeenUnix);
        StatusText = BuildStatus(project);
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
    private string? name;

    [ObservableProperty]
    private string? folderPath;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private string? lastUsedDisplay;

    [ObservableProperty]
    private string? statusText;

    private static string FormatUsed(long unix) =>
        UnixEpoch.AddSeconds(unix).ToLocalTime().ToString("MMM d, yyyy");

    private static string BuildStatus(Project project)
    {
        var seen = FormatUsed(project.LastSeenUnix);
        return project.IsFavorite ? $"Favorite • Last seen {seen}" : $"Last seen {seen}";
    }
}
