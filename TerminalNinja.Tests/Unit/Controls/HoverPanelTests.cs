using TerminalNinja.App;
using TerminalNinja.Controls.Primitives;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for the HoverPanel control. Mirrors the PopupTests structure since
/// the two share the overlay-stack-push lifecycle pattern. The differences
/// covered here are HoverPanel's point-anchored positioning (vs. Popup's
/// element-anchored), the imperative ShowAt/Hide API, and the auto-flip
/// behaviour when Bottom placement overflows the viewport.
/// </summary>
[NotInParallel("ApplicationSingleton")]
public class HoverPanelTests
{
    // ─── Default property values ─────────────────────────────────────

    [Test]
    public async Task IsOpen_DefaultValue_IsFalse()
    {
        var panel = new HoverPanel();
        await Assert.That(panel.IsOpen).IsFalse();
    }

    [Test]
    public async Task Content_DefaultValue_IsNull()
    {
        var panel = new HoverPanel();
        await Assert.That(panel.Content).IsNull();
    }

    [Test]
    public async Task Placement_DefaultValue_IsBottom()
    {
        var panel = new HoverPanel();
        await Assert.That(panel.Placement).IsEqualTo(PlacementMode.Bottom);
    }

    [Test]
    public async Task Anchor_DefaultValues_AreZero()
    {
        var panel = new HoverPanel();
        await Assert.That(panel.AnchorX).IsEqualTo(0);
        await Assert.That(panel.AnchorY).IsEqualTo(0);
    }

    [Test]
    public async Task Offsets_DefaultValues_AreZero()
    {
        var panel = new HoverPanel();
        await Assert.That(panel.HorizontalOffset).IsEqualTo(0);
        await Assert.That(panel.VerticalOffset).IsEqualTo(0);
    }

    // ─── Zero size in visual tree ────────────────────────────────────

    [Test]
    public async Task GetPreferredSize_ReturnsZero()
    {
        var panel = new HoverPanel { Content = new TextBlock { Text = "x" } };
        var size = panel.GetPreferredSize(new Rect(0, 0, 80, 24));
        await Assert.That(size.Width).IsEqualTo(0);
        await Assert.That(size.Height).IsEqualTo(0);
    }

    [Test]
    public async Task CalculateBounds_ReturnsZeroSize()
    {
        var panel = new HoverPanel();
        var bounds = panel.CalculateBounds(new Rect(5, 10, 80, 24));
        await Assert.That(bounds.Width).IsEqualTo(0);
        await Assert.That(bounds.Height).IsEqualTo(0);
    }

    // ─── Open / close lifecycle ──────────────────────────────────────

    [Test]
    public async Task IsOpen_True_PushesOverlayOntoApplication_NotModal_NotDimmed()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var panel = new HoverPanel { Content = new TextBlock { Text = "info" } };
        panel.IsOpen = true;

        await Assert.That(app.Overlays.Count).IsEqualTo(1);
        await Assert.That(app.Overlays[0].IsModal).IsFalse();
        await Assert.That(app.Overlays[0].DimBackground).IsFalse();

