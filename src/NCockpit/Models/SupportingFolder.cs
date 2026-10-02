namespace NCockpit.Models;

/// <summary>
/// Represents a supporting (secondary) folder, e.g. materials that must not
/// end up in a project's source control. Supporting folders are path-only
/// records: they are never scanned for projects and never appear in the
/// project overview. They are first-level subfolders of a secondary folder on
/// the Folders page, and can optionally be linked to a project.
/// </summary>
public class SupportingFolder
{
    public int Id { get; set; }

    /// <summary>
    /// Full path to the folder.
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// The id of the secondary (container) folder on the Folders page this
    /// supporting folder was registered from.
    /// </summary>
    public int FolderId { get; set; }

    /// <summary>
    /// The id of the project this supporting folder is linked to, or null
    /// when the folder is unlinked.
    /// </summary>
    public int? ProjectId { get; set; }

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
