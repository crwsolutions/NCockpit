using CommunityToolkit.Maui;
using NCockpit.Popups;
using NCockpit.ViewModels;

namespace NCockpit.Pages;

public partial class FoldersPage : ContentPage
{
    private readonly FoldersPageViewModel _viewModel;

    public FoldersPage()
    {
        InitializeComponent();
        
        _viewModel = IPlatformApplication.Current!.Services.GetService<FoldersPageViewModel>()
            ?? throw new InvalidOperationException("FoldersPageViewModel not found in services");
        
        BindingContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.LoadFoldersCommand.ExecuteAsync(null);
    }

    private async void OnAddFolderClicked(object sender, EventArgs e)
    {
        var popupService = IPlatformApplication.Current!.Services.GetService<IPopupService>();
        if (popupService == null)
        {
            await DisplayAlertAsync("Error", "Popup service not found", "OK");
            return;
        }

        var result = await popupService.ShowPopupAsync<AddFolderPopup, string>(Shell.Current);
        
        if (result is not null && !result.WasDismissedByTappingOutsideOfPopup && result.Result is not null)
        {
            // Reload folders after successful add
            await _viewModel.LoadFoldersCommand.ExecuteAsync(null);
        }
    }

    private async void OnOpenFolderClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is Models.ProjectFolder folder)
        {
            _viewModel.OpenFolderCommand.Execute(folder.Path);
        }
    }

    private async void OnDeleteFolderClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.BindingContext is Models.ProjectFolder folder)
        {
            var confirmed = await DisplayAlertAsync("Delete Folder", $"Are you sure you want to delete this folder?\n\n{folder.Path}", "Delete", "Cancel");
            if (confirmed)
            {
                await _viewModel.DeleteFolderCommand.ExecuteAsync(folder.Id);
            }
        }
    }
}
