namespace NCockpit.Models;

/// <summary>
/// A single solution file of a project, shown in the "Solutions" list of the
/// project details page.
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
}
