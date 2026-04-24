# Layout Engine Technical Design

## Purpose

This document defines the architecture for a Mermaid-inspired layout engine inside the Unity type-graph viewer.

The goal is not to port Mermaid's browser renderer into Unity.

The goal is to reproduce the useful parts of the layout behavior:

- cleaner namespace grouping
- more readable horizontal flow
- better placement of related classes across groups
- less arbitrary stacking than the current simple column layout

## Design Goal

The layout system should become a separate subsystem between graph extraction and graph rendering.

That means:

- `TypeGraph` remains the source of structural truth
- the layout engine produces positions and bounds
- the UI Toolkit viewer only renders the layout result

This separation is important because later we may want:

- multiple layout algorithms
- saved manual overrides
- layout caching
- partial relayout after filtering or collapsing groups

## High-Level Data Flow

1. `TypeGraphBuilder` produces a `TypeGraph`.
2. A layout coordinator converts it into a layout-specific graph.
3. The selected layout engine computes node positions and group bounds.
4. A layout result is returned to the viewer.
5. The viewer applies those positions to node and group visuals.

## Proposed Runtime Layers

### 1. Graph Domain

Already exists:

- `TypeGraph`
- `TypeNodeData`
- `TypeEdgeData`
- `TypeGroupData`

This layer must stay free of layout-specific state.

### 2. Layout Domain

New layer for layout-only structures.

Suggested folder:

- `Assets/Plugins/MermaidClassDiagramExporter/Editor/Layout/`

Suggested files:

- `LayoutModels.cs`
- `LayoutOptions.cs`
- `GraphLayoutCoordinator.cs`
- `LayoutGraphFactory.cs`
- `NamespaceClusterBuilder.cs`
- `NodeMeasurementService.cs`
- `LayeredLayoutEngine.cs`
- `LayoutResultApplier.cs`

### 3. Viewer Rendering

Already exists in rough form:

- `GraphCanvasView`
- `TypeNodeElement`
- `TypeGraphInspectorView`
- `TypeGraphWindow`

This layer should stop deciding positions directly.

## Core Architectural Rule

`GraphCanvasView` should never invent layout.

Its responsibility should be:

- render nodes
- render groups
- render edges
- apply pan and zoom
- handle interaction

The responsibility for deciding where nodes go belongs to the layout subsystem.

## Proposed Layout Model

### `LayoutGraph`

This is the engine-ready graph.

Responsibilities:

- hold measured node size data
- hold layout edges
- hold cluster membership
- expose only the data the layout algorithm needs

Suggested fields:

- `string Title`
- `IReadOnlyList<LayoutNode> Nodes`
- `IReadOnlyList<LayoutEdge> Edges`
- `IReadOnlyList<LayoutCluster> Clusters`

### `LayoutNode`

Represents a single positioned box.

Suggested fields:

- `string Id`
- `string DisplayName`
- `float Width`
- `float Height`
- `int Rank`
- `int Order`
- `string ClusterId`
- `bool IsPinned`

Notes:

- `Rank` is the horizontal or vertical layer index used by the layered layout
- `Order` is the within-rank ordering after crossing reduction

### `LayoutEdge`

Represents a connection for layout purposes.

Suggested fields:

- `string FromNodeId`
- `string ToNodeId`
- `TypeEdgeKind Kind`
- `float Weight`
- `bool ConstrainsRank`

Notes:

- inheritance and implementation edges should carry stronger rank weight than loose association edges

### `LayoutCluster`

Represents namespace or group bounds.

Suggested fields:

- `string Id`
- `string Label`
- `TypeGroupKind Kind`
- `string ParentClusterId`
- `IReadOnlyList<string> NodeIds`
- `Rect Bounds`

This is the main mechanism for Mermaid-like namespace blocks.

### `LayoutResult`

Output returned from the engine.

Suggested fields:

- `Dictionary<string, Rect> NodeBounds`
- `Dictionary<string, Rect> ClusterBounds`
- `IReadOnlyList<LayoutEdgePath> EdgePaths`
- `Vector2 ContentSize`

### `LayoutEdgePath`

Optional edge route output.

Suggested fields:

- `string FromNodeId`
- `string ToNodeId`
- `IReadOnlyList<Vector2> Points`

V1 may still draw bezier curves without a full routed polyline path, but the model should leave room for better routing later.

## Layout Options

### `LayoutOptions`

Configures the layout engine.

Suggested fields:

