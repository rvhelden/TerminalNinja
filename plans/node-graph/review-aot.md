# AOT Compliance Review: NodeGraph Feature

**Status**: COMPLIANT — No AOT risks identified. The proposed feature follows established TerminalNinja patterns.

---

## 1. AOT Risks: None Identified

The NodeGraph design poses **no Native AOT concerns**:

- **GraphNode/GraphEdge POCOs**: Both marked `[BindableObject]` (following FlameNode/TraceSpan pattern).
  - No runtime type discovery, no `Activator.CreateInstance`, no `Type.GetType(string)`.
  - Property accessors auto-generated at compile-time by `PropertyAccessorGenerator`.

- **Force-directed layout math**: Pure arithmetic (`Math.Sqrt`, loop iterations, distance calculations).
  - No reflection, no dynamic dispatch, no LINQ providers that emit IL.
  - Deterministic (seeded from node index; no `Math.Random`).

- **ChartBase inheritance**: NodeGraph inherits from ChartBase like BarChart/LineChart/FlameGraph/TraceChart.
  - All charts auto-discovered as IControl implementors via generator's type-walks.
  - No manual registration needed.

- **Collection binding (GraphNodesSource / GraphEdgesSource)**: Identical to TraceChart's SpansSource pattern.
  - Both typed as `IEnumerable` bound DP.
  - Chart.RebindCollection() subscribes to INotifyCollectionChanged at runtime (safe for AOT).
  - Flattening/enumeration uses strongly-typed Enumerate<T>() helper (no reflection).

---

## 2. Generator Hooks This Must Use

### PropertyAccessorGenerator
**File**: `/home/ronaldvanhelden/Projects/TerminalNinja/TerminalNinja.Generators/PropertyAccessorGenerator.cs` (lines 18–62)

**What happens**:
- Scans all class declarations for `[BindableObject]` attribute via `GeneratorHelper.IsBindableType()` (line 59).
- Collects all public instance properties on GraphNode/GraphEdge.
- Generates static lambdas in `PropertyAccessors.sbn` template (line 248) that read/write each property.
- Emits `PropertyAccessorRegistry.Register(typeof(GraphNode), "Name", new PropertyAccessor(...))` etc.

**Action needed**: None. Once GraphNode/GraphEdge are marked `[BindableObject]`, they are auto-discovered.

---

### ControlFactoryGenerator
**File**: `/home/ronaldvanhelden/Projects/TerminalNinja/TerminalNinja.Generators/ControlFactoryGenerator.cs` (lines 29–46)

**What happens**:
- NodeGraph inherits from ChartBase → implements IControl (implicitly, via control chain).
- Generator's `GetFactoryType()` at lines 79–107 recognizes it as a target type.
- Emits `ControlFactoryRegistry.Register(typeof(NodeGraph), () => new NodeGraph())` in `ControlFactory.sbn` template (line 19).
- Also emits `TypeNameRegistry.Register(typeof(NodeGraph))` for XAML type resolution (line 20).

**Action needed for GraphNode/GraphEdge**:
- **Add both to `AdditionalFactoryTypes` set** (line 29–46, currently 42 entries including ChartSeries, TraceSpan, FlameNode).
- This ensures they get factory registrations so XAML `<NodeGraph.GraphNodesSource>` inline declarations work.

**Exact edit**:
```csharp
// Line 42–46 in ControlFactoryGenerator.cs
"FlameNode",
"GraphNode",      // ← ADD
"GraphEdge"       // ← ADD
```

---

## 3. APIs to Avoid

None flagged. The plan is safe:
- ✅ Use SetValueInternal() for two-way SelectedNode sync (follows FlameGraph pattern, line 22 intake).
- ✅ Use BrailleCanvas for edge rendering (pure geometry, no IL emission).
- ✅ Use RebindCollection() from ChartBase for IEnumerable binding (line 242–255 ChartBase.cs).
- ✅ Use Enumerate<T>() to safely cast collection items (line 333–347 ChartBase.cs).

---

## 4. Open Questions

1. **Will the force-directed layout use any external math library** (e.g. SciSharp.Numeric)?
   - Confirm it's pure C# without P/Invoke or Reflection.Emit.

2. **Node/Edge rendering: will BrailleCanvas support curved/orthogonal edges**, or straight lines only?
   - Plan says "direct BrailleCanvas lines" — confirm `BrailleCanvas.Line(x0, y0, x1, y1)` is sufficient.
   - Current implementation at `/home/ronaldvanhelden/Projects/TerminalNinja/TerminalNinja/Buffers/BrailleCanvas.cs` (lines 74–100) uses Bresenham — adequate for MVP.

3. **Theme support (Dark/Dracula/GruvboxDark)**: Will theme keys be hardcoded in NodeGraph, or registered in a theme registry?
   - If registry-based, ensure no reflection-driven lookup.

4. **Will the sample/docs require any runtime XAML parsing** beyond what the generator produces?
   - Confirm the WASM playground (docs/samples.js) does not call Xaml.Load or XamlParser at runtime.

---

## Summary

**No code changes required for AOT compliance.** Only add GraphNode and GraphEdge to `AdditionalFactoryTypes` in ControlFactoryGenerator.cs (line 45–46). The feature is otherwise fully compliant with TerminalNinja's AOT patterns.
