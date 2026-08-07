using TerminalNinja.App;
using TerminalNinja.Input;
using TerminalNinja.Tests.Helpers;

namespace TerminalNinja.Tests.Unit.App;

/// <summary>
/// A focused text field gets first refusal on printable characters, ahead of the application's own
/// shortcuts.
/// </summary>
/// <remarks>
/// A terminal application binds bare letters, and a bare letter is also what someone types into a
/// field. Before this the two were indistinguishable — typing "query" into a focused box fired
/// whatever "q" was bound to — so an inline input was impossible and every typing surface had to be
/// a modal, purely so the application could tell the difference by knowing a dialog was open.
///
/// The diversion is narrow on purpose, and the tests below pin both halves: characters go to the
/// field, everything else still reaches the application first.
/// </remarks>
[NotInParallel("ApplicationSingleton")]
public class ApplicationTextInputTests
{
    private static Application Headless(out TextBox box, out QueuedInputBackend backend, bool readOnly = false)
    {
        backend = new QueuedInputBackend();

        var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });

        box = new TextBox { IsReadOnly = readOnly };
        app.RootControl = box;
        app.FocusManager.SetFocus(box);
        return app;
    }

    /// <summary>Feeds one key through the real event loop, the way the terminal would.</summary>
    private static void Press(Application app, QueuedInputBackend backend, KeyEvent key)
    {
        backend.Enqueue(key);
        app.ProcessTick();
    }

    private static KeyEvent Char(char c) => new((ConsoleKey)char.ToUpperInvariant(c), c, false, false, false);

    [Test]
    public async Task PrintableKey_GoesToTheFocusedTextBox_NotTheShortcutHandler()
    {
        using var app = Headless(out var box, out var backend);

        var shortcutFired = false;
        app.KeyDown += (_, _) => shortcutFired = true;

        Press(app, backend, Char('q'));

        await Assert.That(box.Text).IsEqualTo("q");
        await Assert.That(shortcutFired).IsFalse();
    }

    [Test]
    public async Task Escape_StillReachesTheShortcutHandler()
    {
        using var app = Headless(out var box, out var backend);

        var seen = false;
        app.KeyDown += (e, _) => seen = e.Key == ConsoleKey.Escape;

        Press(app, backend, new KeyEvent(ConsoleKey.Escape, '\x1b', false, false, false));

        await Assert.That(seen).IsTrue();
        await Assert.That(box.Text).IsEqualTo("");
    }

    [Test]
    public async Task Enter_OnASingleLineBox_StillReachesTheShortcutHandler()
    {
        // This is what lets a one-field dialog commit on Enter while the box holds focus.
        using var app = Headless(out var box, out var backend);

        var seen = false;
        app.KeyDown += (e, _) => seen = e.Key == ConsoleKey.Enter;

        Press(app, backend, new KeyEvent(ConsoleKey.Enter, '\r', false, false, false));

        await Assert.That(seen).IsTrue();
        await Assert.That(box.Text).IsEqualTo("");
    }

    [Test]
    public async Task Arrows_StillReachTheShortcutHandlerFirst()
    {
        // A field must not shadow the application's navigation keys.
        using var app = Headless(out _, out var backend);

        var seen = false;
        app.KeyDown += (e, args) =>
        {
            seen = e.Key == ConsoleKey.RightArrow;
            args.Handled = true;
        };

        Press(app, backend, new KeyEvent(ConsoleKey.RightArrow, '\0', false, false, false));

        await Assert.That(seen).IsTrue();
    }

    [Test]
    public async Task CtrlChords_StillReachTheShortcutHandlerFirst()
    {
        using var app = Headless(out var box, out var backend);

        var seen = false;
        app.KeyDown += (e, args) =>
        {
            seen = e is { Key: ConsoleKey.S, Ctrl: true };
            args.Handled = true;
        };

        Press(app, backend, new KeyEvent(ConsoleKey.S, 's', false, false, true));

        await Assert.That(seen).IsTrue();
        await Assert.That(box.Text).IsEqualTo("");
    }

    [Test]
    public async Task AReadOnlyBox_ClaimsNothing()
    {
        using var app = Headless(out var box, out var backend, readOnly: true);

        var shortcutFired = false;
        app.KeyDown += (_, _) => shortcutFired = true;

        Press(app, backend, Char('q'));

        await Assert.That(box.Text).IsEqualTo("");
        await Assert.That(shortcutFired).IsTrue();
    }

    [Test]
    public async Task WithNoTextBoxFocused_ShortcutsAreUnaffected()
    {
        var backend = new QueuedInputBackend();

        using var app = new Application(new ApplicationOptions
        {
            Headless = true,
            InputBackend = backend,
        });

        app.RootControl = new TextBlock { Text = "no input here" };

        var shortcutFired = false;
        app.KeyDown += (_, _) => shortcutFired = true;

        Press(app, backend, Char('q'));

        await Assert.That(shortcutFired).IsTrue();
    }
}
