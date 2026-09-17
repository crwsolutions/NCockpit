using System.Diagnostics;

namespace NCockpit.Services;

/// <summary>
/// Abstraction for opening local files through the Windows shell association.
/// </summary>
public interface IWindowsLauncher
{
    /// <summary>
    /// Open a local file with its associated Windows application.
    /// </summary>
    bool TryOpenFile(string path);
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
}
