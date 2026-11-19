using System.Globalization;

namespace NCockpit.Converters;

/// <summary>
/// Converts a boolean favorite flag to the FontAwesome6 solid star glyph. The
/// star is always shown; the color converter (amber vs. light gray) is what
/// distinguishes the favorited and non-favorited states.
/// </summary>
public class BoolToStarGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // \uf005 = star (solid). The only star glyph in the bundled Solid font.
        return "\uf005";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Colors a favorite star: amber when favorited, a muted gray otherwise so the
/// state change is clearly visible on the light page background. Both colors come
/// from the app palette (IconAmber, TextMuted) so the star stays in sync with the
/// SummaryCard favorites icon.
/// </summary>
public class BoolToFavoriteColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? GetResourceColor("IconAmber") : GetResourceColor("TextMuted");
    }

    private static Color GetResourceColor(string key)
    {
        return App.Current?.Resources?[key] is Color color ? color : Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean expand flag to the FontAwesome6 solid chevron glyph:
/// pointing down when expanded, right when collapsed.
/// </summary>
public class BoolToChevronGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // \uf107 = caret-down (expanded), \uf105 = caret-right (collapsed).
        return value is true ? "\uf107" : "\uf105";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Shows the empty-state block only when a count is zero.
/// </summary>
public class IntToEmptyVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is int count && count == 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns a light blue background for selected rows, white for unselected.
/// </summary>
public class SelectedToBackgroundColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Color.FromArgb("#e3f2fd") : Color.FromRgb(255, 255, 255);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

