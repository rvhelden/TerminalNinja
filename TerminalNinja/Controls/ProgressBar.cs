using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A progress indicator control that displays a horizontal or vertical bar
/// showing the proportion of <see cref="Value"/> within <see cref="Minimum"/>
/// and <see cref="Maximum"/>.
/// Supports determinate mode (fill bar) and indeterminate mode (sliding block animation).
/// Equivalent to WPF's ProgressBar.
/// </summary>
[RuntimeNameProperty("Name")]
public sealed class ProgressBar : FrameworkElement, IDisposable
{
    private Timer? _animationTimer;
    private int _animationOffset;
    private bool _animationForward = true;

    /// <summary>Animation tick interval for indeterminate mode.</summary>
    private const int AnimationIntervalMs = 100;

    /// <summary>Fraction of the bar length occupied by the indeterminate sliding block body.</summary>
    private const double IndeterminateBlockRatio = 0.15;

    public ProgressBar()
    {
        DefaultStyleKey = typeof(ProgressBar);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ProgressBar),
            new FrameworkPropertyMetadata(0.0, affectsRender: true,
                propertyChangedCallback: OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ProgressBar),
            new FrameworkPropertyMetadata(100.0, affectsRender: true,
                propertyChangedCallback: OnRangeChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(ProgressBar),
            new FrameworkPropertyMetadata(0.0, affectsRender: true,
                propertyChangedCallback: null, coerceValueCallback: CoerceValue));

    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(ProgressBar),
            new FrameworkPropertyMetadata(false, affectsRender: true,
                propertyChangedCallback: OnIsIndeterminateChanged));

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ProgressBar),
            new FrameworkPropertyMetadata(Orientation.Horizontal, affectsRender: true));

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Color), typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(86, 156, 214), affectsRender: true));

    public static readonly DependencyProperty BackgroundProperty =
        DependencyProperty.Register(nameof(Background), typeof(Color), typeof(ProgressBar),
            new FrameworkPropertyMetadata(Color.Transparent, affectsRender: true));

    public static readonly DependencyProperty TrackForegroundProperty =
        DependencyProperty.Register(nameof(TrackForeground), typeof(Color), typeof(ProgressBar),
            new FrameworkPropertyMetadata(new Color(60, 60, 60), affectsRender: true));

    public static readonly DependencyProperty WidthProperty =
        DependencyProperty.Register(nameof(Width), typeof(Size), typeof(ProgressBar),
            new FrameworkPropertyMetadata(Size.Stretch, affectsRender: true));

    public static readonly DependencyProperty HeightProperty =
        DependencyProperty.Register(nameof(Height), typeof(Size), typeof(ProgressBar),
            new FrameworkPropertyMetadata(Size.Absolute(1), affectsRender: true));

    public static readonly DependencyProperty BarCharacterProperty =
        DependencyProperty.Register(nameof(BarCharacter), typeof(char), typeof(ProgressBar),
            new FrameworkPropertyMetadata('\u2501', affectsRender: true)); // '━' heavy horizontal

    public static readonly DependencyProperty TrackCharacterProperty =
        DependencyProperty.Register(nameof(TrackCharacter), typeof(char), typeof(ProgressBar),
            new FrameworkPropertyMetadata('\u2500', affectsRender: true)); // '─' light horizontal

    public static readonly DependencyProperty IndeterminateAccentCharacterProperty =
        DependencyProperty.Register(nameof(IndeterminateAccentCharacter), typeof(char), typeof(ProgressBar),
            new FrameworkPropertyMetadata('\u2578', affectsRender: true)); // '╸' heavy left

    public static readonly DependencyProperty ShowPercentageProperty =
        DependencyProperty.Register(nameof(ShowPercentage), typeof(bool), typeof(ProgressBar),
            new FrameworkPropertyMetadata(false, affectsRender: true));

    // ─── CLR Property Wrappers ───────────────────────────────────────

    /// <summary>Gets or sets the minimum value of the range. Default is 0.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty)!;
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Gets or sets the maximum value of the range. Default is 100.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty)!;
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Gets or sets the current value. Clamped to [Minimum, Maximum].</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty)!;
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Gets or sets whether the progress bar is in indeterminate mode (animated sliding block).</summary>
    public bool IsIndeterminate
    {
        get => (bool)GetValue(IsIndeterminateProperty)!;
        set => SetValue(IsIndeterminateProperty, value);
    }

    /// <summary>Gets or sets the orientation of the progress bar.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty)!;
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets the fill (bar) color. Default is a soft blue (#569CD6).</summary>
    public Color Foreground
    {
        get => (Color)GetValue(ForegroundProperty)!;
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Gets or sets the panel background behind the entire bar. Default is Transparent.</summary>
    public Color Background
    {
        get => (Color)GetValue(BackgroundProperty)!;
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Gets or sets the color of the track (unfilled) character. Default is Gray.</summary>
    public Color TrackForeground
    {
        get => (Color)GetValue(TrackForegroundProperty)!;
        set => SetValue(TrackForegroundProperty, value);
    }

    /// <summary>Gets or sets the width of the progress bar.</summary>
    public Size Width
    {
        get => (Size)GetValue(WidthProperty)!;
        set => SetValue(WidthProperty, value);
    }

    /// <summary>Gets or sets the height of the progress bar.</summary>
    public Size Height
    {
        get => (Size)GetValue(HeightProperty)!;
        set => SetValue(HeightProperty, value);
    }

    /// <summary>Gets or sets the character used for the filled portion. Default is '━' (heavy horizontal).</summary>
    public char BarCharacter
    {
        get => (char)GetValue(BarCharacterProperty)!;
        set => SetValue(BarCharacterProperty, value);
    }

    /// <summary>Gets or sets the character used for the unfilled portion. Default is '─'.</summary>
    public char TrackCharacter
    {
        get => (char)GetValue(TrackCharacterProperty)!;
        set => SetValue(TrackCharacterProperty, value);
    }

    /// <summary>Gets or sets the accent character used for the leading/trailing gradient in indeterminate mode. Default is '╸' (heavy left).</summary>
    public char IndeterminateAccentCharacter
    {
        get => (char)GetValue(IndeterminateAccentCharacterProperty)!;
        set => SetValue(IndeterminateAccentCharacterProperty, value);
    }

    /// <summary>Gets or sets whether to display a percentage text overlay on the bar.</summary>
    public bool ShowPercentage
    {
        get => (bool)GetValue(ShowPercentageProperty)!;
        set => SetValue(ShowPercentageProperty, value);
    }

    // ─── Coercion and Callbacks ──────────────────────────────────────

    /// <summary>
    /// Coerces Value to stay within [Minimum, Maximum].
    /// </summary>
    private static object? CoerceValue(DependencyObject d, object? baseValue)
    {
        var pb = (ProgressBar)d;
        var value = (double)baseValue!;
        var min = pb.Minimum;
        var max = pb.Maximum;

        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    /// <summary>
    /// When Minimum or Maximum changes, re-coerce Value to ensure it stays in range.
    /// </summary>
    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pb = (ProgressBar)d;
        // Re-set Value to trigger its CoerceValueCallback
        var currentValue = pb.Value;
        var min = pb.Minimum;
        var max = pb.Maximum;

        if (currentValue < min)
        {
            pb.Value = min;
        }
        else if (currentValue > max)
        {
            pb.Value = max;
        }
    }

    /// <summary>
    /// Starts or stops the indeterminate animation timer.
    /// </summary>
    private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var pb = (ProgressBar)d;
        var isIndeterminate = (bool)e.NewValue!;

        if (isIndeterminate)
        {
            pb.StartAnimation();
        }
        else
        {
            pb.StopAnimation();
        }
    }

    private void StartAnimation()
    {
        StopAnimation();
        _animationOffset = 0;
        _animationForward = true;
        _animationTimer = new Timer(OnAnimationTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(AnimationIntervalMs));
    }

    private void StopAnimation()
    {
        _animationTimer?.Dispose();
        _animationTimer = null;
    }

    private void OnAnimationTick(object? state)
    {
        // Thread-safe increment with ping-pong direction
        var offset = Interlocked.CompareExchange(ref _animationOffset, 0, 0);

        if (_animationForward)
        {
            offset++;
            // We don't know the track length here, so just increment and
            // let Render handle wrapping. Use a reasonable upper bound.
            if (offset > 200)
            {
                _animationForward = false;
                offset--;
            }
        }
        else
        {
            offset--;
            if (offset <= 0)
            {
                offset = 0;
                _animationForward = true;
            }
        }

        Interlocked.Exchange(ref _animationOffset, offset);
        InvalidateVisual();
    }

    // ─── Layout ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        if (Orientation == Orientation.Horizontal)
        {
            var w = Width.Resolve(parent.Width);
            return new Size2D(w, 1);
        }
        else
        {
            var h = Height.Resolve(parent.Height);
            return new Size2D(1, h);
        }
    }

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent)
    {
        var w = Width.Resolve(parent.Width);
        var h = Height.Resolve(parent.Height);

        return ApplyAlignment(parent, w, h);
    }

    // ─── Rendering ───────────────────────────────────────────────────

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);

        // Clip to buffer bounds
        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        if (Orientation == Orientation.Horizontal)
        {
            if (IsIndeterminate)
            {
                RenderHorizontalIndeterminate(buffer, clipped);
            }
            else
            {
                RenderHorizontalDeterminate(buffer, clipped);
            }
        }
        else
        {
            if (IsIndeterminate)
            {
                RenderVerticalIndeterminate(buffer, clipped);
            }
            else
            {
                RenderVerticalDeterminate(buffer, clipped);
            }
        }
    }

    private void RenderHorizontalDeterminate(CellBuffer buffer, Rect bounds)
    {
        var range = Maximum - Minimum;
        var fillWidth = range > 0
            ? (int)Math.Round((Value - Minimum) / range * bounds.Width)
            : 0;

        fillWidth = Math.Clamp(fillWidth, 0, bounds.Width);

        // Build percentage text if needed
        var percentText = ShowPercentage ? GetPercentageText() : null;
        var textStartX = percentText != null ? bounds.X + (bounds.Width - percentText.Length) / 2 : -1;

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                var isFilled = x < bounds.X + fillWidth;
                var barChar = isFilled ? BarCharacter : TrackCharacter;
                var fg = isFilled ? Foreground : TrackForeground;
                var bg = isFilled ? Background : Color.Transparent;

                // Overlay percentage text
                if (percentText != null)
                {
                    var textIdx = x - textStartX;
                    if (textIdx >= 0 && textIdx < percentText.Length)
                    {
                        barChar = percentText[textIdx];
                        // Invert fill/track colors so text is readable on both halves
                        fg = isFilled ? Background : Foreground;
                        bg = isFilled ? Foreground : Background;
                    }
                }

                buffer.SetChar(x, y, barChar, fg, bg);
            }
        }
    }

    private void RenderVerticalDeterminate(CellBuffer buffer, Rect bounds)
    {
        var range = Maximum - Minimum;
        var fillHeight = range > 0
            ? (int)Math.Round((Value - Minimum) / range * bounds.Height)
            : 0;

        fillHeight = Math.Clamp(fillHeight, 0, bounds.Height);

        // Fill from bottom to top
        var fillStartY = bounds.Bottom - fillHeight;

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                var isFilled = y >= fillStartY;
                var barChar = isFilled ? BarCharacter : TrackCharacter;
                var fg = isFilled ? Foreground : TrackForeground;
                var bg = isFilled ? Background : Color.Transparent;

                buffer.SetChar(x, y, barChar, fg, bg);
            }
        }
    }

    private void RenderHorizontalIndeterminate(CellBuffer buffer, Rect bounds)
    {
        var trackWidth = bounds.Width;
        var bodySize = Math.Max(1, (int)(trackWidth * IndeterminateBlockRatio));
        // Add lead+trail accent chars for a gradient look (▪■■■▪)
        var hasAccents = bodySize >= 2;
        var totalBlockSize = hasAccents ? bodySize + 2 : bodySize;
        var maxOffset = Math.Max(0, trackWidth - totalBlockSize);

        var offset = Interlocked.CompareExchange(ref _animationOffset, 0, 0);
        offset = Math.Clamp(offset, 0, maxOffset);

        if (offset >= maxOffset) _animationForward = false;
        if (offset <= 0) _animationForward = true;

        var blockStart = bounds.X + offset;
        var blockEnd = blockStart + totalBlockSize;

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                char ch;
                Color fg, bg;

                if (hasAccents && (x == blockStart || x == blockEnd - 1))
                {
                    // Leading/trailing accent — gradient fade
                    ch = IndeterminateAccentCharacter;
                    fg = Foreground;
                    bg = Background;
                }
                else if (x > blockStart && x < blockEnd - (hasAccents ? 1 : 0)
                         || (!hasAccents && x >= blockStart && x < blockEnd))
                {
                    // Body
                    ch = BarCharacter;
                    fg = Foreground;
                    bg = Background;
                }
                else
                {
                    // Track
                    ch = TrackCharacter;
                    fg = TrackForeground;
                    bg = Color.Transparent;
                }

                buffer.SetChar(x, y, ch, fg, bg);
            }
        }
    }

    private void RenderVerticalIndeterminate(CellBuffer buffer, Rect bounds)
    {
        var trackHeight = bounds.Height;
        var bodySize = Math.Max(1, (int)(trackHeight * IndeterminateBlockRatio));
        var hasAccents = bodySize >= 2;
        var totalBlockSize = hasAccents ? bodySize + 2 : bodySize;
        var maxOffset = Math.Max(0, trackHeight - totalBlockSize);

        var offset = Interlocked.CompareExchange(ref _animationOffset, 0, 0);
        offset = Math.Clamp(offset, 0, maxOffset);

        if (offset >= maxOffset) _animationForward = false;
        if (offset <= 0) _animationForward = true;

        var blockStart = bounds.Y + offset;
        var blockEnd = blockStart + totalBlockSize;

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                char ch;
                Color fg, bg;

                if (hasAccents && (y == blockStart || y == blockEnd - 1))
                {
                    ch = IndeterminateAccentCharacter;
                    fg = Foreground;
                    bg = Background;
                }
                else if (y > blockStart && y < blockEnd - (hasAccents ? 1 : 0)
                         || (!hasAccents && y >= blockStart && y < blockEnd))
                {
                    ch = BarCharacter;
                    fg = Foreground;
                    bg = Background;
                }
                else
                {
                    ch = TrackCharacter;
                    fg = TrackForeground;
                    bg = Color.Transparent;
                }

                buffer.SetChar(x, y, ch, fg, bg);
            }
        }
    }

    private string GetPercentageText()
    {
        var range = Maximum - Minimum;
        var percent = range > 0
            ? (int)Math.Round((Value - Minimum) / range * 100)
            : 0;

        return $"{percent}%";
    }

    // ─── Cleanup ─────────────────────────────────────────────────────

    /// <summary>
    /// Disposes the animation timer used in indeterminate mode.
    /// </summary>
    public void Dispose()
    {
        StopAnimation();
    }
}
