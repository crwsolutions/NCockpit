using System.Diagnostics;

namespace NCockpit.Services;

/// <summary>
/// Abstraction for opening local files and folders through the Windows shell.
/// </summary>
public interface IWindowsLauncher
{
    /// <summary>
    /// Open a local file with its associated Windows application.
    /// </summary>
    bool TryOpenFile(string path);

    /// <summary>
    /// Open a local folder in File Explorer.
    /// </summary>
    bool TryOpenFolder(string path);

    /// <summary>
    /// Open a URL in the default browser.
    /// </summary>
    bool TryOpenUrl(string url);

    /// <summary>
    /// Open a local folder in Visual Studio Code via the "code" command.
    /// Returns false when VS Code is not installed or the launch fails.
    /// </summary>
    bool TryOpenInVsCode(string path);
}

/// <summary>
/// Default Windows implementation that uses <see cref="Process.Start"/> with
/// <see cref="ProcessStartInfo.UseShellExecute"/> so the operating system
/// resolves the file association.
/// </summary>
public class WindowsLauncher : IWindowsLauncher
{
    public bool TryOpenFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

#if WINDOWS
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
#else
            return false;
#endif
        }
        catch
        {
            return false;
        }
    }

    public bool TryOpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return false;
            }

#if WINDOWS
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
#else
            return false;
#endif
        }
        catch
        {
            return false;
        }
    }

    public bool TryOpenUrl(string url)
    {
        try
        {
#if WINDOWS
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            return true;
#else
            return false;
#endif
        }
        catch
        {
            return false;
        }
    }

    public bool TryOpenInVsCode(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return false;
            }

#if WINDOWS
            Process.Start(new ProcessStartInfo
            {
                FileName = "code",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
            return true;
#else
            return false;
#endif
        }
        catch
        {
            return false;
        }
    }
}
