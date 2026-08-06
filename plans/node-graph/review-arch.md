# Architecture Review — node-graph

_Captured by orchestrator on behalf of the read-only review agent._

## Verdict

Architecturally sound. `ChartBase` is the correct base class — it already supplies `Focusable`, `DefaultStyleKey`, `Width/Height/Title/Foreground/Background`, palette (`ColorForSeries`), `RebindCollection`/`OnDataCollectionChanged`, `EffectiveSelectionBackground`/`Dim`, `DrawString`, `FillBackground`, `GetPreferredSize`/`CalculateBounds`. `NodeGraph` overrides only `OnRender`, `OnKeyEvent`, `OnMouseEvent` — **never `Render`** (base `UIElement.Render` gates visibility then calls abstract `OnRender`).

Copy **`TraceChart`** (not `FlameGraph`) for selection: it has the full `SelectedIndex`↔`SelectedSpan` pair with the `_syncing` guard + `SetValueInternal`. Template: `TraceChart.cs:122-163`.

## 1. Pattern rules to follow / violations to avoid

1. Override `OnRender(CellBuffer, Rect)`; start with `CalculateBounds(parent).Intersect(buffer bounds)` then `FillBackground`. Never override `Render`.
2. Use `SetValueInternal` (not `SetValue`) inside the selection-sync callbacks, or a user's `{Binding SelectedNode}` is destroyed on first selection move.
3. Guard both sync callbacks with `_syncing` early-return.
4. **Deterministic layout** — no `Math.Random`/`DateTime`/`Guid`. Seed initial positions from node index (circular: `angle = 2π·i/N`), fixed iteration count. Cache the layout; recompute only when node/edge set or bounds size changes (not every `affectsRender` repaint).
5. Capture geometry during `OnRender` into a private `List<NodeEntry(GraphNode, int Index, Rect Box)>` (cleared at top of `OnRender`) for hit-testing — like `FlameGraph._frames` / `TraceChart._rowTop/_rowStride`. Input handlers read captured entries; they do NOT recompute layout.
6. Register `GraphNode` + `GraphEdge` in `ControlFactoryGenerator.AdditionalFactoryTypes` (`ControlFactoryGenerator.cs:29-46`, alongside `TraceSpan`/`FlameNode`).
7. POCO shape mirrors `FlameNode`/`TraceSpan`: `[BindableObject]` + `[ContentProperty]`, plain auto-props, `Color` default `Color.Transparent`, collections `{ get; } = [];`. NOT `DependencyObject`s.
8. `GraphEdge` references nodes by identity (string `From`/`To` ids referencing `GraphNode.Id`) — no manual coordinates in MVP. String ids avoid inline-XAML forward-reference problems.
9. Edges via `BrailleCanvas`: one `BrailleCanvas(bounds.W, bounds.H)` per render, `Blit` at `(bounds.X, bounds.Y)` (Blit forces transparent bg). Draw edges BEFORE node boxes so boxes overpaint line ends.
10. Theme parity is test-enforced: `ThemeTests.BuiltInTheme_ContainsImplicitStyles` (`ThemeTests.cs:298-325`) checks each chart style per concrete type — add a `NodeGraph` assertion + `<Style TargetType="NodeGraph">` to all 3 theme files.
11. Chart `<Style>` blocks do NOT set `SelectedBackground/SelectedForeground` — those come from `ChartBase` DP defaults. NodeGraph style sets only `Foreground/Background/AxisColor/GridColor/LegendColor`.

## 2. DependencyProperty set

Inherited (do not redeclare): `Width, Height, Title, ShowLegend, ShowAxes, ShowGrid, Foreground, Background, AxisColor, GridColor, LegendColor, SeriesPalette, SelectedBackground, SelectedForeground`.

New:

| Member | Type | Kind / Metadata |
|---|---|---|
| `GraphNodes` | `IList<GraphNode>` | **Not a DP** — `ObservableCollection`-backed `IList` wired to `OnDataCollectionChanged` in ctor (mirror `TraceChart.Spans`). `[ContentProperty("GraphNodes")]`. |
| `GraphNodesSource` | `IEnumerable` | DP: `FrameworkPropertyMetadata(null, affectsRender:true, changed → RebindCollection)`. |
| `GraphEdges` | `IList<GraphEdge>` | Not a DP — same `ObservableCollection` pattern. Set via `<NodeGraph.GraphEdges>` property element in XAML. |
| `GraphEdgesSource` | `IEnumerable` | DP: same as `GraphNodesSource`. |
| `SelectedIndex` | `int` | DP: `FrameworkPropertyMetadata(-1, affectsRender:true, changed → OnSelectedIndexChanged){ BindsTwoWayByDefault = true }`. |
| `SelectedNode` | `object` | DP: `FrameworkPropertyMetadata(null, affectsRender:true, changed → OnSelectedNodeChanged){ BindsTwoWayByDefault = true }`. |
| `LayoutIterations` | `int` | DP: `FrameworkPropertyMetadata(default, affectsRender:true)` + coerce clamp `[1, maxCap]`. |

