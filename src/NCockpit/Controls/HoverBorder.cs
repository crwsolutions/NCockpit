using Microsoft.Maui.Controls.Shapes;

namespace NCockpit.Controls;

/// <summary>
/// A <see cref="Border"/> that tints its background while the pointer is over it.
/// The tint is a translucent layer above the border's own background and below its
/// content, so <see cref="VisualElement.Background"/> and its bindings stay intact.
/// The step follows the color that is actually painted: light backgrounds are
/// darkened, dark ones lightened, so the hover reads correctly in every state the
/// border can be in (normal, selected, themed). <see cref="HoverColor"/> replaces
/// that step when it is set. The declared <see cref="Border.Padding"/> positions the
/// content rather than the tint, so the hover spans the whole border.
/// </summary>
public partial class HoverBorder : Border
{
    // A hover has to read as a step next to the rows around it, so it is a share of
    // the painted color rather than a color of its own.
    private const double LightBaseThreshold = 0.5;
    private const int DarkenAlpha = 20;
    private const int LightenAlpha = 26;

    private readonly Rectangle _tint = new() { Fill = new SolidColorBrush(Colors.Transparent) };
    private readonly Grid _padded = new();
    private readonly Grid _host = new();
    private bool _hosting;

    public HoverBorder()
    {
        // The tint spans the border, the content sits in a layer the padding applies to.
        _host.Add(_tint);
        _host.Add(_padded);
        Content = _host;

        // Host whatever the caller declares, and drop the tint of a container that
        // a list hands to another item.
        PropertyChanged += (_, e) => Host(e.PropertyName);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => SetHover(true);
        pointer.PointerExited += (_, _) => SetHover(false);
        GestureRecognizers.Add(pointer);
    }

    /// <summary>
    /// Hover fill of the border. Set it to pick the hover yourself; left unset the
    /// control derives one from the background it is showing.
    /// </summary>
    public static readonly BindableProperty HoverColorProperty =
        BindableProperty.Create(
            nameof(HoverColor),
            typeof(Brush),
            typeof(HoverBorder),
            null);

    public Brush HoverColor
    {
        get => (Brush)GetValue(HoverColorProperty);
        set => SetValue(HoverColorProperty, value);
    }

    private void Host(string? propertyName)
    {
        if (propertyName is null || _hosting)
        {
            return;
        }

        switch (propertyName)
        {
            case nameof(Padding):
                HostPadding();
                break;
            case nameof(Content):
                HostContent();
                break;
            case nameof(BindingContext):
                SetHover(false);
                break;
        }
    }

    /// <summary>
    /// Moves the padding from the border to the layer holding the content, so the
    /// tint can cover the area the padding would otherwise keep free.
    /// </summary>
    private void HostPadding()
    {
        if (Padding == default)
        {
            return;
        }

        _hosting = true;
        _padded.Padding = Padding;
        Padding = default;
        _hosting = false;
    }

    /// <summary>
    /// Puts the declared content on top of the tint layer.
    /// </summary>
    private void HostContent()
    {
        if (Content is null || ReferenceEquals(Content, _host))
        {
            return;
        }

        var content = Content;
        _hosting = true;
        Content = null;
        _padded.Clear();
        _padded.Add(content);
        Content = _host;
        _hosting = false;
    }

    private void SetHover(bool hovered)
    {
        _tint.Fill = hovered
            ? HoverColor ?? new SolidColorBrush(DeriveTint())
            : new SolidColorBrush(Colors.Transparent);
    }

    /// <summary>
    /// Darkens a light base and lightens a dark one.
    /// </summary>
    private Color DeriveTint()
    {
        return Luminance(ResolveBaseColor() ?? ThemeBaseColor()) > LightBaseThreshold
            ? Color.FromRgba(0, 0, 0, DarkenAlpha)
            : Color.FromRgba(255, 255, 255, LightenAlpha);
    }

    /// <summary>
    /// The first solid color painted by this border, then by its ancestors.
    /// </summary>
    private Color? ResolveBaseColor()
    {
        for (var element = (IElement?)this; element is not null; element = element.Parent)
        {
            if (element is not VisualElement visual)
            {
                continue;
            }

            // A color assigned to Background arrives as an immutable solid brush.
            if (visual.Background is SolidColorBrush solid && IsPainted(solid.Color))
            {
                return solid.Color;
            }

            if (IsPainted(visual.BackgroundColor))
            {
                return visual.BackgroundColor;
            }
        }

        return null;
    }

    private static bool IsPainted(Color? color) => color is not null && color.Alpha > 0;

    /// <summary>
    /// WCAG relative luminance: the sRGB channels linearised, then weighted.
    /// </summary>
    private static double Luminance(Color color) =>
        0.2126 * Channel(color.Red) + 0.7152 * Channel(color.Green) + 0.0722 * Channel(color.Blue);

    private static double Channel(double channel) =>
        channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);

    /// <summary>
    /// The card color of the theme, for when nothing below the border is painted.
    /// </summary>
    private static Color ThemeBaseColor() =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#141414") : Colors.White;
}
