using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Renders the scrollback area of the REPL: visible output rows, the scroll-position
/// indicator on the right edge, and inverted highlight for any output-region selection.
/// Owns the scroll offset state.
/// </summary>
internal sealed class OutputRenderer
{
    private static readonly Color ThumbColor = new(0x6C, 0x70, 0x86);

    private readonly OutputLog _output;
    private readonly SelectionModel _selection;
    private int _scrollOffset;

    public OutputRenderer(OutputLog output, SelectionModel selection)
    {
        _output = output;
        _selection = selection;
    }

    public int ScrollOffset => _scrollOffset;

    public void ScrollBy(int lines, int viewportHeight)
    {
        _scrollOffset += lines;
        Clamp(viewportHeight);
    }

    public void ScrollTo(int offset, int viewportHeight)
    {
        _scrollOffset = offset;
        Clamp(viewportHeight);
    }

    public void ScrollToBottom() => _scrollOffset = 0;

    public void Clamp(int viewportHeight)
    {
        int maxOffset = Math.Max(0, _output.LineCount - viewportHeight);
        if (_scrollOffset > maxOffset) _scrollOffset = maxOffset;
        if (_scrollOffset < 0) _scrollOffset = 0;
    }

    public void Render(CellBuffer buffer, in ReplLayout layout, Color fg, Color bg)
    {
        var height = layout.OutputHeight;
        if (height <= 0) return;

        int x = layout.Bounds.X;
        int y = layout.Bounds.Y;
        int width = layout.Bounds.Width;

        var firstLine = Math.Max(0, _output.LineCount - height - _scrollOffset);
        var lastLine = Math.Min(_output.LineCount, firstLine + height);

        for (var i = firstLine; i < lastLine; i++)
        {
            var row = y + (i - firstLine);
            CellPaint.DrawText(buffer, x, row, _output.Lines[i], width, fg, bg);
            ApplySelectionToRow(buffer, x, row, width, i);
        }

        RenderScrollIndicator(buffer, layout.Bounds.Right - 1, y, height, bg);
    }

    /// <summary>
    /// Maps the first visible output row's panel-Y back to a line index, for hit-testing.
    /// Returns -1 when the line is outside the buffer.
    /// </summary>
    public int LineIndexForRow(int rowInPanel, int outputHeight)
    {
        int firstVisible = Math.Max(0, _output.LineCount - outputHeight - _scrollOffset);
        int lineIndex = firstVisible + rowInPanel;
        if (lineIndex < 0 || lineIndex >= _output.LineCount) return -1;
        return lineIndex;
    }

    private void ApplySelectionToRow(CellBuffer buffer, int x, int row, int width, int lineIndex)
    {
        if (_selection.Region != SelectionRegion.Output) return;
        if (!_selection.TryGetSelectedColsForRow(lineIndex, _output.Lines[lineIndex].Length, out var startCol, out var endCol)) return;
        CellPaint.InvertCells(buffer, x + startCol, row, Math.Min(endCol, width) - startCol);
    }

    /// <summary>
    /// One-cell-wide scroll-position indicator on the right edge. Thumb height is
    /// proportional to "visible / total"; top position is proportional to scroll
    /// offset. Track cells stay blank so the indicator looks like a discrete block.
    /// </summary>
    private void RenderScrollIndicator(CellBuffer buffer, int x, int y, int outputHeight, Color bg)
    {
        if (outputHeight <= 1) return;
        if (_output.LineCount <= outputHeight) return;

        int total = _output.LineCount;
        int thumbHeight = Math.Max(1, Math.Min(outputHeight, outputHeight * outputHeight / Math.Max(1, total)));
        int trackLength = outputHeight - thumbHeight;
        int maxOffset = Math.Max(1, total - outputHeight);
        int offsetFromTop = trackLength - (trackLength * Math.Min(_scrollOffset, maxOffset) / maxOffset);
        int thumbTopY = y + offsetFromTop;

        for (int i = 0; i < thumbHeight; i++)
        {
            int row = thumbTopY + i;
            if ((uint)row >= (uint)buffer.Height || (uint)x >= (uint)buffer.Width) continue;
            buffer.SetChar(x, row, '█', ThumbColor, bg);
        }
    }
}
