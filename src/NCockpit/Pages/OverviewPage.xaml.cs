using CommunityToolkit.Maui;
using NCockpit.ViewModels;

namespace NCockpit.Pages;

public partial class OverviewPage : ContentPage
{
    public OverviewPage()
    {
        InitializeComponent();

        var viewModel = IPlatformApplication.Current!.Services.GetService<OverviewPageViewModel>()
            ?? throw new InvalidOperationException("OverviewPageViewModel not found in services");

        BindingContext = viewModel;
    }
}
