using System.Collections.Immutable;
using System.Text.RegularExpressions;
using TerminalNinja.Shell.Ast;
using TerminalNinja.Shell.Lexer;
using TerminalNinja.Shell.Parser;
using TerminalNinja.Shell.Values;

namespace TerminalNinja.Shell.Language.Services;

/// <summary>
/// Tooling-facing analysis surface for NinjaShell source. All members are pure
/// functions of source-and-position — no I/O, no URI tracking, no document
/// lifecycle. Both the LSP server and the in-process REPL consume this directly.
/// </summary>
/// <remarks>
/// Positions are 0-based (line and character), matching the Language Server
/// Protocol. The lexer and parser use 1-based positions for human-readable
/// error messages; this service performs the conversion at the boundary.
/// </remarks>
public static class LanguageService
{
    /// <summary>
    /// Compute the diagnostics for a source string. Currently surfaces lexer and
    /// parser errors as <see cref="DiagnosticSeverity.Error"/> diagnostics. Returns
    /// an empty list when the source parses cleanly.
    /// </summary>
    public static IReadOnlyList<Diagnostic> GetDiagnostics(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = NinjaParser.TryParseScript(source);
        if (result.Diagnostics.Length == 0) return Array.Empty<Diagnostic>();

        var diagnostics = new List<Diagnostic>(result.Diagnostics.Length);
        foreach (var d in result.Diagnostics)
        {
            diagnostics.Add(SpanDiagnostic(d.Line, d.Column, d.Length, d.Message));
        }
        return diagnostics;
    }

    /// <summary>
    /// Convert a 1-based (line, column, length) tuple from the lexer/parser to
    /// a 0-based LSP <see cref="Range"/>. Length clamps to at least 1 so editors
    /// always render at least a one-cell-wide squiggle for a known error.
    /// </summary>
    private static Diagnostic SpanDiagnostic(int line1, int column1, int length, string message)
    {
        int line = Math.Max(line1 - 1, 0);
        int col = Math.Max(column1 - 1, 0);
        int span = Math.Max(length, 1);
        var start = new Position(line, col);
        var end = new Position(line, col + span);
        return new Diagnostic(new Range(start, end), DiagnosticSeverity.Error, message);
    }

    /// <summary>
    /// Compute outline-shaped symbols for a source string. Top-level
    /// <c>let NAME = VALUE</c> statements become Variable (or Function, when the
    /// bound value is a lambda) symbols; top-level <c>source("path")</c> forms
    /// become Module symbols. Source that fails to parse returns an empty list
    /// — the LSP client also gets diagnostics, so we don't surface partial
    /// outlines from a half-parsed tree.
    /// </summary>
    public static IReadOnlyList<DocumentSymbol> GetDocumentSymbols(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var symbols = new List<DocumentSymbol>();
        try
        {
            var forms = NinjaParser.ParseScript(source);
            foreach (var form in forms)
            {
                var sym = TryBuildSymbol(form);
                if (sym is not null) symbols.Add(sym);
            }
        }
        catch (LexerException)  { return Array.Empty<DocumentSymbol>(); }
        catch (ParserException) { return Array.Empty<DocumentSymbol>(); }

        return symbols;
    }

    private static DocumentSymbol? TryBuildSymbol(Expr form) => form switch
    {
        LetStatement ls => new DocumentSymbol(
            Name: ls.Name,
            Detail: ls.Value is Lambda lam ? $"({string.Join(", ", lam.Parameters)}) =>" : null,
            Kind: ls.Value is Lambda ? SymbolKind.Function : SymbolKind.Variable,
            Range: SpanToRange(ls.Span),
            SelectionRange: SpanToRange(ls.Span),
            Children: Array.Empty<DocumentSymbol>()),
        SourceStatement src => new DocumentSymbol(
            Name: BuildSourceName(src),
            Detail: null,
            Kind: SymbolKind.Module,
            Range: SpanToRange(src.Span),
            SelectionRange: SpanToRange(src.Span),
            Children: Array.Empty<DocumentSymbol>()),
        _ => null,
    };

