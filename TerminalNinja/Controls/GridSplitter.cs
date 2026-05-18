using TerminalNinja.App;
using TerminalNinja.Buffers;
using TerminalNinja.Input;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A focusable single-cell-wide vertical splitter the user drags (mouse) or
/// nudges (keyboard) to resize sibling panels. The splitter itself doesn't
/// own any width — it raises a <see cref="Resized"/> event with the cumulative
/// column delta and lets the host (a view model or parent layout) translate
/// that into bound width changes.
/// </summary>
/// <remarks>
/// <para>
/// Drag tracking is anchor-relative: a <see cref="MouseAction.Press"/> sets
/// the anchor at the current X, every subsequent <see cref="MouseAction.Move"/>
/// reports <c>e.X - anchorX</c> as the delta, and on <see cref="MouseAction.Release"/>
/// the anchor is cleared. The host updates a bound width property as deltas
/// come in, then the next render naturally repositions the splitter so the
/// anchor stays under the cursor.
/// </para>
/// <para>
/// Keyboard: left and right arrows emit deltas of -1 and +1. Hold Shift for
/// a larger step (4 cells) — useful when the panel needs to move many cells.
/// </para>
/// <para>
/// The visual: a dim ruled glyph (<c>│</c>) when unfocused, a brighter double
/// glyph (<c>║</c>) when focused so the user can see which splitter the
/// keyboard arrows would move.
/// </para>
/// </remarks>
public sealed class GridSplitter : Control
{
    private int _dragAnchorX;
    private bool _isDragging;

    /// <summary>Identifies the <see cref="Step"/> dependency property.</summary>
    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(int), typeof(GridSplitter),
            new PropertyMetadata(1));

    /// <summary>
    /// Number of cells a single arrow-key press emits. Defaults to 1. Hosts
    /// that want a coarser default keyboard resize can raise this.
    /// </summary>
    public int Step
    {
        get => (int)GetValue(StepProperty)!;
        set => SetValue(StepProperty, value);
    }

    /// <summary>
    /// Raised whenever the splitter has accumulated a non-zero column delta —
    /// once per significant mouse move and once per arrow keypress. The argument
    /// is the signed delta in cells: negative means "shrink the left side, grow
    /// the right side", positive means the reverse.
    /// </summary>
    public event Action<int>? Resized;

    /// <inheritdoc />
    public override Size2D GetPreferredSize(Rect availableSpace) => new(1, availableSpace.Height);

    /// <inheritdoc />
    public override Rect CalculateBounds(Rect parentBounds)
        => new(parentBounds.X, parentBounds.Y, 1, parentBounds.Height);

    /// <inheritdoc />
    protected override void OnRender(CellBuffer buffer, Rect parentBounds)
    {
        var bounds = CalculateBounds(parentBounds);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var glyph = IsFocused ? '║' : '│';
        var fg = IsFocused
            ? new Color(0x89, 0xB4, 0xFA) // focused: blue accent so users can spot which splitter the keys move
            : new Color(0x45, 0x47, 0x5A); // unfocused: same dim grey the existing borders use
        var bg = Background;
        for (int row = 0; row < bounds.Height; row++)
        {
            int y = bounds.Y + row;
            if ((uint)y >= (uint)buffer.Height) continue;
            buffer.SetChar(bounds.X, y, glyph, fg, bg);
        }
    }

    /// <inheritdoc />
    public override void OnMouseEvent(MouseEvent e)
    {
        switch (e.Action)
        {
            case MouseAction.Press when e.Button == MouseButton.Left:
                _dragAnchorX = e.X;
                _isDragging = true;
                // Capture the mouse — the splitter is 1 cell wide, so without
                // capture the very first move event leaves our bounds and gets
                // routed to a sibling instead, making drag-resize impossible.
                Application.Current?.FocusManager.CaptureMouse(this);
                break;
            case MouseAction.Move when _isDragging:
                int delta = e.X - _dragAnchorX;
                if (delta == 0) break;
                Resized?.Invoke(delta);
                // Re-anchor at the current cursor — the next render will place
                // us under the cursor again so subsequent moves are incremental.
                _dragAnchorX = e.X;
                break;
            case MouseAction.Release when e.Button == MouseButton.Left:
                _isDragging = false;
                Application.Current?.FocusManager.ReleaseMouseCapture();
                break;
        }
    }

    /// <inheritdoc />
    public override void OnKeyEvent(KeyEvent e)
    {
        int step = e.Shift ? Math.Max(Step, 4) : Step;
        switch (e.Key)
        {
            case ConsoleKey.LeftArrow:
                Resized?.Invoke(-step);
                return;
            case ConsoleKey.RightArrow:
                Resized?.Invoke(+step);
                return;
        }
    }
}
