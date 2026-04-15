using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A <see cref="DataGrid"/> column that displays bound values as text.
/// Corresponds to WPF's System.Windows.Controls.DataGridTextColumn.
/// </summary>
public class DataGridTextColumn : DataGridBoundColumn
{
    /// <inheritdoc />
    internal override void RenderCell(CellBuffer buffer, int x, int y, int width,
        object? dataItem, Color fg, Color bg)
    {
        var text = GetBindingValue(dataItem)?.ToString() ?? dataItem?.ToString() ?? "";

        for (var i = 0; i < width; i++)
        {
            var charX = x + i;
            if (charX < 0 || charX >= buffer.Width || y < 0 || y >= buffer.Height) continue;
            var ch = i < text.Length ? text[i] : ' ';
            buffer.SetChar(charX, y, ch, fg, bg);
        }
    }
}
