using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

/// <summary>
/// Scan progress popup. Fire-and-forget: the parent view-model shows it with
/// <c>ShowPopup</c>, awaits the scan task, and closes it via
/// <c>Shell.Current.ClosePopupAsync()</c>. All interaction is driven by
/// <see cref="ScanPopupViewModel"/>.
/// </summary>
public partial class ScanPopup : Popup
{
    public ScanPopup(ScanPopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
