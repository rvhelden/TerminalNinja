using System.Text;

namespace NinjaShellUi;

/// <summary>Which region the active selection lives in.</summary>
internal enum SelectionRegion { None, Input, Output }

/// <summary>
/// Region-scoped selection state for <see cref="ReplView"/>. Coordinates are stored in
/// <em>region</em> space (line index within the region + column within that line), not
/// screen space — scrolling the output area doesn't move the selection. A selection
/// never crosses from input to output or vice versa.
/// </summary>
internal sealed class SelectionModel
{
    public SelectionRegion Region { get; private set; } = SelectionRegion.None;
    public (int Row, int Col) Anchor { get; private set; }
    public (int Row, int Col) Head { get; private set; }
    public bool Rectangular { get; private set; }
    public bool IsMouseDragging { get; set; }

    public bool HasSelection
        => Region != SelectionRegion.None
           && (Anchor.Row != Head.Row || Anchor.Col != Head.Col);

    public void Begin(SelectionRegion region, int row, int col, bool rectangular)
    {
        Region = region;
        Rectangular = rectangular;
        Anchor = (row, col);
        Head = (row, col);
    }

    public void ExtendHead(int row, int col)
    {
        Head = (row, col);
    }

    public void Clear()
    {
        Region = SelectionRegion.None;
        Rectangular = false;
        IsMouseDragging = false;
    }

    /// <summary>
    /// Compute the [startCol, endCol) range of selected columns on <paramref name="row"/>,
    /// within a line of <paramref name="lineLength"/>. Returns false when this row falls
    /// outside the selection. Handles both line-flow and rectangular selection modes.
    /// </summary>
    public bool TryGetSelectedColsForRow(int row, int lineLength, out int startCol, out int endCol)
    {
        startCol = endCol = 0;
        if (Region == SelectionRegion.None) return false;

        int rowLo = Math.Min(Anchor.Row, Head.Row);
        int rowHi = Math.Max(Anchor.Row, Head.Row);
        if (row < rowLo || row > rowHi) return false;

        if (Rectangular)
        {
            int colLo = Math.Min(Anchor.Col, Head.Col);
            int colHi = Math.Max(Anchor.Col, Head.Col);
            startCol = Math.Min(colLo, lineLength);
            endCol = Math.Min(colHi, lineLength);
            return endCol > startCol;
        }

        // Line-flow: the first selected row goes from the anchor's column to EOL,
        // intermediate rows are fully selected, the last row goes from BOL to head.
        var (firstRow, firstCol, lastRow, lastCol) = OrderedEndpoints();
        if (row == firstRow && row == lastRow) { startCol = firstCol; endCol = Math.Min(lastCol, lineLength); }
        else if (row == firstRow)              { startCol = firstCol; endCol = lineLength; }
        else if (row == lastRow)               { startCol = 0;        endCol = Math.Min(lastCol, lineLength); }
        else                                   { startCol = 0;        endCol = lineLength; }
        return endCol > startCol;
    }

    private (int FirstRow, int FirstCol, int LastRow, int LastCol) OrderedEndpoints()
    {
        if (Anchor.Row < Head.Row
            || (Anchor.Row == Head.Row && Anchor.Col <= Head.Col))
        {
            return (Anchor.Row, Anchor.Col, Head.Row, Head.Col);
        }
        return (Head.Row, Head.Col, Anchor.Row, Anchor.Col);
    }

    /// <summary>
    /// Build the text payload for the current selection over <paramref name="source"/>,
    /// ready to ship to the clipboard. Lines are joined with <c>\n</c>; rectangular
    /// selections pad/truncate each row to the column band.
    /// </summary>
    public string BuildText(IReadOnlyList<string> source)
    {
        if (Region == SelectionRegion.None) return string.Empty;

        int rowLo = Math.Max(0, Math.Min(Anchor.Row, Head.Row));
        int rowHi = Math.Min(source.Count - 1, Math.Max(Anchor.Row, Head.Row));
        if (rowHi < rowLo) return string.Empty;

        var sb = new StringBuilder();
        for (int r = rowLo; r <= rowHi; r++)
        {
            if (!TryGetSelectedColsForRow(r, source[r].Length, out var s, out var e))
            {
                if (r != rowHi) sb.Append('\n');
                continue;
            }
            sb.Append(source[r], s, e - s);
            if (r != rowHi) sb.Append('\n');
        }
        return sb.ToString();
    }
}
