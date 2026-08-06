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
        // Sdl3Clipboard during SkiaApplication.Initialize, and a real terminal
        // host gets Osc52Clipboard in the Application constructor.
        using var app = new TerminalNinja.App.Application(new TerminalNinja.App.ApplicationOptions { Headless = true });
        await Assert.That(app.Clipboard).IsTypeOf<ProcessClipboard>();
    }

    [Test]
    public async Task Osc52_SetText_EmitsTheEscapeSequence()
    {
        using var output = new StringWriter();
        IClipboard clipboard = new Osc52Clipboard(output);

        clipboard.SetText("hi");

        // base64("hi") == "aGk="
        await Assert.That(output.ToString()).IsEqualTo("\x1b]52;c;aGk=\x07");
    }

    [Test]
    public async Task Osc52_GetText_ReturnsTheLastSetValue()
    {
        // Reading the OS clipboard back needs an OSC 52 query most emulators refuse;
        // the cache keeps in-app copy/paste round-tripping.
        using var output = new StringWriter();
        IClipboard clipboard = new Osc52Clipboard(output);

        await Assert.That(clipboard.GetText()).IsNull();

        clipboard.SetText("payload");
        await Assert.That(clipboard.GetText()).IsEqualTo("payload");
    }

    [Test]
    public async Task Osc52_SetText_EncodesUtf8()
    {
        using var output = new StringWriter();
        IClipboard clipboard = new Osc52Clipboard(output);

        clipboard.SetText("héllo");

        var payload = output.ToString().Replace("\x1b]52;c;", "").Replace("\x07", "");
        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        await Assert.That(decoded).IsEqualTo("héllo");
    }
}
