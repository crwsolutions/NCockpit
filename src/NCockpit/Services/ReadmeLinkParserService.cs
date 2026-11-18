using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace NCockpit.Services;

/// <summary>
/// Parses README.md files to extract and categorize links into mobile
/// and related links. Uses compiled regex patterns for performance.
/// </summary>
public interface IReadmeLinkParserService
{
    /// <summary>
    /// Parse links from the project's README.md file and return them
    /// categorized into mobile and related links. When <paramref name="cache"/>
    /// is true (the default) a previous parse of the same file is returned
    /// without re-reading it; pass false to force a fresh read from disk.
    /// </summary>
    Task<ReadmeLinks> ParseLinksAsync(string projectFolderPath, bool cache = true);
}

/// <summary>
/// Result of parsing a README.md file.
/// </summary>
public class ReadmeLinks
{
    /// <summary>
    /// Whether the README.md file was found and successfully parsed.
    /// </summary>
    public bool HasReadme { get; set; }

    /// <summary>
    /// Mobile-related links (Google Play, App Store, AdMob, etc.)
    /// </summary>
    public List<ReadmeLink> MobileLinks { get; set; } = [];

    /// <summary>
    /// Other related links (GitHub, docs, etc.)
    /// </summary>
    public List<ReadmeLink> RelatedLinks { get; set; } = [];
}

/// <summary>
/// A link parsed from a README file.
/// </summary>
public class ReadmeLink
{
    /// <summary>
    /// The display text of the link.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// The URL of the link.
    /// </summary>
    public string Url { get; set; } = "";

    /// <summary>
    /// The platform/provider this link is associated with.
    /// </summary>
    public string Platform { get; set; } = "";
}

/// <summary>
/// Link category definitions based on URL patterns.
/// </summary>
internal static class LinkCategories
{
    // Google
    public const string PlayStore = "Google Play Store";
    public const string PlayConsole = "Google Play Console";
    public const string SearchConsole = "Google Search Console";
    public const string Ads = "Google Ads";
    public const string Analytics = "Google Analytics";

    // Apple
    public const string AppStore = "Apple App Store";
    public const string TestFlight = "Apple TestFlight";
    public const string AppStoreConnect = "Apple App Store Connect";

    // AdMob
    public const string AdMob = "AdMob";
    public const string AppAdsTxt = "app-ads.txt";

    // Privacy
    public const string Privacy = "Privacy";

    // Other
    public const string GitHub = "GitHub";
    public const string Documentation = "Documentation";
    public const string Other = "Other";
}

/// <summary>
/// Service that parses README.md files to extract and categorize links.
/// Uses compiled regex patterns for performance.
/// </summary>
public class ReadmeLinkParserService : IReadmeLinkParserService
{
    private static readonly Regex UrlPattern = new(
        @"(?:https?://[^\s<>""\[\](){}|\\]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MarkdownLinkPattern = new(
        @"\[([^\]]+)\]\((https?://[^\s<>""\[\](){}|\\]+)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly ConcurrentDictionary<string, ReadmeLinks> Cache = new();

    /// <inheritdoc />
    public async Task<ReadmeLinks> ParseLinksAsync(string projectFolderPath, bool cache = true)
    {
        try
        {
            if (!Directory.Exists(projectFolderPath))
            {
                return new ReadmeLinks();
            }

            var readmePath = Path.Combine(projectFolderPath, "README.md");
            if (!File.Exists(readmePath))
            {
                return new ReadmeLinks();
            }

            if (cache && Cache.TryGetValue(readmePath, out var cached))
            {
                return cached;
            }

            var content = await File.ReadAllTextAsync(readmePath);
            var links = ParseLinks(content);
            Cache[readmePath] = links;
            return links;
        }
        catch
        {
            return new ReadmeLinks();
        }
    }

    private ReadmeLinks ParseLinks(string content)
    {
        var result = new ReadmeLinks { HasReadme = true };
        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Match markdown links first
        var markdownMatches = MarkdownLinkPattern.Matches(content);
        foreach (Match match in markdownMatches)
        {
            var text = match.Groups[1].Value;
            var url = match.Groups[2].Value;
            if (!seenUrls.Add(url))
            {
                continue;
            }

            var link = CategorizeLink(text, url);
            AddLinkToResult(result, link);
        }

        // Also match plain URLs
        var urlMatches = UrlPattern.Matches(content);
        foreach (Match match in urlMatches)
        {
            var url = match.Value;
            if (!seenUrls.Add(url))
            {
                continue;
            }

            // Extract text from the line before the URL
            var line = content.Substring(0, match.Index).Split('\n').Last();
            var text = line.Trim();
            
            // Skip quoted text (e.g., "> Privacy statement...")
            if (text.StartsWith(">"))
            {
                continue;
            }
            
            // Clean up text (remove trailing colon, leading dash)
            text = text.TrimEnd(':');
            if (text.StartsWith("-"))
            {
                text = text.Substring(1).Trim();
            }
            
            var link = CategorizeLink(text, url);
            AddLinkToResult(result, link);
        }

        return result;
    }

    private void AddLinkToResult(ReadmeLinks result, ReadmeLink link)
    {
        if (link.Platform == LinkCategories.Other)
        {
            result.RelatedLinks.Add(link);
        }
        else
        {
            result.MobileLinks.Add(link);
        }
    }

    private ReadmeLink CategorizeLink(string text, string url)
    {
        var link = new ReadmeLink { Text = text, Url = url };

        if (url.Contains("play.google.com/store/apps"))
        {
            link.Platform = LinkCategories.PlayStore;
        }
        else if (url.Contains("play.google.com/apps/testing"))
        {
            link.Platform = LinkCategories.PlayStore;
        }
        else if (url.Contains("play.google.com/console"))
        {
            link.Platform = LinkCategories.PlayConsole;
        }
        else if (url.Contains("search.google.com"))
        {
            link.Platform = LinkCategories.SearchConsole;
        }
        else if (url.Contains("ads.google.com"))
        {
            link.Platform = LinkCategories.Ads;
        }
        else if (url.Contains("analytics.google.com"))
        {
            link.Platform = LinkCategories.Analytics;
        }
        else if (url.Contains("apps.apple.com"))
        {
            link.Platform = LinkCategories.AppStore;
        }
        else if (url.Contains("testflight.apple.com"))
        {
            link.Platform = LinkCategories.TestFlight;
        }
        else if (url.Contains("appstoreconnect.apple.com"))
        {
            link.Platform = LinkCategories.AppStoreConnect;
        }
        else if (url.Contains("admob.google.com"))
        {
            link.Platform = LinkCategories.AdMob;
        }
        else if (url.Contains("app-ads.txt"))
        {
            link.Platform = LinkCategories.AppAdsTxt;
        }
        else if (url.Contains("privacy") || url.Contains("terms"))
        {
            link.Platform = LinkCategories.Privacy;
        }
        else if (url.Contains("github.com"))
        {
            link.Platform = LinkCategories.GitHub;
        }
        else
        {
            link.Platform = LinkCategories.Other;
        }

        return link;
    }
}
