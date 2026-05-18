# Plan — shell-alias-config

## Context

The Ninja shell today only accepts canonical expression-language syntax: to change directory the user must type `fs.cd("path")` — quotes, parens, and module prefix included. That's friction for what is otherwise a "shell." This change adds a thin shell-style sugar layer so common commands feel natural (`cd foo`, `ls`, `ls /tmp`) while preserving the expression language underneath. The same configuration surface also covers REPL-scoped keybindings, and both are housed in a single mutable `NinjaConfig` holder so future user-facing settings have a home. Configuration is expressed by evaluating `~/.ninjarc` (a regular `.ninja` script) at REPL startup — the same language the shell already speaks, no separate format.

## Approach

**Aliases are function-value bindings, not text macros.** `alias.set("ls", fs.ls)` evaluates `fs.ls` to an `NFunc` (or any callable `NValue` — lambdas work too) and binds the alias table entry `"ls"` → that callable. This composes: `alias.set("ll", path => fs.ls(path, { hidden: false }))` binds a user-written wrapper.

**Line interception, not textual rewrite.** Before `LineAccumulator.Feed` runs, the REPL asks `AliasInterceptor.TryIntercept(line, config)`. If the buffer is empty *and* the first token matches a registered alias *and* the line is in "shell shape" (next non-space char isn't `(`, `.`, `=`, etc.), the interceptor tokenizes the rest shell-style (whitespace splits, `"…"` is one token) into `NString` values and returns an `AliasInvocation`. The REPL invokes the callable directly via a new `NinjaEvaluator.Invoke` helper (so lambda values work too) and prints through the existing `Printer.Format` path.

| User types        | Effect                                                    |
| ----------------- | --------------------------------------------------------- |
| `cd`              | invoke alias `cd` with `[]` → `fs.cd()` (arity-mismatch surfaces as the usual `EvaluatorException`) |
| `cd foo`          | invoke alias `cd` with `[NString("foo")]`                 |
| `cd "my docs"`    | invoke alias `cd` with `[NString("my docs")]`             |
| `ls /tmp -r`      | invoke alias `ls` with `[NString("/tmp"), NString("-r")]` |
| `cd("foo")`       | not intercepted (parens) — parsed as expression           |
| `cd \| print`     | not intercepted (`\|` outside quotes) — expression        |
| `let cd = ...`    | not intercepted (first tok `let`) — expression            |

**Config**: a single mutable `NinjaConfig` holder owned by `NinjaRepl`. Stores aliases (name → callable `NValue`) and REPL keybindings (chord string → named action string). Defaults seeded after the env is built (because they reference `fs.cd`, `fs.ls`, etc.). `~/.ninjarc` runs after seeding and may layer overrides via `alias.set`, `alias.unset`, `key.bind`, `key.unbind`.

**Keybindings (REPL-only)**: the `LineEditor` consults `NinjaConfig.Keybindings` on each keystroke; chord-to-action lookup happens before the hardcoded handling. V1 supported actions: `clear`, `history-prev`, `history-next`, `abort`, `submit`, `complete`.

## Tasks

### Task 1 — `NinjaConfig` holder

- **Files**:
  - `TerminalNinja.Shell/Config/NinjaConfig.cs`
  - `TerminalNinja.Shell.Tests/Unit/NinjaConfigTests.cs`
- **Tests**: set/unset/list/TryGet for both aliases and keybindings, `SetAlias` rejects non-callable values, concurrent set safety (light sanity check).
- **Notes**: Internally `ImmutableDictionary<string, NValue>` (aliases) and `ImmutableDictionary<string, string>` (keybindings), swapped atomically with `Interlocked.CompareExchange`. `SetAlias` validates via a small `IsCallable(NValue)` check (initially `NValue is NFunc`; extended in Task 4 if the evaluator exposes a broader notion of callable). Public surface:
  ```csharp
  public sealed class NinjaConfig
  {
      public IReadOnlyDictionary<string, NValue> Aliases { get; }
      public IReadOnlyDictionary<string, string> Keybindings { get; }
      public void SetAlias(string name, NValue callable);
      public bool RemoveAlias(string name);
      public bool TryGetAlias(string name, out NValue callable);
      public void BindKey(string chord, string action);
      public bool UnbindKey(string chord);
      public bool TryGetAction(string chord, out string action);
      public static NinjaConfig Empty();
  }
  ```

### Task 2 — `ShellArgTokenizer` + `AliasInterceptor`

- **Files**:
  - `TerminalNinja.Shell/Repl/ShellArgTokenizer.cs`
  - `TerminalNinja.Shell/Repl/AliasInterceptor.cs`
  - `TerminalNinja.Shell.Tests/Unit/ShellArgTokenizerTests.cs`
  - `TerminalNinja.Shell.Tests/Unit/AliasInterceptorTests.cs`
- **Tests**: every row from the table above; unknown alias passthrough; empty alias map passthrough; leading whitespace; quoted-arg with embedded `\"` and `\\`; quoted token containing `|` still intercepts; unquoted `|` aborts intercept. Tokenizer: `a b c` → 3 tokens; `"a b"` → 1 token; `"\""` → one token containing literal `"`; unterminated quote → a recognizable outcome the interceptor treats as "not a shell line."
- **Notes**: `AliasInterceptor.TryIntercept(string line, NinjaConfig config, out AliasInvocation invocation)` returns `false` for any condition that means "fall through to the parser." `AliasInvocation` is a `readonly record struct` with `Name`, `Func`, `Args`. Tokenizer emits a per-token `WasQuoted` flag so the interceptor can check punctuation only on unquoted tokens.

### Task 3 — `DefaultAliases` + `AliasModule` + `KeyModule` + catalog updates

- **Files**:
  - `TerminalNinja.Shell/Config/DefaultAliases.cs`
  - `TerminalNinja.Shell/Builtins/AliasModule.cs`
  - `TerminalNinja.Shell/Builtins/KeyModule.cs`
  - `TerminalNinja.Shell/Builtins/BuiltinRegistry.cs` (modify — add `CreateDefaultEnvWith(NinjaConfig)`)
  - `TerminalNinja.Shell.Language/Services/BuiltinCatalog.cs` (modify — add `alias` and `key` entries)
  - `TerminalNinja.Shell.Tests/Unit/DefaultAliasesTests.cs`
  - `TerminalNinja.Shell.Tests/Unit/AliasModuleTests.cs`
  - `TerminalNinja.Shell.Tests/Unit/KeyModuleTests.cs`
- **Tests**:
  - `DefaultAliases.Seed` against `BuiltinRegistry.CreateDefaultEnv()` resolves `cd` to the same callable as `fs.cd` (reference equality on the `NValue`). Skips silently when a target is missing.
  - `AliasModule`: `alias.set("foo", fs.cd)` writes to config; non-callable arg throws `EvaluatorException("alias.set: second argument must be a function")`. Lambda alias `alias.set("ll", path => fs.ls(path, { hidden: false }))` stores the lambda value verbatim. `alias.list()` returns a snapshot record.
  - `KeyModule`: bind/unbind/list, invalid-action rejection, invalid-chord rejection.
- **Notes**:
  - Default aliases: `cd→fs.cd`, `ls→fs.ls`, `pwd→fs.pwd`, `cat→fs.cat`, `mkdir→fs.mkdir`, `rm→fs.rm`, `cp→fs.copy`, `mv→fs.move`, `echo→println`.
  - V1 supported actions for `key.bind`: `clear`, `history-prev`, `history-next`, `abort`, `submit`, `complete`.
  - Chord format: `"Ctrl+L"`, `"Alt+R"`, `"Shift+Tab"`. Parsed by a small static `ChordParser` helper inside `KeyModule.cs`.

### Task 4 — `NinjaEvaluator.Invoke` helper + `NinjaRepl` alias execution

- **Files**:
  - `TerminalNinja.Shell/Runtime/NinjaEvaluator.cs` (or wherever `EvalTop` lives — confirmed during implementation)
  - `TerminalNinja.Shell/Repl/NinjaRepl.cs` (modify)
  - `TerminalNinja.Shell.Tests/Unit/NinjaReplAliasTests.cs` (end-to-end through `NinjaRepl`)
- **Tests**: `cd foo` produces the same observable effect as `fs.cd("foo")`. `let cd = 1 in cd` returns `1`. Lambda alias works end-to-end. Arity-mismatch surfaces through the existing error path.
- **Notes**:
  - `Invoke(NValue callable, ImmutableArray<NValue> args, Env env) -> EvalResult` reuses the same code path the evaluator uses for `Call` nodes (NFunc + lambda values).
  - `NinjaRepl` constructor wires: build env → `DefaultAliases.Seed(_config, _env)` → register `AliasModule`/`KeyModule` (they close over `_config`) → `RcLoader.TryLoad` (deferred to Task 5).
  - `HandleInputLine` checks `_accumulator.IsEmpty && AliasInterceptor.TryIntercept(...)` before `_accumulator.Feed(line)`. Existing `Feed`/`ExecuteAndPrint` path otherwise unchanged.

### Task 5 — `RcLoader` + `LineEditor` keybindings

- **Files**:
  - `TerminalNinja.Shell/Repl/RcLoader.cs`
  - `TerminalNinja.Shell/Repl/LineEditor.cs` (modify)
  - `TerminalNinja.Shell.Tests/Unit/RcLoaderTests.cs`
  - `TerminalNinja.Shell.Tests/Unit/LineEditorKeybindingTests.cs`
- **Tests**:
  - `RcLoader`: writes a temp file with `alias.set("zz", fs.pwd)`, loads it, asserts config has `zz` bound to the `fs.pwd` callable. Missing file: silent no-op. Syntax error: written to stderr, config untouched.
  - `LineEditor`: with a `NinjaConfig` containing `Ctrl+L → clear`, the corresponding keystroke from a fake `IKeyReader` triggers the configured action (verify via observable state — buffer cleared, no Enter pressed).
- **Notes**:
  - Path resolution: `Environment.GetFolderPath(SpecialFolder.UserProfile)` + `.ninjarc`.
  - The loader parses the file as a script (multiple top-level statements), evaluates against the live `Env`, and mutates `NinjaConfig` via the module side-effects already wired in Task 3.
  - `LineEditor` builds a chord string from `ConsoleKeyInfo` (`"Ctrl+L"`, `"Alt+R"`), looks it up in `config.Keybindings`, dispatches the named action, then falls through if no binding.

## Out of scope (deliberate)

- JSON config file format (chose `.ninjarc`).
- App-wide keybinding registry consumed by every `Control.OnKeyEvent`.
- Wiring alias interception into the Skia `ReplView` editor (same config shape; one-call follow-up).
- Parameterized aliases (e.g. `$1`, `$@`).
- Persistent history of mutations to `NinjaConfig` between sessions (rc script is the persistence mechanism).

## Verification

1. `dotnet build` — clean.
2. `dotnet run --project TerminalNinja.Shell.Tests/TerminalNinja.Shell.Tests.csproj --treenode-filter "/*/*/(AliasInterceptorTests)|(ShellArgTokenizerTests)|(NinjaConfigTests)|(DefaultAliasesTests)|(AliasModuleTests)|(KeyModuleTests)|(NinjaReplAliasTests)|(RcLoaderTests)|(LineEditorKeybindingTests)/*"` — new tests green.
3. `dotnet run --project TerminalNinja.Shell.Tests/TerminalNinja.Shell.Tests.csproj` — full shell suite green; no regressions.
4. Manual REPL smoke:
   - `pwd`, `cd ..`, `ls` — alias path works.
   - `alias.list()` — shows defaults as a record of callables.
   - `alias.set("hi", println)` then `hi "hello world"` — prints `hello world` (quoted single arg).
   - `alias.set("ll", path => fs.ls(path, { hidden: false }))` then `ll .` — lambda alias.
   - `cd("foo")` — canonical expression still works.
   - `let cd = 1 in cd` — returns `1` (not intercepted).
   - `alias.set("bad", "not a function")` — `runtime error: alias.set: second argument must be a function`.
5. Create `%USERPROFILE%\.ninjarc` with `alias.set("zz", fs.pwd)`, restart REPL, type `zz` — returns CWD.
6. `key.bind("Ctrl+L", "clear")`, press Ctrl+L in the REPL — output cleared. (`LineEditorKeybindingTests` covers the dispatch via fake `IKeyReader`.)

## Review summaries

Phase 2 reviews were skipped per user direction (plan was developed and approved in Claude plan-mode before invoking the skill). The intake captures the design decisions that would normally surface in those reviews:

- **AOT**: All new code uses concrete types, dictionaries, and existing source-generator-discovered patterns. No reflection, no `Activator.CreateInstance`, no expression trees. The alias table holds `NValue` references — already the framework's first-class function representation.
- **Security**: `.ninjarc` is read from `$HOME` only; no path traversal surface. Tokenizer rejects unterminated quotes (no infinite loops). Alias dispatch reuses the same `EvaluatorException` channel as canonical calls — no new error escape.
- **Architecture**: Additive only. `NinjaConfig` is the kind of host-owned mutable holder the framework already uses (e.g. theme name on `Application`). Module registration follows the existing `BuiltinRegistry.RegisterModule` pattern. The interceptor sits *above* the parser, leaving the language layer untouched.
