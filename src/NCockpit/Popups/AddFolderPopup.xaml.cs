using CommunityToolkit.Maui.Views;
using NCockpit.ViewModels.Popups;

namespace NCockpit.Popups;

public partial class AddFolderPopup : Popup<string>
{
    private readonly AddFolderPopupViewModel _viewModel;

    public AddFolderPopup(AddFolderPopupViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        if (await _viewModel.ValidateAndAddAsync())
        {
            // Return the result as "path|isPrimary" string
            string result = $"{_viewModel.FolderPath}|{_viewModel.IsPrimary}";
            await CloseAsync(result);
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await CloseAsync(null);
    }
}