    private static string BuildSourceName(SourceStatement src)
    {
        if (src.Path is Lit { Value: NString s }) return $"source(\"{s.Value}\")";
        return "source(...)";
    }

    private static Range SpanToRange(Span s)
    {
        // 1-based source positions → 0-based LSP positions.
        var start = new Position(Math.Max(s.StartLine - 1, 0), Math.Max(s.StartColumn - 1, 0));
        var end = new Position(Math.Max(s.EndLine - 1, 0), Math.Max(s.EndColumn - 1, 0));
        return new Range(start, end);
    }

    // ─── completions ────────────────────────────────────────────────────────

    private static readonly Regex MemberAccessPattern =
        new(@"([A-Za-z_][A-Za-z0-9_]*)\.([A-Za-z_][A-Za-z0-9_]*)?$", RegexOptions.Compiled);

    private static readonly Regex IdentifierPattern =
        new(@"([A-Za-z_][A-Za-z0-9_]*)$", RegexOptions.Compiled);

    /// <summary>
    /// Produce a list of completions for the given <paramref name="cursor"/> position
    /// in <paramref name="source"/>. Detects two contexts: a member access (the
    /// cursor sits at <c>module.member</c> or <c>module.</c>) → return the named
    /// module's members; otherwise → top-level builtins + keywords filtered by
    /// whatever identifier prefix is to the left of the cursor.
    /// </summary>
    public static IReadOnlyList<CompletionItem> GetCompletions(string source, Position cursor)
        => GetCompletions(source, cursor, scope: null);

    /// <summary>
    /// Scope-aware completion. Same context detection as
    /// <see cref="GetCompletions(string, Position)"/>, plus the keys of
    /// <paramref name="scope"/> surface as <see cref="CompletionKind.Variable"/>
    /// (or <see cref="CompletionKind.Function"/> when the bound value is an
    /// <see cref="NFunc"/>). The detail line shows the value's shape via
    /// <see cref="ValueFormatter.Def"/> so an editor can preview what a name
    /// resolves to before the user picks it.
    /// </summary>
    public static IReadOnlyList<CompletionItem> GetCompletions(
        string source,
        Position cursor,
        IReadOnlyDictionary<string, NValue>? scope)
    {
        ArgumentNullException.ThrowIfNull(source);
        var prefix = TakeSourceBeforeCursor(source, cursor);

        // Interpolation hole — `$"…{cursor here}…"`. Slice the active hole's
        // source and recurse so the user gets normal completion inside the
        // expression. We compute a synthetic Position so member/prefix
        // detection works on just the hole text.
        if (TryExtractActiveInterpolationHole(prefix, out var holeText))
        {
            return GetCompletions(holeText, new Position(0, holeText.Length), scope);
        }

        var memberMatch = MemberAccessPattern.Match(prefix);
        if (memberMatch.Success)
        {
            var targetName = memberMatch.Groups[1].Value;
            var memberPrefix = memberMatch.Groups[2].Success ? memberMatch.Groups[2].Value : string.Empty;
            return GetMemberCompletions(targetName, memberPrefix, scope);
        }

        var identifierMatch = IdentifierPattern.Match(prefix);
        var ident = identifierMatch.Success ? identifierMatch.Groups[1].Value : string.Empty;
        return GetTopLevelCompletions(ident, scope);
    }

