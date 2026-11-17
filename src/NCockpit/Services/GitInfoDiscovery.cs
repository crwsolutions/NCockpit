using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NCockpit.Services;

/// <summary>
/// Read-only git information for a project folder: the current branch and a
/// short summary of its working tree. The branch is taken from the repository's
/// HEAD file, which is a cheap read; the working tree needs
/// <c>git status --porcelain -z</c> because that state cannot be read reliably
/// from git's own files. Both are read when the panel is shown rather than
/// stored during a scan, so what the UI shows is actual.
/// Everything is resilient by design: no repository, no git executable, a
/// timeout or an unreadable file yields null and never throws.
/// </summary>
public static partial class GitInfoDiscovery
{
    private const int StatusTimeoutMilliseconds = 10_000;

    /// <summary>
    /// How many working-tree entries are counted at most. Beyond that the
    /// summary stops growing and says so; a tree that dirty is not worth an
    /// exact count.
    /// </summary>
    private const int MaxCountedEntries = 100;

    /// <summary>
    /// True when the folder is the root of a git repository: it has a .git
    /// folder (a normal clone) or a .git file (a worktree or submodule).
    /// </summary>
    public static bool IsRepository(string repoRoot) => ResolveGitDirectory(repoRoot) is not null;

    /// <summary>
    /// Returns the branch the repository currently has checked out. A detached
    /// HEAD reports its short commit id; a repository whose HEAD cannot be read
    /// yields null.
    /// </summary>
    public static string? GetBranch(string repoRoot)
    {
        try
        {
            if (ResolveGitDirectory(repoRoot) is not string gitDirectory)
            {
                return null;
            }

            var headPath = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(headPath))
            {
                return null;
            }

            var head = File.ReadAllText(headPath).Trim();
            if (HeadRefRegex().Match(head) is { Success: true } refMatch)
            {
                return refMatch.Groups[1].Value;
            }

            if (HeadCommitRegex().IsMatch(head))
            {
                return $"detached {head[..7]}";
            }

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Summarises the working tree of the repository. Returns null when the
    /// folder is not a repository, git is not available, or the command does not
    /// finish in time.
    /// </summary>
    public static async Task<GitWorkingStatus?> GetStatusAsync(
        string repoRoot,
        CancellationToken cancellationToken = default)
    {
        if (ResolveGitDirectory(repoRoot) is null)
        {
            return null;
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("status");
        process.StartInfo.ArgumentList.Add("--porcelain");
        process.StartInfo.ArgumentList.Add("-z");

        try
        {
            process.Start();

            using var timeout = new CancellationTokenSource(StatusTimeoutMilliseconds);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);

            var outputTask = process.StandardOutput.ReadToEndAsync(linked.Token);
            await process.WaitForExitAsync(linked.Token);

            return ParsePorcelain(await outputTask);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return null;
        }
        catch (Exception)
        {
            // git not installed (Win32Exception), the folder unreadable, or the
            // process misbehaving: the panel shows the status as unknown.
            TryKill(process);
            return null;
        }
    }

    /// <summary>
    /// Counts the entries of a NUL separated <c>--porcelain -z</c> listing.
    /// Renames and copies carry their source path as an extra entry, which is
    /// not a change of its own.
    /// </summary>
    internal static GitWorkingStatus ParsePorcelain(string output)
    {
        var entries = output.Split('\0');
        var staged = 0;
        var modified = 0;
        var untracked = 0;

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4)
            {
                continue;
            }

            var indexState = entry[0];
            var workTreeState = entry[1];

            if (indexState == '?' && workTreeState == '?')
            {
                untracked++;
            }
            else
            {
                if (indexState != ' ')
                {
                    staged++;
                }
                else
                {
                    modified++;
                }

                if (indexState is 'R' or 'C')
                {
                    i++;
                }
            }

            if (staged + modified + untracked >= MaxCountedEntries)
            {
                return new GitWorkingStatus(staged, modified, untracked, IsPartial: true);
            }
        }

        return new GitWorkingStatus(staged, modified, untracked, IsPartial: false);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process is gone already; nothing to report.
        }
    }

    /// <summary>
    /// The .git directory of the repository: the folder itself, or the gitdir a
    /// worktree's .git file points at. Null when the folder is not a repository.
    /// </summary>
    private static string? ResolveGitDirectory(string repoRoot)
    {
        try
        {
            var gitPath = Path.Combine(repoRoot, ".git");
            if (Directory.Exists(gitPath))
            {
                return gitPath;
            }

            if (File.Exists(gitPath))
            {
                var content = File.ReadAllText(gitPath).Trim();
                if (!content.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var pointed = content["gitdir:".Length..].Trim();
                if (pointed.Length == 0)
                {
                    return null;
                }

                return Path.GetFullPath(Path.IsPathRooted(pointed) ? pointed : Path.Combine(repoRoot, pointed));
            }

            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    [GeneratedRegex(
        @"\Aref:\s*refs/(?:heads|remotes)/(.+?)\s*\z",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex HeadRefRegex();

    [GeneratedRegex(@"\A[0-9a-f]{40}\s*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex HeadCommitRegex();
}

/// <summary>
/// How dirty a git working tree is: how many entries are staged, how many
/// tracked files differ from the index, and how many files are untracked.
/// <see cref="ToDisplayString"/> turns it into the very short text the project
/// info panel shows.
/// </summary>
/// <param name="Staged">Entries staged for the next commit.</param>
/// <param name="Modified">Tracked files changed since the index.</param>
/// <param name="Untracked">Files git does not track yet.</param>
/// <param name="IsPartial">True when counting stopped early.</param>
public sealed record GitWorkingStatus(int Staged, int Modified, int Untracked, bool IsPartial)
{
    public bool IsClean => Staged == 0 && Modified == 0 && Untracked == 0;

    /// <summary>
    /// "Clean" for a working tree without changes, otherwise very short stats
    /// such as "2 staged, 3 modified, 1 untracked".
    /// </summary>
    public string ToDisplayString()
    {
        if (IsClean)
        {
            return "Clean";
        }

        var parts = new List<string>(3);
        if (Staged > 0)
        {
            parts.Add($"{Staged} staged");
        }

        if (Modified > 0)
        {
            parts.Add($"{Modified} modified");
        }

        if (Untracked > 0)
        {
            parts.Add($"{Untracked} untracked");
        }

        var summary = string.Join(", ", parts);
        return IsPartial ? $"{summary}+" : summary;
    }
}
