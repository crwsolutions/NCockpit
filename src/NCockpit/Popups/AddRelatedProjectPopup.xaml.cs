using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

/// <summary>
/// Popup that lists every project living in a primary folder (except the
/// current one). Selecting a project links it to the current project and
/// closes the popup with that project id; the Cancel button closes without a
/// link. All interaction is driven by
/// <see cref="AddRelatedProjectPopupViewModel"/>.
/// </summary>
public partial class AddRelatedProjectPopup : Popup<AddRelatedProjectResult>
{
    public AddRelatedProjectPopup(AddRelatedProjectPopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Load the project list when the popup opens, once the visual tree is
        // ready so bindings and layout are stable.
        Opened += async (_, _) => await viewModel.LoadAsync();
    }
}
