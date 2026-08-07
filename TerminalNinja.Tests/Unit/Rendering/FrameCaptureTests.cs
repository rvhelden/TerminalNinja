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

    [Test]
    public async Task ToText_RejectsNonPositiveDimensions()
    {
        await Assert.That(() => FrameCapture.ToText(new TextBlock(), 0, 3))
            .ThrowsExactly<ArgumentOutOfRangeException>();
    }
}
