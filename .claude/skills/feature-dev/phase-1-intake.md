# Phase 1 — Intake

Goal: turn the user's initial ask into a concrete, branched, documented starting point.

## Checklist

### 1. Fill gaps with AskUserQuestion

Ask only what you cannot confidently infer. Typical gaps:

- **Kind**: is this a `feature` (new capability) or a `fix` (correcting existing behavior)?
- **Scope**: which control or subsystem? (Button, DataGrid, Binding system, theming, etc.)
- **Acceptance criteria**: how will we know this is done? List 2–4 observable behaviors.
- **Out of scope**: anything adjacent we explicitly should NOT touch.
- **Breaking change risk**: does this change public API, XAML syntax, or theme keys? If yes, is that acceptable?
- **Priority signal**: "urgent for a demo", "no rush, let's do it right", etc. — affects how aggressive we are with refactoring.

Use `AskUserQuestion` with 2–4 questions at a time. Don't interrogate.

### 2. Generate and confirm the slug

- Derive from the scope + acceptance criteria. Examples:
  - "Add a Slider control with min/max/step" → `slider-control`
  - "Fix TextBox selection when text overflows right edge" → `textbox-selection-overflow`
- Show the user the final branch name (`feature/<slug>` or `fix/<slug>`) and ask for confirmation before creating the branch.

### 3. Create the branch

```bash
git checkout main
git pull --ff-only
git checkout -b feature/<slug>    # or fix/<slug>
```

If `main` has local uncommitted changes, stop and ask the user how to proceed — do NOT stash or discard.

### 4. Seed `plans/<slug>/intake.md`

Write the answers and acceptance criteria to `plans/<slug>/intake.md`. Template:

```markdown
# Intake — <slug>

**Branch**: feature/<slug> (or fix/<slug>)
**Kind**: feature | fix
**Scope**: <control / subsystem>
**Date**: <YYYY-MM-DD>

## User ask (verbatim)

> <the user's original message>

## Acceptance criteria

- [ ] <criterion 1>
- [ ] <criterion 2>
- [ ] ...

## Out of scope

- <thing 1 we explicitly won't touch>

## Breaking change risk

<none | public API change in X | XAML syntax change | theme key rename>

## Notes

<any other context the user gave — e.g. related issue numbers, linked screens>
```

### 5. Brief the user and hand off to Phase 2

Tell the user you've created the branch and the intake file, and you're about to kick off the three parallel reviews. Do NOT ask for approval here — the approval gate is after Phase 2 synthesizes the plan.

## Guardrails

- Don't start implementing. No code edits, no new files outside `plans/<slug>/`.
- Don't create the sample screen yet — the plan hasn't been reviewed.
- If the user's ask is ambiguous enough that you'd be guessing, ask another `AskUserQuestion` round rather than proceeding.
- If the user changes their mind about scope mid-intake, update `intake.md` and re-confirm the slug.
