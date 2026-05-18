using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace NinjaShellUi;

/// <summary>
/// Caches the LSP-shaped derived state the REPL refreshes on every keystroke:
/// diagnostics for the current buffer and a hover for the identifier under the
/// cursor. The shell-side facade in front of <see cref="LanguageService"/>.
/// </summary>
internal sealed class LanguageAnalysis
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = Array.Empty<Diagnostic>();
    public Hover? Hover { get; private set; }

    public bool HasDiagnostic => Diagnostics.Count > 0;

    public void Recompute(string text, Position cursor, IReadOnlyDictionary<string, NValue>? scope)
    {
        Diagnostics = text.Length == 0
            ? Array.Empty<Diagnostic>()
            : LanguageService.GetDiagnostics(text);
        Hover = text.Length == 0
            ? null
            : LanguageService.GetHover(text, cursor, scope);
    }

    public void Reset()
    {
        Diagnostics = Array.Empty<Diagnostic>();
        Hover = null;
    }
}