    /// <summary>
    /// Detect when the cursor sits inside the body of a <c>$"…{…}…"</c>
    /// interpolation hole. Walks left from end-of-prefix; if we find an
    /// unmatched <c>{</c> that is preceded by an <c>$"</c> opener (with no
    /// closing <c>"</c> in between) then we're inside a hole — return the
    /// hole's source so the caller can recurse into completion on it.
    /// </summary>
    private static bool TryExtractActiveInterpolationHole(string prefix, out string holeText)
    {
        holeText = string.Empty;
        // Walk left looking for `{`. Count nested braces so `{{` and `{` inside
        // sub-expressions don't trip us up.
        int depth = 0;
        for (int i = prefix.Length - 1; i >= 0; i--)
        {
            char c = prefix[i];
            if (c == '}') { depth++; continue; }
            if (c == '{')
            {
                if (depth > 0) { depth--; continue; }
                // Unmatched `{` — check whether it's an interpolation hole.
                // The character preceding `{` must be inside a `$"..."` literal
                // with no `"` between the `$"` and here. Heuristic: scan back
                // for the nearest `"`; if we find `$"` we're in a hole.
                int j = i - 1;
                while (j >= 0)
                {
                    if (prefix[j] == '"')
                    {
                        // Found a quote — check for `$` immediately before.
                        if (j > 0 && prefix[j - 1] == '$')
                        {
                            holeText = prefix.Substring(i + 1);
                            return true;
                        }
                        return false;       // bare `"…{` is malformed but not a hole
                    }
                    j--;
                }
                return false;
            }
        }
        return false;
    }

