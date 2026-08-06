# Plan — node-graph

## Context

Add a `NodeGraph` chart control to visualize **topologies / networks** (arbitrary relationships) in the terminal. Auto-layout (force-directed) so callers supply only nodes + edges, no coordinates. MVP-first: minimal but legible and interactive, then iterate. User explicitly requires the full docs + sample + playground surface. Working on the `main` branch per the user's choice (flag before first commit).

## Approach

`NodeGraph : ChartBase` (in `TerminalNinja/Controls/Charts/`). Reuse `ChartBase` chrome (title/palette/colors), collection rebinding (`RebindCollection`/`OnDataCollectionChanged`), selection colors, and `DrawString`/`FillBackground`. Override only `OnRender`, `OnKeyEvent`, `OnMouseEvent` — never `Render`.

Data: two `[BindableObject]` + `[ContentProperty]` POCOs, `GraphNode` (`Id`, `Name`, `Value`, `Color`) and `GraphEdge` (`From`, `To`, `Color`), registered in `ControlFactoryGenerator.AdditionalFactoryTypes`. Edges reference nodes by **string `Id`** (`From`/`To`); unknown ids are silently ignored.

Layout: deterministic Fruchterman-Reingold. Seed initial positions from node index (circular: `angle = 2π·i/N`) — no `Math.Random`/`DateTime`. Run in a normalized unit space, fixed `LayoutIterations` (coerced/clamped), with convergence early-exit; **cache** the result and recompute only when the node/edge set or bounds size changes. Reproject normalized coords → cell grid on render. **Cap**: above `MaxLayoutNodes` (~500) render the first N with full layout and draw a `"… N more"` truncation notice (chosen behavior).

Rendering: draw edges first via one `BrailleCanvas` (2×4 Bresenham lines), `Blit` with transparent bg; then draw node boxes (`BorderChars`, 3-row: top border / label / bottom border, width = label + padding clamped to available, `DrawString` ellipsis truncation) so boxes overpaint line ends. **Plain lines, no arrowheads** in MVP. Capture each node's `Rect` into a private `List<NodeEntry>` (cleared at top of `OnRender`) for hit-testing.

Selection: copy `TraceChart`'s `SelectedIndex`↔`SelectedNode` pair — two-way DPs, `_syncing`-guarded callbacks writing the paired property via `SetValueInternal`. `OnMouseEvent` left-click hit-tests captured boxes; `OnKeyEvent` arrows move selection (by index; spatially-nearest is a later iteration — MVP: next/prev index on Left/Up, Down/Right, with Home/End). Public setters used in input handlers; `SetValueInternal` only in the sync callbacks.

Theming reuses existing `ThemeChart*` keys (no new constants) — add `<Style TargetType="NodeGraph">` to all 3 theme files and extend the per-type theme test.

## Decisions (resolved with user)

- Edge endpoints: **string `Id`s** (`GraphNode.Id`; `GraphEdge.From`/`To`). Unknown ids ignored.
- Direction indicator: **plain lines only** in MVP (no arrowheads).
- Large graphs: **cap + truncation notice** — full layout for first ~500 nodes, `"… N more"` notice for the rest.
- Node box: fixed 3-row box, width = label + padding, clamped, ellipsis truncation. (Default.)
- Layout space: **normalized** unit space, reproject to cells on resize. (Default.)
- Overlap: acceptable for MVP (legibility-only requirement).

## Amendments (from user-approved plan analysis, 2026-08-06)

