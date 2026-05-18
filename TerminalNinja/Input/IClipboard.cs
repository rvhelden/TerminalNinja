namespace TerminalNinja.Input;

/// <summary>
/// Abstraction over the platform clipboard. Controls that want copy / paste
/// reach the clipboard via <see cref="App.Application.Clipboard"/> rather than
/// calling platform APIs directly — that keeps <c>TerminalNinja</c> core free
/// of P/Invoke and lets host backends (e.g. <c>TerminalNinja.Skia</c>) wire in
/// a real OS-clipboard implementation.
/// </summary>
public interface IClipboard
{
    /// <summary>Returns the current clipboard text, or <c>null</c> when empty / unavailable.</summary>
    string? GetText();

    /// <summary>Replace the clipboard contents with <paramref name="text"/>.</summary>
    void SetText(string text);
}

/// <summary>
/// Default <see cref="IClipboard"/> for headless / test scenarios. Stores text
/// in a per-instance field with no OS interaction. Production hosts replace
/// this with a platform-bridged implementation (e.g. <c>Sdl3Clipboard</c>).
/// </summary>
public sealed class ProcessClipboard : IClipboard
{
    private string? _text;

    /// <inheritdoc />
    public string? GetText() => _text;

    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }
}
