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

    /// <summary>
    /// Optional actions (e.g. a plus button) shown at the end of the title row.
    /// Invisible when unset, so the header layout stays stable for cards without actions.
    /// </summary>
    public static readonly BindableProperty TitleActionsProperty = BindableProperty.Create(
        nameof(TitleActions), typeof(View), typeof(SectionCard), default(View));

    public View? TitleActions
    {
        get => (View?)GetValue(TitleActionsProperty);
        set => SetValue(TitleActionsProperty, value);
    }
}
