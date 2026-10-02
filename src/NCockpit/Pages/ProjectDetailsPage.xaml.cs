using CommunityToolkit.Maui;
using NCockpit.ViewModels;

namespace NCockpit.Pages;

public partial class ProjectDetailsPage : ContentPage, IQueryAttributable
{
    private readonly ProjectDetailsPageViewModel _viewModel;

    public ProjectDetailsPage()
    {
        InitializeComponent();

        _viewModel = IPlatformApplication.Current!.Services.GetService<ProjectDetailsPageViewModel>()
            ?? throw new InvalidOperationException("ProjectDetailsPageViewModel not found in services");

        BindingContext = _viewModel;
    }

    /// <summary>
    /// Shell invokes this on the target page when navigating with a query
    /// string (e.g. <c>projectdetails?id=123</c>). Forward the parameters to
    /// the view-model, which loads the requested project.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyQueryAttributes(query);
    }
}
