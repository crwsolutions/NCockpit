namespace NCockpit.Models;

/// <summary>
/// Represents a folder configured on the Folders page. Primary folders are
/// scanned for projects; secondary folders are containers whose first-level
/// subfolders are stored as supporting folders.
/// </summary>
public class ProjectFolder
{
    public int Id { get; set; }
    
    /// <summary>
    /// Full path to the folder.
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// True for a primary folder that NCockpit scans for projects. False for a
    /// secondary (supporting) container folder: it is listed on the Folders page
    /// but never scanned; its first-level subfolders are stored as supporting
    /// folders instead.
    /// </summary>
    public bool IsPrimary { get; set; } = true;
    
    /// <summary>
    /// Whether this folder is currently enabled for scanning.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Number of projects found in this folder (from last scan). Zero for
    /// secondary folders.
    /// </summary>
    public int ProjectCount { get; set; }
    
    /// <summary>
    /// Number of supporting folders registered under this folder. Zero for
    /// primary folders.
    /// </summary>
    public int SupportingFolderCount { get; set; }
    
    /// <summary>
    /// Unix timestamp of the last scan, or null if never scanned.
    /// </summary>
    public long? LastScanUnix { get; set; }
    
    /// <summary>
    /// Returns the last scan time as DateTime, or null.
    /// </summary>
    public DateTime? LastScan => LastScanUnix.HasValue 
        ? DateTimeOffset.FromUnixTimeSeconds(LastScanUnix.Value).DateTime 
        : null;
    
    /// <summary>
    /// Returns a formatted string for display, e.g., "2 hours ago" or "Never".
    /// </summary>
    public string LastScanDisplay
    {
        get
        {
            if (LastScan is not DateTime lastScan)
                return "Never scanned";
            
            var elapsed = DateTime.Now - lastScan;
            
            return elapsed.TotalMinutes < 1 ? "Just now" :
                   elapsed.TotalMinutes < 60 ? $"{(int)elapsed.TotalMinutes}m ago" :
                   elapsed.TotalHours < 24 ? $"{(int)elapsed.TotalHours}h ago" :
                   $"{elapsed.Days}d ago";
        }
    }

    /// <summary>
    /// The folder name for display: the last segment of <see cref="Path"/>.
    /// </summary>
    public string FolderName
    {
        get
        {
            var trimmed = Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            return System.IO.Path.GetFileName(trimmed) is { Length: > 0 } name ? name : Path;
        }
    }
}
