# NinjaShell — VS Code extension

Syntax highlighting plus live diagnostics from `ninja-lsp` for `.ninja` files.

## Prerequisites

The `ninja-lsp` binary must be reachable. Either:

- Put the AOT-published `ninja-lsp` (or `ninja-lsp.exe` on Windows) on your `PATH`, or
- Set the `ninja.languageServer.path` setting to its absolute path.

Build it from the repo root:

```
dotnet publish TerminalNinja.Shell.LanguageServer -c Release -r win-x64 -p:PublishAot=true
# ↑ replace win-x64 with linux-x64 / osx-arm64 / etc. as appropriate
```

The published binary will be in
`TerminalNinja.Shell.LanguageServer/bin/Release/net11.0/<rid>/publish/`.

## Development

```
cd editors/vscode
npm install
npm run compile
```

Press `F5` in VS Code with `editors/vscode` open to launch an Extension
Development Host. Open any `.ninja` file — you should see syntax
highlighting immediately, and red squigglies under syntax errors as
`ninja-lsp` parses on each keystroke.

## Settings

| Key | Default | Description |
|---|---|---|
| `ninja.languageServer.path` | `""` | Absolute path to `ninja-lsp`. Empty falls back to PATH lookup. |
| `ninja.languageServer.trace.server` | `"off"` | `"off"` / `"messages"` / `"verbose"` — JSON-RPC trace, shown in the "NinjaShell trace" output channel. |

## What works today

- Syntax highlighting (keywords, strings, numbers, comments, interpolated strings, pwsh blocks).
- Live diagnostics from lexer / parser errors with 0-based ranges.
- Bracket pairing and auto-closing for `()` `[]` `{}` and `""`.

## What's coming

- Hover (signatures / docs for builtins).
- Completion (builtin names + `module.<member>` after `.`).
- Document symbols (outline view).
- Go-to-definition for `let` bindings.
