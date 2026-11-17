namespace NCockpit.Models;

/// <summary>
/// The kind of a discovered project.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// The project is a git repository whose root contains at least one
    /// solution file (.sln / .slnx). Opened in Visual Studio.
    /// </summary>
    Solution = 0,

    /// <summary>
    /// The project is a git repository whose root contains no solution file.
    /// Opened in Visual Studio Code.
    /// </summary>
    Folder = 1
}

/// <summary>
/// Represents a discovered project: a folder. A project is either a git
/// repository (keyed by its root path) or, as a legacy fallback, a solution
/// file found outside any repository (keyed by the solution path).
/// </summary>
public class Project
{
    public int Id { get; set; }

    /// <summary>
    /// Display name of the project. For a repo project this is the repo folder
    /// name; for a legacy project the solution file name (without extension).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The kind of project (solution type or folder type).
    /// </summary>
    public ProjectType Type { get; set; }

    /// <summary>
    /// Full path to the favorite solution file (.sln or .slnx), or null for
    /// folder type (a repo with no solution file). The favorite solution
    /// replaces the previous "primary solution" concept: it is the solution
    /// opened by every "Open in Visual Studio" entry point. The choice is the
    /// user's when one was made and still exists; otherwise the scan's smart
    /// guess takes over.
    /// </summary>
    public string? SolutionPath { get; set; }

    /// <summary>
    /// The project root folder: the repo root for repo projects, the folder
    /// containing the solution file for legacy projects.
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
    /// Unix timestamp of the favorite solution file's last write time, captured
    /// during scanning and used for overview ordering. Null for folder type
    /// projects.
    /// </summary>
    public long? SolutionLastWriteUnix { get; set; }

    /// <summary>
    /// JSON array of file names for common root files discovered during scanning.
    /// </summary>
    public string? FilesJson { get; set; }

    /// <summary>
    /// The git remote URL (origin) discovered during scanning, or null if the
    /// project has no git remote or the URL could not be determined.
    /// </summary>
    public string? GitRepoUrl { get; set; }

    /// <summary>
    /// JSON array of <see cref="SolutionWithProjects"/> (one entry per solution
    /// file found in the repo), each with the projects it references, discovered
    /// during scanning. Null when no solutions were found (folder type) or no
    /// solution could be parsed.
    /// </summary>
    public string? SolutionProjectsJson { get; set; }

    /// <summary>
    /// JSON array of the project files (.csproj, .fsproj, .vbproj) discovered
    /// inside the repo that are not referenced by any solution file ("orphans").
    /// Discovered during scanning. Null or empty when there are none.
    /// </summary>
    public string? OrphanProjectsJson { get; set; }

    /// <summary>
    /// JSON array of the full paths of every solution file (.sln / .slnx)
    /// discovered inside the project root, or null when none were found.
    /// For a repo with a single solution this list contains that one solution.
    /// </summary>
    public string? SolutionsJson { get; set; }

    /// <summary>
    /// Unix timestamp of when the last solution of this project was opened.
    /// Feeds the smart favorite-solution guess ("most recently opened" is one of
    /// its criteria). Null when no solution has been opened yet.
    /// </summary>
    public long? SolutionLastUsedUnix { get; set; }

    /// <summary>
    /// Full path to the user-chosen favorite solution, or null when none was
    /// chosen yet (the smart guess stands in). Kept across scans; re-guessed
    /// automatically when the file no longer exists.
    /// </summary>
    public string? FavoriteSolutionPath { get; set; }

    /// <summary>
    /// Full path to the user-chosen favorite project file, or null when none was
    /// chosen yet (the smart guess stands in). Kept across scans; re-guessed
    /// automatically when the file no longer exists.
    /// </summary>
    public string? FavoriteProjectPath { get; set; }
}
