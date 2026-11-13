namespace NCockpit.Models;

/// <summary>
/// Represents a discovered project (a folder containing a .sln or .slnx file).
/// </summary>
public class Project
{
    public int Id { get; set; }

    /// <summary>
    /// Display name of the project. Defaults to the solution file name (without extension).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Full path to the solution file (.sln or .slnx).
    /// Used as the unique identity for merge/prune operations.
    /// </summary>
    public required string SolutionPath { get; set; }

    /// <summary>
    /// The folder that contains the solution file.
    /// </summary>
    public required string FolderPath { get; set; }

    /// <summary>
    /// Unix timestamp of when this project was last (re)discovered during a scan.
    /// </summary>
    public long LastSeenUnix { get; set; }

    /// <summary>
    /// The Unix timestamp the current scan started. Projects with a newer LastSeenUnix
    /// than this value are considered up to date and are not pruned.
    /// </summary>
    public long ScanUnix { get; set; }

    /// <summary>
    /// The id of the folder this project was discovered in. Deleting that folder
    /// cascades to this project.
    /// </summary>
    public int FolderId { get; set; }

    /// <summary>
    /// Whether the user has marked this project as a favorite.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Unix timestamp of when the project was last opened/viewed. Null means the
    /// project has never been opened.
    /// </summary>
    public long? LastUsedUnix { get; set; }

    /// <summary>
    /// Unix timestamp of the solution file's last write time, captured during
    /// scanning and used for overview ordering.
    /// </summary>
    public long? SolutionLastWriteUnix { get; set; }
}