    private static IReadOnlyList<CompletionItem> GetMemberCompletions(
        string targetName,
        string prefix,
        IReadOnlyDictionary<string, NValue>? scope)
    {
        // Builtin module member access takes priority — `fs.<TAB>` always means
        // "members of the fs module" even if a local also happens to be named fs.
        if (BuiltinCatalog.Modules.TryGetValue(targetName, out var members))
        {
            var result = new List<CompletionItem>();
            foreach (var d in members)
            {
                if (prefix.Length == 0 || d.Name.StartsWith(prefix, StringComparison.Ordinal))
                    result.Add(new CompletionItem(d.Name, d.Kind, d.Detail, null, d.Documentation));
            }
            return result;
        }

        // Scope fallback — `userRec.<TAB>` returns the record's field keys.
        // Other scope value types (lists, scalars) have no statically-completable
        // members; return empty.
        if (scope is not null && scope.TryGetValue(targetName, out var v) && v is NRecord rec)
        {
            var result = new List<CompletionItem>();
            foreach (var key in rec.Fields.Keys)
            {
                if (prefix.Length > 0 && !key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var fieldValue = rec.Fields[key];
                var detail = ValueFormatter.Def(fieldValue);
                var doc = $"{targetName}.{key} :: {ValueFormatter.TypeName(fieldValue)}\n\nshape: {detail}\ndata:  {ValueFormatter.Dump(fieldValue)}";
                result.Add(new CompletionItem(key, CompletionKind.Field, detail, null, doc));
            }
            return result;
        }

        return Array.Empty<CompletionItem>();
    }

    private static IReadOnlyList<CompletionItem> GetTopLevelCompletions(
        string prefix,
        IReadOnlyDictionary<string, NValue>? scope)
    {
        var result = new List<CompletionItem>();

        // Scope first — user-defined names shadow builtins in the evaluator, so they
        // should appear first in the completion list too. We dedupe by label below
        // when adding builtins so a `let where = ...` rebinding doesn't show twice.
        var scopeNames = scope is null ? null : new HashSet<string>(StringComparer.Ordinal);
        if (scope is not null)
        {
            foreach (var kv in scope)
            {
                if (prefix.Length > 0 && !kv.Key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                scopeNames!.Add(kv.Key);
                var kind = kv.Value is NFunc ? CompletionKind.Function : CompletionKind.Variable;
                var detail = ValueFormatter.Def(kv.Value);
                // Documentation carries the value preview — "shape:" and "data:" lines
                // so the details pane shows what the binding actually resolves to.
                var doc = $"{kv.Key} :: {ValueFormatter.TypeName(kv.Value)}\n\nshape: {detail}\ndata:  {ValueFormatter.Dump(kv.Value)}";
                result.Add(new CompletionItem(kv.Key, kind, detail, null, doc));
            }
        }

        AddMatches(BuiltinCatalog.TopLevel, prefix, result, scopeNames);
        AddMatches(BuiltinCatalog.Keywords, prefix, result, scopeNames);
        // Module names themselves (env, fs, proc, obj, json, xml) as Module items.
        foreach (var key in BuiltinCatalog.Modules.Keys)
        {
            if (prefix.Length > 0 && !key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (scopeNames is not null && scopeNames.Contains(key)) continue;
            var members = BuiltinCatalog.Modules[key];
            var moduleDoc = $"module {key}\n\nmembers: {string.Join(", ", members.Select(m => m.Name))}";
            result.Add(new CompletionItem(key, CompletionKind.Module, $"module {key}", null, moduleDoc));
        }
        return result;
    }

    private static void AddMatches(
        IReadOnlyList<BuiltinDescriptor> source,
        string prefix,
        List<CompletionItem> dest,
        HashSet<string>? shadowed)
    {
        foreach (var d in source)
        {
            if (shadowed is not null && shadowed.Contains(d.Name)) continue;
            if (prefix.Length == 0 || d.Name.StartsWith(prefix, StringComparison.Ordinal))
                dest.Add(new CompletionItem(d.Name, d.Kind, d.Detail, null, d.Documentation));
        }
    }

    // ─── signature help ─────────────────────────────────────────────────────

    /// <summary>
    /// Resolve the call site that the cursor is currently inside (an unmatched
    /// <c>(</c> to the left of the cursor) and return its signature with the
    /// active parameter index. Returns <see langword="null"/> when the cursor
    /// isn't inside any call, or when the callable can't be resolved through
    /// <see cref="BuiltinCatalog"/> or <paramref name="scope"/>.
    /// </summary>
    public static SignatureHelp? GetSignatureHelp(string source, Position cursor)
        => GetSignatureHelp(source, cursor, scope: null);

    /// <inheritdoc cref="GetSignatureHelp(string, Position)"/>
    public static SignatureHelp? GetSignatureHelp(
        string source,
        Position cursor,
        IReadOnlyDictionary<string, NValue>? scope)
    {
        ArgumentNullException.ThrowIfNull(source);
        var prefix = TakeSourceBeforeCursor(source, cursor);
        if (!TryFindEnclosingCall(prefix, out var callableEnd, out var openParenIndex, out var activeParam))
            return null;

        var (targetName, memberName) = ExtractCallTarget(prefix, callableEnd);
        if (targetName is null) return null;

        // Resolve the descriptor: module.member, top-level builtin, or scope NFunc.
        BuiltinDescriptor? descriptor = null;
        if (memberName is not null && BuiltinCatalog.Modules.TryGetValue(targetName, out var members))
        {
            foreach (var m in members)
            {
                if (m.Name == memberName) { descriptor = m; break; }
            }
        }
        else if (memberName is null)
        {
            foreach (var d in BuiltinCatalog.TopLevel)
            {
                if (d.Name == targetName) { descriptor = d; break; }
            }
        }

        if (descriptor is not null)
        {
            return BuildSignatureFromDetail(descriptor.Detail, descriptor.Documentation, activeParam);
        }

        // Scope fallback — user-defined NFunc. We only know arity, not parameter
        // names, so build a synthetic "name(arg0, arg1, ...)" label.
        if (memberName is null && scope is not null
            && scope.TryGetValue(targetName, out var v) && v is NFunc f)
        {
            return BuildScopedNFuncSignature(targetName, f.Arity, activeParam);
        }

        return null;
    }

    /// <summary>
    /// Walk left from end-of-string balancing brackets / strings to find an
    /// unmatched <c>(</c>. Returns the index of that paren, the index just
    /// before it (where the callable's last char sits), and the active
    /// parameter index (count of top-level commas between the paren and the
    /// end of <paramref name="prefix"/>).
    /// </summary>
    private static bool TryFindEnclosingCall(
        string prefix,
        out int callableEndExclusive,
        out int openParenIndex,
        out int activeParameter)
    {
        callableEndExclusive = 0;
        openParenIndex = -1;
        activeParameter = 0;

        int depthParen = 0, depthBracket = 0, depthBrace = 0;
        int commaCount = 0;
        // Track the comma count *at the level where we'll exit* by remembering
        // the running comma tally per nested-paren-depth and resetting it on
        // ascent. Simpler: count commas only when depthParen==0 relative to the
        // search target paren — i.e., on the path down.
        var commaStack = new Stack<int>();
        commaStack.Push(0);

        bool inString = false;
        char stringDelim = '"';
        for (int i = prefix.Length - 1; i >= 0; i--)
        {
            char c = prefix[i];
            if (inString)
            {
                // Walking backwards: end-of-string boundary is the matching opening quote.
                // We don't try to interpret backslash escapes from the right (too tricky)
                // — close-enough heuristic: any quote toggles the flag.
                if (c == stringDelim) inString = false;
                continue;
            }
            switch (c)
            {
                case '"':
                    inString = true; stringDelim = '"';
                    break;
                case ')':
                    depthParen++;
                    commaStack.Push(0);
                    break;
                case '(':
                    if (depthParen == 0)
                    {
                        // Found the unmatched (. Active param = comma count at this level.
                        openParenIndex = i;
                        callableEndExclusive = i;
                        activeParameter = commaStack.Peek();
                        return true;
                    }
                    depthParen--;
                    if (commaStack.Count > 1) commaStack.Pop();
                    break;
                case ']':
                    depthBracket++;
                    break;
                case '[':
                    if (depthBracket > 0) depthBracket--;
                    break;
                case '}':
                    depthBrace++;
                    break;
                case '{':
                    if (depthBrace > 0) depthBrace--;
                    break;
                case ',':
                    if (depthParen == 0 && depthBracket == 0 && depthBrace == 0)
                    {
                        int top = commaStack.Pop();
                        commaStack.Push(top + 1);
                    }
                    break;
            }
        }

        _ = commaCount;
        return false;
    }

    /// <summary>
    /// Read the callable expression to the left of <paramref name="callableEndExclusive"/>:
    /// a bare identifier <c>foo</c> or a member access <c>module.member</c>.
    /// Returns <c>(targetName, null)</c> for bare and <c>(targetName, memberName)</c> for
    /// member access. Whitespace between identifier and <c>(</c> is allowed.
    /// </summary>
    private static (string? Target, string? Member) ExtractCallTarget(string prefix, int callableEndExclusive)
    {
        int i = callableEndExclusive - 1;
        while (i >= 0 && (prefix[i] == ' ' || prefix[i] == '\t')) i--;
        if (i < 0) return (null, null);

        int wordEnd = i + 1;
        while (i >= 0 && IsIdentifierChar(prefix[i])) i--;
        if (wordEnd - (i + 1) <= 0) return (null, null);
        string first = prefix.Substring(i + 1, wordEnd - (i + 1));

        if (i >= 0 && prefix[i] == '.')
        {
            int dotPos = i;
            i--;
            int outerEnd = dotPos;
            while (i >= 0 && IsIdentifierChar(prefix[i])) i--;
            if (outerEnd - (i + 1) <= 0) return (first, null);
            string outer = prefix.Substring(i + 1, outerEnd - (i + 1));
            return (outer, first);
        }
        return (first, null);
    }

    /// <summary>
    /// Parse a builtin's <c>name(arg, arg, …)</c> Detail string into a
    /// <see cref="SignatureHelp"/>, splitting on top-level commas to locate
    /// each parameter's substring range inside the label.
    /// </summary>
    private static SignatureHelp BuildSignatureFromDetail(string detail, string? documentation, int activeParam)
    {
        int open = detail.IndexOf('(');
        int close = detail.LastIndexOf(')');
        if (open < 0 || close <= open)
        {
            return new SignatureHelp(detail, ImmutableArray<SignatureParameter>.Empty, 0, documentation);
        }
        string between = detail.Substring(open + 1, close - open - 1);
        var ranges = SplitTopLevelCommas(between, open + 1);
        var b = ImmutableArray.CreateBuilder<SignatureParameter>(ranges.Count);
        foreach (var r in ranges)
        {
            var label = detail.Substring(r.Start, r.Length);
            b.Add(new SignatureParameter(label, r.Start, r.Length, null));
        }
        // Don't clamp ActiveParameter to Count-1 — when the user types past the
        // declared arity, renderers should show no highlight (or "extra argument")
        // rather than pin to the last param and mislead.
        return new SignatureHelp(detail, b.ToImmutable(), activeParam, documentation);
    }

    /// <summary>Synthesise a label and parameter list for a scope-bound NFunc whose param names aren't known.</summary>
    private static SignatureHelp BuildScopedNFuncSignature(string name, int arity, int activeParam)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(name).Append('(');
        var b = ImmutableArray.CreateBuilder<SignatureParameter>(arity);
        for (int i = 0; i < arity; i++)
        {
            if (i > 0) sb.Append(", ");
            int start = sb.Length;
            string p = $"arg{i}";
            sb.Append(p);
            b.Add(new SignatureParameter(p, start, p.Length, null));
        }
        sb.Append(')');
        return new SignatureHelp(sb.ToString(), b.ToImmutable(), activeParam, $"user-defined function ({arity} arg{(arity == 1 ? "" : "s")})");
    }

    /// <summary>
    /// Split <paramref name="between"/> on top-level commas, returning each
    /// piece's substring range relative to the original Detail string
    /// (i.e. offset by <paramref name="absoluteStart"/>). Top-level means
    /// outside <c>()</c>, <c>[]</c>, <c>{}</c>, and string literals.
    /// </summary>
    private static List<(int Start, int Length)> SplitTopLevelCommas(string between, int absoluteStart)
    {
        var ranges = new List<(int Start, int Length)>();
        if (between.Length == 0) return ranges;
        int depthParen = 0, depthBracket = 0, depthBrace = 0;
        bool inString = false;
        char stringDelim = '"';
        int segStart = 0;
        int i = 0;
        // Trim leading whitespace on the first segment.
        while (i < between.Length && (between[i] == ' ' || between[i] == '\t')) { i++; segStart = i; }
        for (; i < between.Length; i++)
        {
            char c = between[i];
            if (inString)
            {
                if (c == '\\' && i + 1 < between.Length) { i++; continue; }
                if (c == stringDelim) inString = false;
                continue;
            }
            switch (c)
            {
                case '"':
                    inString = true; stringDelim = '"'; break;
                case '(': depthParen++; break;
                case ')': if (depthParen > 0) depthParen--; break;
                case '[': depthBracket++; break;
                case ']': if (depthBracket > 0) depthBracket--; break;
                case '{': depthBrace++; break;
                case '}': if (depthBrace > 0) depthBrace--; break;
                case ',' when depthParen == 0 && depthBracket == 0 && depthBrace == 0:
                    {
                        int end = i;
                        // Trim trailing whitespace.
                        while (end > segStart && (between[end - 1] == ' ' || between[end - 1] == '\t')) end--;
                        ranges.Add((absoluteStart + segStart, end - segStart));
                        int next = i + 1;
                        while (next < between.Length && (between[next] == ' ' || between[next] == '\t')) next++;
                        segStart = next;
                        i = next - 1; // for-loop will ++
                        break;
                    }
            }
        }
        // Tail segment.
        int tailEnd = between.Length;
        while (tailEnd > segStart && (between[tailEnd - 1] == ' ' || between[tailEnd - 1] == '\t')) tailEnd--;
        if (tailEnd > segStart) ranges.Add((absoluteStart + segStart, tailEnd - segStart));
        return ranges;
    }

    // ─── hover ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the identifier or <c>module.member</c> path under the cursor to its
    /// <see cref="BuiltinCatalog"/> entry and returns its signature/detail. Returns
    /// <see langword="null"/> when the cursor isn't sitting on an identifier or the
    /// identifier doesn't match anything in the catalog (user-defined names get no
    /// hover info from this overload — call <see cref="GetHover(string, Position, IReadOnlyDictionary{string, NValue}?)"/>
    /// with a scope dictionary to get shape + data for live bindings).
    /// </summary>
    public static Hover? GetHover(string source, Position cursor)
        => GetHover(source, cursor, scope: null);

    /// <summary>
    /// Like <see cref="GetHover(string, Position)"/>, but enriches the result for
    /// identifiers found in <paramref name="scope"/>. When the identifier under
    /// the cursor matches a scope entry the hover gains <c>shape:</c> and <c>data:</c>
    /// lines computed via <see cref="ValueFormatter.Def"/> and <see cref="ValueFormatter.Dump"/>.
    /// Scope takes precedence over the static catalog when both match — what the
    /// evaluator would actually use is what we describe.
    /// </summary>
    public static Hover? GetHover(string source, Position cursor, IReadOnlyDictionary<string, NValue>? scope)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!TryFindWordAtCursor(source, cursor, out var startCol, out var endCol, out var word))
        {
            return null;
        }

        var range = RangeOnLine(cursor.Line, startCol, endCol);
        bool isMemberAccess = startCol > 0 && GetCharAt(source, cursor.Line, startCol - 1) == '.';

        // Look back further: if there's a `.` immediately before this word, treat it as
        // member access and walk back one more identifier to get the module name.
        if (isMemberAccess)
        {
            if (TryReadIdentifierEndingAt(source, cursor.Line, startCol - 1, out var modStart, out var modName))
            {
                if (BuiltinCatalog.Modules.TryGetValue(modName, out var members))
                {
                    foreach (var d in members)
                    {
                        if (d.Name == word)
                        {
                            return Make(d, range);
                        }
                    }
                }
                _ = modStart;
            }
        }

        // Scope wins over the static catalog for bare identifiers — whatever the
        // evaluator would actually use is what we describe.
        if (!isMemberAccess && scope is not null && scope.TryGetValue(word, out var liveValue))
        {
            return new Hover(BuildScopedHoverContents(word, liveValue, staticEntry: FindStatic(word)), range);
        }

        foreach (var d in BuiltinCatalog.TopLevel)
        {
            if (d.Name == word) return Make(d, range);
        }
        foreach (var d in BuiltinCatalog.Keywords)
        {
            if (d.Name == word) return Make(d, range);
        }
        if (BuiltinCatalog.Modules.TryGetValue(word, out _))
        {
            var members = BuiltinCatalog.Modules[word];
            var summary = $"module {word}\n\nmembers: {string.Join(", ", members.Select(m => m.Name))}";
            return new Hover(summary, range);
        }

        return null;

        static Hover Make(BuiltinDescriptor d, Range r) => new($"{d.Name} — {d.Detail}", r);

        static BuiltinDescriptor? FindStatic(string word)
        {
            foreach (var d in BuiltinCatalog.TopLevel)
                if (d.Name == word) return d;
            foreach (var d in BuiltinCatalog.Keywords)
                if (d.Name == word) return d;
            return null;
        }
    }

