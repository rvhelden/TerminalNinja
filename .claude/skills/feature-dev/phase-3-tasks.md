# Phase 3 — Task execution loop

Goal: implement the approved plan one task at a time, with tests before code, five parallel reviews after, a user checkpoint, learnings capture, and a commit per task.

## Loop per task

Re-read `plans/<slug>/plan.md` at the top of each iteration — the user may have edited it.

### 1. Implement tests first

- Write the tests the task needs per `CLAUDE.md` testing guidelines:
  - TUnit, `async Task` + `await Assert.That(...)`
  - Naming: `MethodName_Scenario_ExpectedBehavior`
  - Rendering tests: `new CellBuffer(w, h)` + `control.Render(buffer, new Rect(...))`
  - XAML parsing tests: `TerminalXaml.Load<T>(xaml)`
  - Application-dependent: `new Application(new ApplicationOptions { Headless = true })`
- Run the tests — they must FAIL (red) with a clear failure message. If they pass, the test doesn't exercise the new behavior.
  ```bash
  dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj --treenode-filter "/*/*/<TestClass>/*"
  ```

### 2. Implement the task

- Follow `CLAUDE.md` patterns exactly: file-scoped namespace, `_camelCase` fields, DP declarations, `SetValueInternal` for internal DP writes on binding-aware controls, theming checklist if applicable.
- Keep the diff scoped to the task. No drive-by refactors, no "while I'm here" cleanups.

Run the tests again — they must PASS (green). Run the full suite to confirm no regressions:

```bash
dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj
```

### 3. Launch five reviews in parallel

Emit **one message with five `Task` tool calls**. Each review reads the staged diff (or the task's files) and writes findings to a scratch report — no plans/learnings file for these, they're per-task and ephemeral.

- **XMLDocs review** (Explore): every new public type/member has `///` summary; remarks where behavior is non-obvious; `<see cref>` for related types.
- **NativeAOT review** (Explore): no `Activator.CreateInstance`, `Type.GetType`, reflection-emit, expression trees, unconstrained generics. If the task added a new control type, confirm it's picked up by the generators.
- **Security review** (Explore or `/security-review` skill): input sanitization, path handling, clipboard, ANSI escape in user-controlled strings.
- **Code style review** (Explore): file-scoped namespace, `_camelCase` fields, PascalCase properties, `ArgumentNullException.ThrowIfNull()` at boundaries only, no explicit System.* usings, one public type per file.
- **Unused / unnecessary code review** (Explore): dead code, unused parameters, premature abstractions, over-validation of internal-only inputs, comments that narrate what the code does, backwards-compatibility hacks for code that didn't exist before this commit.

Each review responds with a short bullet list of concrete issues + file:line references, OR "no issues found".

### 4. Apply review-dictated modifications

- Fix everything actionable. Do not suppress warnings or disable review findings without a concrete reason.
- Re-run tests after fixes.
- If a review raises a concern you disagree with, note it in the commit body and move on — don't engage in a back-and-forth.

### 5. User checkpoint

Ask the user to review the task's diff before you commit:

> Task N — `<task name>` is implemented. Tests green. Diff: `git diff --stat`. Please review and approve before I commit, or tell me what to change.

Use `AskUserQuestion` with options: `Approve and commit`, `Request changes` (and the user describes what in notes).

### 6. Capture learnings

If the user requested changes or offered correction-level feedback (not just "looks good"), write a learning file **before** the fix commit:

- Path: `learnings/<subject>/<slug>-task-N.md` — subject is typically the control or subsystem name (`button`, `databinding`, `theming`, `dependency-system`).
- Template:
  ```markdown
  ---
  name: <slug> task N
  subject: <subject>
  ---

  **Rule**: <the corrected approach in one sentence>

  **Why**: <the user's reasoning, as they stated it>

  **How to apply**: <when in future work this rule kicks in>
  ```
- If they just approved without comment, no learnings file — skip.

### 7. Commit — Versionize / Conventional Commits format

Follow the [commit format rules in SKILL.md](SKILL.md#commit-format-versionize--conventional-commits) exactly. Versionize reads `type` to compute the next version; wrong type = wrong release.

**Per-task rules**:

- One task = one commit. Tests + implementation + review fixes + learnings file go together.
- The `type` matches the branch kind: `feature/*` branches produce `feat:` commits for user-visible capability, `fix/*` branches produce `fix:`. Internal plumbing (csproj, generators, tests) uses `build:`, `test:`, `refactor:` even on a `feature/*` branch — the type follows the commit's content, not the branch name.
- Use a scope when the change is localized: `feat(slider):`, `fix(textbox):`. Scope list is in `SKILL.md`.
- Breaking changes: `feat(scope)!:` + `BREAKING CHANGE:` footer.
- Stage specific paths, not `git add -A`.
- Use a HEREDOC for multi-line messages so formatting stays correct.

**Example — feature commit with scope**:

```bash
git add TerminalNinja/Controls/Slider.cs TerminalNinja.Tests/Unit/SliderTests.cs learnings/slider/slider-control-task-1.md
git commit -m "$(cat <<'EOF'
feat(slider): add Slider control with min/max/step dependency properties

Covers horizontal orientation only; vertical and step-snap animations
are deferred to a follow-up task. Tests verify value clamping, step
increments, and keyboard navigation (Left/Right/Home/End).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

**Example — fix commit**:

```bash
git commit -m "$(cat <<'EOF'
fix(textbox): keep selection anchor stable when content overflows right edge

Selection was collapsing because the anchor index was re-clamped on every
scroll update. Anchor is now preserved until the user makes a new selection.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

**Example — breaking change**:

```bash
git commit -m "$(cat <<'EOF'
feat(databinding)!: rename BindingMode.Default to BindingMode.OneWay

BREAKING CHANGE: BindingMode.Default is removed. Replace with
BindingMode.OneWay which preserves the previous behavior for all
built-in controls. Update XAML attributes {Binding Mode=Default}
to {Binding Mode=OneWay}.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

Do NOT produce `chore(release):` commits — those are emitted by `versionize run` during the release process, not during feature work.

## Loop continuation

After the commit, re-read `plans/<slug>/plan.md`:
- Are there more unchecked tasks? → next iteration.
- All tasks done? → Phase 4.

## Guardrails

- Never skip tests because a task "feels trivial".
- Never commit failing tests.
- Never bypass the user checkpoint. The user is the final reviewer for every task.
- If a review finds a blocker that invalidates the plan (e.g. the chosen base class is wrong), stop, tell the user, and go back to Phase 2 to amend `plan.md`.
- Never use `git push --force` or `git commit --amend` here — always fresh commits. Per-task history is part of the value.
