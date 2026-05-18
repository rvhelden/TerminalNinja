using System.Collections.Immutable;

namespace TerminalNinja.Shell.Language.Services;

/// <summary>
/// Static metadata about the built-in functions, modules, and keywords that
/// completions / hovers / signature-help draw from. Hand-maintained: when
/// <c>TerminalNinja.Shell.Builtins.BuiltinRegistry</c> grows a new entry, this
/// catalog must be updated to match. (The Language library can't depend on the
/// Shell project, so we accept the maintenance cost in exchange for keeping
/// the LSP server free of runtime dependencies.)
/// </summary>
internal static class BuiltinCatalog
{
    /// <summary>Top-level builtins that resolve as bare identifiers.</summary>
    public static readonly ImmutableArray<BuiltinDescriptor> TopLevel = ImmutableArray.Create(
        new BuiltinDescriptor("where", "where(seq, predicate)", CompletionKind.Function),
        new BuiltinDescriptor("select", "select(seq, projection)", CompletionKind.Function),
        new BuiltinDescriptor("each", "each(seq, action)", CompletionKind.Function),
        new BuiltinDescriptor("fold", "fold(seq, init, (acc, x) => ...)", CompletionKind.Function),
        new BuiltinDescriptor("take", "take(seq, n)", CompletionKind.Function),
        new BuiltinDescriptor("skip", "skip(seq, n)", CompletionKind.Function),
        new BuiltinDescriptor("count", "count(seq) -> int", CompletionKind.Function),
        new BuiltinDescriptor("sort", "sort(seq[, { by, desc }])", CompletionKind.Function),
        new BuiltinDescriptor("reverse", "reverse(seq) -> list", CompletionKind.Function),
        new BuiltinDescriptor("distinct", "distinct(seq) -> list", CompletionKind.Function),
        new BuiltinDescriptor("head", "head(seq)", CompletionKind.Function),
        new BuiltinDescriptor("tail", "tail(seq) -> list", CompletionKind.Function),
        new BuiltinDescriptor("materialize", "materialize(seq) -> list", CompletionKind.Function),
        new BuiltinDescriptor("print", "print(v)", CompletionKind.Function),
        new BuiltinDescriptor("println", "println(v)", CompletionKind.Function),
        new BuiltinDescriptor("format_table", "format_table(list_of_records) -> string", CompletionKind.Function));

