using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Models;
using NCockpit.Services;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the Folders screen.
/// </summary>
public partial class FoldersPageViewModel : ObservableObject
{
    private readonly IProjectFolderService _folderService;

    [ObservableProperty]
    private List<ProjectFolder> folders = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ProjectFolder? selectedFolder;

    public FoldersPageViewModel(IProjectFolderService folderService)
    {
        _folderService = folderService;
    }

    /// <summary>
    /// Load all folders from the service.
    /// </summary>
    [RelayCommand]
    public async Task LoadFolders()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var folders = await _folderService.GetAllFoldersAsync();
            Folders = folders;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error loading folders: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Delete a folder by ID.
    /// </summary>
    [RelayCommand]
    public async Task DeleteFolder(int folderId)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            await _folderService.DeleteFolderAsync(folderId);
            await LoadFolders();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error deleting folder: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Toggle the enabled state of a folder.
    /// </summary>
    [RelayCommand]
    public async Task ToggleFolderEnabled(ProjectFolder folder)
    {
        try
        {
            folder.IsEnabled = !folder.IsEnabled;
            await _folderService.UpdateFolderAsync(folder);
            // Refresh the list
            await LoadFolders();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error updating folder: {ex.Message}";
        }
    }

    /// <summary>
    /// Open a folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    public void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
#if WINDOWS
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/root,\"{path}\"",
                        UseShellExecute = true
                    }
                };
                process.Start();
#endif
            }
            else
            {
                ErrorMessage = "Folder not found";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error opening folder: {ex.Message}";
        }
    }
}
