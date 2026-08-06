using System.Text;

namespace TerminalNinja.Input;

/// <summary>
/// Clipboard bridged through the terminal emulator with OSC 52: writing emits
/// <c>ESC ] 52 ; c ; base64(text) BEL</c>, asking the terminal itself to set the
/// system clipboard. Needs no external binaries and works over SSH; supported by
/// every modern emulator (kitty, WezTerm, foot, Windows Terminal, alacritty —
/// tmux needs <c>set -g set-clipboard on</c>). Terminals cap the payload they
/// accept (tmux ~74 KB by default); an oversized write is silently ignored by
/// the emulator, never an error here.
/// </summary>
/// <remarks>
/// Reading the OS clipboard back would need an OSC 52 query and a response
/// parsed off stdin — most emulators refuse it for security anyway — so
/// <see cref="GetText"/> returns the last value set by this process, which
/// keeps in-app copy/paste round-tripping.
/// </remarks>
public sealed class Osc52Clipboard(TextWriter? output = null) : IClipboard
{
    private readonly TextWriter _output = output ?? System.Console.Out;

    private string? _lastSet;

    /// <inheritdoc />
    public string? GetText() => _lastSet;

    /// <inheritdoc />
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _lastSet = text;

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
        _output.Write($"\x1b]52;c;{payload}\x07");
        _output.Flush();
    }
}
