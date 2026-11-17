using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

/// <summary>
/// A compact, clickable solution action: a Visual Studio icon, a blue bold
/// title and a trailing chevron, with hover feedback. Tapping opens
/// <see cref="Path"/> in Visual Studio via the configured handler.
/// Styled like the Primary location (FolderAction).
/// </summary>
public partial class SolutionAction : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");

    /// <summary>
    /// Opens a solution file in Visual Studio. Set once at startup; null when
    /// not configured, in which case taps do nothing.
    /// </summary>
    public static Action<string?>? OpenSolutionHandler { get; set; }

    public SolutionAction()
    {
        InitializeComponent();
    }

    /// <summary>
    /// The display text (the solution file name).
    /// </summary>
    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(SolutionAction), default(string));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// The full path to the solution file.
    /// </summary>
    public static readonly BindableProperty PathProperty = BindableProperty.Create(
        nameof(Path), typeof(string), typeof(SolutionAction), default(string));

    public string Path
    {
        get => (string)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Path) || OpenSolutionHandler is null)
        {
            return;
        }

        try
        {
            OpenSolutionHandler(Path);
        }
        catch
        {
            // Opening the solution must never take down the UI.
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
        IconImage.Source = ImageSource.FromFile("visual_studio_icon.png");
    }
}
