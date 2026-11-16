using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

/// <summary>
/// A compact, clickable file action: a file glyph, a bold black title and a
/// trailing chevron, with hover feedback. Tapping opens <see cref="Path"/> in
/// the View File popup.
/// </summary>
public partial class FileAction : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");

    /// <summary>
    /// Opens a file in the View File popup. Set once at startup; null when not
    /// configured, in which case taps do nothing.
    /// </summary>
    public static Action<string?>? OpenFileHandler { get; set; }

    public FileAction()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The display text (e.g. the file name).
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(FileAction), default(string));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The full file path to open when the control is tapped.
    /// </summary>
    public static readonly BindableProperty PathProperty = BindableProperty.Create(
        nameof(Path), typeof(string), typeof(FileAction), default(string));

    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Path) || OpenFileHandler is null)
        {
            return;
        }

        try
        {
            OpenFileHandler(Path);
        }
        catch
        {
            // Opening the file must never take down the UI.
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
        GlyphLabel.Text = "\uf15b";
    }
}
