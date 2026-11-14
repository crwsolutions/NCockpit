using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using CommunityToolkit.Maui;
using NCockpit.Repositories;
using NCockpit.Services;
using NCockpit.ViewModels;
using NCockpit.ViewModels.Popups;
using NCockpit.Popups;

namespace NCockpit;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		
		// Initialize database
		Database.Initialize();
		
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("FontAwesome6FreeSolid.otf", "FontAwesomeSolid");
			});
		
		// Register window and shell
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddSingleton<AppWindow>();

		// Register repositories
		builder.Services.AddSingleton<IProjectFolderRepository, ProjectFolderRepository>();
		builder.Services.AddSingleton<IProjectRepository, ProjectRepository>();
		builder.Services.AddSingleton<ISupportingFolderRepository, SupportingFolderRepository>();
		// Register services
		builder.Services.AddSingleton<IProjectFolderService, ProjectFolderService>();
		builder.Services.AddSingleton<IProjectService, ProjectService>();
		builder.Services.AddSingleton<ISupportingFolderService, SupportingFolderService>();
		builder.Services.AddSingleton<IScanService, ScanService>();
		builder.Services.AddSingleton<IPageService, PageService>();
		builder.Services.AddSingleton<IWindowsLauncher, WindowsLauncher>();
		builder.Services.AddSingleton<IReadmeLinkParserService, ReadmeLinkParserService>();
		// Register ViewModels
		builder.Services.AddSingleton<FoldersPageViewModel>();
		builder.Services.AddSingleton<OverviewPageViewModel>();
		builder.Services.AddSingleton<ProjectDetailsPageViewModel>();
		// Register Popups
		builder.Services.AddTransient<AddFolderPopupViewModel>();
		builder.Services.AddTransientPopup<AddFolderPopup, AddFolderPopupViewModel>();
		// Scan popup view-model is resolved from DI and configured at show-time.
		builder.Services.AddSingleton<ScanPopupViewModel>();
		builder.Services.AddTransientPopup<ScanPopup, ScanPopupViewModel>();
		builder.Services.AddTransient<LinkSecondaryPopupViewModel>();
		builder.Services.AddTransientPopup<LinkSecondaryPopup, LinkSecondaryPopupViewModel>();
		builder.Services.AddTransient<ViewFilePopupViewModel>();
		builder.Services.AddTransientPopup<ViewFilePopup, ViewFilePopupViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
		builder.AddMauiDevFlowAgent();
#endif

		return builder.Build();
	}
}
