using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;
using NCockpit.Models;
using NCockpit.Services;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// Result produced by the Link Secondary Folder popup.
/// </summary>
public sealed class LinkSecondaryResult
{
    public bool Linked { get; init; }
    public int? FolderId { get; init; }
}

/// <summary>
/// ViewModel for the Link Secondary Folder popup. Shows all supporting
/// (secondary) folders that are not linked to a project yet, filtered by
/// folder name. Selecting a folder links it to the project and closes the
/// popup with that folder id. The id of the project to link against is
/// received via <c>shellParameters</c>.
/// </summary>
public partial class LinkSecondaryPopupViewModel : ObservableObject, IQueryAttributable
{
    private readonly ISupportingFolderService _supportingFolderService;

    private List<SupportingFolder> _allFolders = [];
    private int projectId;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial List<SupportingFolder> AvailableFolders { get; set; } = [];

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public LinkSecondaryPopupViewModel(ISupportingFolderService supportingFolderService)
    {
        _supportingFolderService = supportingFolderService;
    }

    /// <summary>
    /// Receives the target project id from the parent view-model via
    /// <c>shellParameters</c>.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("projectId", out var projectIdObject) && projectIdObject is int projectIdValue)
        {
            projectId = projectIdValue;
        }
    }

    /// <summary>
    /// Load all unlinked supporting folders and apply the initial (empty)
    /// filter. Called by the popup when it opens.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            _allFolders = await _supportingFolderService.GetUnlinkedAsync();
            AvailableFolders = _allFolders.ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading folders: {ex.Message}";
        }
    }

    /// <summary>
    /// Invoked when the user taps "Cancel" or the header close button.
    /// </summary>
    [RelayCommand]
    private Task Cancel()
    {
        return Shell.Current.ClosePopupAsync(new LinkSecondaryResult { Linked = false });
    }

    /// <summary>
    /// Invoked when the user selects a supporting folder. Links the folder to
    /// the target project and closes the popup with the linked folder id.
    /// </summary>
    [RelayCommand]
    private async Task SelectFolder(SupportingFolder? folder)
    {
        if (folder is null)
        {
            return;
        }

        try
        {
            await _supportingFolderService.LinkToProjectAsync(folder.Id, projectId);
            await Shell.Current.ClosePopupAsync(new LinkSecondaryResult { Linked = true, FolderId = folder.Id });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error linking folder: {ex.Message}";
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterFolders(value);
    }

    /// <summary>
    /// Filter the loaded folders by the search query. The match is on the
    /// folder name; an empty query shows every folder.
    /// </summary>
    private void FilterFolders(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            AvailableFolders = _allFolders.ToList();
            return;
        }

        var queryTrimmed = query.Trim();
        AvailableFolders = _allFolders
            .Where(f => f.FolderName.Contains(queryTrimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
