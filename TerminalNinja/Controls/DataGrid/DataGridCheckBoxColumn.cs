using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A <see cref="DataGrid"/> column that displays a boolean value as a checkbox indicator.
/// Renders <c>[x]</c> for true and <c>[ ]</c> for false, centered in the cell.
/// Corresponds to WPF's System.Windows.Controls.DataGridCheckBoxColumn.
/// </summary>
public class DataGridCheckBoxColumn : DataGridBoundColumn
{
    private const string Checked = "[x]";
    private const string Unchecked = "[ ]";

    /// <inheritdoc />
    internal override void RenderCell(CellBuffer buffer, int x, int y, int width,
        object? dataItem, Color fg, Color bg)
    {
        var value = GetBindingValue(dataItem);
        var indicator = value is true ? Checked : Unchecked;

        // Center the indicator in the cell
        var padding = Math.Max(0, (width - indicator.Length) / 2);

        for (var i = 0; i < width; i++)
        {
            var charX = x + i;
            if (charX < 0 || charX >= buffer.Width || y < 0 || y >= buffer.Height) continue;
            var ci = i - padding;
            var ch = ci >= 0 && ci < indicator.Length ? indicator[ci] : ' ';
            buffer.SetChar(charX, y, ch, fg, bg);
        }
    }
}
