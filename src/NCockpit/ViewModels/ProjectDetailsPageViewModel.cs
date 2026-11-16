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
using static System.Net.WebRequestMethods;

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
    [NotifyPropertyChangedFor(nameof(HasProject))]
    [NotifyPropertyChangedFor(nameof(HasGitHubUrl))]
    [NotifyPropertyChangedFor(nameof(Files))]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    [NotifyPropertyChangedFor(nameof(SolutionFileName))]
    [NotifyPropertyChangedFor(nameof(SolutionLastWriteDisplay))]
    public partial Project? CurrentProject { get; set; }

    [ObservableProperty]
    public partial List<SupportingFolder> LinkedFolders { get; set; } = [];

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
    /// Whether the project has a git repo URL configured. Used to toggle
    /// the visibility of the GitHub action.
    /// </summary>
    public bool HasGitHubUrl => CurrentProject?.GitRepoUrl is { Length: > 0 };

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
    /// File name of the solution file, e.g. "NCockpit.slnx".
    /// </summary>
    public string? SolutionFileName =>
        CurrentProject is not null ? System.IO.Path.GetFileName(CurrentProject.SolutionPath) : null;

    /// <summary>
    /// Last write time of the solution file as display text, or "Unknown".
    /// </summary>
    public string SolutionLastWriteDisplay =>
        CurrentProject?.SolutionLastWriteUnix is { } lastWrite
            ? FormatUnix(lastWrite)
            : "Unknown";

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
    /// Open the solution file with its associated Windows application and
    /// record the last opened timestamp.
    /// </summary>
    [RelayCommand]
    private async Task OpenSolutionAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            if (_windowsLauncher.TryOpenFile(CurrentProject.SolutionPath))
            {
                await _projectService.TouchAsync(CurrentProject.Id);
            }
            else
            {
                ErrorMessage = $"Could not open {CurrentProject.SolutionPath}";
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
