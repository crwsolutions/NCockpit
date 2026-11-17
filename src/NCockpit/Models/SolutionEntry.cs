using CommunityToolkit.Mvvm.ComponentModel;

namespace NCockpit.Models;

/// <summary>
/// A single solution file of a project, shown as an expandable parent in the
/// "Solutions &amp; Projects" panel of the project details page.
/// </summary>
public class SolutionEntry
{
    /// <summary>
    /// Display name (the file name, e.g. "NCockpit.slnx").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path to the solution file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Unix timestamp of the solution file's last write time, or null when it
    /// could not be read.
    /// </summary>
    public long? LastWriteUnix { get; init; }

    /// <summary>
    /// Whether this is the project's favorite solution (the star is shown on
    /// this row and every "Open in Visual Studio" action opens it).
    /// </summary>
    public bool IsFavorite { get; init; }
}

/// <summary>
/// A project file discovered in a solution or as an orphan. A plain POCO so it
/// can be serialized to / deserialized from the <c>SolutionProjectsJson</c> and
/// <c>OrphanProjectsJson</c> columns without losing any properties.
/// </summary>
public class SolutionProject
{
    /// <summary>
    /// Display name of the project (from the .csproj's ProjectName property
    /// when available, otherwise the folder name).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path to the folder containing the project file.
    /// </summary>
    public required string FolderPath { get; init; }

    /// <summary>
    /// Full path to the project file (.csproj, .fsproj, .vbproj, ...).
    /// </summary>
    public required string ProjectPath { get; init; }
}

/// <summary>
/// A solution file together with the projects it references, discovered during
/// scanning and stored in <see cref="Project.SolutionProjectsJson"/>. A plain
/// POCO for JSON serialization.
/// </summary>
public class SolutionWithProjects
{
    /// <summary>
    /// Full path to the solution file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The projects referenced by this solution file, each with its name,
    /// folder and full project-file path. Empty when the solution file could
    /// not be parsed or references no resolvable project files.
    /// </summary>
    public List<SolutionProject> Projects { get; init; } = [];
}

/// <summary>
/// An observable wrapper around a <see cref="SolutionProject"/> for the
/// "Solutions &amp; Projects" panel. The underlying model stays a plain POCO
/// (for JSON persistence); this wrapper adds the favorite-star state the UI
/// needs, so the star can be updated in place without rebuilding the tree.
/// </summary>
public partial class SolutionProjectRow : ObservableObject
{
    /// <summary>
    /// The underlying project file.
    /// </summary>
    public required SolutionProject Project { get; init; }

    /// <summary>
    /// Whether this is the project's favorite project (the star is shown on
    /// this row).
    /// </summary>
    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public string Name => Project.Name;
    public string FolderPath => Project.FolderPath;
    public string ProjectPath => Project.ProjectPath;
}

/// <summary>
/// The user-facing view of a row in the "Solutions &amp; Projects" panel:
/// either a solution (an expandable parent with its projects as children) or a
/// project (a leaf, shown under its solution or under the "Orphans" header).
/// Observable so the expand state and the favorite star can be updated in
/// place without rebuilding the whole tree.
/// </summary>
public partial class SolutionTreeItem : ObservableObject
{
    /// <summary>
    /// Whether this row is a solution (parent) or a project (leaf).
    /// </summary>
    public required bool IsSolution { get; init; }

    /// <summary>
    /// Display name: the solution file name for a solution row, the project
    /// name for a project row.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Full path: the solution file path for a solution row, the project file
    /// path for a project row.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Whether the row is currently expanded (solution rows only).
    /// </summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    /// Whether this is the project's favorite solution (solution rows) or
    /// favorite project (project rows). Exactly one of each exists.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    /// <summary>
    /// The projects referenced by this solution (solution rows only), wrapped
    /// as observable rows so the favorite star can update in place.
    /// </summary>
    public List<SolutionProjectRow> Projects { get; init; } = [];
}
