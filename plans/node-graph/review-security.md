# Security Review — node-graph

_Captured by orchestrator on behalf of the read-only review agent._

## Verdict: APPROVED with conditions (no blockers)

## 1. Concrete threats

| # | Threat | Severity | Notes |
|---|--------|----------|-------|
| T1 | **ANSI / VT escape injection** via node/edge labels | LOW | Rendering is cell-by-cell: each label codepoint is written to a `Cell` and emitted by `AnsiWriter` as an individual glyph, never re-interpreted as an escape sequence. A `\x1b` in a label lands in one cell as a codepoint; it does not start an escape sequence at the terminal. Inherited safe behavior — NodeGraph needs no special handling beyond using the existing `DrawString`/`SetChar` path. |
| T2 | **Resource exhaustion / UI hang** from O(n²) force-directed layout | MEDIUM | Each iteration is O(nodes²) for repulsion + O(edges) for attraction. 1,000 nodes × 100 iterations ≈ 100M pair interactions per layout → perceptible hang. No existing chart caps input size. |
| T3 | **Control characters** (`\n`, `\x00`, `\x07` bell) in labels render as literal codepoints | LOW | Terminals generally suppress/ignore non-printing codepoints written as cell content. Accepted for MVP. |
| T4 | New NuGet dependency | NONE | Math is `System.Math` / `System.Numerics` std lib only. Zero new dependency surface, no AOT risk. |
| T5 | Untrusted XAML deserialization | OUT OF SCOPE | New POCOs (`GraphNode`/`GraphEdge`) only add settable string/double/color properties; no widening of the XAML attack surface beyond existing chart POCOs. |

## 2. Mitigations (required before ship)

- **Cap layout work.** Clamp the number of nodes the layout iterates over and the iteration count. Suggested defaults (tunable via DP, but with hard ceilings):
  - Max nodes laid out: ~500 (beyond that, skip layout / render a "too many nodes" state rather than hang).
  - Iterations: default ~30, hard max ~100.
- **Convergence early-exit.** Stop iterating once total node displacement in an iteration falls below an epsilon — avoids running the full iteration budget when the graph has already settled.
- **Determinism.** Seed initial positions from node index (circular placement), not `Math.random` — required for AOT-safe reproducibility, stable tests, and avoids per-render jitter (which is also a mild DoS/UX concern).
- Recompute layout only when the node/edge set changes (or bounds change), not every render — cache positions.

## 3. APIs that must sanitize / bound input

- Layout entry point must enforce the node/iteration caps before the O(n²) loop.
- No label sanitization required (T1/T3 inherited-safe), but document in `GraphNode.Name` XML docs that labels should be printable text.

## 4. Open questions for the user

- OQ-S1: Is capping laid-out nodes at ~500 acceptable for the MVP, or do you need larger graphs (which would require a different, non-O(n²) layout later)?
- OQ-S2: When the node count exceeds the cap, prefer (a) render first N + a truncation notice, or (b) render all nodes but skip force-layout (fall back to a cheap deterministic placement)?
