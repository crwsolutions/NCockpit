using CommunityToolkit.Maui.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Services;

namespace NCockpit.ViewModels.Popups;

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

    public async Task<bool> ValidateAndAddAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(FolderPath))
        {
            ErrorMessage = "Please enter a folder path";
            return false;
        }

        try
        {
            // Validate folder exists
            if (!Directory.Exists(FolderPath))
            {
                ErrorMessage = "Folder does not exist";
                return false;
            }

            // Check if already configured
            if (await _folderService.FolderExistsAsync(FolderPath))
            {
                ErrorMessage = "This folder is already configured";
                return false;
            }

            // Add the folder (validation happens in service)
            await _folderService.AddFolderAsync(FolderPath, IsPrimary);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            return false;
        }
    }
}
