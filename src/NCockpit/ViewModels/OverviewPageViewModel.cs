using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Models;
using NCockpit.Services;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the Overview (Projects) screen. Shows a time-of-day greeting,
/// a compact summary, and the project list ordered by recent usage. Selecting
/// a project opens its details page and records the usage.
/// </summary>
public partial class OverviewPageViewModel : ObservableObject
{
    private readonly IProjectService _projectService;

    /// <summary>
    /// Greeting line, e.g. "Good afternoon, Colin".
    /// </summary>
    [ObservableProperty]
    private string greeting;

    /// <summary>
    /// Projects ordered by recent usage.
    /// </summary>
    [ObservableProperty]
    private List<ProjectListItem> projects = [];

    [ObservableProperty]
    private int totalProjects;

    [ObservableProperty]
    private int favoriteCount;

    [ObservableProperty]
    private int recentlyUsedCount;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public OverviewPageViewModel(
        IProjectService projectService)
    {
        _projectService = projectService;

        Greeting = BuildGreeting();
    }

    /// <summary>
    /// Build the greeting using the local time of day and the Windows user name.
    /// </summary>
    private static string BuildGreeting()
    {
        var hour = DateTime.Now.Hour;
        string period;
        if (hour >= 5 && hour < 12)
        {
            period = "Good morning";
        }
        else if (hour >= 12 && hour < 18)
        {
            period = "Good afternoon";
        }
        else if (hour >= 18 && hour < 22)
        {
            period = "Good evening";
        }
        else
        {
            period = "Good night";
        }

        var name = Environment.UserName;
        return string.IsNullOrWhiteSpace(name) ? period : $"{period}, {name}";
    }

    /// <summary>
    /// Load projects and recompute the summary counts.
    /// </summary>
    [RelayCommand]
    public async Task LoadProjects()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var models = await _projectService.GetProjectsAsync();
            Projects = models.Select(ProjectListItem.Create).ToList();
            TotalProjects = Projects.Count;
            FavoriteCount = Projects.Count(p => p.IsFavorite);
            RecentlyUsedCount = Projects.Count(p => IsRecentlyUsed(p.Project));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading projects: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Open the selected project's details page and record the usage.
    /// </summary>
    [RelayCommand]
    public async Task OpenProject(ProjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        await _projectService.TouchAsync(item.Project.Id);

        try
        {
            await Shell.Current.GoToAsync("projectdetails");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening project: {ex.Message}";
        }
    }

    /// <summary>
    /// A project counts as "recently used" if it was opened within the last 7 days.
    /// </summary>
    private static bool IsRecentlyUsed(Project project)
    {
        var sevenDaysAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 7 * 24 * 60 * 60;
        return project.LastUsedUnix is { } lastUsed && lastUsed >= sevenDaysAgo;
    }
}
