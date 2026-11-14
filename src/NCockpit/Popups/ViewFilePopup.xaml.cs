using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

/// <summary>
/// View file popup. Displays the contents of a text file in a read-only editor.
/// All interaction is driven by <see cref="ViewFilePopupViewModel"/>.
/// </summary>
public partial class ViewFilePopup : Popup
{
    public ViewFilePopup(ViewFilePopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
