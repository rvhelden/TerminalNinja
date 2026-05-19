## Summary

Adds a shell-mode sugar layer + per-REPL configuration to the Ninja shell. Typing `cd foo` now invokes `fs.cd("foo")` end-to-end via a pre-parse interceptor that resolves the first identifier against an alias table. The alias table is part of a new mutable `NinjaConfig` holder that also stores REPL line-editor keybindings. Both surfaces are mutated by new `alias` and `key` modules, and seeded from `~/.ninjarc` at startup.

Aliases bind to **callable values**, not text targets — `alias.set("ls", fs.ls)` and `alias.set("ll", path => fs.ls(path, { hidden: true }))` both work the way the user typed them.

## Acceptance criteria (from `plans/shell-alias-config/intake.md`)

- [x] `cd foo` at the REPL invokes the same callable as `fs.cd("foo")` and returns the same result
- [x] `ls`, `ls /tmp`, `ls "/my docs"` keep the path as a single arg when quoted
- [x] `cd("foo")`, `cd | print`, `let cd = 1 in cd` are NOT intercepted — existing semantics preserved
- [x] `alias.set("ll", path => fs.ls(path, { hidden: false }))` lambda alias works end-to-end
- [x] `alias.set("bad", "not a function")` raises `EvaluatorException` at the module boundary
- [x] `~/.ninjarc` is evaluated at startup; missing file is silent; syntax / runtime errors go to stderr without aborting startup
- [x] `key.bind("Ctrl+L", "clear")` clears the screen in the interactive line editor
- [x] All existing shell tests continue to pass (391/391 in `TerminalNinja.Shell.Tests`)

## Tasks

Each task = one focused commit, tests-first, with 5 parallel reviews (XMLDocs, AOT, security, style, dead-code):

1. `c4d1a23` — `NinjaConfig` holder (immutable-dict + Interlocked.CompareExchange) + 23 tests
2. `41dcc09` — `ShellArgTokenizer` + `AliasInterceptor` + 30 tests
3. `f30b642` — `DefaultAliases`, `AliasModule`, `KeyModule`, `BuiltinRegistry.CreateDefaultEnvWith`, `BuiltinCatalog` entries + 22 tests
4. `95cfe65` — `NinjaEvaluator.Invoke` helper + `NinjaRepl` interception wiring + 7 end-to-end tests
5. `49e2b9d` — `RcLoader` + `LineEditor` keybinding dispatch via shared `ChordKey` helper + 10 tests
6. `536058d` — docs: `docs/ninjarc.md` worked example + `docs/llms.txt` reference

Plan, intake, and the sample-gate descope record are in `plans/shell-alias-config/`.

## Heads-up on commit `f111f28 Various`

An intervening "Various" commit by the branch owner landed between Task 3 and Task 4 of my work — it captured an unrelated test-project restructure (renaming `samples/NinjaShellUi/` to `TerminalNinja.Shell.Skia/`, splitting test projects per assembly) plus my in-flight Task 4 edits to `NinjaRepl.cs`, `NinjaEvaluator.cs`, and `NinjaReplAliasTests.cs`. The Task 4 work is conceptually one commit but landed across `f111f28` (main bulk) + `95cfe65` (review-driven polish). Squash before merge if you want a clean history, or keep as-is.

## Samples

This feature has no XAML control surface (it's shell-language). Per Phase 4 sample-gate descope (recorded in `plans/shell-alias-config/intake.md`), the demonstrable artifact is `docs/ninjarc.md` — a worked example of every new module function. Wiring the alias interceptor into the Skia `ReplView` is explicitly out of scope and tracked as a follow-up.

## Test plan

- [x] `dotnet run --project TerminalNinja.Shell.Tests/TerminalNinja.Shell.Tests.csproj` — 391/391 green
- [x] `dotnet run --project TerminalNinja.Shell.Language.Tests/...` — 88/88 green (BuiltinCatalog entries added)
- [x] `dotnet run --project TerminalNinja.Shell.LanguageServer.Tests/...` — 107/107 green
- [ ] Manual REPL smoke — `pwd`, `cd ..`, `alias.set("ll", path => fs.ls(path, { hidden: true }))`, `alias.list()`, drop a `~/.ninjarc` and restart
- [ ] Manual keybinding smoke — `key.bind("Ctrl+L", "clear")` then press Ctrl+L in the interactive editor

## Release impact

- Bump: **minor** (feat)
- Breaking change: **no** — purely additive: new types (`NinjaConfig`, `AliasInterceptor`, `AliasModule`, `KeyModule`, `RcLoader`, `ChordKey`, `DefaultAliases`); new `NinjaEvaluator.Invoke` method; new `BuiltinRegistry.CreateDefaultEnvWith` method; new optional `NinjaConfig?` ctor param on `LineEditor` (existing callers compile unchanged)
- Changelog entry: `feat(shell): aliases, ~/.ninjarc, REPL keybindings, NinjaConfig`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
