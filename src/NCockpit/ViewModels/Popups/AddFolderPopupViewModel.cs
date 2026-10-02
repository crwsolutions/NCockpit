using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// Result produced by the Add Folder popup flow.
/// </summary>
public sealed class AddFolderResult
{
    public bool Added { get; init; }

    /// <summary>
    /// The path of the folder the user selected.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// True when the user chose a primary folder (scanned for projects);
    /// false for a secondary folder (first-level subfolders become supporting
    /// folders).
    /// </summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// ViewModel for the Add Folder popup. Collects a folder path and whether it
/// is a primary or secondary folder. The popup closes with the result; the
/// actual adding (scan for primary, supporting folders for secondary) is done
/// by the <c>FoldersPageViewModel</c>.
/// </summary>
public partial class AddFolderPopupViewModel : ObservableObject
{
    [ObservableProperty]
    private string folderPath = string.Empty;

    [ObservableProperty]
    private bool isPrimary = true;

    [ObservableProperty]
    private string? errorMessage;

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
    /// Invoked when the user taps "Add Folder". Validates the input; on success
    /// the popup closes so the <c>FoldersPageViewModel</c> can add the folder.
    /// On validation failure the popup stays open with the error message shown.
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

        if (!Directory.Exists(FolderPath))
        {
            ErrorMessage = "Folder does not exist";
            return;
        }

        await Shell.Current.ClosePopupAsync(new AddFolderResult
        {
            Added = true,
            Path = FolderPath,
            IsPrimary = IsPrimary
        });
    }
}
