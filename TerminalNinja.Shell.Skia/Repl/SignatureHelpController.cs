using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Highlighting;
using TerminalNinja.Input;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Owns the signature-help overlay. Re-evaluated on every keystroke: opens when the
/// cursor sits inside an open paren that resolves to a known callable, hides otherwise.
/// Content is rendered via <see cref="HoverBox"/> — bounded, scrollable, and
/// syntax-highlighted — so a long Documentation payload doesn't blow the panel up
/// to fill the REPL pane.
/// </summary>
internal sealed class SignatureHelpController
{
    private readonly HoverPanel _panel = new();
    private readonly HoverBox _box = new();
    private SignatureHelp? _active;

    public bool IsOpen => _panel.IsOpen;

    public SyntaxTheme Theme
    {
        get => _box.Theme;
        set => _box.Theme = value;
    }

    /// <summary>Forward a key (PgUp/PgDn/Ctrl+up/down) to the panel's hover box.</summary>
    public bool ForwardKey(KeyEvent e) => _panel.IsOpen && _box.HandleKey(e);

    public void Refresh(string text, int cursorLine, int cursorCol, IReadOnlyDictionary<string, NValue>? scope, in ReplLayout layout)
    {
        var sig = text.Length == 0
            ? null
            : LanguageService.GetSignatureHelp(text, new Position(cursorLine, cursorCol), scope);
        _active = sig;

        if (sig is null)
        {
            if (_panel.IsOpen) _panel.Hide();
            return;
        }

        var sb = new StringBuilder();
        sb.Append(sig.Label);
        if (sig.ActiveParameter >= 0 && sig.ActiveParameter < sig.Parameters.Length)
        {
            var p = sig.Parameters[sig.ActiveParameter];
            sb.Append('\n').Append(new string(' ', p.LabelStart)).Append(new string('▔', p.LabelLength));
        }
        if (sig.Documentation is not null)
        {
            sb.Append("\n\n").Append(sig.Documentation);
        }

        _box.Text = sb.ToString();
        _box.Language = "ninja";

        var anchorY = layout.InputTopY + cursorLine;
        var anchorX = layout.Bounds.X + layout.PromptWidth + cursorCol;
        _panel.Placement = PlacementMode.Top;
        _panel.ShowAt(anchorX, anchorY, _box);
    }

    public void Hide()
    {
        _active = null;
        if (_panel.IsOpen) _panel.Hide();
    }
}