    /// <summary>
    /// Build the multi-section hover string for a name resolved through scope.
    /// Top line is either the static signature (if the name is also a known
    /// builtin) or just <c>name :: type</c>; followed by <c>shape:</c> and
    /// <c>data:</c> lines from <see cref="ValueFormatter"/>.
    /// </summary>
    private static string BuildScopedHoverContents(string name, NValue value, BuiltinDescriptor? staticEntry)
    {
        var sb = new System.Text.StringBuilder();
        if (staticEntry is not null)
        {
            sb.Append(staticEntry.Name).Append(" — ").AppendLine(staticEntry.Detail);
        }
        else
        {
            sb.Append(name).Append(" :: ").AppendLine(ValueFormatter.TypeName(value));
        }
        sb.AppendLine();
        sb.Append("shape: ").AppendLine(ValueFormatter.Def(value));
        sb.Append("data:  ").Append(ValueFormatter.Dump(value));
        return sb.ToString();
    }

    /// <summary>
    /// Finds the identifier the cursor sits in or immediately to the right of. Returns
    /// the [start, end) column range (0-based) and the identifier text. Mirrors how
    /// editors typically pick a hover target — if the cursor is at the end of a word,
    /// we still want that word.
    /// </summary>
    private static bool TryFindWordAtCursor(string source, Position cursor, out int startCol, out int endCol, out string word)
    {
        startCol = 0;
        endCol = 0;
        word = string.Empty;
        if (cursor.Line < 0 || cursor.Character < 0) return false;

        var line = GetLine(source, cursor.Line);
        if (line is null) return false;

        var col = Math.Min(cursor.Character, line.Length);

        // Walk left to find the start of an identifier we're sitting in.
        var s = col;
        while (s > 0 && IsIdentifierChar(line[s - 1])) s--;
        // Walk right from `s` to find the end of the identifier.
        var e = s;
        while (e < line.Length && IsIdentifierChar(line[e])) e++;
        if (s == e) return false;

        // Reject pure-numeric tokens — those aren't named symbols.
        if (line[s] >= '0' && line[s] <= '9') return false;

        startCol = s;
        endCol = e;
        word = line.Substring(s, e - s);
        return true;
    }

