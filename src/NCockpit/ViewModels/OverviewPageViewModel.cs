using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Models;
using NCockpit.Popups;
using NCockpit.Services;
using NCockpit.ViewModels.Popups;

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
    private readonly IScanService _scanService;
    private readonly IPopupService _popupService;
    private readonly ScanPopupViewModel _scanPopupViewModel;
    private CancellationTokenSource? _scanCts;

    /// <summary>
    /// Greeting line, e.g. "Good afternoon, Colin".
    /// </summary>
    [ObservableProperty]
    public partial string Greeting { get; set; }

    /// <summary>
    /// All projects ordered by last opened, then solution file timestamp.
    /// </summary>
    private List<ProjectListItem> _allProjects = [];

    /// <summary>
    /// Projects shown in the list (filtered by search).
    /// </summary>
    [ObservableProperty]
    public partial List<ProjectListItem> Projects { get; set; } = [];

    /// <summary>
    /// Currently selected project. Clicking a project selects it; clicking
    /// the chevron navigates to details.
    /// </summary>
    [ObservableProperty]
    public partial ProjectListItem? SelectedProject { get; set; }

    /// <summary>
    /// Search filter text.
    /// </summary>
    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalProjects { get; set; }

    [ObservableProperty]
    public partial int FavoriteCount { get; set; }

    [ObservableProperty]
    public partial int RecentlyUsedCount { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool ShowFavoritesOnly { get; set; }

    /// <summary>
    /// Most recently opened projects for the Recent Activity sidebar.
    /// </summary>
    [ObservableProperty]
    public partial List<ProjectListItem> RecentActivity { get; set; } = [];

    public OverviewPageViewModel(
        IProjectService projectService,
        IWindowsLauncher windowsLauncher,
        IScanService scanService,
        IPopupService popupService,
        ScanPopupViewModel scanPopupViewModel)
    {
        _projectService = projectService;
        _windowsLauncher = windowsLauncher;
        _scanService = scanService;
        _popupService = popupService;
        _scanPopupViewModel = scanPopupViewModel;

        Greeting = BuildGreeting();
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplySearchFilter(value);
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
            _allProjects = models.Select(ProjectListItem.Create).ToList();
            ApplySearchFilter(SearchQuery);

            TotalProjects = _allProjects.Count;
            FavoriteCount = _allProjects.Count(p => p.IsFavorite);
            RecentlyUsedCount = _allProjects.Count(p => IsRecentlyUsed(p.Project));

            // Most recently opened projects for the sidebar
            RecentActivity = _allProjects.Take(5).ToList();
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
    /// Select a project (highlight it) without opening the solution or
    /// navigating.
    /// </summary>
    [RelayCommand]
    public void SelectProject(ProjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        // Deselect previously selected item
        if (SelectedProject is not null && SelectedProject != item)
        {
            SelectedProject.IsSelected = false;
        }

        item.IsSelected = true;
        SelectedProject = item;
    }

    /// <summary>
    /// Toggle the favorite flag for a project.
    /// </summary>
    [RelayCommand]
    public async Task ToggleFavorite(ProjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var newValue = await _projectService.ToggleFavoriteAsync(item.Project.Id);
            item.IsFavorite = newValue;
            item.Project.IsFavorite = newValue;
            FavoriteCount = _allProjects.Count(p => p.IsFavorite);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error toggling favorite: {ex.Message}";
        }
    }

    /// <summary>
    /// Rescan all folders and reload the project list.
    /// </summary>
    [RelayCommand]
    public async Task RescanAll()
    {
        try
        {
            _scanCts?.Cancel();
            _scanCts?.Dispose();
            _scanCts = new CancellationTokenSource();

            _scanPopupViewModel.Reset();
            _popupService.ShowPopup<ScanPopup>(
                Shell.Current,
                shellParameters: new Dictionary<string, object> { ["cts"] = _scanCts });

            var progress = new Progress<ScanProgress>(p =>
            {
                _scanPopupViewModel.Progress = Math.Clamp(p.Percentage, 0, 100);
                _scanPopupViewModel.StatusText = p.Status;
            });

            await _scanService.ScanAsync(null, progress, _scanCts.Token);

            _scanPopupViewModel.Title = "Scan complete";
            _scanPopupViewModel.StatusText = "Projects updated.";
            _scanPopupViewModel.IsBusy = false;

            await Shell.Current.ClosePopupAsync();

            await LoadProjects();
        }
        catch (OperationCanceledException)
        {
            _scanPopupViewModel.Title = "Scan cancelled";
            _scanPopupViewModel.StatusText = "The scan was stopped.";
            _scanPopupViewModel.IsBusy = false;
            await Shell.Current.ClosePopupAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error rescanning: {ex.Message}";
        }
        finally
        {
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    /// <summary>
    /// Open the last project that was opened.
    /// </summary>
    [RelayCommand]
    public async Task OpenLastProject()
    {
        if (_allProjects.Count == 0)
        {
            return;
        }

        // Projects are already sorted by last opened
        var lastProject = _allProjects[0];
        await OpenProjectDetails(lastProject);
    }

    /// <summary>
    /// Open the selected project's folder in File Explorer.
    /// </summary>
    [RelayCommand]
    public void OpenInExplorer()
    {
        if (SelectedProject is null)
        {
            return;
        }

        try
        {
            _windowsLauncher.TryOpenFolder(SelectedProject.Project.FolderPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Open a project in its IDE: the primary solution in Visual Studio for
    /// solution type projects, the repo folder in VS Code for folder type
    /// projects. Used by both the quick actions (no parameter, so the selected
    /// project) and the per-row shortcuts in the project list.
    /// </summary>
    [RelayCommand]
    public async Task OpenInIDE(ProjectListItem? item)
    {
        item ??= SelectedProject;
        if (item is null)
        {
            return;
        }

        if (item.IsFolderType)
        {
            await OpenInVsCode(item);
        }
        else
        {
            await OpenSolution(item);
        }
    }

    /// <summary>
    /// Open the project's folder in Visual Studio Code and record the last
    /// opened timestamp if the launch succeeds.
    /// </summary>
    [RelayCommand]
    public async Task OpenInVsCode(ProjectListItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (_windowsLauncher.TryOpenInVsCode(item.Project.FolderPath))
            {
                await _projectService.TouchAsync(item.Project.Id);
            }
            else
            {
                ErrorMessage = $"Could not open {item.Project.FolderPath} in Visual Studio Code. Is VS Code installed?";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Open the selected project's git repo URL in the default browser.
    /// </summary>
    [RelayCommand]
    public void OpenGitHub()
    {
        if (SelectedProject is null || string.IsNullOrEmpty(SelectedProject.Project.GitRepoUrl))
        {
            return;
        }

        try
        {
            if (!_windowsLauncher.TryOpenUrl(SelectedProject.Project.GitRepoUrl))
            {
                ErrorMessage = $"Could not open {SelectedProject.Project.GitRepoUrl}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening GitHub: {ex.Message}";
        }
    }

    /// <summary>
    /// Toggle the favorites-only filter.
    /// </summary>
    [RelayCommand]
    public void ToggleFavoritesFilter()
    {
        ShowFavoritesOnly = !ShowFavoritesOnly;
        ApplySearchFilter(SearchQuery);
    }

    /// <summary>
    /// Apply the search and favorites filters to the project list.
    /// </summary>
    private void ApplySearchFilter(string query)
    {
        IEnumerable<ProjectListItem> filtered = _allProjects;

        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(p => p.IsFavorite);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim().ToLowerInvariant();
            filtered = filtered.Where(p => p.Name?.ToLowerInvariant().Contains(normalized) == true
                                       || p.FolderPath?.ToLowerInvariant().Contains(normalized) == true);
        }

        Projects = filtered.ToList();
    }

    /// <summary>
    /// Open the project's primary solution file with its associated Windows
    /// application and record the last opened timestamp if the launch succeeds.
    /// </summary>
    [RelayCommand]
    public async Task OpenSolution(ProjectListItem? item)
    {
        if (item is null || item.Project.SolutionPath is not { Length: > 0 } solutionPath)
        {
            return;
        }

        try
        {
            if (_windowsLauncher.TryOpenFile(solutionPath))
            {
                await _projectService.TouchAsync(item.Project.Id);
            }
            else
            {
                ErrorMessage = $"Could not open {solutionPath}";
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
