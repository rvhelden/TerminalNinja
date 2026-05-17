# Intake — ninjashell

**Branch**: feature/ninjashell
**Kind**: feature
**Scope**: new sibling project — `TerminalNinja.Shell/` (NinjaShell language, REPL, pwsh bridge)
**Date**: 2026-05-17

## User ask (verbatim)

> Implement NinjaShell MVP per approved plan at C:\Users\rvanh\.claude\plans\here-is-the-approved-merry-perlis.md — F#-flavored object-pipeline shell as new TerminalNinja.Shell project. Six phases: (1) bump SDK to .NET 11 preview + scaffold project with NValue union, (2) lexer, (3) parser + AST, (4) evaluator + pipeline + core builtins, (5) PowerShell subprocess bridge, (6) FS builtins + REPL loop. Plan, language surface, value model, AOT gate, and verification smoke tests are fully spelled out in the plan file. User has authorized working without stopping for clarifying questions.

## Acceptance criteria

- [ ] `dotnet build` at solution root succeeds with `TreatWarningsAsErrors=true` on .NET 11 preview SDK; existing `net10.0` sibling projects still build.
- [ ] `dotnet publish TerminalNinja.Shell/TerminalNinja.Shell.csproj -c Release -r win-x64` produces a working AOT binary with zero IL2026 / IL3050 warnings.
- [ ] `dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj --treenode-filter "/*/*/(LexerTests)|(ParserTests)|(EvaluatorTests)|(PipelineTests)|(PwshBridgeTests)/*"` is green.
- [ ] `dotnet run --project TerminalNinja.Shell/TerminalNinja.Shell.csproj` opens a `ninja>` REPL; the smoke tests in the plan's Verification section all produce the expected results.
- [ ] `pwsh { Get-Date | Select-Object Year, Month, Day }` returns a record with three integer fields (gated to skip when `pwsh` is unavailable).
- [ ] All NinjaShell behavior described in the plan's "Language surface" section is implemented at MVP scope (no if/else, no loops, switch-expression only, parenthesised calls only).

## Out of scope

- Hosting the REPL inside a `TerminalView` (explicitly deferred per the plan).
- CLIXML interop with PowerShell (JSON channel for MVP).
- Persistent pwsh runspace, completion, history, syntax highlighting.
- User-defined named types, modules, static type checking.
- `while`, `for`, range `step`, float ranges.
- A `Builtins` source generator (hand-maintained registry at MVP scale).

## Breaking change risk

**None** for existing consumers. This is a new sibling project. The only repo-wide change is the `global.json` SDK bump to .NET 11 preview — existing `net10.0` projects keep building because the .NET 11 SDK is multi-target-capable.

## Notes

- Approved plan source: `C:\Users\rvanh\.claude\plans\here-is-the-approved-merry-perlis.md` (also copied into this folder as `plan.md`).
- User authorized continuous execution without clarifying-question stops.
- Available SDK: `11.0.100-preview.4.26230.115` (newer than the plan's preview.2 reference — will pin to this build in `global.json`).
- Sample gate per `feature-dev` skill: deferred together with TerminalView hosting (the REPL is its own user-facing surface; a `Sample/Samples/Shell/` screen wrapping the REPL is a follow-up).
