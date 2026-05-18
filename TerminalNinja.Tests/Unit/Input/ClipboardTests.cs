using TerminalNinja.Input;

namespace TerminalNinja.Tests.Unit.Input;

/// <summary>
/// Pins the public contract of <see cref="IClipboard"/> via the default
/// in-process implementation. Real OS-clipboard backends (e.g.
/// <c>Sdl3Clipboard</c>) live in platform projects and are exercised in their
/// own integration tests.
/// </summary>
public class ClipboardTests
{
    [Test]
    public async Task GetText_BeforeAnySet_ReturnsNull()
    {
        IClipboard clipboard = new ProcessClipboard();
        await Assert.That(clipboard.GetText()).IsNull();
    }

    [Test]
    public async Task SetText_RoundTripsThroughGetText()
    {
        IClipboard clipboard = new ProcessClipboard();
        clipboard.SetText("hello");
        await Assert.That(clipboard.GetText()).IsEqualTo("hello");
    }

    [Test]
    public async Task SetText_OverwritesPreviousContents()
    {
        IClipboard clipboard = new ProcessClipboard();
        clipboard.SetText("first");
        clipboard.SetText("second");
        await Assert.That(clipboard.GetText()).IsEqualTo("second");
    }

    [Test]
    public async Task SetText_Null_Throws()
    {
        IClipboard clipboard = new ProcessClipboard();
        await Assert.That(() => clipboard.SetText(null!)).ThrowsExactly<ArgumentNullException>();
    }

    [Test]
    public async Task Application_DefaultClipboard_IsProcessClipboard()
    {
        // Headless host gets the in-process default; the Skia host swaps in
        // Sdl3Clipboard during SkiaApplication.Initialize.
        using var app = new TerminalNinja.App.Application(new TerminalNinja.App.ApplicationOptions { Headless = true });
        await Assert.That(app.Clipboard).IsTypeOf<ProcessClipboard>();
    }
}
