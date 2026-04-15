using TerminalNinja.Buffers;
using TerminalNinja.Primitives;

namespace TerminalNinja.Controls;

/// <summary>
/// A <see cref="DataGrid"/> column that renders each cell using a <see cref="DataTemplate"/>.
/// The template's <see cref="FrameworkElement.DataContext"/> is set to the row's data item.
/// Corresponds to WPF's System.Windows.Controls.DataGridTemplateColumn.
/// </summary>
public class DataGridTemplateColumn : DataGridColumn
{
    /// <summary>
    /// Gets or sets the template used to render each cell.
    /// </summary>
    public DataTemplate? CellTemplate { get; set; }

    /// <inheritdoc />
    internal override void RenderCell(CellBuffer buffer, int x, int y, int width,
        object? dataItem, Color fg, Color bg)
    {
        if (CellTemplate == null)
        {
            // Fallback: render ToString()
            var text = dataItem?.ToString() ?? "";
            for (var i = 0; i < width; i++)
            {
                var charX = x + i;
                if (charX < 0 || charX >= buffer.Width || y < 0 || y >= buffer.Height) continue;
                var ch = i < text.Length ? text[i] : ' ';
                buffer.SetChar(charX, y, ch, fg, bg);
            }
            return;
        }

        var content = CellTemplate.CreateContent();
        if (content is FrameworkElement fe)
        {
            fe.DataContext = dataItem;
        }

        content?.Render(buffer, new Rect(x, y, width, 1));
    }
}
