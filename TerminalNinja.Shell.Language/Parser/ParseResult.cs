using System.Collections.Immutable;
using TerminalNinja.Shell.Ast;

namespace TerminalNinja.Shell.Parser;

/// <summary>
/// Outcome of a recovering parse — every top-level form that the parser was
/// able to recognise, plus every diagnostic it collected while syncing past
/// errors. <see cref="HasErrors"/> is the usual gate for "should this be
/// evaluated?".
/// </summary>
public sealed record ParseResult(
    ImmutableArray<Expr> Forms,
    ImmutableArray<ParseDiagnostic> Diagnostics)
{
    /// <summary>True when at least one diagnostic was reported.</summary>
    public bool HasErrors => Diagnostics.Length > 0;
}
