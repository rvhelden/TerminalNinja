namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// An unselected ListBoxItem must render a TextBlock child with the child's own colours —
/// per-row bound foregrounds are a feature, not something to flatten to the item default.
/// Only a selected item overrides them with the selection colours.
/// </summary>
public class ListBoxItemForegroundTests
{
    private static ListBoxItem CreateItem(bool isSelected) => new()
    {
        Content = new TextBlock { Text = "X", Foreground = Color.Red },
        Foreground = Color.White,
        Background = Color.Black,
        SelectedForeground = Color.Yellow,
        SelectedBackground = Color.Blue,
        IsSelected = isSelected,
        ShowSelectionIndicator = false,
    };

    private static Cell RenderAndFindGlyph(ListBoxItem item)
    {
        using var buffer = new CellBuffer(10, 1);
        item.Render(buffer, new Rect(0, 0, 10, 1));

        for (var x = 0; x < 10; x++)
        {
            var cell = buffer.GetCell(x, 0);
            if (cell.Codepoint == 'X')
            {
                return cell;
            }
        }

        throw new InvalidOperationException("Glyph not rendered");
    }

    [Test]
    public async Task Render_Unselected_KeepsTextBlockOwnForeground()
    {
        var cell = RenderAndFindGlyph(CreateItem(isSelected: false));
        await Assert.That(cell.Foreground).IsEqualTo(Color.Red);
    }

    [Test]
    public async Task Render_Selected_OverridesWithSelectedForeground()
    {
        var cell = RenderAndFindGlyph(CreateItem(isSelected: true));
        await Assert.That(cell.Foreground).IsEqualTo(Color.Yellow);
    }
}
