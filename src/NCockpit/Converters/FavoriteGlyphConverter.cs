using System.Globalization;

namespace NCockpit.Converters;

/// <summary>
/// Converts a boolean favorite flag to a FontAwesome6 Free Solid star glyph so
/// the star is always visible (used together with a color converter to make it
/// filled/faint).
/// </summary>
public class BoolToStarGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "\uf005" : "\uf005";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Colors a favorite star: amber when favorited, light gray otherwise.
/// </summary>
public class BoolToFavoriteColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? Color.FromArgb("#f5a623") : Color.FromArgb("#cfcfcf");
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

