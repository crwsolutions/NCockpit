namespace NCockpit.Controls;

/// <summary>
/// A compact pill: a rounded background with a short label, used to mark a
/// single attribute of a row (a project's type or its target frameworks).
/// </summary>
public partial class Badge : ContentView
{
    public Badge()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The short label shown inside the pill, e.g. ".NET MAUI app".
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(Badge), string.Empty);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The pill's outline.
    /// </summary>
    public static readonly BindableProperty BadgeBackgroundProperty = BindableProperty.Create(
        nameof(BadgeBackground), typeof(Color), typeof(Badge), Color.Parse("#e2e8f0"));

    public Color BadgeBackground
    {
        get => (Color)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    /// <summary>
    /// The pill's outline.
    /// </summary>
    public static readonly BindableProperty StrokeColorProperty = BindableProperty.Create(
        nameof(StrokeColor), typeof(Color), typeof(Badge), Color.Parse("#e2e8f0"));

    public Color StrokeColor
    {
        get => (Color)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    /// <summary>
    /// Colour of the label.
    /// </summary>
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(Badge), Color.Parse("#334155"));

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }
}
