using TerminalNinja.Input;
using TerminalNinja.Skia;
using TerminalNinja.Skia.Native;

namespace TerminalNinja.Tests.Unit.Skia;

/// <summary>
/// Tests for <see cref="SdlInputBackend"/>'s keycode / mouse-button mapping. We cannot
/// drive <c>SDL_PollEvent</c> without a running SDL instance, so the tests focus on the
/// static conversion logic exposed via internals.
/// </summary>
public class SdlInputBackendTests
{
    [Test]
    [Arguments(Sdl3.SDLK_RETURN, ConsoleKey.Enter)]
    [Arguments(Sdl3.SDLK_ESCAPE, ConsoleKey.Escape)]
    [Arguments(Sdl3.SDLK_BACKSPACE, ConsoleKey.Backspace)]
    [Arguments(Sdl3.SDLK_TAB, ConsoleKey.Tab)]
    [Arguments(Sdl3.SDLK_SPACE, ConsoleKey.Spacebar)]
    [Arguments(Sdl3.SDLK_LEFT, ConsoleKey.LeftArrow)]
    [Arguments(Sdl3.SDLK_RIGHT, ConsoleKey.RightArrow)]
    [Arguments(Sdl3.SDLK_UP, ConsoleKey.UpArrow)]
    [Arguments(Sdl3.SDLK_DOWN, ConsoleKey.DownArrow)]
    [Arguments(Sdl3.SDLK_HOME, ConsoleKey.Home)]
    [Arguments(Sdl3.SDLK_END, ConsoleKey.End)]
    [Arguments(Sdl3.SDLK_PAGEUP, ConsoleKey.PageUp)]
    [Arguments(Sdl3.SDLK_PAGEDOWN, ConsoleKey.PageDown)]
    [Arguments(Sdl3.SDLK_F1, ConsoleKey.F1)]
    [Arguments(Sdl3.SDLK_F12, ConsoleKey.F12)]
    [Arguments(0x61u /* 'a' */, ConsoleKey.A)]
    [Arguments(0x7Au /* 'z' */, ConsoleKey.Z)]
    [Arguments(0x30u /* '0' */, ConsoleKey.D0)]
    [Arguments(0x39u /* '9' */, ConsoleKey.D9)]
    public async Task MapKeycode_KnownKeys_MappedToConsoleKey(uint sdlKey, ConsoleKey expected)
    {
        await Assert.That(SdlInputBackend.MapKeycode(sdlKey)).IsEqualTo(expected);
    }

    [Test]
    public async Task MapKeycode_UnknownKey_ReturnsNoName()
    {
        await Assert.That(SdlInputBackend.MapKeycode(0xDEADBEEFu)).IsEqualTo(ConsoleKey.NoName);
    }

    [Test]
    [Arguments(Sdl3.SDL_BUTTON_LEFT, MouseButton.Left)]
    [Arguments(Sdl3.SDL_BUTTON_MIDDLE, MouseButton.Middle)]
    [Arguments(Sdl3.SDL_BUTTON_RIGHT, MouseButton.Right)]
    [Arguments((byte)42, MouseButton.None)]
    public async Task MapButton_AllSupported_Map(byte sdlButton, MouseButton expected)
    {
        await Assert.That(SdlInputBackend.MapButton(sdlButton)).IsEqualTo(expected);
    }

    [Test]
    public async Task Construction_RejectsNonPositiveCellMetrics()
    {
        await Assert.That(() => new SdlInputBackend(0, 18)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => new SdlInputBackend(9, -1)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task QuitRequested_StartsFalse()
    {
        var backend = new SdlInputBackend(9, 18);
        await Assert.That(backend.QuitRequested).IsFalse();
    }

    [Test]
    public async Task EnableDisableMouseTracking_DoesNotThrow()
    {
        var backend = new SdlInputBackend(9, 18);
        backend.DisableMouseTracking();
        backend.EnableMouseTracking();
        backend.Dispose();
        // Disposed backend rejects further calls.
        await Assert.That(() => backend.DisableMouseTracking()).ThrowsExactly<ObjectDisposedException>();
    }
}
