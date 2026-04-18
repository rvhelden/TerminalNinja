# Phase 2 — Parallel reviews + plan synthesis

Goal: pressure-test the approach from three angles before writing code, synthesize the findings into a concrete plan, and get the user's approval.

## Checklist

### 1. Launch three reviews in parallel

Emit **one message with three `Task` tool calls**. Do not serialize.

Each review gets the contents of `plans/<slug>/intake.md` plus relevant file paths you already know. Each writes a short markdown report with findings + recommendations.

**Review A — NativeAOT review** (subagent_type: `Explore`)

Prompt template:

> Review the proposed change described in `plans/<slug>/intake.md` against TerminalNinja's Native AOT constraints. The library must compile with `IsAotCompatible=true` and has no tolerance for:
> - `Activator.CreateInstance`, `Type.GetType(string)`, `Assembly.Load`
> - `System.Reflection.Emit` or expression trees
> - Unconstrained generics that require runtime type metadata
> - LINQ providers that emit IL at runtime
> Expected patterns for new types: source generators (`TerminalNinja.Generators`) auto-discover controls and emit property accessors / control factories. Theme/resource XAML is embedded and loaded via generated registries.
> Output a short markdown report to `plans/<slug>/review-aot.md` with: (1) AOT risks in the proposed approach, (2) specific TerminalNinja generator hooks this must use, (3) any API the plan should avoid, (4) open questions for the user.

**Review B — Security review** (subagent_type: `Explore`, or invoke the `/security-review` bundled skill if available)

Prompt template:

> Review the proposed change described in `plans/<slug>/intake.md` for security concerns relevant to a terminal UI library distributed as a NuGet package. Look for:
> - Input handling that could escape VT sequences, inject ANSI, or manipulate the terminal state outside the app's surface
> - File I/O that resolves paths from untrusted input (FilePicker / FolderPicker)
> - Clipboard access that could leak or be spoofed
> - Deserialization of untrusted XAML (TerminalXaml.Parse from user-supplied strings)
> - Any new dependency added (NuGet reference) — check for maintenance / provenance
> Output `plans/<slug>/review-security.md` with: (1) concrete threats, (2) mitigations, (3) APIs that must sanitize input, (4) open questions.

**Review C — Architecture review** (subagent_type: `Plan`)

Prompt template:

> Review the proposed change described in `plans/<slug>/intake.md` against TerminalNinja's architecture and patterns (source of truth: `CLAUDE.md` and `AGENTS.md`). Specifically verify:
> - Correct base class choice (`Control` vs `FrameworkElement` vs `ContentControl` vs `Panel` vs `ItemsControl` vs `Selector` vs `ButtonBase`)
> - DependencyProperty pattern: metadata (FrameworkPropertyMetadata vs PropertyMetadata), `affectsRender`, callbacks, coercion
> - `SetValue` vs `SetValueInternal` choice (internal DP writes in binding-aware controls MUST use `SetValueInternal`)
> - Theming checklist compliance (ThemeResourceKeys.cs, all 3 theme files, implicit style)
> - Sample & docs checklist (`Sample/Samples/<Name>/`, `ShellViewModel`, `docs/samples/<name>.html`, `samples.js`, `index.html` card)
> - Attached properties + `[ContentProperty]` / `[RuntimeNameProperty]` attributes where relevant
> - ISelectableContainer interface for selection-capable container items
> - Test patterns (TUnit async, `CellBuffer` + `Render` for rendering tests)
> Output `plans/<slug>/review-arch.md` with: (1) pattern violations or gaps, (2) files to add/modify with paths, (3) task decomposition suggestion, (4) open questions.

### 2. Resolve open questions

If any review raises an open question (likely), use `AskUserQuestion` to resolve before synthesizing. Do not guess.

### 3. Synthesize `plans/<slug>/plan.md`

Combine the three reviews plus the intake into a single, scannable plan. Required sections:

```markdown
# Plan — <slug>

## Context
<why, from intake>

## Approach
<one paragraph of the chosen approach, reconciling AOT/security/arch reviews>

## Tasks

### Task 1 — <name>
- Files: <paths>
- Tests: <what to test, where>
- Notes: <anything review-specific, e.g. "use SetValueInternal for Text DP">

### Task 2 — <name>
...

## Out of scope
<from intake>

## Verification
<how we prove it works end-to-end — dotnet test filter, sample screen interaction, etc.>

## Review summaries
- **AOT**: <one-line summary + link to review-aot.md>
- **Security**: <one-line summary + link to review-security.md>
- **Architecture**: <one-line summary + link to review-arch.md>
```

Task decomposition rule: each task must be small enough that its test + implementation + 5-review cycle fits one reasonable commit. Typical feature: 3–6 tasks. A one-line bug fix may be 1 task.

### 4. Mandatory user approval gate

Tell the user: "Plan is at `plans/<slug>/plan.md`. Please review and edit directly — I'll re-read it before starting Phase 3."

Then stop. Do NOT proceed until the user confirms. Accept any edits they make to the file verbatim — re-read it on continuation and use the edited version as the source of truth for Phase 3.

## Guardrails

- No code edits in Phase 2. Only `plans/<slug>/*.md`.
- If a review surfaces a fundamental blocker (e.g. "this approach can't be AOT-safe"), stop and tell the user. Do not synthesize a plan around an approach you already know is wrong.
- The three reviews must be issued in a single tool-call message for true parallelism. Check your output before sending: three `Task` calls, not one-after-another.
