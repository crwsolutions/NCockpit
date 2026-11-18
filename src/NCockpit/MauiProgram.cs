using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using CommunityToolkit.Maui;
using NCockpit.Controls;
using NCockpit.Models;
using NCockpit.Pages;
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
		builder.Services.AddSingleton<IFilePreviewService, FilePreviewService>();
		// Register ViewModels
		builder.Services.AddSingleton<AboutPageViewModel>();
		builder.Services.AddSingleton<FoldersPageViewModel>();
		builder.Services.AddSingleton<OverviewPageViewModel>();
		builder.Services.AddTransient<ProjectDetailsPageViewModel>();
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

		// Let the SolutionAction control open solutions in Visual Studio.
		SolutionAction.OpenSolutionHandler = async path =>
		{
			if (string.IsNullOrEmpty(path))
			{
				return;
			}

			var launcher = app.Services.GetRequiredService<IWindowsLauncher>();
			if (!launcher.TryOpenFile(path))
			{
				return;
			}

			// Touch the project so the recent-usage ordering is updated.
			if (Shell.Current?.CurrentPage?.BindingContext is ProjectDetailsPageViewModel details
				&& details.CurrentProject is { } project)
			{
				var projectService = app.Services.GetRequiredService<IProjectService>();
				await projectService.TouchAsync(project.Id);
			}
		};

		// Let the FileAction control open files in the View File popup.
		FileAction.OpenFileHandler = async path =>
		{
			// Resolve a file name to a full path via the project shown on the
			// currently visible details page. The view-model is transient, so it
			// must be taken from the page, not created here.
			string? folderPath = null;
			if (Shell.Current?.CurrentPage?.BindingContext is ProjectDetailsPageViewModel details)
			{
				folderPath = details.CurrentProject?.FolderPath;
			}

			var fullPath = string.IsNullOrEmpty(folderPath)
				? path
				: System.IO.Path.Combine(folderPath, path);

			var popupService = app.Services.GetRequiredService<IPopupService>();
			await popupService.ShowPopupAsync<ViewFilePopup>(
				Shell.Current,
				options: null,
				shellParameters: new Dictionary<string, object> { ["path"] = fullPath });
		};

		return app;
	}
}
