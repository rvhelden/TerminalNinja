using TerminalNinja.App;

namespace TerminalNinja.Tests.Unit.Controls;

/// <summary>
/// Tests for Window modal dialog support:
/// - DialogResult property
/// - IsModal property
/// - ShowDialogAsync / CloseDialog lifecycle
/// - Centering when modal
/// </summary>
[NotInParallel("ApplicationSingleton")]
public class WindowDialogTests
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

    // ─── DialogResult property ───────────────────────────────────────

    [Test]
    public async Task DialogResult_DefaultValue_IsNull()
    {
        var window = new Window();
        await Assert.That(window.DialogResult).IsNull();
    }

    [Test]
    public async Task DialogResult_CanBeSet()
    {
        var window = new Window();
        window.DialogResult = true;
        // Note: when not modal, setting DialogResult doesn't trigger close
        await Assert.That(window.DialogResult).IsTrue();
    }

    // ─── IsModal property ────────────────────────────────────────────

    [Test]
    public async Task IsModal_DefaultValue_IsFalse()
    {
        var window = new Window();
        await Assert.That(window.IsModal).IsFalse();
    }

    // ─── Centering when modal ────────────────────────────────────────

    [Test]
    public async Task CalculateBounds_NonModal_DoesNotCenter()
    {
        var window = new Window
        {
            Width = Size.Absolute(40),
            Height = Size.Absolute(10)
        };
        var parent = new Rect(0, 0, 80, 24);

        var bounds = window.CalculateBounds(parent);

        // Non-modal: starts at parent origin
        await Assert.That(bounds.X).IsEqualTo(0);
        await Assert.That(bounds.Y).IsEqualTo(0);
        await Assert.That(bounds.Width).IsEqualTo(40);
        await Assert.That(bounds.Height).IsEqualTo(10);
    }

    // ─── CloseDialog ─────────────────────────────────────────────────

    [Test]
    public async Task CloseDialog_WhenNotModal_DoesNothing()
    {
        var window = new Window();
        window.CloseDialog();
        // Should not throw
        await Assert.That(window.IsModal).IsFalse();
    }

    // ─── ShowDialogAsync + DialogResult close flow ───────────────────

    [Test]
    public async Task ShowDialogAsync_SetsIsModalTrue()
    {
        // We need an Application instance for ShowDialogAsync
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window
        {
            Width = Size.Absolute(30),
            Height = Size.Absolute(10),
            Content = new TextBlock { Text = "Dialog" }
        };

        var task = dialog.ShowDialogAsync();

        await Assert.That(dialog.IsModal).IsTrue();
        await Assert.That(task.IsCompleted).IsFalse();

        // Cleanup: close the dialog
        dialog.DialogResult = true;
        var result = await task;
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task ShowDialogAsync_PushesOverlayOntoApplication()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window
        {
            Width = Size.Absolute(30),
            Height = Size.Absolute(10)
        };

        var task = dialog.ShowDialogAsync();

        await Assert.That(app.Overlays.Count).IsEqualTo(1);
        await Assert.That(app.Overlays[0].Element).IsEqualTo(dialog);
        await Assert.That(app.Overlays[0].IsModal).IsTrue();
        await Assert.That(app.Overlays[0].DimBackground).IsTrue();

        // Cleanup
        dialog.DialogResult = false;
        await task;
    }

    [Test]
    public async Task ShowDialogAsync_SettingDialogResult_ClosesAndCompletesTask()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window();
        var task = dialog.ShowDialogAsync();

        // Setting DialogResult should close the modal
        dialog.DialogResult = false;

        var result = await task;
        await Assert.That(result).IsFalse();
        await Assert.That(dialog.IsModal).IsFalse();
        await Assert.That(app.Overlays.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ShowDialogAsync_CloseDialog_CompletesWithNull()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window();
        var task = dialog.ShowDialogAsync();

        // Close without setting DialogResult
        dialog.CloseDialog();

        var result = await task;
        await Assert.That(result).IsNull();
        await Assert.That(dialog.IsModal).IsFalse();
    }

    [Test]
    public async Task ShowDialogAsync_ThrowsWhenAlreadyModal()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window();
        var task = dialog.ShowDialogAsync();

        await Assert.That(() => dialog.ShowDialogAsync())
            .ThrowsExactly<InvalidOperationException>();

        // Cleanup
        dialog.CloseDialog();
        await task;
    }

    [Test]
    public async Task ShowDialogAsync_ThrowsWithoutApplication()
    {
        // Ensure no application instance — create a headless one just to dispose it
        // so Current is guaranteed null
        using (var temp = new Application(new ApplicationOptions { Headless = true }))
        {
            // temp is now Current
        }
        // After dispose, Current == null

        var dialog = new Window();

        await Assert.That(() => dialog.ShowDialogAsync())
            .ThrowsExactly<InvalidOperationException>();
    }

    // ─── Modal dialog renders centered ───────────────────────────────

    [Test]
    public async Task ModalDialog_CalculateBounds_CentersInViewport()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window
        {
            Width = Size.Absolute(40),
            Height = Size.Absolute(10)
        };
        var task = dialog.ShowDialogAsync();

        var viewport = new Rect(0, 0, 80, 24);
        var bounds = dialog.CalculateBounds(viewport);

        // Centered: X = (80 - 40) / 2 = 20, Y = (24 - 10) / 2 = 7
        await Assert.That(bounds.X).IsEqualTo(20);
        await Assert.That(bounds.Y).IsEqualTo(7);
        await Assert.That(bounds.Width).IsEqualTo(40);
        await Assert.That(bounds.Height).IsEqualTo(10);

        // Cleanup
        dialog.CloseDialog();
        await task;
    }

    // ─── Close via Window.Close() ────────────────────────────────────

    [Test]
    public async Task Close_WhenModal_ClosesDialog()
    {
        using var app = new Application(new ApplicationOptions { Headless = true });
        app.RootControl = new Window();

        var dialog = new Window();
        var task = dialog.ShowDialogAsync();

        // Close() should detect modal and call CloseDialog()
        dialog.Close();

        var result = await task;
        await Assert.That(dialog.IsModal).IsFalse();
        await Assert.That(result).IsNull();
    }
}
