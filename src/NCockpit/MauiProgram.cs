using Microsoft.Extensions.Logging;
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
		
		// Register repositories
		builder.Services.AddSingleton<IProjectFolderRepository, ProjectFolderRepository>();
		// Register services
		builder.Services.AddSingleton<IProjectFolderService, ProjectFolderService>();
		// Register ViewModels
		builder.Services.AddSingleton<FoldersPageViewModel>();
		// Register Popups
		builder.Services.AddTransient<AddFolderPopupViewModel>();
		builder.Services.AddTransientPopup<AddFolderPopup, AddFolderPopupViewModel>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