- `LayoutDirection Direction`
- `float RankSpacing`
- `float NodeSpacing`
- `float ClusterPadding`
- `float EdgeSpacing`
- `bool TreatNamespacesAsClusters`
- `bool SeparateDisconnectedComponents`
- `bool PrioritizeInheritanceChains`
- `bool AllowAssociationEdgesToPullRanks`

Suggested defaults for the Mermaid-like mode:

- left-to-right direction
- namespace clusters enabled
- inheritance prioritized
- disconnected components separated

### `LayoutDirection`

Suggested values:

- `LeftToRight`
- `TopToBottom`

For our use case, `LeftToRight` should be the default.

## Main Layout Classes

### `GraphLayoutCoordinator`

This should be the main entry point used by the viewer.

Responsibilities:

- accept a `TypeGraph`
- accept `LayoutOptions`
- build the intermediate `LayoutGraph`
- run the selected layout engine
- return `LayoutResult`

This keeps the viewer code simple.

Example responsibility split:

- `TypeGraphWindow` says "layout this graph"
- `GraphLayoutCoordinator` decides how

### `LayoutGraphFactory`

Builds the engine-ready graph from `TypeGraph`.

Responsibilities:

- map `TypeNodeData` into `LayoutNode`
- map `TypeEdgeData` into `LayoutEdge`
- attach cluster membership
- normalize layout weights by edge kind

This is where we translate structural graph meaning into layout hints.

### `NamespaceClusterBuilder`

Creates and normalizes namespace clusters.

Responsibilities:

- build `LayoutCluster` records from `TypeGroupData`
- support future nested namespaces
- support fallback grouping when no explicit groups exist

This is the part that should make our namespace blocks cleaner and more Mermaid-like.

### `NodeMeasurementService`

Measures desired node size before layout.

Responsibilities:

- estimate width and height from node data
- account for visible member count
- support compact vs expanded node styles

Important note:

The layout engine should never guess positions before node sizes are known.

We do not need pixel-perfect text measurement in V1.
We do need consistent approximate size estimates.

### `IGraphLayoutEngine`

Interface for layout engines.

Suggested API:

```csharp
internal interface IGraphLayoutEngine
{
    LayoutResult Run(LayoutGraph graph, LayoutOptions options);
}
```

This lets us have:

- `SimpleColumnLayoutEngine` for fallback/debug
- `LayeredLayoutEngine` for Mermaid-like behavior
- later experimental engines without rewriting the viewer

### `LayeredLayoutEngine`

This should be the first serious engine.

Purpose:

- mimic the broad layout behavior of Mermaid's cleaner graph arrangement

Responsibilities:

- assign ranks
- reduce edge crossings
- order nodes within ranks
- compute node coordinates
- compute cluster bounds

This engine should be layered and cluster-aware.

## Internal Subsystems Inside `LayeredLayoutEngine`

These can begin as private helpers and later become separate files if needed.

### `ComponentSplitter`

Responsibilities:

- split disconnected graph components
- lay out each component independently
- pack components into the final canvas

This matters because disconnected namespaces should not distort each other.

### `RankAssignmentService`

Responsibilities:

- choose which nodes belong in which rank
- keep inheritance chains flowing consistently
- prevent weak associations from dominating the graph

Practical rule:

- inheritance and implementation edges should strongly influence rank assignment
- association edges should influence ordering, but less aggressively

### `CrossingReductionService`

Responsibilities:

- reduce visual crossings between adjacent ranks
- sort nodes within a rank using barycenter or median heuristics

This is one of the key differences between our current layout and a more Mermaid-like result.

### `CoordinateAssignmentService`

Responsibilities:

- assign final x/y positions after ranks and order are known
- respect node spacing and cluster padding
- keep related nodes visually tight

### `ClusterBoundsService`

Responsibilities:

- compute bounds for namespace containers
- add header room and padding
- support future nested groups

### `ComponentPackingService`

Responsibilities:

- place disconnected components in rows or columns
- minimize wasted space
- keep the overall graph visually balanced

This is important because the browser screenshot shows Mermaid handling separate subsystems as distinct regions instead of one long strip.

## Viewer Integration Classes

### `LayoutResultApplier`

Takes a `LayoutResult` and updates the viewer model.

Responsibilities:

- apply node bounds to `TypeNodeElement`
- apply cluster bounds to group visuals
- provide edge geometry to the edge layer

This keeps `GraphCanvasView` from containing layout math and UI update math in one place.

### `GraphCanvasView`

After refactor, responsibilities should be narrowed to:

- host visuals
- store current layout result
- apply pan and zoom
- react to selection and search
- request redraws

What should move out of it:

