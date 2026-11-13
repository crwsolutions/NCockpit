using Microsoft.Maui.Devices;

namespace NCockpit;

public partial class AppWindow : Window
{
    public AppWindow(AppShell appShell)
    {
        InitializeComponent();
        Page = appShell;

        var desiredWidth = 1600.0;
        var desiredHeight = 1000.0;

        var mainDisplay = DeviceDisplay.Current.MainDisplayInfo;
        Width = Math.Min(mainDisplay.Width, desiredWidth);
        Height = Math.Min(mainDisplay.Height, desiredHeight);
        X = (mainDisplay.Width - Width) / 2;
        Y = (mainDisplay.Height - Height) / 2;
    }
}
