using CommunityToolkit.Maui;
using NCockpit.ViewModels;

namespace NCockpit.Pages;

public partial class OverviewPage : ContentPage
{
    private readonly OverviewPageViewModel _viewModel;

    public OverviewPage()
    {
        InitializeComponent();

        _viewModel = IPlatformApplication.Current!.Services.GetService<OverviewPageViewModel>()
            ?? throw new InvalidOperationException("OverviewPageViewModel not found in services");

        BindingContext = _viewModel;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _viewModel.LoadProjectsCommand.ExecuteAsync(null);
    }

    private async void OnProjectSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not CollectionView collectionView)
        {
            return;
        }

        var item = e.CurrentSelection.OfType<ProjectListItem>().FirstOrDefault();
        collectionView.SelectedItem = null;

        if (item is not null)
        {
            await _viewModel.OpenProjectCommand.ExecuteAsync(item);
        }
    }
}
