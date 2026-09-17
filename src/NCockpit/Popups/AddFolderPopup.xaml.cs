using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

/// <summary>
/// Add Folder popup. All interaction is driven by <see cref="AddFolderPopupViewModel"/>;
/// the code-behind only hosts the constructor and <c>BindingContext</c> assignment.
/// </summary>
public partial class AddFolderPopup : Popup<AddFolderResult>
{
    public AddFolderPopup(AddFolderPopupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
