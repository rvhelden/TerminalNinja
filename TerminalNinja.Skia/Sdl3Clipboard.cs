using System.Runtime.InteropServices;
using TerminalNinja.Input;
using TerminalNinja.Skia.Native;

namespace TerminalNinja.Skia;

/// <summary>
/// SDL3-backed clipboard. Bridges <see cref="IClipboard"/> to the OS clipboard
/// via <c>SDL_GetClipboardText</c> / <c>SDL_SetClipboardText</c>. Wired into
/// <see cref="App.Application.Clipboard"/> by <see cref="SkiaApplication"/>
/// during initialization.
/// </summary>
public sealed class Sdl3Clipboard : IClipboard
{
    /// <inheritdoc />
    public string? GetText()
    {
        var ptr = Sdl3.SDL_GetClipboardText();
        if (ptr == IntPtr.Zero) return null;
        try
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            Sdl3.SDL_free(ptr);
        }
    }

    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Sdl3.SDL_SetClipboardText(text);
    }
}
