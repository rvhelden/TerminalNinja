using TerminalNinja.Rendering;

namespace TerminalNinja.Tests.Unit.Rendering;

public class FrameCaptureTests
{
    [Test]
    public async Task ToText_RendersControlAsLines()
    {
        var text = FrameCapture.ToText(new TextBlock { Text = "Hello" }, 10, 3);

        await Assert.That(text).Contains("Hello");
    }

    [Test]
    public async Task ToText_TrimsTrailingBlankLinesAndPadding()
    {
        var text = FrameCapture.ToText(new TextBlock { Text = "Hi" }, 10, 5);

        await Assert.That(text.EndsWith("Hi")).IsTrue();
    }

    [Test]
    public async Task ToAnsi_EmitsTrueColourSequences()
    {
        var ansi = FrameCapture.ToAnsi(new TextBlock { Text = "X", Foreground = Color.Red }, 3, 1);

        await Assert.That(ansi).Contains("\e[38;2;");
    }

    /// <summary>
    /// TextBlock's plain-text path writes UTF-16 code units, so an astral character lands in the
    /// buffer as two lone surrogates. Capturing such a frame used to throw out of
    /// <c>char.ConvertFromUtf32</c> — no frame at all, because one log line held an emoji.
    /// </summary>
    [Test]
    public async Task ToText_DoesNotThrowOnALoneSurrogate()
    {
        var text = FrameCapture.ToText(new TextBlock { Text = "ok \U0001F680 go" }, 20, 1);

        await Assert.That(text).Contains("ok ");
        await Assert.That(text).Contains(" go");
    }

    [Test]
    public async Task ToAnsi_DoesNotThrowOnALoneSurrogate()
    {
        var ansi = FrameCapture.ToAnsi(new TextBlock { Text = "\U0001F680" }, 4, 1);

        await Assert.That(ansi).Contains("\e[38;2;");
    }

    [Test]
    public async Task ToText_RejectsNonPositiveDimensions()
    {
        await Assert.That(() => FrameCapture.ToText(new TextBlock(), 0, 3))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
