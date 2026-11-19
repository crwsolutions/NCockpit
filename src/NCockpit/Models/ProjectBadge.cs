namespace NCockpit.Models;

/// <summary>
/// A coloured pill shown on the overview project row. Built from code with its
/// colours already resolved for the type label it carries, so the XAML binds
/// straight to the values and needs no converter.
/// </summary>
public sealed class ProjectBadge
{
    public ProjectBadge(string text, Color background, Color stroke, Color textColor)
    {
        Text = text;
        Background = background;
        Stroke = stroke;
        TextColor = textColor;
    }

    /// <summary>
    /// The short label shown inside the pill, e.g. ".NET MAUI app".
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Background of the pill.
    /// </summary>
    public Color Background { get; }

    /// <summary>
    /// Border of the pill.
    /// </summary>
    public Color Stroke { get; }

    /// <summary>
    /// Colour of the label.
    /// </summary>
    public Color TextColor { get; }
}
