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
| `ninja.debugAdapter.path` | `""` | Absolute path to the `ninja` executable (hosts `--dap` debug adapter). Empty falls back to PATH lookup. |

## Debugging `.ninja` scripts

The extension contributes a debugger of type `ninja` that drives the
interpreter via the Debug Adapter Protocol. Supported in the MVP:
breakpoints, pause/continue, step-over (F10), step-in (F11), step-out
(Shift+F11), call-stack view, and the Locals scope.

Quick start:

1. Build / publish the `ninja` binary:
   ```
   dotnet publish TerminalNinja.Shell -c Release -r <rid>
   ```
   The published binary is at
   `TerminalNinja.Shell/bin/Release/net11.0/<rid>/publish/ninja(.exe)`.
   Either put it on `PATH` or set `ninja.debugAdapter.path` to its absolute path.

2. Open any `.ninja` file and press **F5**. Without a `launch.json` the
   extension synthesizes one that runs the active file. Otherwise add a
   config like:
   ```json
   {
     "type": "ninja",
     "request": "launch",
     "name": "Run NinjaShell file",
     "program": "${file}",
     "stopOnEntry": false
   }
   ```

3. Set breakpoints by clicking the gutter (or with F9). Hit F5 to run —
   execution pauses at the breakpoint, the call stack and Locals pane
   populate, and the step keys behave as in any VS Code debugger.

Script `stdout` / `stderr` are forwarded to the Debug Console as
`output` events, so they don't corrupt the DAP stream.

## What works today

- Syntax highlighting (keywords, strings, numbers, comments, interpolated strings, pwsh blocks).
- Live diagnostics from lexer / parser errors with 0-based ranges.
- Bracket pairing and auto-closing for `()` `[]` `{}` and `""`.
- Document symbols (outline view).
- Completion: builtin names plus `module.<member>` after `.`.
- Signature help: parameter hints inside open parens, with active-parameter tracking on `,`.
- Hover: signatures / docs for builtins and `module.member` paths, rendered as markdown.
- Go-to-definition: jump from a reference to the declaring top-level `let NAME = …` (most recent shadowing definition wins).

## What's coming

- Find-all-references for `let` bindings.
- Go-to-definition for nested `let … in …` expressions (currently only top-level lets).
