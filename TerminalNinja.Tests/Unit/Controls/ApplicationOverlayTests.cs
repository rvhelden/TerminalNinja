using TerminalNinja.App;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Application overlay stack infrastructure:
/// - PushOverlay / RemoveOverlay / PopOverlay
/// - ActiveModal / IsModal
/// - Overlay rendering (dim + overlay on top of root)
/// </summary>
[NotInParallel("ApplicationSingleton")]
public class ApplicationOverlayTests
{
    // ─── Overlay stack management ────────────────────────────────────

    [Test]
    public async Task PushOverlay_AddsToOverlayStack()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay = new TextBlock { Text = "Overlay" };

        app.PushOverlay(overlay);

        await Assert.That(app.Overlays.Count).IsEqualTo(1);
        await Assert.That(app.Overlays[0].Element).IsEqualTo(overlay);
    }

    [Test]
    public async Task PushOverlay_Modal_SetsIsModal()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay = new TextBlock { Text = "Modal" };

        app.PushOverlay(overlay, isModal: true, dimBackground: true);

        await Assert.That(app.IsModal).IsTrue();
        await Assert.That(app.ActiveModal).IsNotNull();
        await Assert.That(app.ActiveModal!.Element).IsEqualTo(overlay);
    }

    [Test]
    public async Task PushOverlay_NonModal_IsModalIsFalse()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay = new TextBlock { Text = "Non-modal" };

        app.PushOverlay(overlay, isModal: false);

        await Assert.That(app.IsModal).IsFalse();
        await Assert.That(app.ActiveModal).IsNull();
    }

    [Test]
    public async Task RemoveOverlay_RemovesSpecificOverlay()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay1 = new TextBlock { Text = "A" };
        var overlay2 = new TextBlock { Text = "B" };
        app.PushOverlay(overlay1);
        app.PushOverlay(overlay2);

        var removed = app.RemoveOverlay(overlay1);

        await Assert.That(removed).IsTrue();
        await Assert.That(app.Overlays.Count).IsEqualTo(1);
        await Assert.That(app.Overlays[0].Element).IsEqualTo(overlay2);
    }

    [Test]
    public async Task RemoveOverlay_NotFound_ReturnsFalse()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay = new TextBlock { Text = "A" };

        var removed = app.RemoveOverlay(overlay);

        await Assert.That(removed).IsFalse();
    }

    [Test]
    public async Task PopOverlay_RemovesTopmost()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay1 = new TextBlock { Text = "A" };
        var overlay2 = new TextBlock { Text = "B" };
        app.PushOverlay(overlay1);
        app.PushOverlay(overlay2);

        var popped = app.PopOverlay();

        await Assert.That(popped).IsNotNull();
        await Assert.That(popped!.Element).IsEqualTo(overlay2);
        await Assert.That(app.Overlays.Count).IsEqualTo(1);
    }

    [Test]
    public async Task PopOverlay_EmptyStack_ReturnsNull()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });

        var popped = app.PopOverlay();

        await Assert.That(popped).IsNull();
    }

    [Test]
    public async Task PushOverlay_NullElement_Throws()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });

        await Assert.That(() => app.PushOverlay(null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    // ─── Multiple overlays: ActiveModal is topmost ───────────────────

    [Test]
    public async Task ActiveModal_WithMultipleModals_ReturnsTopmost()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var modal1 = new TextBlock { Text = "M1" };
        var modal2 = new TextBlock { Text = "M2" };

        app.PushOverlay(modal1, isModal: true);
        app.PushOverlay(modal2, isModal: true);

        await Assert.That(app.ActiveModal!.Element).IsEqualTo(modal2);

        // Remove top modal — now bottom modal is active
        app.RemoveOverlay(modal2);
        await Assert.That(app.ActiveModal!.Element).IsEqualTo(modal1);
    }

    // ─── Overlay rendering with dimming ──────────────────────────────

    [Test]
    public async Task OverlayRendering_DimBackground_DimsCellsBeneathOverlay()
    {
        // This test verifies the rendering pipeline concept:
        // 1. Root control renders red cells
        // 2. A dim overlay dims those cells
        // 3. The overlay content renders on top

        using var buffer = new CellBuffer(20, 5);
        var viewport = new Rect(0, 0, 20, 5);

        // Step 1: render root (red background)
        var root = new global::TerminalNinja.Controls.Border { Background = Color.Red };
        root.Render(buffer, viewport);
        await Assert.That(buffer.GetCell(0, 0).Background).IsEqualTo(Color.Red);

        // Step 2: dim
        buffer.DimAll();
        var dimmed = buffer.GetCell(0, 0);
        await Assert.That(dimmed.Background.R).IsEqualTo((byte)127);

        // Step 3: overlay on top (green TextBlock)
        var overlay = new TextBlock { Text = "Hi", Foreground = Color.Green };
        overlay.Render(buffer, viewport);
        var overlayCell = buffer.GetCell(0, 0);
        await Assert.That(overlayCell.Character).IsEqualTo('H');
        await Assert.That(overlayCell.Foreground).IsEqualTo(Color.Green);
    }

    // ─── DimBackground flag on overlay entry ─────────────────────────

    [Test]
    public async Task PushOverlay_DimBackground_Flag()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay = new TextBlock { Text = "X" };

        app.PushOverlay(overlay, isModal: true, dimBackground: true);

        await Assert.That(app.Overlays[0].DimBackground).IsTrue();
    }

    [Test]
    public async Task PushOverlay_NoDimBackground_Flag()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        var overlay = new TextBlock { Text = "X" };

        app.PushOverlay(overlay, isModal: false, dimBackground: false);

        await Assert.That(app.Overlays[0].DimBackground).IsFalse();
    }
}
