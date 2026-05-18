namespace TerminalNinja.Shell.Ast;

/// <summary>
/// A 1-based source range — <see cref="StartLine"/> / <see cref="StartColumn"/> mark
/// the first character of the construct (inclusive) and <see cref="EndLine"/> /
/// <see cref="EndColumn"/> the character immediately after the last (exclusive).
/// All four values are 1-based to match the lexer / parser exception positions;
/// the public <see cref="Services.LanguageService"/> converts to 0-based at the
/// boundary for LSP consumers.
/// </summary>
public readonly record struct Span(int StartLine, int StartColumn, int EndLine, int EndColumn)
{
    /// <summary>Sentinel span used for AST nodes that don't have a real source location (e.g. test-constructed nodes).</summary>
    public static readonly Span None = default;

    /// <summary>True when this span is the <see cref="None"/> sentinel.</summary>
    public bool IsNone => StartLine == 0 && StartColumn == 0 && EndLine == 0 && EndColumn == 0;
}