- **Edge colors**: `BrailleCanvas.Blit` takes ONE foreground color per blit — bucket edges by effective color (default `AxisColor` when `GraphEdge.Color` is `Transparent`), one canvas per bucket.
- **`[ContentProperty]`**: `GraphNode` uses `[ContentProperty("Name")]` (it has no children — do not copy the siblings' `"Children"`); `GraphEdge` has no content property.
- **Pinned caps**: `LayoutIterations` default **60**, coerce-clamped **[1, 200]**; `MaxLayoutNodes` = **500** (`private const`).
- **Inherited chrome**: `ShowAxes`/`ShowGrid`/`ShowLegend` are ignored by `OnRender` (meaningless for a node graph); docs page must say so.
- **Empty state**: draw `"(no nodes)"` when the effective node list is empty (mirrors `TraceChart`'s `"(no spans)"`).

## Tasks

### Task 1 — GraphNode / GraphEdge data models
- Files: `TerminalNinja/Controls/Charts/GraphNode.cs`, `TerminalNinja/Controls/Charts/GraphEdge.cs`; `TerminalNinja.Generators/ControlFactoryGenerator.cs` (add `"GraphNode"`, `"GraphEdge"` to `AdditionalFactoryTypes`).
- POCO shape: `[BindableObject]`; `GraphNode` `[ContentProperty("Name")]`-style like siblings, props `Id` (string), `Name` (string), `Value` (double), `Color` (default `Color.Transparent`). `GraphEdge` props `From` (string), `To` (string), `Color`. XML docs note labels should be printable text.
- Tests (`TerminalNinja.Tests/Unit/`): `TerminalXaml.Load` a small graph XAML; assert node/edge counts and a `Color` override parses via `ColorTypeConverter`.
- Commit: `feat(charts): add GraphNode and GraphEdge data models`

### Task 2 — NodeGraph control + deterministic force layout
- Files: `TerminalNinja/Controls/Charts/NodeGraph.cs`.
- DPs: `GraphNodesSource`/`GraphEdgesSource` (IEnumerable, affectsRender, changed→`RebindCollection`); `LayoutIterations` (int, affectsRender, coerce clamp `[1, MaxIterations]`). Inline `GraphNodes`/`GraphEdges` = `ObservableCollection`-backed `IList` wired to `OnDataCollectionChanged` in ctor (mirror `TraceChart.Spans`). `[ContentProperty("GraphNodes")]`; edges via `<NodeGraph.GraphEdges>` element.
- `OnRender`: `CalculateBounds(parent).Intersect(buffer)` → `FillBackground` → (re)compute+cache layout → draw edges (BrailleCanvas) → draw node boxes → truncation notice if capped. Deterministic seeding; convergence early-exit; caps (`MaxLayoutNodes`, `MaxIterations` as `private const`).
- Tests: `CellBuffer`+`Render` — box glyphs at stable cells for a fixed 3-node graph; **two consecutive renders byte-identical** (determinism); capped graph shows notice.
- Commit: `feat(nodegraph): add NodeGraph control with deterministic force layout`

### Task 3 — Selection + keyboard/mouse navigation
- Files: `TerminalNinja/Controls/Charts/NodeGraph.cs`.
- `SelectedIndex` (int, -1, two-way, `OnSelectedIndexChanged`) + `SelectedNode` (object, null, two-way, `OnSelectedNodeChanged`), `_syncing` guard, paired writes via `SetValueInternal`. Capture `List<NodeEntry(GraphNode, int Index, Rect Box)>` in `OnRender`. `OnMouseEvent`: left-press hit-test boxes → set public `SelectedIndex`. `OnKeyEvent`: Left/Up = prev, Right/Down = next, Home/End = first/last (index-based), clamp.
- Tests: click at a known box coord selects the right node; setting `SelectedIndex` updates `SelectedNode` and vice-versa; a two-way `{Binding SelectedNode}` survives repeated selection moves (guards the `SetValueInternal` requirement). Focus-dependent assertions use `new Application(new ApplicationOptions { Headless = true })`.
- Commit: `feat(nodegraph): add selection with keyboard and mouse navigation`

### Task 4 — Theming across all 3 themes
- Files: `TerminalNinja/Themes/Dark.xaml`, `Dracula.xaml`, `GruvboxDark.xaml` (add `<Style TargetType="NodeGraph">` with `Foreground/Background/AxisColor/GridColor/LegendColor` → existing `ThemeChart*` keys); `TerminalNinja.Tests/Xaml/ThemeTests.cs` (extend `BuiltInTheme_ContainsImplicitStyles` with a per-type `NodeGraph` check).
- Tests: the extended theme test passes for all 3 themes.
- Commit: `feat(theming): add NodeGraph implicit style to all themes`

### Task 5 — Sample, docs page, and playground
- Files: `Sample/Samples/NodeGraph/NodeGraphScreen.xaml` (inline nodes+edges, no VM needed); `Sample/Samples/MainMenu/MainMenuViewModel.cs` (add `"Node Graph"`); `Sample/ShellViewModel.cs` (add `NavigateToSample` case); `docs/samples/nodegraph.html` (Overview, Properties, Examples, Keyboard Shortcuts, Key Concepts — clone `flamegraph.html`); `docs/samples.js` (SAMPLES entry — playground, `<Window>` with NO Width/Height); `docs/index.html` (sample-card in grid).
- Verification: `dotnet build`; `dotnet run --project Sample/Sample.csproj` → Node Graph screen renders and is navigable.
- Commit: `docs(nodegraph): add sample screen, docs page, and playground entry`

## Out of scope (MVP)

- Orthogonal edge routing / obstacle avoidance (direct lines only).
- Arrowheads / direction glyphs (deferred).
- Manual node positioning; edge labels.
- Advanced force tuning DPs (spring/repulsion) beyond `LayoutIterations`.
- Spatially-aware (nearest-neighbor) keyboard navigation — MVP uses index order.

## Verification

- `dotnet run --project TerminalNinja.Tests/TerminalNinja.Tests.csproj` (all green, incl. new NodeGraph + theme tests).
- Determinism: two renders of the same graph produce byte-identical buffers.
- `dotnet build` succeeds (AOT-compatible; no reflection introduced).
- `dotnet run --project Sample/Sample.csproj` → Node Graph screen renders, click + arrow selection works.
- Docs playground entry parses (POCOs registered in the factory).

## Review summaries

- **AOT**: Fully compliant — POCOs auto-discovered by `PropertyAccessorGenerator`, control auto-registered by inheritance; only action is adding the two strings to `ControlFactoryGenerator.AdditionalFactoryTypes`. Pure-arithmetic layout, no reflection. → `review-aot.md`
- **Security**: Approved with conditions — ANSI injection LOW (cell-by-cell render), zero new deps; MEDIUM O(n²) DoS risk mitigated by node/iteration caps + convergence early-exit (implemented via cap + truncation notice). → `review-security.md`
- **Architecture**: `ChartBase` correct; copy `TraceChart` selection (index+object, `_syncing`+`SetValueInternal`); never override `Render`; deterministic cached layout; reuse `ThemeChart*` keys + extend per-type theme test. → `review-arch.md`
