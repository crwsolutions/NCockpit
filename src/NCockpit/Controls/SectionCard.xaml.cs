using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

[ContentProperty(nameof(Content))]
public partial class SectionCard : ContentView
{
    public SectionCard()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(SectionCard), default(string));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
