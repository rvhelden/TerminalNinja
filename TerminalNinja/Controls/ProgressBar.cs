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

    /// <summary>How much to lighten the Foreground at the trailing edge of the gradient (0-1).</summary>
    private const double GradientLightenAmount = 0.35;

    // Unicode block characters for sub-cell precision rendering
    private const char FullBlock = '\u2588';       // █
    private const char LeftHalfBlock = '\u258C';   // ▌
    private const char RightHalfBlock = '\u2590';  // ▐
    private const char UpperHalfBlock = '\u2580';  // ▀
    private const char LowerHalfBlock = '\u2584';  // ▄
    private const char TrackDot = '\u00B7';        // · middle dot

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
            // let Render handle wrapping. Use a reasonable upper bound (half-cell units).
            if (offset > 400)
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
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
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
        var percentage = range > 0 ? (Value - Minimum) / range : 0.0;

        // Half-cell precision: 2x horizontal resolution
        var fillHalfCells = (int)Math.Round(percentage * bounds.Width * 2);
        fillHalfCells = Math.Clamp(fillHalfCells, 0, bounds.Width * 2);
        var fullFilledCells = fillHalfCells / 2;
        var hasHalfCell = fillHalfCells % 2 == 1;
        var totalFillCells = fullFilledCells + (hasHalfCell ? 1 : 0);

        // Build percentage text if needed
        var percentText = ShowPercentage ? GetPercentageText() : null;
        var textStartX = percentText != null ? bounds.X + (bounds.Width - percentText.Length) / 2 : -1;

        var fg = Foreground;
        var trackFg = TrackForeground;
        var bg = Background;
        var gradientEnd = Lighten(fg, GradientLightenAmount);

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                var relX = x - bounds.X;
                char ch;
                Color cellFg, cellBg;

                if (relX < fullFilledCells)
                {
                    var t = totalFillCells > 1 ? (double)relX / (totalFillCells - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = FullBlock;
                    cellFg = color;
                    cellBg = color;
                }
                else if (relX == fullFilledCells && hasHalfCell)
                {
                    var t = totalFillCells > 1 ? (double)relX / (totalFillCells - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = LeftHalfBlock;
                    cellFg = color;
                    cellBg = bg;
                }
                else
                {
                    // Track dot
                    ch = TrackDot;
                    cellFg = trackFg;
                    cellBg = bg;
                }

                // Overlay percentage text
                if (percentText != null)
                {
                    var textIdx = x - textStartX;
                    if (textIdx >= 0 && textIdx < percentText.Length)
                    {
                        var isFilled = relX < totalFillCells;
                        ch = percentText[textIdx];
                        if (isFilled)
                        {
                            var t = totalFillCells > 1 ? (double)Math.Min(relX, totalFillCells - 1) / (totalFillCells - 1) : 0.0;
                            var fillColor = LerpColor(fg, gradientEnd, t);
                            cellFg = trackFg;
                            cellBg = fillColor;
                        }
                        else
                        {
                            cellFg = fg;
                            cellBg = trackFg;
                        }
                    }
                }

                buffer.SetChar(x, y, ch, cellFg, cellBg);
            }
        }
    }

    private void RenderVerticalDeterminate(CellBuffer buffer, Rect bounds)
    {
        var range = Maximum - Minimum;
        var percentage = range > 0 ? (Value - Minimum) / range : 0.0;

        // Half-cell precision: 2x vertical resolution
        var fillHalfCells = (int)Math.Round(percentage * bounds.Height * 2);
        fillHalfCells = Math.Clamp(fillHalfCells, 0, bounds.Height * 2);
        var fullFilledCells = fillHalfCells / 2;
        var hasHalfCell = fillHalfCells % 2 == 1;
        var totalFillCells = fullFilledCells + (hasHalfCell ? 1 : 0);

        // Fill from bottom: fully filled rows start at fillStartY
        var fillStartY = bounds.Bottom - fullFilledCells;
        // Boundary row (if any) is one row above the fully filled region
        var boundaryY = hasHalfCell ? fillStartY - 1 : -1;

        var fg = Foreground;
        var trackFg = TrackForeground;
        var bg = Background;
        var gradientEnd = Lighten(fg, GradientLightenAmount);

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                char ch;
                Color cellFg, cellBg;

                if (y >= fillStartY)
                {
                    // Gradient: bottom = Foreground, top of fill = lighter
                    var fillRelPos = bounds.Bottom - 1 - y;
                    var t = totalFillCells > 1 ? (double)fillRelPos / (totalFillCells - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = FullBlock;
                    cellFg = color;
                    cellBg = color;
                }
                else if (y == boundaryY)
                {
                    // Boundary: bottom half = fill (lightest gradient), top half = background
                    var color = totalFillCells > 1 ? gradientEnd : fg;
                    ch = LowerHalfBlock;
                    cellFg = color;
                    cellBg = bg;
                }
                else
                {
                    // Track dot
                    ch = TrackDot;
                    cellFg = trackFg;
                    cellBg = bg;
                }

                buffer.SetChar(x, y, ch, cellFg, cellBg);
            }
        }
    }

    private void RenderHorizontalIndeterminate(CellBuffer buffer, Rect bounds)
    {
        // Half-cell units for smooth sliding
        var trackHalfCells = bounds.Width * 2;
        var bodyHalfSize = Math.Max(2, (int)(trackHalfCells * IndeterminateBlockRatio));
        var maxOffset = Math.Max(0, trackHalfCells - bodyHalfSize);

        var offset = Interlocked.CompareExchange(ref _animationOffset, 0, 0);
        offset = Math.Clamp(offset, 0, maxOffset);

        if (offset >= maxOffset) _animationForward = false;
        if (offset <= 0) _animationForward = true;

        var blockStartHalf = offset;
        var blockEndHalf = offset + bodyHalfSize;

        var fg = Foreground;
        var trackFg = TrackForeground;
        var bg = Background;
        var gradientEnd = Lighten(fg, GradientLightenAmount);

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                var relX = x - bounds.X;
                var cellLeftHalf = relX * 2;
                var cellRightHalf = relX * 2 + 1;

                var leftInBlock = cellLeftHalf >= blockStartHalf && cellLeftHalf < blockEndHalf;
                var rightInBlock = cellRightHalf >= blockStartHalf && cellRightHalf < blockEndHalf;

                char ch;
                Color cellFg, cellBg;

                if (leftInBlock && rightInBlock)
                {
                    var t = bodyHalfSize > 1 ? (double)(cellLeftHalf - blockStartHalf) / (bodyHalfSize - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = FullBlock;
                    cellFg = color;
                    cellBg = color;
                }
                else if (leftInBlock)
                {
                    var t = bodyHalfSize > 1 ? (double)(cellLeftHalf - blockStartHalf) / (bodyHalfSize - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = LeftHalfBlock;
                    cellFg = color;
                    cellBg = bg;
                }
                else if (rightInBlock)
                {
                    var t = bodyHalfSize > 1 ? (double)(cellRightHalf - blockStartHalf) / (bodyHalfSize - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = RightHalfBlock;
                    cellFg = color;
                    cellBg = bg;
                }
                else
                {
                    // Track dot
                    ch = TrackDot;
                    cellFg = trackFg;
                    cellBg = bg;
                }

                buffer.SetChar(x, y, ch, cellFg, cellBg);
            }
        }
    }

    private void RenderVerticalIndeterminate(CellBuffer buffer, Rect bounds)
    {
        // Half-cell units for smooth sliding
        var trackHalfCells = bounds.Height * 2;
        var bodyHalfSize = Math.Max(2, (int)(trackHalfCells * IndeterminateBlockRatio));
        var maxOffset = Math.Max(0, trackHalfCells - bodyHalfSize);

        var offset = Interlocked.CompareExchange(ref _animationOffset, 0, 0);
        offset = Math.Clamp(offset, 0, maxOffset);

        if (offset >= maxOffset) _animationForward = false;
        if (offset <= 0) _animationForward = true;

        var blockStartHalf = offset;
        var blockEndHalf = offset + bodyHalfSize;

        var fg = Foreground;
        var trackFg = TrackForeground;
        var bg = Background;
        var gradientEnd = Lighten(fg, GradientLightenAmount);

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                if (!buffer.IsInBounds(x, y)) continue;

                var relY = y - bounds.Y;
                var cellTopHalf = relY * 2;
                var cellBottomHalf = relY * 2 + 1;

                var topInBlock = cellTopHalf >= blockStartHalf && cellTopHalf < blockEndHalf;
                var bottomInBlock = cellBottomHalf >= blockStartHalf && cellBottomHalf < blockEndHalf;

                char ch;
                Color cellFg, cellBg;

                if (topInBlock && bottomInBlock)
                {
                    var t = bodyHalfSize > 1 ? (double)(cellTopHalf - blockStartHalf) / (bodyHalfSize - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = FullBlock;
                    cellFg = color;
                    cellBg = color;
                }
                else if (topInBlock)
                {
                    var t = bodyHalfSize > 1 ? (double)(cellTopHalf - blockStartHalf) / (bodyHalfSize - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = UpperHalfBlock;
                    cellFg = color;
                    cellBg = bg;
                }
                else if (bottomInBlock)
                {
                    var t = bodyHalfSize > 1 ? (double)(cellBottomHalf - blockStartHalf) / (bodyHalfSize - 1) : 0.0;
                    var color = LerpColor(fg, gradientEnd, t);
                    ch = LowerHalfBlock;
                    cellFg = color;
                    cellBg = bg;
                }
                else
                {
                    // Track dot
                    ch = TrackDot;
                    cellFg = trackFg;
                    cellBg = bg;
                }

                buffer.SetChar(x, y, ch, cellFg, cellBg);
            }
        }
    }

    private static Color LerpColor(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private static Color Lighten(Color color, double amount)
    {
        return LerpColor(color, new Color(255, 255, 255), amount);
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
