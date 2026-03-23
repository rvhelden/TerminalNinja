using TerminalNinja.App;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the Popup control covering:
/// - IsOpen / Child / PlacementTarget / Placement properties
/// - Opening and closing pushes/removes overlay
/// - Positioning via PlacementMode
/// - Events (Opened / Closed)
/// </summary>
[NotInParallel("ApplicationSingleton")]
public class PopupTests
{
    private CellBuffer _buffer = null!;
    private const int BufferWidth = 80;
    private const int BufferHeight = 24;

    [Before(Test)]
    public Task Setup()
    {
        _buffer = new CellBuffer(BufferWidth, BufferHeight);
        return Task.CompletedTask;
    }

    [After(Test)]
    public Task Cleanup()
    {
        _buffer.Dispose();
        return Task.CompletedTask;
    }

    // ─── Default property values ─────────────────────────────────────

    [Test]
    public async Task IsOpen_DefaultValue_IsFalse()
    {
        var popup = new Popup();
        await Assert.That(popup.IsOpen).IsFalse();
    }

    [Test]
    public async Task Child_DefaultValue_IsNull()
    {
        var popup = new Popup();
        await Assert.That(popup.Child).IsNull();
    }

    [Test]
    public async Task Placement_DefaultValue_IsBottom()
    {
        var popup = new Popup();
        await Assert.That(popup.Placement).IsEqualTo(PlacementMode.Bottom);
    }

    [Test]
    public async Task StaysOpen_DefaultValue_IsTrue()
    {
        var popup = new Popup();
        await Assert.That(popup.StaysOpen).IsTrue();
    }

    [Test]
    public async Task HorizontalOffset_DefaultValue_IsZero()
    {
        var popup = new Popup();
        await Assert.That(popup.HorizontalOffset).IsEqualTo(0);
    }

    [Test]
    public async Task VerticalOffset_DefaultValue_IsZero()
    {
        var popup = new Popup();
        await Assert.That(popup.VerticalOffset).IsEqualTo(0);
    }

    // ─── Zero size in visual tree ────────────────────────────────────

    [Test]
    public async Task GetPreferredSize_ReturnsZero()
    {
        var popup = new Popup
        {
            Child = new TextBlock { Text = "Large content" }
        };

        var size = popup.GetPreferredSize(new Rect(0, 0, 80, 24));
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task CalculateBounds_ReturnsZeroSize()
    {
        var popup = new Popup();
        var bounds = popup.CalculateBounds(new Rect(5, 10, 80, 24));
        await Assert.That(bounds.Width).IsEqualTo(0);
        await Assert.That(bounds.Height).IsEqualTo(0);
    }

    // ─── Open / Close overlay lifecycle ──────────────────────────────

    [Test]
    public async Task IsOpen_True_PushesOverlayOntoApplication()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var popup = new Popup
        {
            Child = new TextBlock { Text = "Popup content" }
        };

        popup.IsOpen = true;

        await Assert.That(app.Overlays.Count).IsEqualTo(1);
        await Assert.That(app.Overlays[0].IsModal).IsFalse();
        await Assert.That(app.Overlays[0].DimBackground).IsFalse();

        popup.IsOpen = false;
        await Assert.That(app.Overlays.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IsOpen_False_RemovesOverlay()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var popup = new Popup
        {
            Child = new TextBlock { Text = "Content" }
        };

        popup.IsOpen = true;
        await Assert.That(app.Overlays.Count).IsEqualTo(1);

        popup.IsOpen = false;
        await Assert.That(app.Overlays.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IsOpen_SetTrueTwice_OnlyOnePush()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var popup = new Popup { Child = new TextBlock { Text = "X" } };

        popup.IsOpen = true;
        popup.IsOpen = true; // second set should be no-op (value unchanged by DP)

        await Assert.That(app.Overlays.Count).IsEqualTo(1);

        popup.IsOpen = false;
    }

    // ─── Events ──────────────────────────────────────────────────────

    [Test]
    public async Task Opened_Event_RaisedWhenOpened()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var popup = new Popup { Child = new TextBlock { Text = "X" } };
        var opened = false;
        popup.Opened += (_, _) => opened = true;

        popup.IsOpen = true;

        await Assert.That(opened).IsTrue();
        popup.IsOpen = false;
    }

    [Test]
    public async Task Closed_Event_RaisedWhenClosed()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var popup = new Popup { Child = new TextBlock { Text = "X" } };
        var closed = false;
        popup.Closed += (_, _) => closed = true;

        popup.IsOpen = true;
        popup.IsOpen = false;

        await Assert.That(closed).IsTrue();
    }

    // ─── No Application: open is no-op ──────────────────────────────

    [Test]
    public async Task IsOpen_WithoutApplication_DoesNotThrow()
    {
        // Ensure no application instance — create a headless one just to dispose it
        using (var temp = new Application(new ApplicationOptions { Headless = true }))
        {
            // temp is now Current
        }
        // After dispose, Current == null

        var popup = new Popup { Child = new TextBlock { Text = "X" } };
        popup.IsOpen = true; // Should not throw, just no-op

        await Assert.That(popup.IsOpen).IsTrue(); // Property set, but no overlay pushed
    }
}
