using System.Collections;
using System.Collections.Specialized;
using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls.Charts;

/// <summary>
/// Shared base class for the chart controls. Provides common chrome (title, legend,
/// axis and grid colors), a categorical color palette, size/layout handling, and
/// helpers for filling the background, drawing clipped text, and reacting to changes
/// in a bound data collection.
/// </summary>
[RuntimeNameProperty("Name")]
public abstract class ChartBase : FrameworkElement
{
    /// <summary>Character used to indicate truncated text.</summary>
    protected const char Ellipsis = '…';

    /// <summary>
    /// Default categorical palette used when no per-series color and no
    /// <see cref="SeriesPalette"/> override is supplied. Chosen to read well on dark
    /// backgrounds and to stay distinguishable in order.
    /// </summary>
    protected static readonly Color[] DefaultPalette =
    [
        new(86, 156, 214),   // blue
        new(78, 201, 176),   // teal/green
        new(220, 220, 170),  // sand
        new(197, 134, 192),  // purple
        new(206, 145, 120),  // orange
        new(244, 135, 113),  // salmon
    ];

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(ChartBase),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(ChartBase),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ChartBase),
            new FrameworkPropertyMetadata("", affectsRender: true));

    public static readonly DependencyProperty ShowLegendProperty =
        DependencyProperty.Register(nameof(ShowLegend), typeof(bool), typeof(ChartBase),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty ShowAxesProperty =
        DependencyProperty.Register(nameof(ShowAxes), typeof(bool), typeof(ChartBase),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty ShowGridProperty =
        DependencyProperty.Register(nameof(ShowGrid), typeof(bool), typeof(ChartBase),
            new FrameworkPropertyMetadata(true, affectsRender: true));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(ChartBase),
            new FrameworkPropertyMetadata(new Color(212, 212, 212), affectsRender: true));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(ChartBase),
            new FrameworkPropertyMetadata(Color.Transparent, affectsRender: true));

    public static readonly DependencyProperty AxisColorProperty =
        DependencyProperty.Register(nameof(AxisColor), typeof(Color), typeof(ChartBase),
            new FrameworkPropertyMetadata(new Color(120, 120, 120), affectsRender: true));

    public static readonly DependencyProperty GridColorProperty =
        DependencyProperty.Register(nameof(GridColor), typeof(Color), typeof(ChartBase),
            new FrameworkPropertyMetadata(new Color(60, 60, 60), affectsRender: true));

    public static readonly DependencyProperty LegendColorProperty =
        DependencyProperty.Register(nameof(LegendColor), typeof(Color), typeof(ChartBase),
            new FrameworkPropertyMetadata(new Color(160, 160, 160), affectsRender: true));

    public static readonly DependencyProperty SeriesPaletteProperty =
        DependencyProperty.Register(nameof(SeriesPalette), typeof(IList<Color>), typeof(ChartBase),
            new FrameworkPropertyMetadata(null, affectsRender: true));

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>Gets or sets the width of the chart. Default is <see cref="Size.Stretch"/>.</summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }

    /// <summary>Gets or sets the height of the chart. Default is <see cref="Size.Stretch"/>.</summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    /// <summary>Gets or sets the chart title, drawn along the top edge. Empty hides it.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty)!;
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets whether the legend is shown (for multi-series charts).</summary>
    public bool ShowLegend
    {
        get => (bool)GetValue(ShowLegendProperty)!;
        set => SetValue(ShowLegendProperty, value);
    }

    /// <summary>Gets or sets whether the axes and their labels are drawn.</summary>
    public bool ShowAxes
    {
        get => (bool)GetValue(ShowAxesProperty)!;
        set => SetValue(ShowAxesProperty, value);
    }

    /// <summary>Gets or sets whether grid lines are drawn behind the data.</summary>
    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty)!;
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Gets or sets the color for text (title, labels, tick values).</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty)!;
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the fill color behind the whole chart. Transparent by default.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the color of the axis lines.</summary>
    public Color AxisColor
    {
        get => (Color)GetValue(AxisColorProperty)!;
        set => SetValue(AxisColorProperty, value);
    }

    /// <summary>Gets or sets the color of the grid lines.</summary>
    public Color GridColor
    {
        get => (Color)GetValue(GridColorProperty)!;
        set => SetValue(GridColorProperty, value);
    }

    /// <summary>Gets or sets the color of legend text.</summary>
    public Color LegendColor
    {
        get => (Color)GetValue(LegendColorProperty)!;
        set => SetValue(LegendColorProperty, value);
    }

    /// <summary>
    /// Gets or sets an optional palette override used to color series/points by index.
    /// When null, <see cref="DefaultPalette"/> is used.
    /// </summary>
    public IList<Color>? SeriesPalette
    {
        get => (IList<Color>?)GetValue(SeriesPaletteProperty);
        set => SetValue(SeriesPaletteProperty, value);
    }

    // ─── Palette ─────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the color for the series/point at <paramref name="index"/>. A non-transparent
    /// <paramref name="overrideColor"/> wins; otherwise the palette entry (wrapping) is used.
    /// </summary>
    protected Color ColorForSeries(int index, Color overrideColor = default)
    {
        if (!overrideColor.IsTransparent)
        {
            return overrideColor;
        }

        var palette = SeriesPalette is { Count: > 0 } custom ? custom : DefaultPalette;
        var i = index % palette.Count;
        if (i < 0)
        {
            i += palette.Count;
        }

        return palette[i];
    }

    // ─── Data collection plumbing ────────────────────────────────────

    /// <summary>
    /// Subscribes/unsubscribes an <see cref="INotifyCollectionChanged"/> source so the
    /// chart re-renders on collection changes. Call from a source DP's changed callback.
    /// </summary>
    protected void RebindCollection(object? oldValue, object? newValue)
    {
        if (oldValue is INotifyCollectionChanged oldObservable)
        {
            oldObservable.CollectionChanged -= OnDataCollectionChanged;
        }

        if (newValue is INotifyCollectionChanged newObservable)
        {
            newObservable.CollectionChanged += OnDataCollectionChanged;
        }

        InvalidateVisual();
    }

    /// <summary>Handles a change in a bound data collection by requesting a re-render.</summary>
    protected void OnDataCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateVisual();
    }

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        return new Size2D(w, h);
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);
        return ApplyAlignment(parent, w, h);
    }

    // ─── Rendering helpers ───────────────────────────────────────────

    /// <summary>Fills <paramref name="bounds"/> with <see cref="Background"/> when it is not transparent.</summary>
    protected void FillBackground(CellBuffer buffer, Rect bounds)
    {
        if (Background.IsTransparent)
        {
            return;
        }

        var cell = new Cell(' ', Foreground, Background);
        buffer.FillRect(bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height)), cell);
    }

    /// <summary>
    /// Draws <paramref name="text"/> starting at (<paramref name="x"/>, <paramref name="y"/>),
    /// clipped to <paramref name="maxWidth"/> cells and to the buffer. If the text is longer
    /// than <paramref name="maxWidth"/> it is truncated with an ellipsis. Returns the number
    /// of cells written.
    /// </summary>
    protected static int DrawString(CellBuffer buffer, int x, int y, string text, Color fg, Color bg, int maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
        {
            return 0;
        }

        var truncated = text.Length > maxWidth;
        var count = truncated ? Math.Max(0, maxWidth - 1) : Math.Min(text.Length, maxWidth);

        var written = 0;
        for (var i = 0; i < count; i++)
        {
            var cx = x + i;
            if (buffer.IsInBounds(cx, y))
            {
                buffer.SetChar(cx, y, text[i], fg, bg);
            }

            written++;
        }

        if (truncated && buffer.IsInBounds(x + written, y))
        {
            buffer.SetChar(x + written, y, Ellipsis, fg, bg);
            written++;
        }

        return written;
    }

    /// <summary>Enumerates a possibly-null source as a strongly-typed sequence, skipping mismatches.</summary>
    protected static IEnumerable<T> Enumerate<T>(IEnumerable? source)
    {
        if (source == null)
        {
            yield break;
        }

        foreach (var item in source)
        {
            if (item is T typed)
            {
                yield return typed;
            }
        }
    }
}
