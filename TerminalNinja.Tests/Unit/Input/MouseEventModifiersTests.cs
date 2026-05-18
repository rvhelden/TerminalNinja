using TerminalNinja.Input;

namespace TerminalNinja.Tests.Unit.Input;

/// <summary>
/// Pins the back-compat surface of <see cref="MouseEvent"/> after adding
/// Shift / Alt / Ctrl. Existing positional 4-arg construction must keep
/// working with all modifiers defaulting to false; the new fields are
/// only set when callers (typically platform input backends) populate them.
/// </summary>
public class MouseEventModifiersTests
{
    [Test]
    public async Task DefaultModifiers_AreAllFalse()
    {
        var e = new MouseEvent(5, 10, MouseButton.Left, MouseAction.Press);
        await Assert.That(e.Shift).IsFalse();
        await Assert.That(e.Alt).IsFalse();
        await Assert.That(e.Ctrl).IsFalse();
        await Assert.That(e.HasModifiers).IsFalse();
    }

    [Test]
    public async Task ExplicitModifiers_PreservedThroughEquality()
    {
        var a = new MouseEvent(1, 2, MouseButton.Left, MouseAction.Move, Shift: true);
        var b = new MouseEvent(1, 2, MouseButton.Left, MouseAction.Move, Shift: true);
        var c = new MouseEvent(1, 2, MouseButton.Left, MouseAction.Move, Shift: false);
        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a).IsNotEqualTo(c);
    }

    [Test]
    public async Task HasModifiers_TrueWhenAnyHeld()
    {
        await Assert.That(new MouseEvent(0, 0, MouseButton.None, MouseAction.Move, Shift: true).HasModifiers).IsTrue();
        await Assert.That(new MouseEvent(0, 0, MouseButton.None, MouseAction.Move, Alt: true).HasModifiers).IsTrue();
        await Assert.That(new MouseEvent(0, 0, MouseButton.None, MouseAction.Move, Ctrl: true).HasModifiers).IsTrue();
    }

    [Test]
    public async Task PositionalFourArgConstructor_StillWorks_NoModifiers()
    {
        // This is the back-compat point: every existing call site uses the
        // four positional args. Defaults must fill in the rest cleanly.
        var e = new MouseEvent(3, 4, MouseButton.Right, MouseAction.Release);
        await Assert.That(e.X).IsEqualTo(3);
        await Assert.That(e.Y).IsEqualTo(4);
        await Assert.That(e.Button).IsEqualTo(MouseButton.Right);
        await Assert.That(e.Action).IsEqualTo(MouseAction.Release);
        await Assert.That(e.HasModifiers).IsFalse();
    }
}
