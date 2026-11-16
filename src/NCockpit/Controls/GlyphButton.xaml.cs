using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

public partial class GlyphButton : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");

    public GlyphButton()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(GlyphButton), default(string));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly BindableProperty GlyphColorProperty = BindableProperty.Create(
        nameof(GlyphColor), typeof(Color), typeof(GlyphButton), Color.Parse("#3b82f6"),
        propertyChanged: OnGlyphColorChanged);

    public Color GlyphColor
    {
        get => (Color)GetValue(GlyphColorProperty);
        set => SetValue(GlyphColorProperty, value);
    }

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize), typeof(double), typeof(GlyphButton), 14.0);

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly BindableProperty ButtonPaddingProperty = BindableProperty.Create(
        nameof(ButtonPadding), typeof(double), typeof(GlyphButton), 8.0);

    public double ButtonPadding
    {
        get => (double)GetValue(ButtonPaddingProperty);
        set => SetValue(ButtonPaddingProperty, value);
    }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(GlyphButton), null);

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(GlyphButton), null);

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    private static void OnGlyphColorChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (GlyphButton)sender;
        control.GlyphLabel.TextColor = control.GlyphColor;
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
        GlyphLabel.Text = Glyph;
        GlyphLabel.TextColor = GlyphColor;
        GlyphLabel.FontSize = IconSize;
        Border.Padding = new Thickness(ButtonPadding);
    }
}
