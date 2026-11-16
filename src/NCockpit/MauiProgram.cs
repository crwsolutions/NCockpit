using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using CommunityToolkit.Maui;
using NCockpit.Controls;
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

		var app = builder.Build();

		// Let the LinkAction control open URLs in the default browser.
		LinkAction.OpenUrlHandler = url =>
		{
			var launcher = app.Services.GetRequiredService<IWindowsLauncher>();
			launcher.TryOpenUrl(url);
		};

		// Let the FolderAction control open folders in Windows Explorer.
		FolderAction.OpenFolderHandler = path =>
		{
#if WINDOWS
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
			{
				FileName = "explorer.exe",
				Arguments = $"/root,\"{path}\"",
				UseShellExecute = true
			});
#endif
		};

		// Let the FileAction control open files in the View File popup.
		FileAction.OpenFileHandler = async path =>
		{
			// Resolve a file name to a full path via the current project.
			var details = app.Services.GetRequiredService<ProjectDetailsPageViewModel>();
			var fullPath = string.IsNullOrEmpty(details.CurrentProject?.FolderPath)
				? path
				: System.IO.Path.Combine(details.CurrentProject.FolderPath, path);

			var popupService = app.Services.GetRequiredService<IPopupService>();
			await popupService.ShowPopupAsync<ViewFilePopup>(
				Shell.Current,
				options: null,
				shellParameters: new Dictionary<string, object> { ["path"] = fullPath });
		};

		return app;
	}
}
