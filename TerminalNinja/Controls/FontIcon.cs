using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// Displays a single glyph from a Nerd Font icon set.
/// Use either the <see cref="Symbol"/> property with a named <see cref="Primitives.Symbol"/>
/// value, or the <see cref="Glyph"/> property with an arbitrary Unicode character.
/// Requires a Nerd Font-patched terminal font to render icons correctly.
/// <para>
/// <b>XAML usage:</b>
/// <code>
/// &lt;FontIcon Symbol="Check" Foreground="Green" /&gt;
/// &lt;FontIcon Glyph="&#xE0A0;" Foreground="Cyan" /&gt;
/// </code>
/// </para>
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class FontIcon : FrameworkElement
{
    public FontIcon()
    {
        DefaultStyleKey = typeof(FontIcon);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    /// <summary>
    /// Identifies the <see cref="Symbol"/> dependency property.
    /// When set, the <see cref="Glyph"/> property is automatically updated
    /// to the corresponding Unicode character.
    /// </summary>
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(Symbol), typeof(FontIcon),
            new FrameworkPropertyMetadata(Symbol.None, affectsRender: true,
                propertyChangedCallback: OnSymbolChanged));

    /// <summary>
    /// Identifies the <see cref="Glyph"/> dependency property.
    /// The Unicode character string to display. When <see cref="Symbol"/> is set,
    /// this is updated automatically. Can also be set directly for custom glyphs.
    /// </summary>
    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(FontIcon),
            new FrameworkPropertyMetadata("", affectsRender: true));

    /// <summary>
    /// Identifies the <see cref="Foreground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(FontIcon),
            new FrameworkPropertyMetadata(Color.White, affectsRender: true));

    /// <summary>
    /// Identifies the <see cref="Background"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(FontIcon),
            new FrameworkPropertyMetadata(Color.Transparent, affectsRender: true));

    /// <summary>
    /// Identifies the <see cref="Width"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(FontIcon),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    /// <summary>
    /// Identifies the <see cref="Height"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(FontIcon),
            new FrameworkPropertyMetadata(Size.Auto, affectsRender: true));

    // ─── Callback ────────────────────────────────────────────────────

    private static void OnSymbolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var fontIcon = (FontIcon)d;
        var symbol = (Symbol)e.NewValue!;
        fontIcon.Glyph = symbol == Symbol.None
            ? ""
            : ((char)(ushort)symbol).ToString();
    }

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>
    /// Gets or sets the named Nerd Font symbol to display.
    /// Setting this property automatically updates <see cref="Glyph"/>.
    /// </summary>
    public Symbol Symbol
    {
        get => (Symbol)GetValue(SymbolProperty)!;
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>
    /// Gets or sets the Unicode character string to display.
    /// Typically set automatically via <see cref="Symbol"/>, but can also be
    /// set directly using a Unicode escape (e.g., <c>"\uE0A0"</c>) or XAML
    /// character reference (e.g., <c>&amp;#xE0A0;</c>).
    /// Only the first character of the string is rendered.
    /// </summary>
    public string Glyph
    {
        get => (string)GetValue(GlyphProperty)!;
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>
    /// Gets or sets the foreground (icon) color.
    /// </summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty)!;
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the background color.
    /// Defaults to <see cref="Color.Transparent"/> so the icon does not paint over
    /// its parent's background.
    /// </summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the width. Defaults to <see cref="Size.Auto"/> (1 cell).
    /// </summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height. Defaults to <see cref="Size.Auto"/> (1 cell).
    /// </summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    // ─── Layout ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the preferred size of this font icon.
    /// A single glyph occupies exactly 1×1 cells.
    /// </summary>
    public override Size2D GetPreferredSize(Rect parent)
    {
        // A single icon is always 1x1 in terminal cells
        return new Size2D(1, 1);
    }

    /// <summary>
    /// Calculates the absolute bounds of this font icon within the parent bounds.
    /// When Width/Height are Auto, the icon uses 1×1. Otherwise the resolved size is used,
    /// and alignment is applied.
    /// </summary>
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Mode == SizeMode.Auto ? 1 : Width.Resolve(parent.Width);
        var h = Height.Mode == SizeMode.Auto ? 1 : Height.Resolve(parent.Height);

        return ApplyAlignment(parent, w, h);
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <summary>
    /// Renders the icon glyph to the specified cell buffer.
    /// The glyph is centered within the calculated bounds.
    /// </summary>
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);

        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        // Fill background if non-transparent
        if (!Background.IsTransparent)
        {
            var bgCell = new Cell(' ', Foreground, Background);
            buffer.FillRect(clipped, bgCell);
        }

        // Determine the glyph character to render
        if (string.IsNullOrEmpty(Glyph))
        {
            return;
        }

        var ch = Glyph[0];

        // Center the single glyph within the bounds
        var iconX = bounds.X + (bounds.Width - 1) / 2;
        var iconY = bounds.Y + (bounds.Height - 1) / 2;

        if (buffer.IsInBounds(iconX, iconY))
        {
            buffer.SetChar(iconX, iconY, ch, Foreground, Background);
        }
    }
}
