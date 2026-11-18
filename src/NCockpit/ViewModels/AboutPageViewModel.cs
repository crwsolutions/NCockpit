using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;

namespace NCockpit.ViewModels;

/// <summary>
/// ViewModel for the About screen. Surfaces the application name, version and
/// framework, all retrieved from the assembly metadata and the running
/// runtime — no hardcoded values.
/// </summary>
public partial class AboutPageViewModel : ObservableObject
{
    /// <summary>
    /// Display name of the application, from the <c>AssemblyTitle</c> attribute.
    /// </summary>
    public string Name { get; } =
        typeof(AboutPageViewModel).Assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title
        ?? "NCockpit";

    /// <summary>
    /// Version of the application, from the <c>AssemblyVersion</c> attribute.
    /// </summary>
    public string Version { get; } =
        typeof(AboutPageViewModel).Assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version
        ?? "1.0.0";

    /// <summary>
    /// Framework the application runs on, e.g. ".NET 11 · MAUI 11".
    /// Computed from the running runtime and the version of the
    /// <c>Microsoft.Maui.Controls</c> assembly.
    /// </summary>
    public string Framework { get; } =
        $".NET {Environment.Version.Major} · MAUI {MauiVersion}";

    private static string MauiVersion
    {
        get
        {
            // Grid lives in Microsoft.Maui.Controls.dll, the controls assembly.
            var version = typeof(Grid).Assembly.GetCustomAttribute<AssemblyVersionAttribute>()?.Version;
            return System.Version.TryParse(version, out var parsed) ? $"{parsed.Major}" : "unknown";
        }
    }
}
