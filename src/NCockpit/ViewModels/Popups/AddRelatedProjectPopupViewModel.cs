using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Models;
using NCockpit.Services;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// Result produced by the Add Related Project popup.
/// </summary>
public sealed class AddRelatedProjectResult
{
    public bool Linked { get; init; }
    public int? ProjectId { get; init; }
}

/// <summary>
/// ViewModel for the Add Related Project popup. Lists every project that lives
/// in a primary folder and is not the current project, filtered by name.
/// Selecting a project links it to the current project and closes the popup
/// with that project id. The id of the current project is received via
/// <c>shellParameters</c>.
/// </summary>
public partial class AddRelatedProjectPopupViewModel : ObservableObject, IQueryAttributable
{
    private readonly IRelatedProjectService _relatedProjectService;

    private List<Project> _allProjects = [];
    private int currentProjectId;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial List<Project> AvailableProjects { get; set; } = [];

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public AddRelatedProjectPopupViewModel(IRelatedProjectService relatedProjectService)
    {
        _relatedProjectService = relatedProjectService;
    }

    /// <summary>
    /// Receives the current project id from the parent view-model via
    /// <c>shellParameters</c>.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("projectId", out var projectIdObject) && projectIdObject is int projectIdValue)
        {
            currentProjectId = projectIdValue;
        }
    }

    /// <summary>
    /// Load all candidate projects (primary folders only, the current project
    /// excluded) and apply the initial (empty) filter. Called by the popup
    /// when it opens.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            _allProjects = await _relatedProjectService.GetCandidatesAsync(currentProjectId);
            AvailableProjects = _allProjects.ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading projects: {ex.Message}";
        }
    }

    /// <summary>
    /// Invoked when the user taps "Cancel" or the header close button.
    /// </summary>
    [RelayCommand]
    private Task Cancel()
    {
        return Shell.Current.ClosePopupAsync(new AddRelatedProjectResult { Linked = false });
    }

    /// <summary>
    /// Invoked when the user selects a project. Links it to the current
    /// project and closes the popup with the linked project id.
    /// </summary>
    [RelayCommand]
    private async Task SelectProject(Project? project)
    {
        if (project is null)
        {
            return;
        }

        try
        {
            await _relatedProjectService.LinkAsync(currentProjectId, project.Id);
            await Shell.Current.ClosePopupAsync(new AddRelatedProjectResult { Linked = true, ProjectId = project.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error linking project: {ex.Message}";
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterProjects(value);
    }

    /// <summary>
    /// Filter the loaded projects by the search query. The match is on the
    /// project name; an empty query shows every project.
    /// </summary>
    private void FilterProjects(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            AvailableProjects = _allProjects.ToList();
            return;
        }

        var queryTrimmed = query.Trim();
        AvailableProjects = _allProjects
            .Where(p => p.Name.Contains(queryTrimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
