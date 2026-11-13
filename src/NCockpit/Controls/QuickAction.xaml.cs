using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

public partial class QuickAction : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");

    public QuickAction()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(QuickAction), default(string));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(QuickAction), null);

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(QuickAction), null);

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(QuickAction), null,
        propertyChanged: OnGlyphChanged);

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public static readonly BindableProperty GlyphColorProperty = BindableProperty.Create(
        nameof(GlyphColor), typeof(Color), typeof(QuickAction), Color.Parse("#3b82f6"),
        propertyChanged: OnGlyphColorChanged);

    public Color GlyphColor
    {
        get => (Color)GetValue(GlyphColorProperty);
        set => SetValue(GlyphColorProperty, value);
    }

    public static readonly BindableProperty ImageSourceProperty = BindableProperty.Create(
        nameof(ImageSource), typeof(ImageSource), typeof(QuickAction), null,
        propertyChanged: OnImageSourceChanged);

    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize), typeof(double), typeof(QuickAction), 28.0,
        propertyChanged: OnIconSizeChanged);

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    private static void OnGlyphChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (QuickAction)sender;
        control.UpdateIconVisibility();
    }

    private static void OnGlyphColorChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (QuickAction)sender;
        control.GlyphLabel.TextColor = control.GlyphColor;
    }

    private static void OnImageSourceChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (QuickAction)sender;
        control.UpdateIconVisibility();
    }

    private static void OnIconSizeChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (QuickAction)sender;
        control.ApplyIconSize();
    }

    private void UpdateIconVisibility()
    {
        if (!string.IsNullOrEmpty(Glyph))
        {
            GlyphLabel.Text = Glyph;
            GlyphLabel.IsVisible = true;
            ImageIcon.IsVisible = false;
        }
        else if (ImageSource != null)
        {
            ImageIcon.Source = ImageSource;
            ImageIcon.IsVisible = true;
            GlyphLabel.IsVisible = false;
        }
        else
        {
            GlyphLabel.IsVisible = false;
            ImageIcon.IsVisible = false;
        }
    }

    private void ApplyIconSize()
    {
        GlyphLabel.WidthRequest = IconSize;
        GlyphLabel.FontSize = IconSize - 6;
        ImageIcon.WidthRequest = IconSize;
        ImageIcon.HeightRequest = IconSize;
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
        UpdateIconVisibility();
        ApplyIconSize();
        GlyphLabel.TextColor = GlyphColor;
    }
}
