using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NCockpit.Popups;
using NCockpit.Services;
using NCockpit.ViewModels.Popups;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the Overview screen. Currently only hosts the "Scan all"
/// action, which runs a full scan of all enabled primary folders and shows
/// a progress popup that the user can cancel.
/// </summary>
public partial class OverviewPageViewModel : ObservableObject
{
    private readonly IScanService _scanService;
    private readonly IPopupService _popupService;
    private readonly ScanPopupViewModel _scanPopupViewModel;

    private CancellationTokenSource? _scanCts;

    public OverviewPageViewModel(IScanService scanService, IPopupService popupService, ScanPopupViewModel scanPopupViewModel)
    {
        _scanService = scanService;
        _popupService = popupService;
        _scanPopupViewModel = scanPopupViewModel;
    }

    /// <summary>
    /// Scan all enabled primary folders: merge discovered projects into the
    /// database and remove projects that no longer exist on disk. The progress
    /// popup stays open until the scan completes or the user cancels.
    /// </summary>
    [RelayCommand]
    public async Task Scan()
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
            await _scanService.ScanAsync(null, progress, _scanCts.Token);

            _scanPopupViewModel.Title = "Scan complete";
            _scanPopupViewModel.StatusText = "Projects updated.";
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
}
