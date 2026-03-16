using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// Base class for controls that have a visual appearance with background, foreground,
/// border, padding, and focus capability. Corresponds to WPF's System.Windows.Controls.Control.
/// </summary>
public abstract class Control : FrameworkElement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Control"/> class.
    /// Sets <see cref="UIElement.Focusable"/> to <c>true</c> by default (WPF convention).
    /// </summary>
    protected Control()
    {
        Focusable = true;
    }

    private Color _background = Color.Black;
    /// <summary>Gets or sets the background color.</summary>
    public Color Background
    {
        get => _background;
        set => SetProperty(ref _background, value);
    }

    private Color _foreground = Color.White;
    /// <summary>Gets or sets the foreground (text) color.</summary>
    public Color Foreground
    {
        get => _foreground;
        set => SetProperty(ref _foreground, value);
    }

    private Thickness _padding = new(0);
    /// <summary>Gets or sets the internal padding.</summary>
    public Thickness Padding
    {
        get => _padding;
        set => SetProperty(ref _padding, value);
    }

    private BorderStyle _borderStyleStyle = BorderStyle.None;
    /// <summary>Gets or sets the border style and color for this control.</summary>
    public BorderStyle BorderStyle
    {
        get => _borderStyleStyle;
        set => SetProperty(ref _borderStyleStyle, value);
    }

    /// <summary>
    /// Gets or sets the tab order index for keyboard navigation.
    /// Lower values receive focus first.
    /// </summary>
    public int TabIndex { get; set; }

    /// <summary>
    /// Gets or sets the control template (stub — template rendering not yet implemented).
    /// </summary>
    public object? Template { get; set; }
}
