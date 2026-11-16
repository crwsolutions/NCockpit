using Microsoft.Maui.Controls;
using NCockpit.Services;

namespace NCockpit.Controls;

/// <summary>
/// A compact, clickable link action: a platform icon, a label and a trailing
/// chevron, with hover feedback. Tapping opens <see cref="Url"/> in the default
/// browser. The platform icon is resolved from <see cref="Platform"/> (or falls
/// back to <see cref="ImageSource"/>), so no converter is needed.
/// </summary>
public partial class LinkAction : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");

    /// <summary>
    /// Opens a URL in the default browser. Set once at startup; null when not
    /// configured, in which case taps do nothing.
    /// </summary>
    public static Action<string?>? OpenUrlHandler { get; set; }

    public LinkAction()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The text to display for the link.
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(LinkAction), default(string));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The URL to open when the link is tapped.
    /// </summary>
    public static readonly BindableProperty UrlProperty = BindableProperty.Create(
        nameof(Url), typeof(string), typeof(LinkAction), default(string));

    public string Url
    {
        get => (string)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    /// <summary>
    /// The link platform/provider (e.g. "Google Play Store"). When set, the
    /// control shows the matching platform icon.
    /// </summary>
    public static readonly BindableProperty PlatformProperty = BindableProperty.Create(
        nameof(Platform), typeof(string), typeof(LinkAction), null,
        propertyChanged: OnPlatformChanged);

    public string Platform
    {
        get => (string)GetValue(PlatformProperty);
        set => SetValue(PlatformProperty, value);
    }

    /// <summary>
    /// An explicit icon to use when <see cref="Platform"/> does not match a
    /// known platform.
    /// </summary>
    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
        nameof(ImageSource), typeof(ImageSource), typeof(LinkAction), null,
        propertyChanged: OnImageSourceChanged);

    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    /// <summary>
    /// Whether the trailing chevron is shown.
    /// </summary>
    public static readonly BindableProperty ShowChevronProperty = BindableProperty.Create(
        nameof(ShowChevron), typeof(bool), typeof(LinkAction), true,
        propertyChanged: (s, _, n) => ((LinkAction)s).Chevron.IsVisible = (bool)n);

    public bool ShowChevron
    {
        get => (bool)GetValue(ShowChevronProperty);
        set => SetValue(ShowChevronProperty, value);
    }

    /// <summary>
    /// Maps a link platform (see <see cref="LinkCategories"/>) to its icon image.
    /// </summary>
    private static string GetPlatformIcon(string? platform) =>
        platform switch
        {
            LinkCategories.PlayStore => "google_play_store_icon.png",
            LinkCategories.PlayConsole => "google_play_console.png",
            LinkCategories.SearchConsole => "google_search_console.png",
            LinkCategories.Ads => "google_ads_icon.png",
            LinkCategories.Analytics => "google_analytics_icon.png",
            LinkCategories.AppStore => "apple_app_store.png",
            LinkCategories.TestFlight => "apple_app_store.png",
            LinkCategories.AppStoreConnect => "apple_app_store_connect.png",
            LinkCategories.AdMob => "google_admob_icon.png",
            LinkCategories.AppAdsTxt => "google_ads_icon.png",
            LinkCategories.Privacy => "web_icon.png",
            LinkCategories.GitHub => "github.png",
            _ => "web_icon.png"
        };

    private static void OnPlatformChanged(BindableObject sender, object oldValue, object newValue)
    {
        ((LinkAction)sender).UpdateIcon();
    }

    private static void OnImageSourceChanged(BindableObject sender, object oldValue, object newValue)
    {
        ((LinkAction)sender).UpdateIcon();
    }

    private void UpdateIcon()
    {
        var icon = !string.IsNullOrEmpty(Platform)
            ? (ImageSource)GetPlatformIcon(Platform)
            : ImageSource;

        ImageIcon.Source = icon;
        ImageIcon.IsVisible = icon is not null;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Url) || OpenUrlHandler is null)
        {
            return;
        }

        try
        {
            OpenUrlHandler(Url);
        }
        catch
        {
            // Opening the URL must never take down the UI.
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        Border.Background = HoverColor;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        Border.Background = Colors.Transparent;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        UpdateIcon();
        Chevron.IsVisible = ShowChevron;
    }
}
