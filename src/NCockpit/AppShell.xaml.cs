using NCockpit.Pages;

namespace NCockpit;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Project Details is reached by selecting a project on the Overview
        // screen. The route is registered here (rather than via a ShellContent
        // in XAML) so it does not appear in the left navigation flyout.
        Routing.RegisterRoute("projectdetails", typeof(ProjectDetailsPage));
    }
}
