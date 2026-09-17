using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Services;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// Result produced by the Add Folder popup flow.
/// </summary>
public sealed class AddFolderResult
{
    public bool Added { get; init; }
    public int? FolderId { get; init; }
    public string? FolderPath { get; init; }
    public bool IsPrimary { get; init; }
}

/// <summary>
/// ViewModel for the Add Folder popup. Drives the popup's footer buttons and
/// closes the popup via <c>Shell.Current.ClosePopupAsync(result)</c> so the
/// parent <c>FoldersPageViewModel</code> can read the result and initiate the
/// scan.
/// </summary>
public partial class AddFolderPopupViewModel : ObservableObject
{
    private readonly IProjectFolderService _folderService;

    [ObservableProperty]
    private string folderPath = string.Empty;

    [ObservableProperty]
    private bool isPrimary = true;

    [ObservableProperty]
    private string? errorMessage;

    public AddFolderPopupViewModel(IProjectFolderService folderService)
    {
        _folderService = folderService;
    }

    /// <summary>
    /// Invoked when the user taps the folder browser ("...") button.
    /// </summary>
    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync();
            if (!result.IsSuccessful || result.Folder is null || string.IsNullOrEmpty(result.Folder.Path))
            {
                return;
            }

            FolderPath = result.Folder.Path;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error picking a folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Invoked when the user taps "Cancel" or the header close button.
    /// Closes the popup with a "not added" result so the caller can tell the
    /// user walked away from the dialog.
    /// </summary>
    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.ClosePopupAsync(new AddFolderResult { Added = false });
    }

    /// <summary>
    /// Invoked when the user taps "Add Folder". Validates and persists the
    /// folder; on success the popup closes with the new folder id so the
    /// parent view-model can initiate the scan. On validation failure the
    /// popup stays open with the error message shown.
    /// </summary>
    [RelayCommand]
    private async Task AddFolderAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            ErrorMessage = "Please enter a folder path";
            return;
        }

        try
        {
            if (!Directory.Exists(FolderPath))
            {
                ErrorMessage = "Folder does not exist";
                return;
            }

            if (await _folderService.FolderExistsAsync(FolderPath))
            {
                ErrorMessage = "This folder is already configured";
                return;
            }

            var added = await _folderService.AddFolderAsync(FolderPath, IsPrimary);

            await Shell.Current.ClosePopupAsync(new AddFolderResult
            {
                Added = true,
                FolderId = added.Id,
                FolderPath = added.Path,
                IsPrimary = IsPrimary
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
    }
}
