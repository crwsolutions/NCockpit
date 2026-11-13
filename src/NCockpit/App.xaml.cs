using Microsoft.Extensions.DependencyInjection;

namespace NCockpit;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return IPlatformApplication.Current!.Services.GetService<AppWindow>()
			?? throw new InvalidOperationException("AppWindow not found in services");
	}
}