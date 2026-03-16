using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for controls that have a visual appearance with background, foreground,
/// border, padding, and focus capability. Corresponds to WPF's System.Windows.Controls.Control.
/// </summary>
public abstract class Control : FrameworkElement
{
    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(Control),
            new FrameworkPropertyMetadata(Color.Black, affectsRender: true));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(Control),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    public static readonly DependencyProperty PaddingProperty =
        DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(Control),
            new FrameworkPropertyMetadata(new Thickness(0), affectsRender: true));

    public static readonly DependencyProperty BorderStyleProperty =
        DependencyProperty.Register(nameof(BorderStyle), typeof(BorderStyle), typeof(Control),
            new FrameworkPropertyMetadata(BorderStyle.None, affectsRender: true));

    public static readonly DependencyProperty TabIndexProperty =
        DependencyProperty.Register(nameof(TabIndex), typeof(int), typeof(Control),
            new PropertyMetadata(0));

    public static readonly DependencyProperty TemplateProperty =
        DependencyProperty.Register(nameof(Template), typeof(object), typeof(Control),
            new PropertyMetadata((object?)null));

    /// <summary>
    /// Initializes a new instance of the <see cref="Control"/> class.
    /// Sets <see cref="UIElement.Focusable"/> to <c>true</c> by default (WPF convention).
    /// </summary>
    protected Control()
    {
        Focusable = true;
    }

    /// <summary>Gets or sets the background color.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the foreground (text) color.</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty)!;
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the internal padding.</summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty)!;
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>Gets or sets the border style and color for this control.</summary>
    public BorderStyle BorderStyle
    {
        get => (BorderStyle)GetValue(BorderStyleProperty)!;
        set => SetValue(BorderStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the tab order index for keyboard navigation.
    /// Lower values receive focus first.
    /// </summary>
    public int TabIndex
    {
        get => (int)GetValue(TabIndexProperty)!;
        set => SetValue(TabIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets the control template (stub — template rendering not yet implemented).
    /// </summary>
    public object? Template
    {
        get => GetValue(TemplateProperty);
        set => SetValue(TemplateProperty, value);
    }
}
