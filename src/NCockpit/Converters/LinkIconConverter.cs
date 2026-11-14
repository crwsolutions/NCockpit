using System.Globalization;
using Microsoft.Maui.Controls;
using NCockpit.Services;

namespace NCockpit.Converters;

/// <summary>
/// Converts a link platform string to the appropriate icon image source.
/// </summary>
public class LinkIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.ToString() switch
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
            LinkCategories.AppAdsTxt => "github.png",
            LinkCategories.Privacy => "github.png",
            LinkCategories.GitHub => "github.png",
            _ => "github.png"
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
