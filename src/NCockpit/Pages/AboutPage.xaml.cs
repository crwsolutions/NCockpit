using CommunityToolkit.Maui;
using NCockpit.ViewModels;

namespace NCockpit.Pages;

public partial class AboutPage : ContentPage
{
    private readonly AboutPageViewModel _viewModel;

    public AboutPage()
    {
        InitializeComponent();

        _viewModel = IPlatformApplication.Current!.Services.GetService<AboutPageViewModel>()
            ?? throw new InvalidOperationException("AboutPageViewModel not found in services");

        BindingContext = _viewModel;
    }
}
