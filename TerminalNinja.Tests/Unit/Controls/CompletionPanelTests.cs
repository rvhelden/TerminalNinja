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
        // Multiple items so the panel renders at full height (the details pane
        // needs vertical room to show Detail + Documentation; a 1-row panel
        // truncates to just a scroll indicator).
        var root = BuildRoot(0,
            Entry("where", "Filter a sequence."),
            Entry("select", "Map a sequence."),
            Entry("fold", "Left-fold a sequence."),
            Entry("take", "Take the first N."));
        using var buffer = new CellBuffer(80, 24);
        var viewport = new Rect(0, 0, 80, 24);
        root.Render(buffer, viewport);

        // Re-compute bounds to find where the panel actually rendered (Bottom
        // placement at AnchorY=0 lands one row below the anchor).
        var bounds = root.CalculateBounds(viewport);

        // The two-pane layout shows Label in the list pane and Detail in the
        // details pane. Don't pin the precise row for each — search the whole
        // rendered region so the test survives minor layout shifts.
        var region = ExtractRegion(buffer, bounds);
        await Assert.That(region).Contains("where");
        await Assert.That(region).Contains("where(arg)");
    }

    private static string ExtractRegion(CellBuffer buffer, Rect bounds)
    {
        var sb = new System.Text.StringBuilder();
        for (int row = 0; row < bounds.Height; row++)
        {
            sb.Append(ExtractRow(buffer, bounds.X, bounds.Y + row, bounds.Width));
            sb.Append('\n');
        }
        return sb.ToString();
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
