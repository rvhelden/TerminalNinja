using System.Text;
using TerminalNinja.Controls;
using TerminalNinja.Primitives;
using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;
using TerminalNinja.Styling;

namespace NinjaShellUi;

/// <summary>
/// Owns the signature-help overlay. Re-evaluated on every keystroke: opens when the
/// cursor sits inside an open paren that resolves to a known callable, hides otherwise.
/// Uses <see cref="HoverPanel"/> (not <see cref="CompletionPanel"/>) — signature help
/// is a styled single-block tooltip, not a navigable list.
/// </summary>
internal sealed class SignatureHelpController
{
    private static readonly Color BorderColor = new(0x89, 0xB4, 0xFA);

    private readonly HoverPanel _panel = new();
    private SignatureHelp? _active;

    public bool IsOpen => _panel.IsOpen;

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

        var content = new Border
        {
            Child = new TextBlock { Text = sb.ToString(), Padding = new Thickness(1, 0) },
            BorderStyle = BorderStyle.Rounded(BorderColor),
        };

        var anchorY = layout.InputTopY + cursorLine;
        var anchorX = layout.Bounds.X + layout.PromptWidth + cursorCol;
        _panel.Placement = PlacementMode.Top;
        _panel.ShowAt(anchorX, anchorY, content);
    }

    public void Hide()
    {
        _active = null;
        if (_panel.IsOpen) _panel.Hide();
    }
}