    private static bool TryReadIdentifierEndingAt(string source, int line, int beforeCol, out int startCol, out string ident)
    {
        startCol = 0;
        ident = string.Empty;
        var lineText = GetLine(source, line);
        if (lineText is null) return false;

        var e = beforeCol;
        if (e <= 0 || e > lineText.Length) return false;
        var s = e;
        while (s > 0 && IsIdentifierChar(lineText[s - 1])) s--;
        if (s == e) return false;
        if (lineText[s] >= '0' && lineText[s] <= '9') return false;

        startCol = s;
        ident = lineText.Substring(s, e - s);
        return true;
    }

    private static char GetCharAt(string source, int line, int col)
    {
        var text = GetLine(source, line);
        if (text is null || col < 0 || col >= text.Length) return '\0';
        return text[col];
    }

    private static string? GetLine(string source, int lineIndex)
    {
        if (lineIndex < 0) return null;
        int start = 0;
        int line = 0;
        while (line < lineIndex && start < source.Length)
        {
            if (source[start] == '\n') line++;
            start++;
        }
        if (line < lineIndex) return null;
        var end = start;
        while (end < source.Length && source[end] != '\n') end++;
        // Drop a trailing '\r' so CRLF line endings don't leak into the returned text.
        if (end > start && source[end - 1] == '\r') end--;
        return source.Substring(start, end - start);
    }

    private static bool IsIdentifierChar(char c)
        => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';

    private static Range RangeOnLine(int line, int startCol, int endCol)
        => new(new Position(line, startCol), new Position(line, endCol));

    /// <summary>
    /// Take source up to (but not past) the given 0-based <paramref name="cursor"/>
    /// position. If the cursor is past end-of-line, clamps to end-of-line. If
    /// the cursor is past end-of-file, clamps to end-of-file.
    /// </summary>
    internal static string TakeSourceBeforeCursor(string source, Position cursor)
    {
        if (cursor.Line < 0 || cursor.Character < 0) return string.Empty;

        int offset = 0;
        int line = 0;
        while (line < cursor.Line && offset < source.Length)
        {
            if (source[offset] == '\n') line++;
            offset++;
        }
        int end = offset;
        int taken = 0;
        while (end < source.Length && source[end] != '\n' && taken < cursor.Character)
        {
            end++;
            taken++;
        }
        return source.Substring(0, end);
    }
}
