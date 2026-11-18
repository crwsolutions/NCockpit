using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

/// <summary>
/// View file popup. Shows a file as an HTML document: Markdown is rendered,
/// other text files are shown verbatim. All interaction is driven by
/// <see cref="ViewFilePopupViewModel"/>.
/// </summary>
public partial class ViewFilePopup : Popup
{
    public ViewFilePopup(ViewFilePopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
