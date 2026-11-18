using NTokenizers.ToHtml;

namespace NCockpit.Services;

/// <summary>
/// Renders the text of a file as a complete HTML document for the View File
/// popup: Markdown is converted by NTokenizers, any other text file is presented
/// verbatim as a code block.
/// </summary>
public interface IFilePreviewService
{
    /// <summary>
    /// Read <paramref name="filePath"/> and return the HTML document that displays it.
    /// </summary>
    Task<string> BuildHtmlDocumentAsync(string filePath);
}

/// <summary>
/// HTML preview of text files, produced by the NTokenizers ToHtml converter. The
/// converter supplies the document shell, its CSS and the copy-to-clipboard
/// script for code blocks, so the app adds no HTML template of its own.
/// </summary>
public class FilePreviewService : IFilePreviewService
{
    private static readonly string[] MarkdownExtensions = [".md", ".markdown"];

    /// <inheritdoc/>
    public async Task<string> BuildHtmlDocumentAsync(string filePath)
    {
        var text = await File.ReadAllTextAsync(filePath);
        var markdown = IsMarkdownFile(filePath) ? text : WrapAsCodeBlock(text);

        return await MarkdownConverter.ToHtmlDocumentAsync(markdown);
    }

    /// <summary>
    /// Wrap non-Markdown content as a fenced code block so it reaches the HTML
    /// verbatim instead of being parsed as Markdown. The fence is one backtick
    /// longer than the longest run of backticks in the file, so no line of the
    /// content can close it early.
    /// </summary>
    private static string WrapAsCodeBlock(string text)
    {
        var fence = new string('`', Math.Max(3, LongestBacktickRun(text) + 1));
        return $"{fence}\n{text}\n{fence}";
    }

    private static int LongestBacktickRun(string text)
    {
        var longest = 0;
        var run = 0;

        foreach (var character in text)
        {
            run = character == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static bool IsMarkdownFile(string filePath) =>
        MarkdownExtensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
}
