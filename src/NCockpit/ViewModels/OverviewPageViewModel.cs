using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Models;
using NCockpit.Services;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the Overview (Projects) screen. Shows a time-of-day greeting,
/// a compact summary, and the project list ordered by last opened and solution
/// file timestamp. Opening a solution records the last opened timestamp, while
/// opening project details does not.
/// </summary>
public partial class OverviewPageViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IWindowsLauncher _windowsLauncher;

    /// <summary>
    /// Greeting line, e.g. "Good afternoon, Colin".
    /// </summary>
    [ObservableProperty]
    private string greeting;

    /// <summary>
    /// Projects ordered by last opened, then solution file timestamp.
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
        IProjectService projectService,
        IWindowsLauncher windowsLauncher)
    {
        _projectService = projectService;
        _windowsLauncher = windowsLauncher;

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
    /// Open the project's solution file with its associated Windows application
    /// and record the last opened timestamp if the launch succeeds.
    /// </summary>
    [RelayCommand]
    public async Task OpenSolution(ProjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (_windowsLauncher.TryOpenFile(item.Project.SolutionPath))
            {
                await _projectService.TouchAsync(item.Project.Id);
            }
            else
            {
                ErrorMessage = $"Could not open {item.Project.SolutionPath}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening solution: {ex.Message}";
        }
    }

    /// <summary>
    /// Open the selected project's details page without recording a last opened
    /// timestamp.
    /// </summary>
    [RelayCommand]
    public async Task OpenProjectDetails(ProjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await Shell.Current.GoToAsync($"projectdetails?id={item.Project.Id}");
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
