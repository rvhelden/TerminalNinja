using TerminalNinja.Input;
using TerminalNinja.Terminal;

namespace TerminalNinja.Tests.Unit.Terminal;

/// <summary>
/// Tests for <see cref="KeyEventEncoder.Encode"/> — the KeyEvent → ANSI byte mapping that
/// connects the user's keyboard to the shell's stdin.
/// </summary>
public class KeyEventEncoderTests
{
    private static KeyEvent Key(ConsoleKey k, char ch = '\0', bool shift = false, bool alt = false, bool ctrl = false)
        => new(k, ch, shift, alt, ctrl);

    [Test]
    public async Task Enter_EmitsCarriageReturn()
    {
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.Enter));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x0D });
    }

    [Test]
    public async Task Backspace_EmitsDel()
    {
        // xterm convention: BS key sends DEL (0x7F), not BS (0x08).
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.Backspace));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x7F });
    }

    [Test]
    public async Task Escape_EmitsEsc()
    {
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.Escape));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x1B });
    }

    [Test]
    [Arguments(ConsoleKey.UpArrow, (byte)'A')]
    [Arguments(ConsoleKey.DownArrow, (byte)'B')]
    [Arguments(ConsoleKey.RightArrow, (byte)'C')]
    [Arguments(ConsoleKey.LeftArrow, (byte)'D')]
    public async Task Arrows_EmitCsiSequence(ConsoleKey key, byte finalByte)
    {
        var result = KeyEventEncoder.Encode(Key(key));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x1B, (byte)'[', finalByte });
    }

    [Test]
    public async Task PageUpPageDown_EmitsCsiTilde()
    {
        await Assert.That(KeyEventEncoder.Encode(Key(ConsoleKey.PageUp)))
            .IsEquivalentTo(new byte[] { 0x1B, (byte)'[', (byte)'5', (byte)'~' });
        await Assert.That(KeyEventEncoder.Encode(Key(ConsoleKey.PageDown)))
            .IsEquivalentTo(new byte[] { 0x1B, (byte)'[', (byte)'6', (byte)'~' });
    }

    [Test]
    public async Task F1ThroughF4_EmitSs3Sequence()
    {
        // xterm sends F1-F4 via ESC O P/Q/R/S (SS3 prefix), F5+ via ESC [ N ~.
        await Assert.That(KeyEventEncoder.Encode(Key(ConsoleKey.F1)))
            .IsEquivalentTo(new byte[] { 0x1B, (byte)'O', (byte)'P' });
        await Assert.That(KeyEventEncoder.Encode(Key(ConsoleKey.F4)))
            .IsEquivalentTo(new byte[] { 0x1B, (byte)'O', (byte)'S' });
    }

    [Test]
    public async Task CtrlC_EmitsAsciiControlCode()
    {
        // Ctrl-C → 0x03 (ETX). The shell typically delivers SIGINT.
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.C, '\0', ctrl: true));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x03 });
    }

    [Test]
    public async Task CtrlZ_EmitsAscii0x1A()
    {
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.Z, '\0', ctrl: true));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x1A });
    }

    [Test]
    public async Task ShiftTab_EmitsCsiZ()
    {
        // BackTab — used for reverse focus traversal in most TUIs.
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.Tab, '\t', shift: true));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x1B, (byte)'[', (byte)'Z' });
    }

    [Test]
    public async Task RegularAsciiChar_PassesThrough()
    {
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.A, 'a'));
        await Assert.That(result).IsEquivalentTo(new byte[] { (byte)'a' });
    }

    [Test]
    public async Task AltLetter_EmitsEscapePrefix()
    {
        // xterm: Alt+x → ESC x. Equivalent to the meta-key convention.
        var result = KeyEventEncoder.Encode(Key(ConsoleKey.X, 'x', alt: true));
        await Assert.That(result).IsEquivalentTo(new byte[] { 0x1B, (byte)'x' });
    }

    [Test]
    public async Task UnsupportedKey_ReturnsNull()
    {
        // Modifier-only press, no KeyChar — nothing to send.
        var result = KeyEventEncoder.Encode(new KeyEvent(ConsoleKey.LeftWindows, '\0', false, false, false));
        await Assert.That(result).IsNull();
    }
}
