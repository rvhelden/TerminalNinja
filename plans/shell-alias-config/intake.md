# Intake — shell-alias-config

**Branch**: feature/shell-alias-config
**Kind**: feature
**Scope**: TerminalNinja.Shell (REPL, builtins, runtime)
**Date**: 2026-05-18

## User ask (verbatim)

> We need a concept of an alias, for example cd <string> -> fs.cd(<string>), ls -> fs.ls(), ls <string> -> fs.ls(<string>) and others, but they should also be configurable user defined, and we should have a global config for our setup of aliases, keybindings and we should have a global config record
>
> i think we need an alias module where we can use it like this `alias.set("ls", fs.ls)` and where by convention the args are split on space and passed as args, but do account for "this is a single arg"
>
> or this as well `alias.set("ls", path => fs.ls(path, { hidden: false }))`

## Clarifying answers (from AskUserQuestion)

- **Alias mechanism**: pre-parse interception — first token of empty-buffer line is looked up; tokenized args become string `NValue`s; invoke directly.
- **Config storage**: `~/.ninjarc` startup script (evaluated against the same env as the REPL).
- **Keybindings scope**: REPL-only (LineEditor), not app-wide control keymap.
- **Defaults**: ship common aliases out of the box (`cd`, `ls`, `pwd`, `cat`, `mkdir`, `rm`, `cp`, `mv`, `echo`).

## Acceptance criteria

- [ ] Typing `cd foo` at the REPL invokes the same callable as `fs.cd("foo")` and returns the same result.
- [ ] `ls` (no args) invokes `fs.ls()`; `ls /tmp` invokes `fs.ls("/tmp")`; `ls "/my docs"` keeps the path as a single arg.
- [ ] Expression-mode lines (`cd("foo")`, `cd | print`, `let cd = 1 in cd`) are NOT intercepted — existing semantics preserved.
- [ ] `alias.set("ll", path => fs.ls(path, { hidden: false }))` works: subsequent `ll .` invokes the lambda alias.
- [ ] `alias.set("bad", "not a function")` raises `EvaluatorException` (callable validation at module boundary).
- [ ] A `~/.ninjarc` script is evaluated at REPL startup; missing file is silent; syntax error is reported but doesn't abort startup.
- [ ] `key.bind("Ctrl+L", "clear")` makes Ctrl+L clear the screen in the interactive REPL line editor.
- [ ] All existing shell tests continue to pass.

## Out of scope

- JSON config file format (chose `.ninjarc`).
- App-wide control-level keybinding registry (`Control.OnKeyEvent` overrides untouched).
- Wiring alias interception into the Skia `ReplView` editor (config shape is the same; one-call follow-up).
- Parameterized aliases with `$1` / `$@` substitution. V1 always emits one string-arg per token.
- Persisting `alias.set` mutations between sessions (rc script is the persistence mechanism).

## Breaking change risk

None. The change is purely additive at the REPL and runtime layer:
- New types (`NinjaConfig`, `AliasInterceptor`, `AliasModule`, `KeyModule`, `RcLoader`) — all new.
- `LineAccumulator` is unchanged.
- `LineEditor` gains an optional ctor param — existing call sites compile.
- `BuiltinRegistry` adds a new method (`CreateDefaultEnvWith`); existing methods unchanged.
- `NinjaEvaluator` gains a new public `Invoke` helper; existing API unchanged.
- `BuiltinCatalog` adds entries — additive.

## Notes

- The full design plan is at `plan.md` in this folder.
- The approved plan was developed in Claude plan-mode and pre-approved by the user; Phase 2 reviews are skipped per user direction ("do NOT redo intake or planning").
- Working tree at branch creation has unrelated WIP (test project split, `samples/NinjaShellUi/` → `TerminalNinja.Shell.Skia/` rename). That WIP is untouched by this branch's commits — only feature-specific paths are staged.
