using TerminalNinja.Buffers;
using TerminalNinja.Controls;
using TerminalNinja.Controls.Primitives;
using TerminalNinja.Primitives;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Pins the rendering surface of <see cref="CompletionPanelRoot"/> directly —
/// the public <see cref="CompletionPanel"/> wraps it and pushes onto the
/// overlay stack, which requires an Application context.
/// </summary>
public class CompletionPanelTests
{
    private static CompletionPanelRoot BuildRoot(int selectedIndex = 0, params CompletionEntry[] items)
        => new()
        {
            Items = items,
            SelectedIndex = selectedIndex,
            AnchorX = 0,
            AnchorY = 0,
            Placement = PlacementMode.Bottom,
        };

    private static CompletionEntry Entry(string label, string? doc = null)
        => new(label, "ƒ", new Color(0x89, 0xB4, 0xFA), $"{label}(arg)", doc);

    [Test]
    public async Task EmptyItems_ZeroSizeBounds()
    {
        var root = BuildRoot();
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        await Assert.That(bounds.Width).IsEqualTo(0);
        await Assert.That(bounds.Height).IsEqualTo(0);
    }

    [Test]
    public async Task SingleItem_HeightIsOne()
    {
        var root = BuildRoot(0, Entry("where"));
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        await Assert.That(bounds.Height).IsEqualTo(1);
    }

    [Test]
    public async Task TwoPaneRender_ShowsLabelAndDetail()
    {
        var root = BuildRoot(0, Entry("where", "Filter a sequence."));
        using var buffer = new CellBuffer(80, 24);
        var viewport = new Rect(0, 0, 80, 24);
        root.Render(buffer, viewport);

        // Re-compute bounds to find where the panel actually rendered (Bottom
        // placement at AnchorY=0 lands one row below the anchor).
        var bounds = root.CalculateBounds(viewport);

        // Label "where" appears in the list pane.
        string listRow = ExtractRow(buffer, bounds.X, bounds.Y, 28);
        await Assert.That(listRow).Contains("where");
        // Detail "where(arg)" appears in the details pane (right of the separator).
        string detailRow = ExtractRow(buffer, bounds.X + 29, bounds.Y, 50);
        await Assert.That(detailRow).Contains("where(arg)");
    }

    [Test]
    public async Task NoDetailsForFocusedItem_PanelOmitsDetailsPane()
    {
        // Item with no Detail or Documentation → details pane should be skipped.
        var bare = new CompletionEntry("plain", "α", new Color(255, 255, 255), null, null);
        var root = BuildRoot(0, bare);
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        // List pane only — narrower than list+separator+details.
        await Assert.That(bounds.Width).IsLessThan(50);
    }

    [Test]
    public async Task BottomPlacement_OverflowingViewport_FlipsAboveAnchor()
    {
        // Anchor near bottom of viewport with content that won't fit below.
        var root = BuildRoot(0, Entry("a"), Entry("b"), Entry("c"));
        root.AnchorY = 23;          // last row of a 24-row viewport
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        await Assert.That(bounds.Y).IsLessThan(root.AnchorY);
    }

    private static string ExtractRow(CellBuffer buffer, int x, int y, int width)
    {
        var sb = new System.Text.StringBuilder(width);
        for (int i = 0; i < width; i++)
        {
            var c = buffer.GetCell(x + i, y);
            sb.Append((char)c.Codepoint);
        }
        return sb.ToString();
    }
}
