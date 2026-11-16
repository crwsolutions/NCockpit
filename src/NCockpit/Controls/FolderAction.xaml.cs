using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

/// <summary>
/// A compact, clickable folder action: a folder glyph, a blue title and a
/// trailing chevron, with hover feedback. Tapping opens <see cref="Path"/> in
/// Windows Explorer.
/// </summary>
public partial class FolderAction : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");

    /// <summary>
    /// Opens a folder in Windows Explorer. Set once at startup; null when not
    /// configured, in which case taps do nothing.
    /// </summary>
    public static Action<string?>? OpenFolderHandler { get; set; }

    public FolderAction()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The display text (e.g. the folder name or path).
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(FolderAction), default(string));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The folder path to open when the control is tapped.
    /// </summary>
    public static readonly BindableProperty PathProperty = BindableProperty.Create(
        nameof(Path), typeof(string), typeof(FolderAction), default(string));

    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Path) || OpenFolderHandler is null)
        {
            return;
        }

        try
        {
            OpenFolderHandler(Path);
        }
        catch
        {
            // Opening the folder must never take down the UI.
        }
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
        GlyphLabel.Text = "\uf07b";
    }
}
