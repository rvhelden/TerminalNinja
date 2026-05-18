namespace TerminalNinja.Shell.Language.Services;

/// <summary>A 0-based source position (matches the Language Server Protocol).</summary>
public sealed record Position(int Line, int Character);

/// <summary>A half-open source range from <see cref="Start"/> (inclusive) to <see cref="End"/> (exclusive).</summary>
public sealed record Range(Position Start, Position End);

/// <summary>Severity values match the LSP integer codes so they can be serialised directly.</summary>
public enum DiagnosticSeverity
{
    /// <summary>A hard error — the source cannot be evaluated as-is.</summary>
    Error = 1,
    /// <summary>Likely a problem but the source still parses / runs.</summary>
    Warning = 2,
    /// <summary>Informational note.</summary>
    Information = 3,
    /// <summary>A subtle suggestion the editor may render very lightly.</summary>
    Hint = 4,
}

/// <summary>A single analysis result attached to a <see cref="Range"/> of source text.</summary>
public sealed record Diagnostic(Range Range, DiagnosticSeverity Severity, string Message);