        panel.IsOpen = false;
        await Assert.That(app.Overlays.Count).IsEqualTo(0);
    }

    [Test]
    public async Task IsOpen_SetTrueTwice_OnlyOnePush()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var panel = new HoverPanel { Content = new TextBlock { Text = "x" } };
        panel.IsOpen = true;
        panel.IsOpen = true;
        await Assert.That(app.Overlays.Count).IsEqualTo(1);
        panel.IsOpen = false;
    }

    [Test]
    public async Task IsOpen_WithoutApplication_DoesNotThrow()
    {
        using (var temp = new Application(new ApplicationOptions { Headless = true })) { /* current */ }
        // After dispose, Application.Current is null.

        var panel = new HoverPanel { Content = new TextBlock { Text = "x" } };
        panel.IsOpen = true;

        // Property set, but no overlay pushed.
        await Assert.That(panel.IsOpen).IsTrue();
    }

    // ─── Imperative API ──────────────────────────────────────────────

    [Test]
    public async Task ShowAt_SetsAnchorContentAndOpens()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var panel = new HoverPanel();
        var tb = new TextBlock { Text = "tooltip" };

        panel.ShowAt(12, 7, tb);

        await Assert.That(ReferenceEquals(panel.Content, tb)).IsTrue();
        await Assert.That(panel.AnchorX).IsEqualTo(12);
        await Assert.That(panel.AnchorY).IsEqualTo(7);
        await Assert.That(panel.IsOpen).IsTrue();
        await Assert.That(app.Overlays.Count).IsEqualTo(1);

        panel.Hide();
        await Assert.That(panel.IsOpen).IsFalse();
        await Assert.That(app.Overlays.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ShowAt_NullContent_Throws()
    {
        var panel = new HoverPanel();
        await Assert.That(() => panel.ShowAt(0, 0, null!))
            .ThrowsExactly<ArgumentNullException>();
    }

    // ─── Events ──────────────────────────────────────────────────────

    [Test]
    public async Task Opened_Event_RaisedWhenOpened()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var panel = new HoverPanel { Content = new TextBlock { Text = "x" } };
        bool opened = false;
        panel.Opened += (_, _) => opened = true;

        panel.IsOpen = true;
        await Assert.That(opened).IsTrue();
        panel.IsOpen = false;
    }

    [Test]
    public async Task Closed_Event_RaisedWhenClosed()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var panel = new HoverPanel { Content = new TextBlock { Text = "x" } };
        bool closed = false;
        panel.Closed += (_, _) => closed = true;

        panel.IsOpen = true;
        panel.IsOpen = false;
        await Assert.That(closed).IsTrue();
    }

    // ─── Positioning (via the internal root) ─────────────────────────

    [Test]
    public async Task Anchor_PlacementBottom_PositionsBelowAnchorCell()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var tb = new TextBlock { Text = "hello" };
        var panel = new HoverPanel
        {
            Content = tb,
            AnchorX = 5,
            AnchorY = 3,
            Placement = PlacementMode.Bottom,
        };
        panel.IsOpen = true;

        var root = (FrameworkElement)app.Overlays[0].Element;
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        // Bottom = anchor.Y + 1 (anchor is a 1×1 cell, so Bottom is Y+1).
        await Assert.That(bounds.Y).IsEqualTo(4);
        await Assert.That(bounds.X).IsEqualTo(5);

        panel.IsOpen = false;
    }

    [Test]
    public async Task Anchor_PlacementBottom_NearViewportBottom_FlipsAbove()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var tb = new TextBlock { Text = "five-line\ntooltip\nwith plenty\nof content\nrows" };
        var panel = new HoverPanel
        {
            Content = tb,
            AnchorX = 5,
            AnchorY = 23, // last row of a 24-row viewport
            Placement = PlacementMode.Bottom,
        };
        panel.IsOpen = true;

        var root = (FrameworkElement)app.Overlays[0].Element;
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        // Bottom-placement would overflow; flip should put us at anchor.Y - height.
        await Assert.That(bounds.Y).IsLessThan(23);

        panel.IsOpen = false;
    }

    [Test]
    public async Task Anchor_ClampsToViewport_NeverNegative()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var tb = new TextBlock { Text = "tooltip" };
        var panel = new HoverPanel
        {
            Content = tb,
            AnchorX = -10,
            AnchorY = -5,
            Placement = PlacementMode.Bottom,
        };
        panel.IsOpen = true;

        var root = (FrameworkElement)app.Overlays[0].Element;
        var bounds = root.CalculateBounds(new Rect(0, 0, 80, 24));
        await Assert.That(bounds.X).IsGreaterThanOrEqualTo(0);
        await Assert.That(bounds.Y).IsGreaterThanOrEqualTo(0);

        panel.IsOpen = false;
    }

    // ─── Any UIElement content ───────────────────────────────────────

    [Test]
    public async Task Content_AcceptsAnyUIElement()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        // Compose a non-trivial content tree: Border around a TextBlock.
        var content = new Border
        {
            Child = new TextBlock { Text = "rich" },
            BorderStyle = BorderStyle.Rounded(Color.White),
        };
        var panel = new HoverPanel { Content = content };
        panel.IsOpen = true;

        await Assert.That(ReferenceEquals(panel.Content, content)).IsTrue();

        panel.IsOpen = false;
    }

    // ─── Escape dismisses without exiting the app ────────────────────

    [Test]
    public async Task Escape_WhileOpen_ClosesPanel_AndDoesNotExitApp()
    {
        // Hover panels are non-modal overlays. The Application's built-in Escape
        // handler closes the topmost MODAL overlay or exits the app — so without
        // the KeyDown interception inside HoverPanel a stray Escape during hover
        // would quit the host. Pin that we close the panel and swallow the key.
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));
        // Follow with a second Escape so the test can't hang if the first one
        // failed to dismiss the hover (the second Escape would then exit cleanly).
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));

        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new Window();

        var panel = new HoverPanel { Content = new TextBlock { Text = "info" } };
        panel.IsOpen = true;

        var closedByEscape = false;
        // After the first Escape we expect the panel to be closed; if so the second
        // Escape exits the loop without re-firing Closed. If the first didn't close
        // it, this would be false.
        panel.Closed += (_, _) =>
        {
            if (panel.IsOpen == false) closedByEscape = true;
        };

        app.Run();

        await Assert.That(closedByEscape).IsTrue();
        await Assert.That(panel.IsOpen).IsFalse();
        await Assert.That(app.Overlays.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Escape_WithModifier_DoesNotDismiss()
    {
        // Only bare Escape dismisses — Shift+Escape (and the other modifiers) pass
        // through so the Application's normal handling runs. Application's built-in
        // Escape check requires HasModifiers: false, so Shift+Escape doesn't exit
        // either — we follow it with a plain Escape to let the loop terminate.
        var backend = new QueuedInputBackend();
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: true, Alt: false, Ctrl: false));
        backend.Enqueue(new KeyEvent(ConsoleKey.Escape, '\x1b', Shift: false, Alt: false, Ctrl: false));

        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });
        app.RootControl = new Window();

        var panel = new HoverPanel { Content = new TextBlock { Text = "info" } };
        panel.IsOpen = true;

        var panelWasOpenAfterShiftedEscape = false;
        app.KeyDown += (e, _) =>
        {
            if (e.Key == ConsoleKey.Escape && e.Shift && panel.IsOpen)
            {
                panelWasOpenAfterShiftedEscape = true;
            }
        };

        app.Run();

        // While Shift+Escape was being delivered the panel was still open
        // (the bare Escape that followed closed it and exited the loop).
        await Assert.That(panelWasOpenAfterShiftedEscape).IsTrue();
    }
}
