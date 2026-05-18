using System.Collections.Immutable;

namespace TerminalNinja.Shell.Language.Services;

/// <summary>
/// Signature-help payload — the parameter hint that pops up while the user is
/// typing arguments inside an open <c>(</c>. Mirrors the LSP shape so the
/// language server can serialise it directly.
/// </summary>
public sealed record SignatureHelp(
    string Label,
    ImmutableArray<SignatureParameter> Parameters,
    int ActiveParameter,
    string? Documentation);

/// <summary>
/// One parameter inside a <see cref="SignatureHelp.Label"/>, identified by its
/// substring range inside the label so the renderer can highlight the active
/// one.
/// </summary>
public sealed record SignatureParameter(
    string Label,
    int LabelStart,
    int LabelLength,
    string? Documentation);
