# Phase 4 — Finalize

Goal: verify the full change matches the user's original intent, enforce the sample gate, update AI-facing docs, commit the polish, and open the PR.

## Checklist

### 1. Conformance check against intake

Re-read `plans/<slug>/intake.md` — specifically the acceptance criteria. For each criterion:

- Confirm an observable behavior demonstrates it (a test, a sample interaction, or a manual verification).
- If any criterion is NOT met, stop. Tell the user what's missing. Either amend the plan and go back to Phase 3, or explicitly descope the criterion with the user's agreement (update `intake.md` to reflect the descope).

### 2. Sample gate — MANDATORY

This is where the skill refuses to cut corners. The rule:

- **Every `feature/*` branch MUST ship a new or updated sample screen.**
- **Every `fix/*` branch MUST update every sample that exercises the affected control or behavior, OR add a new sample demonstrating the corrected behavior if no existing sample covers it.**

#### For `feature/*`

Follow the Sample & Docs Checklist in `CLAUDE.md` to the letter:

1. `Sample/Samples/<ControlName>/<ControlName>Screen.xaml` (+ ViewModel if data binding / commands are demonstrated)
2. Add entry to `Sample/Samples/MainMenu/MainMenuViewModel.cs` `Samples` list
3. Add navigation case to `Sample/ShellViewModel.cs` `NavigateToSample` switch
4. `docs/samples/<controlname>.html` with sections: Overview, Properties table, Examples, Keyboard Shortcuts, Key Concepts
5. Add entry to `docs/samples.js` `SAMPLES` array (self-contained Window, no Width/Height)
6. Add `<a class="sample-card">` to `docs/index.html` samples grid

Smoke-test the sample:

```bash
dotnet run --project Sample/Sample.csproj
# navigate to the new screen, try every interaction, confirm theme switching works
```

If you cannot run the terminal app interactively, say so explicitly to the user — do not claim the sample works on inspection alone.

#### For `fix/*`

1. Grep `Sample/Samples/` and `docs/samples/` for references to the affected control or behavior.
2. For each hit, update the sample so the corrected behavior is visible. If the bug produced a specific regression scenario (e.g. "text selection broke when content overflowed"), ensure a sample exercises that scenario.
3. If no existing sample covers the scenario, add one (follow the `feature/*` checklist above for the new sample).
4. Smoke-test per the `feature/*` checklist.

#### Ask the user to confirm

Before continuing, enumerate the samples you touched or created and ask the user to confirm this is the right coverage:

> Samples touched:
> - `Sample/Samples/Slider/` (new)
> - `docs/samples/slider.html` (new)
> - `docs/samples.js` (new entry)
> - `docs/index.html` (new card)
>
> Confirm this is complete, or tell me which samples I missed.

### 3. Update AI-facing docs

- **`docs/llms.txt`**: add a new entry under "Control samples" for a feature. For a fix, edit the affected entry if its description changed. Keep alphabetical order within the control samples section.
- **`AGENTS.md`**: only if the architecture changed (new base class pattern, new generator hook, new convention). Most tasks do not require this.
- **`CLAUDE.md`**: only if a convention changed (e.g. a new DP pattern, a new mandatory attribute). Most tasks do not require this.
- **`README.md`**: only if public API or quickstart changed.

If you touch `AGENTS.md` or `CLAUDE.md`, tell the user explicitly — these are high-signal changes and they should know.

### 4. Commit the finalize step — Versionize / Conventional Commits format