Layout tuning for MVP = **just `LayoutIterations`**. Keep spring/repulsion constants as `private const`; promote to DPs later only if needed. `[ContentProperty]` = `GraphNodes`; edges via property element.

## 3. Where `SetValueInternal` is used

Only in the two sync callbacks, both `_syncing`-guarded (verbatim shape from `TraceChart.cs:122-163`):
- `OnSelectedIndexChanged` → `SetValueInternal(SelectedNodeProperty, indexInRange ? nodes[index] : null)`.
- `OnSelectedNodeChanged` → resolve node's index, `SetValueInternal(SelectedIndexProperty, index)`.

Key/mouse handlers assign the **public** `SelectedIndex`/`SelectedNode` setters (route through `SetValue`, fire callback) — same as `TraceChart.OnKeyEvent`/`OnMouseEvent`.

## 4. Theming

No new `ThemeResourceKeys` constants — reuse existing `ThemeChart*` keys (`ThemeResourceKeys.cs:252-269`). Node fill/edge colors come from the palette + selection colors (already have `ChartBase` defaults). Add to each of `Dark.xaml`, `Dracula.xaml`, `GruvboxDark.xaml`:

```xml
<Style TargetType="NodeGraph">
    <Setter Property="Foreground"  Value="{StaticResource ThemeChartForegroundColor}" />
    <Setter Property="Background"  Value="{StaticResource ThemeChartBackgroundColor}" />
    <Setter Property="AxisColor"   Value="{StaticResource ThemeChartAxisColor}" />
    <Setter Property="GridColor"   Value="{StaticResource ThemeChartGridColor}" />
    <Setter Property="LegendColor" Value="{StaticResource ThemeChartLegendColor}" />
</Style>
```

Then extend `ThemeTests.BuiltInTheme_ContainsImplicitStyles` with a per-type `NodeGraph` check (no numeric count to bump — the CLAUDE.md "update count" wording is stale; the test is per-type).

## 5. Sample & Docs file list

- **Add** `Sample/Samples/NodeGraph/NodeGraphScreen.xaml` (auto-discovered → `XamlLayouts.NodeGraphScreen`). ViewModel only if it binds a source; inline `<GraphNode>`/`<GraphEdge>` needs no VM.
- **Modify** `Sample/Samples/MainMenu/MainMenuViewModel.cs` — add `"Node Graph"` to Samples list (after `"Flame Graph"`).
- **Modify** `Sample/ShellViewModel.cs` — add `"Node Graph" => TerminalXaml.Load<Border>(XamlLayouts.NodeGraphScreen),` to `NavigateToSample`.
- **Add** `docs/samples/nodegraph.html` (Overview, Properties, Examples, Keyboard Shortcuts, Key Concepts) — clone `flamegraph.html`.
- **Modify** `docs/samples.js` — add SAMPLES entry (playground); `<Window>` with NO Width/Height.
- **Modify** `docs/index.html` — add `<a class="sample-card">` to grid.

## 6. Suggested task decomposition

1. `feat(charts): add GraphNode/GraphEdge data models` + register in `AdditionalFactoryTypes`. Test: `TerminalXaml.Load` a small graph, assert counts + `Color` override parse.
2. `feat(nodegraph): add NodeGraph control with deterministic force layout` — control, DPs, `OnRender` (node boxes + Braille edges), seeded FR layout with cache. Test: `CellBuffer`+`Render`, stable glyph coords for a fixed 3-node graph; two renders byte-identical.
3. `feat(nodegraph): add selection + keyboard/mouse navigation` — geometry capture, hit-test, arrow nav, `SelectedIndex`↔`SelectedNode` sync. Test: click selects right node; binding survives.
4. `feat(theming): theme NodeGraph across Dark/Dracula/GruvboxDark` — 3 styles + extend theme test.
5. `docs(nodegraph): add sample screen, docs page, and playground entry` — sample + wiring + html + samples.js + index.html card.

## 7. Test patterns

TUnit async `Method_Scenario_Expected` with `await Assert.That(...)`, under `TerminalNinja.Tests/Unit/`. Render tests via `new CellBuffer(w,h)` + `Render`. Determinism: two consecutive renders byte-identical, tiny fixed graphs, fixed `LayoutIterations`. XAML/POCO via `TerminalXaml.Load`. Two-way sync test binds a VM to `SelectedNode`, drives `SelectedIndex`, asserts VM updated + binding still live. Focus-dependent tests use `new Application(new ApplicationOptions { Headless = true })`.

## 8. Open questions (routed to user)

1. Edge endpoint identity: string `From`/`To` ids referencing a new `GraphNode.Id` (recommended) vs object refs.
2. Directed edges: draw an arrowhead glyph at the target vs plain lines for MVP.
3. Node box sizing: fixed 3-row box (border/label/border), width = label+padding clamped to available; `DrawString` ellipsis truncation. (Defaulting unless user objects.)
4. Layout: normalized unit-space layout, reproject to cells on resize (recommended) vs recompute in cell space.
5. Overlap: MVP accepts possible box overlap on dense graphs (legibility-only requirement).
6. Large-graph cap behavior (from security review): cap laid-out nodes and choose truncate-with-notice vs cheap-fallback-layout.
