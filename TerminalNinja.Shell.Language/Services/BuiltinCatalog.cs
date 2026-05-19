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
        new BuiltinDescriptor("where", "where(seq, predicate)", CompletionKind.Function,
            "Filter a sequence to items where predicate returns true. Lazy — pulls from the source only as the result is iterated."),
        new BuiltinDescriptor("select", "select(seq, projection)", CompletionKind.Function,
            "Map each item through projection. Lazy. Use with records to reshape pipelines: `xs | select(r => r.Name)`."),
        new BuiltinDescriptor("each", "each(seq, action)", CompletionKind.Function,
            "Run action for every item. Eager — drains the sequence. Returns unit."),
        new BuiltinDescriptor("fold", "fold(seq, init, (acc, x) => ...)", CompletionKind.Function,
            "Left fold: thread acc through the sequence starting at init. Eager."),
        new BuiltinDescriptor("take", "take(seq, n)", CompletionKind.Function,
            "First n items. Lazy — only pulls n from the source."),
        new BuiltinDescriptor("skip", "skip(seq, n)", CompletionKind.Function,
            "Drop the first n items. Lazy."),
        new BuiltinDescriptor("count", "count(seq) -> int", CompletionKind.Function,
            "Number of items. Eager — fully drains the sequence."),
        new BuiltinDescriptor("sort", "sort(seq[, { by, desc }])", CompletionKind.Function,
            "Sort by natural order, or by `{ by: r => r.Field, desc: true }`. Eager."),
        new BuiltinDescriptor("reverse", "reverse(seq) -> list", CompletionKind.Function,
            "Materialise and reverse. Eager."),
        new BuiltinDescriptor("distinct", "distinct(seq) -> list", CompletionKind.Function,
            "Drop duplicates by structural equality. Eager."),
        new BuiltinDescriptor("head", "head(seq)", CompletionKind.Function,
            "First item, or unit if empty. Lazy — pulls one."),
        new BuiltinDescriptor("tail", "tail(seq) -> list", CompletionKind.Function,
            "All but the first item. Eager."),
        new BuiltinDescriptor("materialize", "materialize(seq) -> list", CompletionKind.Function,
            "Force a lazy sequence into a fully-realised list. Useful before storing in a let or printing."),
        new BuiltinDescriptor("print", "print(v)", CompletionKind.Function,
            "Write v to stdout (no trailing newline)."),
        new BuiltinDescriptor("println", "println(v)", CompletionKind.Function,
            "Write v + newline to stdout."),
        new BuiltinDescriptor("format_table", "format_table(list_of_records) -> string", CompletionKind.Function,
            "Render a list of records as an aligned ASCII table. Uses field order of the first row."));

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
                new BuiltinDescriptor("dump", "obj.dump(v[, depth]) -> string  (vertical property table; depth caps recursion, default 2)", CompletionKind.Function),
                new BuiltinDescriptor("table", "obj.table(list) -> string  (force aligned record-table rendering)", CompletionKind.Function),
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
            ["http"] = ImmutableArray.Create(
                new BuiltinDescriptor("get", "http.get(url[, options]) -> record", CompletionKind.Function,
                    "GET an HTTP URL. Returns { status, status_text, ok, headers, body, url, elapsed_ms }. Pass { json: true } to parse a JSON response and/or serialize a JSON body."),
                new BuiltinDescriptor("post", "http.post(url[, options]) -> record", CompletionKind.Function),
                new BuiltinDescriptor("put", "http.put(url[, options]) -> record", CompletionKind.Function),
                new BuiltinDescriptor("patch", "http.patch(url[, options]) -> record", CompletionKind.Function),
                new BuiltinDescriptor("delete", "http.delete(url[, options]) -> record", CompletionKind.Function),
                new BuiltinDescriptor("head", "http.head(url[, options]) -> record", CompletionKind.Function),
                new BuiltinDescriptor("request", "http.request({ method, url, ... }) -> record", CompletionKind.Function),
                new BuiltinDescriptor("download", "http.download(url, path[, options]) -> record", CompletionKind.Function,
                    "Stream the response body to a file. Returns { status, headers, path, bytes, elapsed_ms }."),
                new BuiltinDescriptor("stream", "http.stream(url[, options]) -> seq", CompletionKind.Function,
                    "Lazy sequence of lines (or SSE event records when Content-Type is text/event-stream).")),
            ["alias"] = ImmutableArray.Create(
                new BuiltinDescriptor("set", "alias.set(name, fn)", CompletionKind.Function,
                    "Bind a shell-mode alias: typing `name arg1 arg2` at the REPL invokes fn with each token as a string. fn must be a function; lambda wrappers are supported."),
                new BuiltinDescriptor("unset", "alias.unset(name) -> bool", CompletionKind.Function,
                    "Remove a shell-mode alias; returns true if a binding existed."),
                new BuiltinDescriptor("list", "alias.list() -> record", CompletionKind.Function,
                    "Snapshot of all registered aliases as a record of callable values."),
                new BuiltinDescriptor("get", "alias.get(name) -> fn | unit", CompletionKind.Function,
                    "Look up an alias; returns the callable, or unit when none is bound.")),
            ["key"] = ImmutableArray.Create(
                new BuiltinDescriptor("bind", "key.bind(chord, action)", CompletionKind.Function,
                    "Bind a REPL line-editor chord (e.g. \"Ctrl+L\") to a named action. Supported actions: clear, history-prev, history-next, abort, submit, complete."),
                new BuiltinDescriptor("unbind", "key.unbind(chord) -> bool", CompletionKind.Function,
                    "Remove a key binding; returns true if one existed."),
                new BuiltinDescriptor("list", "key.list() -> record", CompletionKind.Function,
                    "Snapshot of all key bindings keyed by chord.")),
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

/// <summary>
/// An entry in the <see cref="BuiltinCatalog"/>. <see cref="Detail"/> is the
/// one-line signature shown in lists; <see cref="Documentation"/> is a longer
/// human-readable explanation rendered in the details pane.
/// </summary>
internal sealed record BuiltinDescriptor(
    string Name,
    string Detail,
    CompletionKind Kind,
    string? Documentation = null);
