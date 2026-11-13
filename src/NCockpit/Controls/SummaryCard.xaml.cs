using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

public partial class SummaryCard : ContentView
{
    public SummaryCard()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty IconGlyphProperty = BindableProperty.Create(
        nameof(IconGlyph), typeof(string), typeof(SummaryCard), default(string));

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public static readonly BindableProperty IconColorProperty = BindableProperty.Create(
        nameof(IconColor), typeof(Color), typeof(SummaryCard), Colors.Transparent);

    public Color IconColor
    {
        get => (Color)GetValue(IconColorProperty);
        set => SetValue(IconColorProperty, value);
    }

    public static readonly BindableProperty IconBackgroundColorProperty = BindableProperty.Create(
        nameof(IconBackgroundColor), typeof(Color), typeof(SummaryCard), Colors.Transparent);

    public Color IconBackgroundColor
    {
        get => (Color)GetValue(IconBackgroundColorProperty);
        set => SetValue(IconBackgroundColorProperty, value);
    }

    public static readonly BindableProperty CountValueProperty = BindableProperty.Create(
        nameof(CountValue), typeof(string), typeof(SummaryCard), default(string));

    public string CountValue
    {
        get => (string)GetValue(CountValueProperty);
        set => SetValue(CountValueProperty, value);
    }

    public static readonly BindableProperty CountLabelProperty = BindableProperty.Create(
        nameof(CountLabel), typeof(string), typeof(SummaryCard), default(string));

    public string CountLabel
    {
        get => (string)GetValue(CountLabelProperty);
        set => SetValue(CountLabelProperty, value);
    }
}
