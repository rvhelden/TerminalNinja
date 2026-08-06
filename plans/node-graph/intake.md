# Intake — node-graph

**Branch**: main (user chose to work on the current branch; not creating feature/node-graph)
**Kind**: feature
**Scope**: TerminalNinja/Controls/Charts — new `NodeGraph` control + `GraphNode`/`GraphEdge` data models
**Date**: 2026-08-06

## User ask (verbatim)

> would it be possible to make something of a node graph to show topologies and flows?

Refined via intake questions:
- **Use case**: topologies / networks (arbitrary relationships), not strictly DAGs/flows.
- **Layout**: auto-layout (control computes node positions; no manual coordinates required).
- **Ambition**: MVP first — minimal but working, then iterate.

## Acceptance criteria

- [ ] A `NodeGraph : ChartBase` control renders a set of nodes (boxes with labels) and directed edges (connectors) from bound `GraphNodes` / `GraphEdges` collections.
- [ ] Node positions are auto-computed via a force-directed (Fruchterman-Reingold-style) layout and snapped to the cell grid — no manual coordinates required.
- [ ] `GraphNode` and `GraphEdge` are `[BindableObject]` POCOs, XAML-instantiable (registered in the generator's `AdditionalFactoryTypes`).
- [ ] Selection works: click a node and arrow-key navigation, with `SelectedIndex` / `SelectedNode` two-way bindable via `SetValueInternal` (following FlameGraph/TraceChart patterns).
- [ ] Themed across all 3 themes (Dark, Dracula, GruvboxDark), with a sample screen and a docs page.

## Out of scope (MVP)

- Orthogonal edge routing / obstacle avoidance (edges drawn as direct BrailleCanvas lines).
- Advanced force-directed tuning beyond what's needed to be legible.
- Edge labels (deferred — user declined adding to MVP).
- Manual node positioning.

## Breaking change risk

None — purely additive (new control + new POCOs + new theme keys). No public API or existing XAML/theme-key changes.

## Notes

- Inherit from `ChartBase` to reuse chrome (title/legend/palette), binding, focus, and selection infrastructure.
- Edge rendering: `BrailleCanvas` (2×4 sub-cell Bresenham lines) for arbitrary directions; nodes as `BorderChars` boxes.
- Follow the `FlameGraph`/`TraceChart` geometry-capture pattern for hit-testing and the `SetValueInternal` two-way selection sync (guarded by `_syncing`).
- Force layout must be deterministic (no `Math.random`) — seed positions from node index (e.g. circular initial placement) so renders are stable and testable.
- **User explicitly requested (do not skip):** documentation page, sample screen, AND the docs playground. Full Sample & Docs Checklist from CLAUDE.md:
  - `Sample/Samples/NodeGraph/NodeGraphScreen.xaml` (+ ViewModel) → register in `MainMenuViewModel` + `ShellViewModel`.
  - `docs/samples/nodegraph.html` (Overview, Properties, Examples, Keyboard Shortcuts, Key Concepts).
  - `docs/samples.js` SAMPLES entry — this is the interactive **playground** (runs via TerminalNinja.Wasm; no Width/Height on Window).
  - `docs/index.html` sample-card in the grid.
