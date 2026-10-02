using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

/// <summary>
/// Popup that lists all secondary folders that are not linked to a project
/// yet. Selecting a folder links it to the target project and closes the
/// popup with that folder id; the Cancel button closes without a link.
/// All interaction is driven by <see cref="LinkSecondaryPopupViewModel"/>.
/// </summary>
public partial class LinkSecondaryPopup : Popup<LinkSecondaryResult>
{
    public LinkSecondaryPopup(LinkSecondaryPopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Load the folder list when the popup opens, once the visual tree is
        // ready so bindings and layout are stable.
        Opened += async (_, _) => await viewModel.LoadAsync();
    }
}
