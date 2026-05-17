# Intake — ninjashell-streaming

**Branch**: feature/ninjashell-streaming
**Kind**: feature
**Scope**: TerminalNinja.Shell — lazy pipelines (NinjaShell-side only)
**Date**: 2026-05-17

## User ask (verbatim)

> Make pipelines lazy so that `1..1_000_000 | take(3) | fold(...)` doesn't materialize the full range. NinjaShell-side only — pwsh bridge stays eager (NDJSON streaming is a follow-up).

## Scope (locked)

- **Lazy sources**: `lo..hi` evaluates to an `NSeq` backed by a yield-generator (the int.MaxValue range-size cap is dropped).
- **Lazy transformers**: `where`, `select`, `take`, `skip`, `head` return `NSeq` for any iterable input (NList or NSeq). They never materialize.
- **Sinks (materialize)**: `count`, `fold`, `sort`, `distinct`, `tail`, `each`. These consume the full pipeline.
- **New builtin**: `materialize(seq)` for explicit conversion `NSeq → NList`.
- **Display**: NSeq renders as a list when interpolated / printed. The Printer materialises to do so (acceptable — display is a sink).

## Acceptance criteria

- [ ] `1..1_000_000_000 | take(3) | count` completes in <1 s (it would OOM if eager — currently it does).
- [ ] `1..5 | fold(0, (acc, x) => acc + x) == 15` still works (sink consumes lazy seq).
- [ ] `[1, 2, 3, 4] | where(x => x > 1) | select(x => x * 2)` returns `[4, 6, 8]` (lazy chain → NSeq → consumed by print).
- [ ] `materialize(1..3)` returns an `NList` of `[1, 2, 3]`.
- [ ] All 1755 existing tests stay green; new tests cover the laziness contract.
- [ ] AOT publish stays warning-free.

## Out of scope

- pwsh bridge streaming (NDJSON, subprocess cancellation on `take`) — separate PR.
- `Sample/Samples/Shell/` and `docs/samples/shell.html` — still deferred pending TerminalView hosting.
- Multi-pass NSeq guarantees (NSeq is yield-backed, which is re-enumerable but recomputes the chain per pass; we document this rather than special-casing it).
- Async streams, IAsyncEnumerable.

## Breaking change risk

**Low but non-zero.** The result type of `where`/`select`/`take`/`skip` changes from NList to NSeq; the result type of a range literal changes from NList to NSeq. NinjaShell user code is unaffected (these types are interchangeable for all user-visible operations including pipe composition, display, and interpolation). The existing C#-side test code that asserts `v is NList` after a range/pipeline op needs updating — handled in this PR.

## Notes

- NSeq backed by `IEnumerable<NValue>` (yield methods). Each `GetEnumerator` call returns a fresh walker, so the value is safely re-iterable; the cost is that the upstream chain is recomputed per pass. Document this in the NSeq XML doc comment.
- Approach informed by F#'s `seq` and C#'s LINQ-to-objects — same shape, different syntax.
