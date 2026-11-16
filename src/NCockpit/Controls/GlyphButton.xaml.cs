using System.ComponentModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace NCockpit.Controls;

public partial class GlyphButton : ContentView
{
    private static readonly Color HoverColor = Color.Parse("#f5f5f5");
    private static readonly Color DisabledColor = Color.Parse("#d1d5db");

    private bool _lastCanExecute = true;

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
        nameof(Command), typeof(ICommand), typeof(GlyphButton), null,
        propertyChanged: OnCommandChanged);

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

    public static readonly BindableProperty CanExecuteProperty = BindableProperty.Create(
        nameof(CanExecute), typeof(bool), typeof(GlyphButton), true);

    /// <summary>
    /// Whether the <see cref="Command"/> can currently be executed.
    /// When the glyph is disabled the text color is dimmed.
    /// </summary>
    public bool CanExecute
    {
        get => (bool)GetValue(CanExecuteProperty);
        set => SetValue(CanExecuteProperty, value);
    }

    private static void OnGlyphColorChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (GlyphButton)sender;
        control.ApplyCanExecuteState();
    }

    private static void OnCommandChanged(BindableObject sender, object oldValue, object newValue)
    {
        var control = (GlyphButton)sender;
        control.UpdateCanExecuteFromCommand();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        Border.Background = HoverColor;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        Border.Background = Colors.Transparent;
    }

    /// <summary>
    /// Reads CanExecute from the bound command (if it implements
    /// <c>ICommand.CanExecute</c>) and updates the glyph color accordingly.
    /// </summary>
    private void UpdateCanExecuteFromCommand()
    {
        if (Command is not null)
        {
            CanExecute = Command.CanExecute(CommandParameter);
        }
        ApplyCanExecuteState();
    }

    /// <summary>
    /// Dims the glyph when the bound command is not runnable.
    /// </summary>
    private void ApplyCanExecuteState()
    {
        if (CanExecute == _lastCanExecute)
        {
            return;
        }

        _lastCanExecute = CanExecute;
        GlyphLabel.TextColor = CanExecute ? GlyphColor : DisabledColor;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        GlyphLabel.Text = Glyph;
        GlyphLabel.TextColor = GlyphColor;
        GlyphLabel.FontSize = IconSize;
        Border.Padding = new Thickness(ButtonPadding);
        UpdateCanExecuteFromCommand();

        // Subscribe to PropertyChanged so that CanExecute changes raised by
        // IAsyncRelayCommand (or any other INotifyPropertyChanged command)
        // are picked up automatically.
        if (Command is INotifyPropertyChanged npc)
        {
            npc.PropertyChanged += OnCommandPropertyChanged;
        }
    }

    private void OnCommandPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "CanExecute" || e.PropertyName == string.Empty)
        {
            UpdateCanExecuteFromCommand();
        }
    }
}
