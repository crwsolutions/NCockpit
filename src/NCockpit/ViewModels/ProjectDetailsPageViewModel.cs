using System.Text.Json;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;
using Microsoft.Maui.Graphics;
using NCockpit.Models;
using NCockpit.Popups;
using NCockpit.Services;
using NCockpit.ViewModels.Popups;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the Project Details page. Shows the project identity, the
/// "Solutions &amp; Projects" tree (every solution with its referenced projects
/// and the orphan project files), and the project info of the favorite
/// project. The user can pick exactly one favorite solution and exactly one
/// favorite project (the star on a row); the favorites drive every "Open in
/// Visual Studio" action and the project info panel.
/// </summary>
public partial class ProjectDetailsPageViewModel : ObservableObject, IQueryAttributable
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Colour of the git status row: green for a clean working tree, amber when
    /// it carries changes, gray when the state could not be read.
    /// </summary>
    private static readonly Color GitCleanColor = Color.FromArgb("#16a34a");

    private static readonly Color GitDirtyColor = Color.FromArgb("#b45309");

    private static readonly Color GitUnknownColor = Color.FromArgb("#9ca3af");

    /// <summary>
    /// Guards the project info reads: the newest load owns the panel, so an
    /// earlier one still reading from disk cannot overwrite it.
    /// </summary>
    private int _infoLoadSequence;

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
    [NotifyPropertyChangedFor(nameof(SolutionsTree))]
    [NotifyPropertyChangedFor(nameof(HasSolutionsTree))]
    [NotifyPropertyChangedFor(nameof(OrphanProjects))]
    [NotifyPropertyChangedFor(nameof(HasOrphanProjects))]
    [NotifyPropertyChangedFor(nameof(OpenInIdeText))]
    [NotifyPropertyChangedFor(nameof(IsFolderTypeProject))]
    [NotifyPropertyChangedFor(nameof(ShowSolutionsPanel))]
    [NotifyPropertyChangedFor(nameof(InfoProjectName))]
    [NotifyPropertyChangedFor(nameof(InfoProjectPath))]
    [NotifyPropertyChangedFor(nameof(InfoProjectFolder))]
    [NotifyPropertyChangedFor(nameof(HasInfoProject))]
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
    [NotifyPropertyChangedFor(nameof(HasLinks))]
    public partial List<ReadmeLink> Links { get; set; } = [];

    /// <summary>
    /// The "Solutions &amp; Projects" tree: every solution as an expandable
    /// parent, its referenced projects as children. Rebuilt whenever the
    /// current project's solution data changes.
    /// </summary>
    [ObservableProperty]
    public partial List<SolutionTreeItem> SolutionsTree { get; set; } = [];

    /// <summary>
    /// The orphan project files (not referenced by any solution), shown under
    /// the "Orphans" header at the bottom of the panel.
    /// </summary>
    [ObservableProperty]
    public partial List<SolutionProjectRow> OrphanProjects { get; set; } = [];

    public bool HasLinks => Links.Count > 0;

    /// <summary>
    /// Whether a project is currently selected and can be shown.
    /// </summary>
    public bool HasProject => CurrentProject is not null;

    /// <summary>
    /// Whether the current project is a folder type (opens in VS Code).
    /// </summary>
    public bool IsFolderTypeProject => CurrentProject?.Type == ProjectType.Folder;

    /// <summary>
    /// Whether the "Solutions &amp; Projects" panel is shown. It is hidden for
    /// folder type projects (a repo with no solution files).
    /// </summary>
    public bool ShowSolutionsPanel => CurrentProject?.Type == ProjectType.Solution
        && (SolutionsTree.Count > 0 || OrphanProjects.Count > 0);

    /// <summary>
    /// Whether the solutions tree has any rows.
    /// </summary>
    public bool HasSolutionsTree => SolutionsTree.Count > 0;

    /// <summary>
    /// Whether there are orphan project files to show.
    /// </summary>
    public bool HasOrphanProjects => OrphanProjects.Count > 0;

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
    /// File name of the favorite solution file, e.g. "NCockpit.slnx".
    /// Null for folder type projects.
    /// </summary>
    public string? SolutionFileName =>
        CurrentProject?.SolutionPath is { Length: > 0 } solutionPath
            ? System.IO.Path.GetFileName(solutionPath)
            : null;

    /// <summary>
    /// The full path of the favorite solution file, or null for folder type
    /// projects.
    /// </summary>
    public string? PrimarySolutionPath => CurrentProject?.SolutionPath;

    /// <summary>
    /// Last write time of the favorite solution file as display text, or
    /// "Unknown".
    /// </summary>
    public string SolutionLastWriteDisplay =>
        CurrentProject?.SolutionLastWriteUnix is { } lastWrite
            ? FormatUnix(lastWrite)
            : "Unknown";

    /// <summary>
    /// The favorite project's name, as shown in the Project info panel. Null
    /// when there is no favorite project (folder type or no projects found).
    /// </summary>
    public string? InfoProjectName => FavoriteProject?.Name;

    /// <summary>
    /// The favorite project's file path, as shown in the Project info panel.
    /// Null when there is no favorite project.
    /// </summary>
    public string? InfoProjectPath => FavoriteProject?.ProjectPath;

    /// <summary>
    /// The favorite project's folder, as shown in the Project info panel.
    /// Null when there is no favorite project.
    /// </summary>
    public string? InfoProjectFolder => FavoriteProject?.FolderPath;

    /// <summary>
    /// The kind of the favorite project as its project file shows it, e.g.
    /// ".NET MAUI app", "ASP.NET Core web app", "Console app", "Class library".
    /// Filled by <see cref="LoadProjectInfoAsync"/>.
    /// </summary>
    [ObservableProperty]
    public partial string? InfoProjectType { get; set; }

    /// <summary>
    /// The version the favorite project sets, or "Not set" when it sets none
    /// (the version the SDK derives from the target framework then applies).
    /// </summary>
    [ObservableProperty]
    public partial string? InfoProjectVersion { get; set; }

    /// <summary>
    /// The target framework(s) the favorite project builds for.
    /// </summary>
    [ObservableProperty]
    public partial string? InfoProjectTargetFrameworks { get; set; }

    /// <summary>
    /// The SDK the favorite project declares, e.g. "Microsoft.NET.Sdk".
    /// </summary>
    [ObservableProperty]
    public partial string? InfoProjectSdk { get; set; }

    /// <summary>
    /// When the favorite project was last worked on: the newest write time of
    /// any file in its folder.
    /// </summary>
    [ObservableProperty]
    public partial string? InfoProjectLastModifiedDisplay { get; set; }

    /// <summary>
    /// Whether the current project's folder is a git repository. The git rows of
    /// the info panel only make sense then.
    /// </summary>
    [ObservableProperty]
    public partial bool IsGitRepository { get; set; }

    /// <summary>
    /// The branch the repository has checked out. Read from the repository while
    /// the panel is shown, so it is actual rather than what a scan once stored.
    /// </summary>
    [ObservableProperty]
    public partial string? GitBranchDisplay { get; set; }

    /// <summary>
    /// The working tree as very short stats: "Clean", or "2 staged, 3 modified,
    /// 1 untracked". Null when the folder is no repository.
    /// </summary>
    [ObservableProperty]
    public partial string? GitStatusDisplay { get; set; }

    /// <summary>
    /// Colour of <see cref="GitStatusDisplay"/>: green when the working tree is
    /// clean, amber when it carries changes, gray when the state is unknown.
    /// </summary>
    [ObservableProperty]
    public partial Color GitStatusColor { get; set; } = GitUnknownColor;

    /// <summary>
    /// When the scan last saw the current project.
    /// </summary>
    [ObservableProperty]
    public partial string? LastScannedDisplay { get; set; }

    /// <summary>
    /// Whether a favorite project exists to show in the Project info panel.
    /// </summary>
    public bool HasInfoProject => FavoriteProject is not null;

    /// <summary>
    /// The favorite project (the project the Project info panel is pointed
    /// at), resolved from the project's FavoriteProjectPath. Null for folder
    /// type projects or when no favorite project was chosen and no guess
    /// could be made.
    /// </summary>
    public SolutionProject? FavoriteProject
    {
        get
        {
            if (CurrentProject is not { } project
                || project.Type != ProjectType.Solution
                || project.FavoriteProjectPath is not { Length: > 0 } favoritePath)
            {
                return null;
            }

            var all = AllProjectFiles(project);
            return all.FirstOrDefault(p => string.Equals(p.ProjectPath, favoritePath, StringComparison.OrdinalIgnoreCase));
        }
    }

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

    partial void OnCurrentProjectChanged(Project? value)
    {
        RebuildSolutionsTree();
    }

    partial void OnSolutionsTreeChanged(List<SolutionTreeItem> value)
    {
        OnPropertyChanged(nameof(HasSolutionsTree));
        OnPropertyChanged(nameof(ShowSolutionsPanel));
    }

    partial void OnOrphanProjectsChanged(List<SolutionProjectRow> value)
    {
        OnPropertyChanged(nameof(HasOrphanProjects));
        OnPropertyChanged(nameof(ShowSolutionsPanel));
    }

    /// <summary>
    /// Rebuild the "Solutions &amp; Projects" tree from the current project's
    /// solution data: every solution as an expandable parent (favorite first,
    /// the rest in discovery order), its referenced projects as children, and
    /// the orphan project files under a separate list. The favorite solution
    /// and favorite project get their star.
    /// </summary>
    private void RebuildSolutionsTree()
    {
        var project = CurrentProject;

        if (project is null || project.Type != ProjectType.Solution)
        {
            SolutionsTree = [];
            OrphanProjects = [];
            return;
        }

        var solutions = ParseSolutionPaths(project.SolutionsJson);
        var withProjects = DeserializeSolutionProjects(project.SolutionProjectsJson);
        var favoriteSolutionPath = project.FavoriteSolutionPath is { Length: > 0 } f
            ? f
            : project.SolutionPath;
        var favoriteProjectPath = project.FavoriteProjectPath;

        var withProjectsByPath = withProjects
            .ToDictionary(w => w.Path, w => w.Projects, StringComparer.OrdinalIgnoreCase);

        var tree = new List<SolutionTreeItem>();
        foreach (var solutionPath in solutions)
        {
            withProjectsByPath.TryGetValue(solutionPath, out var projects);
            projects ??= [];

            // Wrap each project in an observable row and stamp the favorite
            // project's star. The same project may appear under several
            // solutions; each occurrence is stamped.
            var rows = projects
                .Select(p => new SolutionProjectRow
                {
                    Project = p,
                    IsFavorite = string.Equals(
                        p.ProjectPath, favoriteProjectPath, StringComparison.OrdinalIgnoreCase)
                })
                .ToList();

            tree.Add(new SolutionTreeItem
            {
                IsSolution = true,
                Name = System.IO.Path.GetFileName(solutionPath),
                Path = solutionPath,
                IsFavorite = string.Equals(solutionPath, favoriteSolutionPath, StringComparison.OrdinalIgnoreCase),
                Projects = rows,
                // The favorite solution starts expanded so its projects (and
                // the favorite project's star) are visible without a tap.
                IsExpanded = string.Equals(solutionPath, favoriteSolutionPath, StringComparison.OrdinalIgnoreCase)
            });
        }

        // The favorite solution first; the rest in discovery order.
        tree = tree
            .OrderBy(i => i.IsFavorite ? 0 : 1)
            .ToList();

        SolutionsTree = tree;

        OrphanProjects = DeserializeOrphanProjects(project.OrphanProjectsJson)
            .Select(p => new SolutionProjectRow
            {
                Project = p,
                IsFavorite = string.Equals(
                    p.ProjectPath, favoriteProjectPath, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();
    }

    private static List<SolutionProject> AllProjectFiles(Project project)
    {
        var withProjects = DeserializeSolutionProjects(project.SolutionProjectsJson);
        return withProjects
            .SelectMany(w => w.Projects)
            .Concat(DeserializeOrphanProjects(project.OrphanProjectsJson))
            .ToList();
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
                Links = [.. links.MobileLinks, .. links.RelatedLinks];
            }

            await LoadProjectInfoAsync();
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
    /// Fill the Project info panel for the current project: what the scan stored
    /// about the selected project file (read from disk when an older scan has
    /// none), the repository's branch and working tree (always read live, so the
    /// panel says what is actual now, not what a scan once found), the project's
    /// last modified time and the time the scan last saw it. Every read runs off
    /// the UI thread and is resilient: an unreadable file or a missing git
    /// executable shows as "Unknown" instead of failing the page.
    /// </summary>
    private async Task LoadProjectInfoAsync()
    {
        if (CurrentProject is not { } project)
        {
            ClearProjectInfo();
            return;
        }

        var sequence = ++_infoLoadSequence;
        var favorite = FavoriteProject;
        var folderPath = project.FolderPath;

        // A row discovered before the scan collected project metadata gets it
        // read here once, so the panel is complete either way.
        Task<ProjectFileMetadata>? metadataTask = favorite is null || HasStoredMetadata(favorite)
            ? null
            : ProjectFileMetadata.ReadAsync(favorite.ProjectPath);

        IsGitRepository = GitInfoDiscovery.IsRepository(folderPath);
        GitBranchDisplay = null;
        GitStatusDisplay = IsGitRepository ? "Checking…" : null;
        GitStatusColor = GitUnknownColor;
        LastScannedDisplay = project.LastSeenUnix > 0
            ? FormatRelativeTime(project.LastSeenUnix)
            : "Never";

        var branchTask = Task.Run(() => GitInfoDiscovery.GetBranch(folderPath));
        var statusTask = GitInfoDiscovery.GetStatusAsync(folderPath);
        var lastWriteTask = Task.Run(() => ResolveLastModifiedUnix(project, favorite));

        var branch = await branchTask;
        var status = await statusTask;
        var lastModifiedUnix = await lastWriteTask;
        var metadata = metadataTask is null ? null : await metadataTask;

        if (sequence != _infoLoadSequence)
        {
            return;
        }

        GitBranchDisplay = branch ?? "Unknown";
        GitStatusDisplay = status?.ToDisplayString() ?? (IsGitRepository ? "Unknown" : null);
        GitStatusColor = status switch
        {
            null => GitUnknownColor,
            { IsClean: true } => GitCleanColor,
            _ => GitDirtyColor
        };

        if (favorite is not null)
        {
            InfoProjectType = favorite.Type ?? metadata?.TypeLabel ?? "Project";
            InfoProjectVersion = favorite.Version ?? metadata?.Version ?? "Not set";
            InfoProjectTargetFrameworks = favorite.TargetFrameworks ?? metadata?.TargetFrameworks ?? "Unknown";
            InfoProjectSdk = favorite.Sdk ?? metadata?.Sdk ?? "Unknown";
            InfoProjectLastModifiedDisplay = lastModifiedUnix is { } lastWrite
                ? FormatRelativeTime(lastWrite)
                : "Unknown";
        }
        else
        {
            // No selected project file: the panel is hidden, so clear what an
            // earlier project left in it rather than showing a mix.
            InfoProjectType = null;
            InfoProjectVersion = null;
            InfoProjectTargetFrameworks = null;
            InfoProjectSdk = null;
            InfoProjectLastModifiedDisplay = null;
        }
    }

    private void ClearProjectInfo()
    {
        _infoLoadSequence++;
        InfoProjectType = null;
        InfoProjectVersion = null;
        InfoProjectTargetFrameworks = null;
        InfoProjectSdk = null;
        InfoProjectLastModifiedDisplay = null;
        GitBranchDisplay = null;
        GitStatusDisplay = null;
        GitStatusColor = GitUnknownColor;
        IsGitRepository = false;
        LastScannedDisplay = null;
    }

    /// <summary>
    /// The newest write time under the selected project's folder: what "last
    /// modified" means for a project. Without a selected project file the
    /// project root stands in, and a stored solution timestamp is the fallback.
    /// </summary>
    private static long? ResolveLastModifiedUnix(Project project, SolutionProject? favorite)
    {
        return ProjectMetadataDiscovery.GetFolderLastWriteUnix(favorite?.FolderPath ?? project.FolderPath)
            ?? project.SolutionLastWriteUnix;
    }

    private static bool HasStoredMetadata(SolutionProject project) =>
        project.Type is not null && project.Sdk is not null && project.TargetFrameworks is not null;

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
    /// Open the favorite solution in Visual Studio. Used for solution type
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
    /// and record the last opened timestamp. Used by the "Solutions &amp;
    /// Projects" tree on the details page.
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
    /// Toggle the expanded state of a solution row in the "Solutions &amp;
    /// Projects" tree.
    /// </summary>
    [RelayCommand]
    private void ToggleSolutionExpand(SolutionTreeItem item)
    {
        if (item is null)
        {
            return;
        }

        item.IsExpanded = !item.IsExpanded;
    }

    /// <summary>
    /// Toggle the favorite solution: the star moves to the tapped solution, or
    /// is re-guessed when the only favorite is tapped off. The invariant is
    /// that exactly one favorite solution always exists.
    /// </summary>
    [RelayCommand]
    private async Task ToggleFavoriteSolutionAsync(string solutionPath)
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;

            var isAlreadyFavorite = string.Equals(
                CurrentProject.FavoriteSolutionPath is { Length: > 0 } f ? f : CurrentProject.SolutionPath,
                solutionPath, StringComparison.OrdinalIgnoreCase);

            // Tapping the favorite solution "turns it off", which immediately
            // re-establishes one via the smart guess. Tapping any other
            // solution moves the star to it.
            await _projectService.SetFavoriteSolutionAsync(CurrentProject.Id, isAlreadyFavorite ? null : solutionPath);

            await ReloadProjectAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error setting favorite solution: {ex.Message}";
        }
    }

    /// <summary>
    /// Toggle the favorite project: the star moves to the tapped project, or
    /// is re-guessed when the only favorite is tapped off. The invariant is
    /// that exactly one favorite project always exists.
    /// </summary>
    [RelayCommand]
    private async Task ToggleFavoriteProjectAsync(string projectPath)
    {
        if (CurrentProject is null)
        {
            return;
        }

        try
        {
            ErrorMessage = null;

            var isAlreadyFavorite = string.Equals(
                CurrentProject.FavoriteProjectPath,
                projectPath, StringComparison.OrdinalIgnoreCase);

            await _projectService.SetFavoriteProjectAsync(CurrentProject.Id, isAlreadyFavorite ? null : projectPath);

            await ReloadProjectAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error setting favorite project: {ex.Message}";
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
    /// Refresh the current project only: re-read every solution's projects,
    /// the orphan project files, the solution last-write time, the root files
    /// and the git remote, then re-parse the README and reload the linked
    /// supporting folders. This does not walk the folder tree or touch other
    /// projects.
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
            Links = [.. links.MobileLinks, .. links.RelatedLinks];

            await LoadLinkedFoldersAsync();
            await LoadProjectInfoAsync();

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

    /// <summary>
    /// Reload the current project from the service and rebuild the dependent
    /// UI state (the tree, the favorites, the project info). Used after the
    /// user changes a favorite.
    /// </summary>
    private async Task ReloadProjectAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        var projects = await _projectService.GetProjectsAsync();
        CurrentProject = projects.FirstOrDefault(p => p.Id == CurrentProject.Id) ?? CurrentProject;

        await LoadProjectInfoAsync();
    }

    private static List<string> ParseSolutionPaths(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json)
                ?.Where(p => p.Length > 0)
                .ToList()
                ?? [];
        }
        catch
        {
            return [];
        }
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

    private static string FormatUnix(long unix) =>
        UnixEpoch.AddSeconds(unix).ToLocalTime().ToString("MMM d, yyyy");

    /// <summary>
    /// Format a Unix timestamp as a relative time string, e.g. "Today 10:24",
    /// "Yesterday 16:42", or "Sep 12, 2026 14:18".
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

        return $"{dateTime:MMM d, yyyy HH:mm}";
    }
}
