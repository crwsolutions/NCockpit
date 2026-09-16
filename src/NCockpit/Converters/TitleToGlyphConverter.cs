using System.Globalization;

namespace NCockpit.Converters;

/// <summary>
/// Flyout title → FontAwesome6 Free Solid glyph (home, document, folder, gear, info).
/// Used in Shell.ItemTemplate for the left navigation menu.
/// </summary>
public class TitleToGlyphConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Glyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Overview"] = "\uf015",        // house
        ["Project Details"] = "\uf15b", // file-lines
        ["Folders"] = "\uf07b",         // folder
        ["Settings"] = "\uf013",        // gear
        ["About"] = "\uf05a"            // info
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string title && Glyphs.TryGetValue(title, out var glyph) ? glyph : "\uf015";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
