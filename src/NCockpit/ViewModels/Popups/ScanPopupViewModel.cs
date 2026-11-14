using CommunityToolkit.Maui;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;

namespace NCockpit.ViewModels.Popups;

/// <summary>
/// Fire-and-forget scan progress popup. The parent view-model owns the
/// <see cref="CancellationTokenSource"/> and the scan task; this VM is only
/// responsible for displaying activity/progress while the scan runs and for
/// calling <c>cts.Cancel()</c> when the user taps Cancel. The parent closes
/// the popup via <c>Shell.Current.ClosePopupAsync()</c> once the work
/// finishes or is cancelled.
/// </summary>
public partial class ScanPopupViewModel : ObservableObject, IQueryAttributable
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    public partial string Title { get; set; } = "Scanning...";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting scan...";

    [ObservableProperty]
    public partial bool IsBusy { get; set; } = true;

    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    /// Receives the cancellation token from the parent view-model via
    /// <c>shellParameters</c>.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("cts", out var ctsObj) && ctsObj is CancellationTokenSource cts)
        {
            _cts = cts;
        }
    }

    /// <summary>
    /// Reset the displayed state. Call this right before (re)showing the
    /// popup for a new scan.
    /// </summary>
    public void Reset()
    {
        Title = "Scanning...";
        StatusText = "Starting scan...";
        IsBusy = true;
        Progress = 0;
    }

    /// <summary>
    /// Invoked when the user taps "Cancel". Cancels the token; the parent
    /// view-model awaits the scan task and closes the popup.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }
}