Use the rules in [SKILL.md → Commit format](SKILL.md#commit-format-versionize--conventional-commits). Pick the type by content:

- **New sample screen only** (no library code change in this commit): `docs(sample):` — sample assets are documentation from Versionize's perspective.
- **Sample + `docs/llms.txt` + `docs/samples/*.html`**: `docs(sample):` covers it all.
- **Only touching `AGENTS.md` / `CLAUDE.md`**: `docs:` with no scope.
- **Touching packaging (`csproj`, MSBuild targets, `build/` folder)**: `build(packaging):`.

```bash
git add Sample/ docs/ AGENTS.md CLAUDE.md README.md
git commit -m "$(cat <<'EOF'
docs(sample): add <slug> sample screen and docs page

Registers the new sample in Sample/MainMenu, docs/samples.js, and
docs/index.html. llms.txt updated with the new entry.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Stage only the paths you actually changed. If `AGENTS.md`/`CLAUDE.md`/`README.md` are untouched, don't include them.

Do NOT produce a `chore(release):` commit — that's Versionize's output during the release run, not part of feature work.

### 5. Run the full test suite one more time

```bash
dotnet build
dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj
```

If anything fails, fix it in a new commit on the branch — do not amend earlier commits.

### 6. Open the PR — Versionize-format title

The PR title MUST follow the Versionize / Conventional Commits shape defined in [SKILL.md](SKILL.md#commit-format-versionize--conventional-commits). Versionize or a squash-merge will use this title as the commit summary, so the version bump depends on it being correct.

**Title rules**:

- `<type>(<scope>): <summary>` or `<type>: <summary>`
- The type is the single most representative change across the PR:
  - If the PR introduces a new user-facing capability → `feat`
  - If the PR corrects defective behavior → `fix`
  - If the PR is purely internal (refactor/tests/packaging) → pick the matching type (`refactor`, `test`, `build`, `ci`, `chore`)
- Scope matches the control or subsystem (see SKILL.md list)
- Breaking changes: `<type>(<scope>)!:` AND include a `BREAKING CHANGE:` section in the body

```bash
git push -u origin <branch>
gh pr create --title "feat(slider): add Slider control with min/max/step" --body "$(cat <<'EOF'
## Summary

<2-4 bullets from plan.md>

## Acceptance criteria

- [x] <from intake.md>
- [x] <...>

## Samples

- <list of sample files added/updated>

## Test plan

- [ ] `dotnet build` — clean
- [ ] `dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj` — green
- [ ] Run sample app, navigate to new/updated screen, exercise every interaction
- [ ] Theme-switch while the sample is visible (Dark / Dracula / GruvboxDark)

## Reviews

- [Architecture review](../blob/<branch>/plans/<slug>/review-arch.md)
- [NativeAOT review](../blob/<branch>/plans/<slug>/review-aot.md)
- [Security review](../blob/<branch>/plans/<slug>/review-security.md)

## Release impact

<!-- How Versionize will treat this PR once merged: -->
- Bump: **minor** (feat) | **patch** (fix / perf) | **none** (refactor / docs / test / build / ci / chore)
- Breaking change: **no** | **yes** — see BREAKING CHANGE section below
- Changelog entry: <the PR title, verbatim>

<!-- If this is a breaking change, include a BREAKING CHANGE section: -->
<!--
## BREAKING CHANGE

<describe the break + migration steps>
-->

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Example titles** (match the shape Versionize expects):

- `feat(slider): add Slider control with min/max/step`
- `fix(textbox): keep selection anchor stable when content overflows`
- `refactor(databinding): collapse BindingExpression resolve paths`
- `build(packaging): emit XML docs and symbol package`
- `feat(databinding)!: rename BindingMode.Default to BindingMode.OneWay` (breaking)

### 7. Hand off

Give the user the PR URL. Do NOT merge. Do NOT comment on review feedback without explicit user direction.

## Guardrails

- **Do not skip the sample gate.** If you're tempted to argue "this feature doesn't need a sample", re-read the rule. Every feature ships a sample. Every fix updates the affected samples. If a change genuinely has no user-visible surface, surface it to the user and ask them to confirm the exception in writing (they'll update `intake.md` with the descope).
- **Do not merge, force-push, or close issues** unless the user explicitly asks.
- **Do not `git push --force`** to a feature branch that's already pushed, unless the user explicitly asks and understands why.
- **Do not leave `plans/<slug>/` or `learnings/<subject>/` uncommitted** — they are part of the PR.