- layout generation
- group placement decisions
- hard-coded column placement

## Manual Layout Support

We should design for auto-layout first, but leave a clean seam for future manual edits.

### `ManualLayoutOverrideSet`

Suggested future class.

Responsibilities:

- store user-pinned node positions
- store expanded/collapsed group state
- merge manual positions with fresh auto-layout

Example rule:

- if a node is pinned, keep its position during relayout when possible

This lets us keep auto-layout as the baseline without losing user adjustments.

## Layout Strategy For V1

The first version does not need to fully reproduce Mermaid internals.

It should aim for these behaviors:

1. build namespace clusters
2. split disconnected components
3. assign nodes into left-to-right ranks
4. prioritize inheritance and implementation edges
5. reduce crossings inside each component
6. compute stable positions
7. compute cluster bounds after node placement

That will already be a major improvement over the current vertical-stack-per-group layout.

## Suggested File-Level Architecture

Inside `Assets/Plugins/MermaidClassDiagramExporter/Editor/Layout/`:

- `LayoutModels.cs`
  Contains `LayoutGraph`, `LayoutNode`, `LayoutEdge`, `LayoutCluster`, `LayoutResult`

- `LayoutOptions.cs`
  Contains engine options and enums

- `IGraphLayoutEngine.cs`
  Interface for engines

- `GraphLayoutCoordinator.cs`
  Main orchestration entry point

- `LayoutGraphFactory.cs`
  Converts `TypeGraph` into `LayoutGraph`

- `NamespaceClusterBuilder.cs`
  Builds cluster hierarchy and cluster metadata

- `NodeMeasurementService.cs`
  Estimates node dimensions from current node visual rules

- `SimpleColumnLayoutEngine.cs`
  Keeps the existing fallback behavior for debugging

- `LayeredLayoutEngine.cs`
  Mermaid-inspired primary engine

- `LayoutResultApplier.cs`
  Applies engine output to the viewer

If `LayeredLayoutEngine.cs` grows too large, split it into:

- `RankAssignmentService.cs`
- `CrossingReductionService.cs`
- `CoordinateAssignmentService.cs`
- `ClusterBoundsService.cs`
- `ComponentPackingService.cs`

## Responsibilities By Current Existing Class

### `TypeGraphBuilder`

Should continue to:

- build structural graph data

Should not:

- decide layout coordinates

### `TypeGraphWindow`

Should:

- trigger graph build
- trigger layout
- host controls for layout mode and relayout

Should not:

- compute placement itself

### `GraphCanvasView`

Should:

- render a provided layout
- handle interaction

Should not:

- compute namespace columns on its own

## Recommended Implementation Order

### Phase 1: Extract Layout Out Of The Canvas

- add `LayoutModels`
- add `LayoutOptions`
- add `GraphLayoutCoordinator`
- move current simple placement into `SimpleColumnLayoutEngine`

Deliverable:

- viewer still looks the same
- layout no longer lives inside `GraphCanvasView`

### Phase 2: Add Mermaid-Inspired Clustered Layered Layout

- implement `LayoutGraphFactory`
- implement `NamespaceClusterBuilder`
- implement `NodeMeasurementService`
- implement `LayeredLayoutEngine`

Deliverable:

- cleaner namespace grouping
- better relation-aware placement

### Phase 3: Improve Stability And Readability

- component packing
- crossing reduction tuning
- edge weights by relation kind
- better cluster sizing

Deliverable:

- more stable and less tangled large graphs

### Phase 4: Prepare For User Control

- saved layout mode
- pinned nodes
- relayout selected subset
- collapse/expand groups

Deliverable:

- practical day-to-day workflow instead of one-shot layout

## Risks

### 1. Over-Coupling Layout To Current UI

If node size estimation depends too directly on current UI element internals, layout becomes fragile.

Mitigation:

- centralize measurement in `NodeMeasurementService`

### 2. Large Graph Performance

Layered layout is more expensive than simple stacking.

Mitigation:

- split disconnected components early
- cache layout results
- relayout only when graph structure changes

### 3. Excessive Fidelity Chasing

Trying to exactly reproduce Mermaid may slow us down without improving usability enough.

Mitigation:

- target Mermaid-like readability, not code-level parity

## Recommendation

The next implementation step should be:

1. move layout logic out of `GraphCanvasView`
2. introduce `SimpleColumnLayoutEngine`
3. introduce `GraphLayoutCoordinator`
4. then implement `LayeredLayoutEngine`

That sequence gives us a clean architecture first, then better layout second.