    /// <summary>Modules and their members, accessed via <c>module.member</c>.</summary>
    public static readonly ImmutableDictionary<string, ImmutableArray<BuiltinDescriptor>> Modules =
        new Dictionary<string, ImmutableArray<BuiltinDescriptor>>(StringComparer.Ordinal)
        {
            ["env"] = ImmutableArray.Create(
                new BuiltinDescriptor("all", "env.all() -> record", CompletionKind.Function),
                new BuiltinDescriptor("get", "env.get(name[, default])", CompletionKind.Function),
                new BuiltinDescriptor("set", "env.set(name, value) | env.set(record)", CompletionKind.Function),
                new BuiltinDescriptor("unset", "env.unset(name)", CompletionKind.Function),
                new BuiltinDescriptor("has", "env.has(name) -> bool", CompletionKind.Function)),
            ["fs"] = ImmutableArray.Create(
                new BuiltinDescriptor("pwd", "fs.pwd() -> string", CompletionKind.Function),
                new BuiltinDescriptor("cd", "fs.cd(path)", CompletionKind.Function),
                new BuiltinDescriptor("ls", "fs.ls([path][, { recurse, hidden, pattern }])", CompletionKind.Function),
                new BuiltinDescriptor("cat", "fs.cat(path) -> string", CompletionKind.Function),
                new BuiltinDescriptor("read", "fs.read(path) -> string", CompletionKind.Function),
                new BuiltinDescriptor("write", "fs.write(path, content)", CompletionKind.Function),
                new BuiltinDescriptor("append", "fs.append(path, content)", CompletionKind.Function),
                new BuiltinDescriptor("exists", "fs.exists(path) -> bool", CompletionKind.Function),
                new BuiltinDescriptor("is_dir", "fs.is_dir(path) -> bool", CompletionKind.Function),
                new BuiltinDescriptor("mkdir", "fs.mkdir(path[, { recursive }])", CompletionKind.Function),
                new BuiltinDescriptor("rm", "fs.rm(path[, { recursive, force }])", CompletionKind.Function),
                new BuiltinDescriptor("move", "fs.move(src, dst)", CompletionKind.Function),
                new BuiltinDescriptor("copy", "fs.copy(src, dst)", CompletionKind.Function)),
            ["proc"] = ImmutableArray.Create(
                new BuiltinDescriptor("args", "proc.args() -> list", CompletionKind.Function),
                new BuiltinDescriptor("pid", "proc.pid() -> int", CompletionKind.Function),
                new BuiltinDescriptor("hostname", "proc.hostname() -> string", CompletionKind.Function),
                new BuiltinDescriptor("user", "proc.user() -> string", CompletionKind.Function),
                new BuiltinDescriptor("home", "proc.home() -> string", CompletionKind.Function),
                new BuiltinDescriptor("os", "proc.os() -> string", CompletionKind.Function),
                new BuiltinDescriptor("arch", "proc.arch() -> string", CompletionKind.Function),
                new BuiltinDescriptor("exit", "proc.exit(code) -> never", CompletionKind.Function),
                new BuiltinDescriptor("sleep", "proc.sleep(ms)", CompletionKind.Function)),
            ["obj"] = ImmutableArray.Create(
                new BuiltinDescriptor("type", "obj.type(v) -> string", CompletionKind.Function),
                new BuiltinDescriptor("size", "obj.size(v) -> int", CompletionKind.Function),
                new BuiltinDescriptor("dump", "obj.dump(v) -> string  (data + types)", CompletionKind.Function),
                new BuiltinDescriptor("def", "obj.def(v) -> string  (shape only)", CompletionKind.Function),
                new BuiltinDescriptor("pairs", "obj.pairs(r) -> list of {Key, Value}", CompletionKind.Function),
                new BuiltinDescriptor("from_pairs", "obj.from_pairs(seq) -> record", CompletionKind.Function),
                new BuiltinDescriptor("keys", "obj.keys(r) -> list", CompletionKind.Function),
                new BuiltinDescriptor("values", "obj.values(r) -> list", CompletionKind.Function),
                new BuiltinDescriptor("from_rows", "obj.from_rows([[headers], rows...]) -> table", CompletionKind.Function),
                new BuiltinDescriptor("to_rows", "obj.to_rows(table) -> list of lists", CompletionKind.Function),
                new BuiltinDescriptor("columns", "obj.columns(table) -> record of lists", CompletionKind.Function),
                new BuiltinDescriptor("from_columns", "obj.from_columns(record) -> table", CompletionKind.Function),
                new BuiltinDescriptor("normalize", "obj.normalize(table[, defaults]) -> uniform table", CompletionKind.Function)),
            ["json"] = ImmutableArray.Create(
                new BuiltinDescriptor("parse", "json.parse(s) -> value", CompletionKind.Function),
                new BuiltinDescriptor("stringify", "json.stringify(v[, { indent }]) -> string", CompletionKind.Function)),
            ["xml"] = ImmutableArray.Create(
                new BuiltinDescriptor("doc", "xml.doc(s) -> record", CompletionKind.Function),
                new BuiltinDescriptor("save", "xml.save(record[, { indent, declaration }]) -> string", CompletionKind.Function),
                new BuiltinDescriptor("text", "xml.text(elem) -> string", CompletionKind.Function),
                new BuiltinDescriptor("attr", "xml.attr(elem, name[, default]) -> string", CompletionKind.Function),
                new BuiltinDescriptor("find", "xml.find(elem, name) -> record | unit", CompletionKind.Function),
                new BuiltinDescriptor("find_all", "xml.find_all(elem, name) -> list", CompletionKind.Function),
                new BuiltinDescriptor("xpath", "xml.xpath(elem, expression) -> list", CompletionKind.Function)),
        }.ToImmutableDictionary(StringComparer.Ordinal);

    /// <summary>NinjaShell reserved keywords.</summary>
    public static readonly ImmutableArray<BuiltinDescriptor> Keywords = ImmutableArray.Create(
        new BuiltinDescriptor("let", "let NAME = VALUE in EXPR", CompletionKind.Keyword),
        new BuiltinDescriptor("in", "let NAME = VALUE in EXPR", CompletionKind.Keyword),
        new BuiltinDescriptor("switch", "expr switch { pattern => body, ... }", CompletionKind.Keyword),
        new BuiltinDescriptor("pwsh", "pwsh { POWERSHELL ... }", CompletionKind.Keyword),
        new BuiltinDescriptor("source", "source(\"path\")  (top-level only)", CompletionKind.Keyword),
        new BuiltinDescriptor("true", "true", CompletionKind.Keyword),
        new BuiltinDescriptor("false", "false", CompletionKind.Keyword));
}

/// <summary>An entry in the <see cref="BuiltinCatalog"/>.</summary>
internal sealed record BuiltinDescriptor(string Name, string Detail, CompletionKind Kind);
