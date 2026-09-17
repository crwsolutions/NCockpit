using CommunityToolkit.Maui;
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
}
