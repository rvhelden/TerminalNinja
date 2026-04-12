using System.Windows.Markup;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Styling;

namespace TerminalNinja.Controls;

/// <summary>
/// A scrollable viewport control that wraps a single child element.
/// Supports vertical and horizontal scrolling with keyboard, mouse wheel, and
/// programmatic control. Shows optional scroll indicators on the viewport edges.
/// Corresponds to WPF's System.Windows.Controls.ScrollViewer.
/// </summary>
[ContentProperty("Content")]
[RuntimeNameProperty("Name")]
public class ScrollViewer : ContentControl
{
    public ScrollViewer()
    {
        DefaultStyleKey = typeof(ScrollViewer);
    }

    // ─── Dependency Properties ───────────────────────────────────────

    public static readonly DependencyProperty VerticalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(VerticalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Visible, affectsRender: true));

    public static readonly DependencyProperty HorizontalScrollBarVisibilityProperty =
        DependencyProperty.Register(nameof(HorizontalScrollBarVisibility), typeof(ScrollBarVisibility), typeof(ScrollViewer),
            new FrameworkPropertyMetadata(ScrollBarVisibility.Disabled, affectsRender: true));

    public static readonly DependencyProperty VerticalOffsetProperty =
        DependencyProperty.Register(nameof(VerticalOffset), typeof(int), typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0, affectsRender: true));

    public static readonly DependencyProperty HorizontalOffsetProperty =
        DependencyProperty.Register(nameof(HorizontalOffset), typeof(int), typeof(ScrollViewer),
            new FrameworkPropertyMetadata(0, affectsRender: true));

    public static readonly DependencyProperty ScrollIndicatorForegroundProperty =
        DependencyProperty.Register(nameof(ScrollIndicatorForeground), typeof(Color), typeof(ScrollViewer),
            new FrameworkPropertyMetadata(Color.Gray, affectsRender: true));

    // ─── CLR Wrappers ────────────────────────────────────────────────

    /// <summary>Gets or sets the vertical scrollbar visibility mode.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(VerticalScrollBarVisibilityProperty)!;
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>Gets or sets the horizontal scrollbar visibility mode.</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => (ScrollBarVisibility)GetValue(HorizontalScrollBarVisibilityProperty)!;
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>Gets or sets the vertical scroll offset in rows.</summary>
    public int VerticalOffset
    {
        get => (int)GetValue(VerticalOffsetProperty)!;
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>Gets or sets the horizontal scroll offset in columns.</summary>
    public int HorizontalOffset
    {
        get => (int)GetValue(HorizontalOffsetProperty)!;
        set => SetValue(HorizontalOffsetProperty, value);
    }

    /// <summary>Gets or sets the foreground color for scroll indicators.</summary>
    public Color ScrollIndicatorForeground
    {
        get => (Color)GetValue(ScrollIndicatorForegroundProperty)!;
        set => SetValue(ScrollIndicatorForegroundProperty, value);
    }

    // ─── Computed Properties ─────────────────────────────────────────

    private int _viewportWidth;
    private int _viewportHeight;
    private int _extentWidth;
    private int _extentHeight;

    /// <summary>Gets the width of the visible viewport in columns.</summary>
    public int ViewportWidth => _viewportWidth;

    /// <summary>Gets the height of the visible viewport in rows.</summary>
    public int ViewportHeight => _viewportHeight;

    /// <summary>Gets the total width of the content.</summary>
    public int ExtentWidth => _extentWidth;

    /// <summary>Gets the total height of the content.</summary>
    public int ExtentHeight => _extentHeight;

    /// <summary>Gets the maximum horizontal scroll offset.</summary>
    public int ScrollableWidth => Math.Max(0, _extentWidth - _viewportWidth);

    /// <summary>Gets the maximum vertical scroll offset.</summary>
    public int ScrollableHeight => Math.Max(0, _extentHeight - _viewportHeight);

    // ─── Scroll helpers ──────────────────────────────────────────────

    private bool CanScrollVertically => VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;
    private bool CanScrollHorizontally => HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;

    private bool ShowVerticalIndicator => VerticalScrollBarVisibility switch
    {
        ScrollBarVisibility.Visible => true,
        ScrollBarVisibility.Auto => _extentHeight > _viewportHeight,
        _ => false
    };

    private bool ShowHorizontalIndicator => HorizontalScrollBarVisibility switch
    {
        ScrollBarVisibility.Visible => true,
        ScrollBarVisibility.Auto => _extentWidth > _viewportWidth,
        _ => false
    };

    /// <summary>Scrolls to the specified vertical offset, clamped to valid range.</summary>
    public void ScrollToVerticalOffset(int offset)
    {
        VerticalOffset = Math.Clamp(offset, 0, ScrollableHeight);
    }

    /// <summary>Scrolls to the specified horizontal offset, clamped to valid range.</summary>
    public void ScrollToHorizontalOffset(int offset)
    {
        HorizontalOffset = Math.Clamp(offset, 0, ScrollableWidth);
    }

    /// <summary>Scrolls vertically so that the specified row is visible.</summary>
    public void ScrollIntoView(int row)
    {
        if (row < VerticalOffset)
        {
            VerticalOffset = row;
        }
        else if (row >= VerticalOffset + _viewportHeight)
        {
            VerticalOffset = row - _viewportHeight + 1;
        }
    }

    // ─── Layout & Rendering ──────────────────────────────────────────

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parent) => parent;

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect parent)
    {
        return new Size2D(parent.Width, parent.Height);
    }

    /// <inheritdoc />
    public override void Render(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);

        var clipped = bounds.Intersect(new Rect(0, 0, buffer.Width, buffer.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0)
        {
            return;
        }

        // Fill background
        var bgCell = new Cell(' ', Foreground, Background);
        buffer.FillRect(clipped, bgCell);

        // Measure extent by asking child for its preferred size in a large area
        var measureWidth = CanScrollHorizontally ? 10000 : bounds.Width;
        var measureHeight = CanScrollVertically ? 10000 : bounds.Height;
        var measureRect = new Rect(0, 0, measureWidth, measureHeight);
        var contentSize = base.GetPreferredSize(measureRect);

        // Update extent (never smaller than viewport area — we'll finalize viewport below)
        _extentWidth = Math.Max(contentSize.Width, 1);
        _extentHeight = Math.Max(contentSize.Height, 1);

        // Determine if indicators are needed (chicken-and-egg: indicator space affects viewport)
        // Do two passes: first assume no indicators, then check if they're needed
        var showVIndicator = false;
        var showHIndicator = false;

        var vpWidth = bounds.Width;
        var vpHeight = bounds.Height;

        if (VerticalScrollBarVisibility == ScrollBarVisibility.Visible)
        {
            showVIndicator = true;
            vpWidth = Math.Max(1, bounds.Width - 1);
        }

        if (HorizontalScrollBarVisibility == ScrollBarVisibility.Visible)
        {
            showHIndicator = true;
            vpHeight = Math.Max(1, bounds.Height - 1);
        }

        if (VerticalScrollBarVisibility == ScrollBarVisibility.Auto && _extentHeight > vpHeight)
        {
            showVIndicator = true;
            vpWidth = Math.Max(1, bounds.Width - 1);
        }

        if (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && _extentWidth > vpWidth)
        {
            showHIndicator = true;
            vpHeight = Math.Max(1, bounds.Height - 1);
        }

        _viewportWidth = vpWidth;
        _viewportHeight = vpHeight;

        // Ensure extent is at least viewport size
        _extentWidth = Math.Max(_extentWidth, _viewportWidth);
        _extentHeight = Math.Max(_extentHeight, _viewportHeight);

        // Clamp offsets
        var vOffset = Math.Clamp(VerticalOffset, 0, ScrollableHeight);
        var hOffset = Math.Clamp(HorizontalOffset, 0, ScrollableWidth);
        if (vOffset != VerticalOffset) VerticalOffset = vOffset;
        if (hOffset != HorizontalOffset) HorizontalOffset = hOffset;

        // Render content
        if (_extentWidth <= _viewportWidth && _extentHeight <= _viewportHeight)
        {
            // Content fits — render directly to the main buffer (no intermediate buffer needed)
            var viewportBounds = new Rect(bounds.X, bounds.Y, _viewportWidth, _viewportHeight);
            RenderContent(buffer, viewportBounds);
        }
        else
        {
            // Content overflows — render to an intermediate buffer and blit the visible region
            var cappedExtentW = Math.Min(_extentWidth, 1000);
            var cappedExtentH = Math.Min(_extentHeight, 1000);

            using var intermediate = new CellBuffer(cappedExtentW, cappedExtentH);

            // Fill intermediate with background
            intermediate.FillRect(new Rect(0, 0, cappedExtentW, cappedExtentH), bgCell);

            // Render child into intermediate buffer at full extent size
            var extentBounds = new Rect(0, 0, cappedExtentW, cappedExtentH);
            RenderContent(intermediate, extentBounds);

            // Blit the visible region to the main buffer
            var sourceRect = new Rect(hOffset, vOffset, _viewportWidth, _viewportHeight);
            intermediate.CopyRegionTo(buffer, sourceRect, bounds.X, bounds.Y);
        }

        // Render scroll indicators
        if (showVIndicator)
        {
            RenderVerticalIndicator(buffer, bounds);
        }

        if (showHIndicator)
        {
            RenderHorizontalIndicator(buffer, bounds);
        }
    }

    /// <summary>
    /// Renders the content using the base ContentControl's rendering pipeline.
    /// </summary>
    private void RenderContent(CellBuffer buffer, Rect contentBounds)
    {
        // Use the ContentPresenter from ContentControl
        foreach (var (child, _) in base.GetChildrenWithBounds(contentBounds))
        {
            if (child is UIElement uiChild)
            {
                uiChild.Render(buffer, contentBounds);
            }
        }
    }

    /// <inheritdoc />
    public override IEnumerable<(Visual Child, Rect ChildParentBounds)> GetChildrenWithBounds(Rect myBounds)
    {
        // Adjust child bounds to account for scroll offset so hit-testing works correctly
        var vpWidth = _viewportWidth > 0 ? _viewportWidth : myBounds.Width;
        var vpHeight = _viewportHeight > 0 ? _viewportHeight : myBounds.Height;
        var extW = _extentWidth > 0 ? _extentWidth : vpWidth;
        var extH = _extentHeight > 0 ? _extentHeight : vpHeight;

        var childParent = new Rect(
            myBounds.X - HorizontalOffset,
            myBounds.Y - VerticalOffset,
            extW,
            extH);

        return base.GetChildrenWithBounds(childParent);
    }

    // ─── Scroll Indicators ───────────────────────────────────────────

    private void RenderVerticalIndicator(CellBuffer buffer, Rect bounds)
    {
        var trackX = bounds.X + bounds.Width - 1;
        var trackY = bounds.Y;
        var trackHeight = ShowHorizontalIndicator ? bounds.Height - 1 : bounds.Height;

        if (trackHeight <= 0 || trackX < 0 || trackX >= buffer.Width)
        {
            return;
        }

        var indicatorColor = ScrollIndicatorForeground;
        var bg = Background;

        // Draw track
        var trackCell = new Cell('░', indicatorColor, bg);
        for (var y = trackY; y < trackY + trackHeight; y++)
        {
            if (y >= 0 && y < buffer.Height)
            {
                buffer.SetCell(trackX, y, trackCell);
            }
        }

        // Draw thumb
        if (ScrollableHeight > 0 && trackHeight > 1)
        {
            var thumbHeight = Math.Max(1, _viewportHeight * trackHeight / _extentHeight);
            var thumbY = trackY + VerticalOffset * (trackHeight - thumbHeight) / ScrollableHeight;
            thumbY = Math.Clamp(thumbY, trackY, trackY + trackHeight - thumbHeight);

            var thumbCell = new Cell('█', indicatorColor, bg);
            for (var y = thumbY; y < thumbY + thumbHeight; y++)
            {
                if (y >= 0 && y < buffer.Height)
                {
                    buffer.SetCell(trackX, y, thumbCell);
                }
            }
        }
    }

    private void RenderHorizontalIndicator(CellBuffer buffer, Rect bounds)
    {
        var trackX = bounds.X;
        var trackY = bounds.Y + bounds.Height - 1;
        var trackWidth = ShowVerticalIndicator ? bounds.Width - 1 : bounds.Width;

        if (trackWidth <= 0 || trackY < 0 || trackY >= buffer.Height)
        {
            return;
        }

        var indicatorColor = ScrollIndicatorForeground;
        var bg = Background;

        // Draw track
        var trackCell = new Cell('░', indicatorColor, bg);
        for (var x = trackX; x < trackX + trackWidth; x++)
        {
            if (x >= 0 && x < buffer.Width)
            {
                buffer.SetCell(x, trackY, trackCell);
            }
        }

        // Draw thumb
        if (ScrollableWidth > 0 && trackWidth > 1)
        {
            var thumbWidth = Math.Max(1, _viewportWidth * trackWidth / _extentWidth);
            var thumbX = trackX + HorizontalOffset * (trackWidth - thumbWidth) / ScrollableWidth;
            thumbX = Math.Clamp(thumbX, trackX, trackX + trackWidth - thumbWidth);

            var thumbCell = new Cell('█', indicatorColor, bg);
            for (var x = thumbX; x < thumbX + thumbWidth; x++)
            {
                if (x >= 0 && x < buffer.Width)
                {
                    buffer.SetCell(x, trackY, thumbCell);
                }
            }
        }
    }

    // ─── Input Handling ──────────────────────────────────────────────

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        switch (e)
        {
            case { Key: ConsoleKey.UpArrow } when CanScrollVertically:
                ScrollToVerticalOffset(VerticalOffset - 1);
                break;
            case { Key: ConsoleKey.DownArrow } when CanScrollVertically:
                ScrollToVerticalOffset(VerticalOffset + 1);
                break;
            case { Key: ConsoleKey.LeftArrow } when CanScrollHorizontally:
                ScrollToHorizontalOffset(HorizontalOffset - 1);
                break;
            case { Key: ConsoleKey.RightArrow } when CanScrollHorizontally:
                ScrollToHorizontalOffset(HorizontalOffset + 1);
                break;
            case { Key: ConsoleKey.PageUp } when CanScrollVertically:
                ScrollToVerticalOffset(VerticalOffset - _viewportHeight);
                break;
            case { Key: ConsoleKey.PageDown } when CanScrollVertically:
                ScrollToVerticalOffset(VerticalOffset + _viewportHeight);
                break;
            case { Key: ConsoleKey.Home, Ctrl: true }:
                ScrollToVerticalOffset(0);
                ScrollToHorizontalOffset(0);
                break;
            case { Key: ConsoleKey.End, Ctrl: true }:
                ScrollToVerticalOffset(ScrollableHeight);
                ScrollToHorizontalOffset(ScrollableWidth);
                break;
            case { Key: ConsoleKey.Home } when CanScrollVertically:
                ScrollToVerticalOffset(0);
                break;
            case { Key: ConsoleKey.End } when CanScrollVertically:
                ScrollToVerticalOffset(ScrollableHeight);
                break;
        }
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        switch (e.Action)
        {
            case MouseAction.ScrollUp when CanScrollVertically:
                ScrollToVerticalOffset(VerticalOffset - 3);
                break;
            case MouseAction.ScrollDown when CanScrollVertically:
                ScrollToVerticalOffset(VerticalOffset + 3);
                break;
        }
    }
}
