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
/// ViewModel for the Folders screen.
/// </summary>
public partial class FoldersPageViewModel : ObservableObject
{
    private readonly IProjectFolderService _folderService;
    private readonly IScanService _scanService;
    private readonly IPopupService _popupService;
    private readonly ScanPopupViewModel _scanPopupViewModel;
    private readonly IPageService _pageService;

    private CancellationTokenSource? _scanCts;

    [ObservableProperty]
    private List<ProjectFolder> folders = [];

    [ObservableProperty]
    private bool isLoading;

    /// <summary>
    /// Transient status message shown at the bottom of the page (e.g. after a
    /// scan). Includes a timestamp so the user knows when the action happened.
    /// </summary>
    [ObservableProperty]
    private string? statusText;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private ProjectFolder? selectedFolder;

    public FoldersPageViewModel(
        IProjectFolderService folderService,
        IScanService scanService,
        IPopupService popupService,
        ScanPopupViewModel scanPopupViewModel,
        IPageService pageService)
    {
        _folderService = folderService;
        _scanService = scanService;
        _popupService = popupService;
        _scanPopupViewModel = scanPopupViewModel;
        _pageService = pageService;
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
    /// Open the Add Folder popup and, on success, scan the new folder and reload
    /// the folder list. Cancellation is a no-op for this page.
    /// </summary>
    [RelayCommand]
    public async Task AddFolder()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var result = await _popupService.ShowPopupAsync<AddFolderPopup, AddFolderResult>(Shell.Current);

            if (result is null || result.WasDismissedByTappingOutsideOfPopup || result.Result is null)
            {
                return;
            }

            var addResult = result.Result;
            if (!addResult.Added || addResult.FolderId is not int folderId)
            {
                return;
            }

            // Scan the newly added folder and save its projects to the database.
            await RunScanWithPopupAsync(folderId);
            SetScanCompletedStatus(addResult.FolderPath ?? string.Empty);

            await LoadFolders();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error adding folder: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Show the scan progress popup and run a single-folder scan for the given
    /// folder. Discovered projects are saved to the database.
    /// </summary>
    [RelayCommand]
    public async Task ScanFolder(ProjectFolder folder)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            await RunScanWithPopupAsync(folder.Id);
            SetScanCompletedStatus(folder.Path);

            await LoadFolders();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error scanning folder: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Common scan-with-popup flow: create a cancellation token, show the
    /// progress popup (fire-and-forget), await the scan, and close the popup.
    /// Used by both <see cref="AddFolder"/> and <see cref="ScanFolder(ProjectFolder)"/>.
    /// </summary>
    private async Task RunScanWithPopupAsync(int? folderId)
    {
        _scanCts?.Cancel();
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();

        _scanPopupViewModel.Reset();

        // Fire-and-forget: ShowPopup returns immediately, so the popup stays
        // open while the scan runs. It is closed via ClosePopupAsync() below.
        _popupService.ShowPopup<ScanPopup>(
            Shell.Current,
            options: new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false },
            shellParameters: new Dictionary<string, object> { ["cts"] = _scanCts });

        var progress = new Progress<ScanProgress>(p =>
        {
            _scanPopupViewModel.Progress = Math.Clamp(p.Percentage, 0, 100);
            _scanPopupViewModel.StatusText = p.Status;
        });

        try
        {
            await _scanService.ScanAsync(folderId, progress, _scanCts.Token);

            _scanPopupViewModel.Title = "Scan complete";
            _scanPopupViewModel.StatusText = folderId.HasValue ? "Projects saved." : "Projects updated.";
            _scanPopupViewModel.IsBusy = false;
        }
        catch (OperationCanceledException)
        {
            _scanPopupViewModel.Title = "Scan cancelled";
            _scanPopupViewModel.StatusText = "The scan was stopped.";
            _scanPopupViewModel.IsBusy = false;
        }
        finally
        {
            await Shell.Current.ClosePopupAsync();
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    /// <summary>
    /// Set a "Scan completed" status message with a timestamp, shown at the
    /// bottom of the page.
    /// </summary>
    private void SetScanCompletedStatus(string folderPath)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        StatusText = $"Scan completed at {timestamp} — {folderPath}";
    }

    /// <summary>
    /// Delete a folder by ID after showing a confirmation dialog.
    /// </summary>
    [RelayCommand]
    public async Task DeleteFolder(ProjectFolder folder)
    {
        try
        {
            var confirmed = await _pageService.ConfirmAsync(
                "Delete Folder",
                $"Are you sure you want to delete this folder?\n\n{folder.Path}",
                "Delete", "Cancel");

            if (!confirmed)
            {
                return;
            }

            IsLoading = true;
            ErrorMessage = null;

            await _folderService.DeleteFolderAsync(folder.Id);
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
