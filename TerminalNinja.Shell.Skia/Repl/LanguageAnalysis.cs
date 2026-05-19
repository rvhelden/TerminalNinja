using TerminalNinja.Shell.Language.Services;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Skia;

/// <summary>
/// Caches the LSP-shaped derived state the REPL refreshes on every keystroke:
/// diagnostics for the current buffer. Hover is no longer cached — Ctrl+Space
/// queries <see cref="LanguageService.GetHover"/> on demand and shows the
/// result in the shared HoverPanel overlay.
/// </summary>
internal sealed class LanguageAnalysis
{
    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = Array.Empty<Diagnostic>();

    public bool HasDiagnostic => Diagnostics.Count > 0;

    public void Recompute(string text, Position _cursor, IReadOnlyDictionary<string, NValue>? _scope)
    {
        Diagnostics = text.Length == 0
            ? Array.Empty<Diagnostic>()
            : LanguageService.GetDiagnostics(text);
    }

    public void Reset()
    {
        Diagnostics = Array.Empty<Diagnostic>();
    }
}
