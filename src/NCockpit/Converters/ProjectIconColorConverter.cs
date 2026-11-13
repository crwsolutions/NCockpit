using System.Globalization;

namespace NCockpit.Converters;

/// <summary>
/// Converts a project name to a background color for the icon box, using a
/// hash-based approach to consistently assign colors from a palette.
/// </summary>
public class ProjectIconColorConverter : IValueConverter
{
    private static readonly Color[] Palette =
    {
        Color.FromArgb("#10b981"), // green
        Color.FromArgb("#3b82f6"), // blue
        Color.FromArgb("#0d9488"), // teal
        Color.FromArgb("#8b5cf6"), // purple
        Color.FromArgb("#f59e0b"), // amber
        Color.FromArgb("#ef4444"), // red
        Color.FromArgb("#ec4899"), // pink
        Color.FromArgb("#14b8a6"), // cyan
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string name)
        {
            return Palette[0];
        }

        var hash = Math.Abs(name.GetHashCode());
        var index = hash % Palette.Length;
        return Palette[index];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
