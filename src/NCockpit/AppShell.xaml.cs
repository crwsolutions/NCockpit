using NCockpit.Pages;
using NCockpit.Repositories;

namespace NCockpit;

public partial class AppShell : Shell
{
    public const string FlyoutMinimizedSettingKey = "FlyoutMinimized";
    public const double FlyoutMinimizedWidth = 65;
    private const double FlyoutDefaultWidth = 200;

    private readonly IAppSettingsRepository _settings;
    private double? _flyoutWidth;

    public AppShell(IAppSettingsRepository settings)
    {
        _settings = settings;
        InitializeComponent();

        // Project Details is reached by selecting a project on the Overview
        // screen. The route is registered here (rather than via a ShellContent
        // in XAML) so it does not appear in the left navigation flyout.
        Routing.RegisterRoute("projectdetails", typeof(ProjectDetailsPage));

        // Restore the persisted collapsed state before the window shows.
        if (_settings.GetValueAsync(FlyoutMinimizedSettingKey).GetAwaiter().GetResult() == "true")
        {
            FlyoutWidth = FlyoutMinimizedWidth;
            Expander.Text = "\uf152"; // angle-right glyph (collapsed)
        }
        else
        {
            FlyoutWidth = FlyoutDefaultWidth;
            Expander.Text = "\uf191"; // angle-left glyph (expanded)
        }

        _flyoutWidth = FlyoutDefaultWidth;
    }

    private void TapGestureRecognizer_Tapped(object? sender, TappedEventArgs e)
    {
        _flyoutWidth ??= FlyoutWidth;

        if (FlyoutWidth > FlyoutMinimizedWidth)
        {
            Animate(_flyoutWidth.Value, FlyoutMinimizedWidth, "\uf152");
        }
        else
        {
            Animate(FlyoutMinimizedWidth, _flyoutWidth.Value, "\uf191");
        }
    }

    private void Animate(double a, double b, string c)
    {
        var animation = new Animation(v => FlyoutWidth = v, a, b);
        animation.Commit(this, "FlyoutWidthAnimation", easing: Easing.CubicInOut, finished: (_, _) =>
        {
            Expander.Text = c;

            _ = _settings.SetAsync(FlyoutMinimizedSettingKey, b <= FlyoutMinimizedWidth ? "true" : "false"); // Save state
        });
    }
}
