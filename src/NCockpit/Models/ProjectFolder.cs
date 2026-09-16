namespace NCockpit.Models;

/// <summary>
/// Represents a folder that NCockpit scans for projects.
/// Primary folders are scanned; secondary folders are linked to specific projects.
/// </summary>
public class ProjectFolder
{
    public int Id { get; set; }
    
    /// <summary>
    /// Full path to the folder.
    /// </summary>
    public required string Path { get; set; }
    
    /// <summary>
    /// True if this folder should be scanned for projects.
    /// False if it's secondary (linked to a specific project).
    /// </summary>
    public bool IsPrimary { get; set; }
    
    /// <summary>
    /// Whether this folder is currently enabled for scanning.
    /// </summary>
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Number of projects found in this folder (from last scan).
    /// </summary>
    public int ProjectCount { get; set; }
    
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
}
