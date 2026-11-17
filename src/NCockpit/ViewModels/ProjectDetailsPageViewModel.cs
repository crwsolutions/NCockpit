using System.Text.Json;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;
using NCockpit.Models;
using NCockpit.Popups;
using NCockpit.Services;
using NCockpit.ViewModels.Popups;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the Project Details page. Shows the real project identity
/// and location data, and allows managing the supporting folders that are
/// linked to the project (materials that must not end up in the project's
/// source control).
/// </summary>
public partial class ProjectDetailsPageViewModel : ObservableObject, IQueryAttributable
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IProjectService _projectService;
    private readonly ISupportingFolderService _supportingFolderService;
    private readonly IPopupService _popupService;
    private readonly IWindowsLauncher _windowsLauncher;
    private readonly IReadmeLinkParserService _readmeLinkParser;

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    /// <summary>
    /// When the project was last refreshed, shown next to the refresh button.
    /// </summary>
    [ObservableProperty]
    public partial string? LastRefreshedDisplay { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(Files))]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    [NotifyPropertyChangedFor(nameof(SolutionFileName))]
    [NotifyPropertyChangedFor(nameof(PrimarySolutionPath))]
    [NotifyPropertyChangedFor(nameof(SolutionLastWriteDisplay))]
    [NotifyPropertyChangedFor(nameof(SolutionProjects))]
    [NotifyPropertyChangedFor(nameof(HasSolutionProjects))]
    [NotifyPropertyChangedFor(nameof(Solutions))]
    [NotifyPropertyChangedFor(nameof(HasSolutions))]
    [NotifyPropertyChangedFor(nameof(OpenInIdeText))]
    [NotifyPropertyChangedFor(nameof(IsFolderTypeProject))]
    public partial Project? CurrentProject { get; set; }

    [ObservableProperty]
    public partial List<SupportingFolder> LinkedFolders { get; set; } = [];

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMobileLinks))]
    public partial List<ReadmeLink> MobileLinks { get; set; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRelatedLinks))]
    public partial List<ReadmeLink> RelatedLinks { get; set; } = [];

    public bool HasMobileLinks => MobileLinks.Count > 0;

    public bool HasRelatedLinks => RelatedLinks.Count > 0;

    /// <summary>
    /// Whether a project is currently selected and can be shown.
    /// </summary>
    public bool HasProject => CurrentProject is not null;

    /// <summary>
    /// Whether the current project is a folder type (opens in VS Code).
    /// </summary>
    public bool IsFolderTypeProject => CurrentProject?.Type == ProjectType.Folder;

    /// <summary>
    /// Whether any supporting folders are linked to the current project.
    /// </summary>
    public bool HasLinkedFolders => LinkedFolders.Count > 0;

    /// <summary>
    /// Root files discovered during scanning, parsed from the project's
    /// FilesJson. Returns an empty list when no files were recorded.
    /// </summary>
    public List<string> Files
    {
        get
        {
            if (CurrentProject?.FilesJson is not { Length: > 0 } json)
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Whether any root files were discovered for the current project.
    /// </summary>
    public bool HasFiles => Files.Count > 0;

    /// <summary>
    /// File name of the primary solution file, e.g. "NCockpit.slnx".
    /// Null for folder type projects.
    /// </summary>
    public string? SolutionFileName =>
        CurrentProject?.SolutionPath is { Length: > 0 } solutionPath
            ? System.IO.Path.GetFileName(solutionPath)
            : null;

    /// <summary>
    /// The full path of the primary solution file, or null for folder type
    /// projects.
    /// </summary>
    public string? PrimarySolutionPath => CurrentProject?.SolutionPath;

    /// <summary>
    /// Last write time of the primary solution file as display text, or
    /// "Unknown".
    /// </summary>
    public string SolutionLastWriteDisplay =>
        CurrentProject?.SolutionLastWriteUnix is { } lastWrite
            ? FormatUnix(lastWrite)
            : "Unknown";

    /// <summary>
    /// The projects referenced by the primary solution file, parsed from the
    /// project's ProjectsJson. Returns an empty list when no projects were
    /// recorded.
    /// </summary>
    public List<SolutionProject> SolutionProjects
    {
        get
        {
            if (CurrentProject?.ProjectsJson is not { Length: > 0 } json)
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
    }

    /// <summary>
    /// Whether any projects were resolved for the current solution.
    /// </summary>
    public bool HasSolutionProjects => SolutionProjects.Count > 0;

    /// <summary>
    /// The solutions (.sln / .slnx) discovered inside the project, parsed
    /// from the project's SolutionsJson. Returns an empty list when no
    /// solutions were recorded.
    /// </summary>
    public List<SolutionEntry> Solutions
    {
        get
        {
            if (CurrentProject?.SolutionsJson is not { Length: > 0 } json)
            {
                return [];
            }

            try
            {
                var paths = JsonSerializer.Deserialize<List<string>>(json) ?? [];
                return paths
                    .Where(p => p.Length > 0)
                    .Select(p => new SolutionEntry
                    {
                        Name = System.IO.Path.GetFileName(p),
                        Path = p
                    })
                    .ToList();
            }
            catch
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Whether any solutions were discovered for the current project.
    /// </summary>
    public bool HasSolutions => Solutions.Count > 0;

    /// <summary>
    /// Label for the open-in-IDE quick action, matching the project type:
    /// "Open in VS Code" for folder type, "Open in Visual Studio" for
    /// solution type.
    /// </summary>
    public string OpenInIdeText =>
        CurrentProject?.Type == ProjectType.Folder
            ? "Open in VS Code"
            : "Open in Visual Studio";

    public ProjectDetailsPageViewModel(
        IProjectService projectService,
        ISupportingFolderService supportingFolderService,
        IPopupService popupService,
        IWindowsLauncher windowsLauncher,
        IReadmeLinkParserService readmeLinkParser)
    {
        _projectService = projectService;
        _supportingFolderService = supportingFolderService;
        _popupService = popupService;
        _windowsLauncher = windowsLauncher;
        _readmeLinkParser = readmeLinkParser;
    }

    /// <summary>
    /// Receives the project id from the overview page via
    /// <c>GoToAsync("projectdetails?id=123")</c>.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var idObject) && int.TryParse(idObject?.ToString(), out var id))
        {
            _ = LoadProjectAsync(id);
        }
    }


    partial void OnLinkedFoldersChanged(List<SupportingFolder> value)
    {
        OnPropertyChanged(nameof(HasLinkedFolders));
    }

    partial void OnIsFavoriteChanged(bool value)
    {
        if (CurrentProject is not null)
        {
            CurrentProject.IsFavorite = value;
        }
    }

    /// <summary>
    /// Load the project by id and its linked supporting folders.
    /// </summary>
    public async Task LoadProjectAsync(int projectId)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var projects = await _projectService.GetProjectsAsync();
            CurrentProject = projects.FirstOrDefault(p => p.Id == projectId);
            IsFavorite = CurrentProject?.IsFavorite ?? false;

            // A freshly loaded project has not been refreshed in this session.
            LastRefreshedDisplay = null;

            await LoadLinkedFoldersAsync();

            if (CurrentProject is not null)
            {
                var links = await _readmeLinkParser.ParseLinksAsync(CurrentProject.FolderPath);
                MobileLinks = links.MobileLinks;
                RelatedLinks = links.RelatedLinks;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading project: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Load the supporting folders linked to the current project.
    /// </summary>
    private async Task LoadLinkedFoldersAsync()
    {
        if (CurrentProject is null)
        {
            LinkedFolders = [];
            return;
        }

        LinkedFolders = await _supportingFolderService.GetLinkedToProjectAsync(CurrentProject.Id);
    }

    /// <summary>
    /// Open the Link Secondary Folder popup to associate an unlinked supporting
    /// folder with the current project.
    /// </summary>
    [RelayCommand]
    private async Task LinkSecondaryFolderAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;

            var result = await _popupService.ShowPopupAsync<LinkSecondaryPopup, LinkSecondaryResult>(
                Shell.Current,
                shellParameters: new Dictionary<string, object> { ["projectId"] = CurrentProject.Id });

            if (result is not null && !result.WasDismissedByTappingOutsideOfPopup && result.Result is not null)
            {
                if (result.Result.Linked)
                {
                    await LoadLinkedFoldersAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error linking folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Remove the link between a supporting folder and the current project.
    /// No confirmation by design.
    /// </summary>
    [RelayCommand]
    private async Task UnlinkSecondaryFolderAsync(SupportingFolder folder)
    {
        try
        {
            ErrorMessage = null;
            await _supportingFolderService.UnlinkFromProjectAsync(folder.Id);
            await LoadLinkedFoldersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error unlinking folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Open the project's folder in Visual Studio Code and record the last
    /// opened timestamp if the launch succeeds. Used for folder type projects.
    /// </summary>
    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;

            if (_windowsLauncher.TryOpenInVsCode(CurrentProject.FolderPath))
            {
                await _projectService.TouchAsync(CurrentProject.Id);
            }
            else
            {
                ErrorMessage = $"Could not open {CurrentProject.FolderPath} in Visual Studio Code. Is VS Code installed?";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Open the primary solution in Visual Studio. Used for solution type
    /// projects. Records the last opened timestamp.
    /// </summary>
    [RelayCommand]
    private async Task OpenSolutionAsync()
    {
        if (CurrentProject?.SolutionPath is not { Length: > 0 } solutionPath)
        {
            return;
        }

        await OpenSolutionFileAsync(solutionPath);
    }

    /// <summary>
    /// Open a specific solution file with its associated Windows application
    /// and record the last opened timestamp. Used by the Solutions list on
    /// the details page.
    /// </summary>
    [RelayCommand]
    private async Task OpenSolutionFileAsync(string solutionPath)
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            if (_windowsLauncher.TryOpenFile(solutionPath))
            {
                await _projectService.TouchAsync(CurrentProject.Id);
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
    /// Navigate back to the projects overview.
    /// </summary>
    [RelayCommand]
    private async Task BackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    /// <summary>
    /// Open a location (project folder or linked supporting folder) in
    /// Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenLocation(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                ErrorMessage = "Folder not found";
                return;
            }

#if WINDOWS
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/root,\"{path}\"",
                UseShellExecute = true
            });
#else
            ErrorMessage = "Opening folders is only supported on Windows";
#endif
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Toggle the favorite flag of the current project. The star next to the
    /// title reflects the new state.
    /// </summary>
    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            IsFavorite = await _projectService.ToggleFavoriteAsync(CurrentProject.Id);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error toggling favorite: {ex.Message}";
        }
    }

    /// <summary>
    /// Refresh the current project only: re-read its on-disk metadata (solution
    /// last-write time, root files, git remote, referenced solution projects),
    /// then re-parse the README and reload the linked supporting folders. This
    /// does not walk the folder tree or touch other projects.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRefreshProject))]
    private async Task RefreshProjectAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        IsRefreshing = true;

        try
        {
            ErrorMessage = null;

            var refreshed = await _projectService.RefreshProjectAsync(CurrentProject);
            CurrentProject = refreshed;

            var links = await _readmeLinkParser.ParseLinksAsync(refreshed.FolderPath, cache: false);
            MobileLinks = links.MobileLinks;
            RelatedLinks = links.RelatedLinks;

            await LoadLinkedFoldersAsync();

            LastRefreshedDisplay = $"Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error refreshing project: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private bool CanRefreshProject() => CurrentProject is not null && !IsRefreshing;

    /// <summary>
    /// Open the project's git repo URL in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        if (CurrentProject is null || string.IsNullOrEmpty(CurrentProject.GitRepoUrl))
        {
            return;
        }

        try
        {
            if (!_windowsLauncher.TryOpenUrl(CurrentProject.GitRepoUrl))
            {
                ErrorMessage = $"Could not open {CurrentProject.GitRepoUrl}";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening GitHub: {ex.Message}";
        }
    }

    private static string FormatUnix(long unix) =>
        UnixEpoch.AddSeconds(unix).ToLocalTime().ToString("MMM d, yyyy");
}
