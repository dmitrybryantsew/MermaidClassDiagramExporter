# Real Layout Pipeline Technical Design

## Purpose

This document defines the target architecture for moving the Unity viewer closer to a real Mermaid-like layout pipeline.

The goal is not to copy Mermaid's renderer line-for-line.

The goal is to adopt the same architectural shape:

- generic graph data
- explicit preprocessing passes
- real node measurement
- compound-cluster-aware layout
- post-layout cleanup
- rendering that consumes layout output instead of inventing geometry

This is the design we should grow toward from the current heuristic `LayeredLayoutEngine`.

## Design Principles

- `TypeGraph` remains the structural source of truth.
- Layout-specific state lives in a separate layout graph.
- Layout is a pipeline of named passes, not one giant method.
- Rendering only consumes `LayoutResult`.
- Cluster and edge cleanup happen before final placement.
- Measured size should drive layout more than estimates.
- Internal-only clusters should be able to become local subgraphs.

## High-Level Data Flow

1. `TypeGraphBuilder` builds a `TypeGraph`.
2. `LayoutGraphFactory` converts it into a `LayoutGraph`.
3. `LayoutPipeline` runs preprocessing passes.
4. `NodeMeasurementService` fills real measured sizes.
5. `CompoundLayeredLayoutEngine` computes positions.
6. Post-layout passes adjust cluster title space and edge endpoints.
7. `LayoutResult` is returned to the viewer.
8. `GraphCanvasView` renders nodes, groups, and routed edges from that result.

## Main Runtime Layers

### 1. Structural Graph Layer

Already exists.

Classes:

- `TypeGraph`
- `TypeNodeData`
- `TypeEdgeData`
- `TypeGroupData`
- `TypeGraphMetadata`

Responsibility:

- represent project structure without layout concerns

### 2. Layout Graph Layer

New target layer.

Classes:

- `LayoutGraph`
- `LayoutNode`
- `LayoutEdge`
- `LayoutCluster`
- `LayoutSubgraph`

Responsibility:

- hold layout-ready data including cluster membership, measured size, rewritten edges, and extracted subgraphs

### 3. Layout Pipeline Layer

New target orchestration layer.

Classes:

- `LayoutPipeline`
- `ILayoutPass`
- pass implementations listed later in this document

Responsibility:

- mutate or transform `LayoutGraph` step-by-step before placement

### 4. Layout Engine Layer

Classes:

- `CompoundLayeredLayoutEngine`
- `SimpleColumnLayoutEngine`
- engine sub-services listed later in this document

Responsibility:

- compute positions and bounds from a prepared layout graph

### 5. Post-Layout and Routing Layer

Classes:

- `ClusterTitleOffsetPass`
- `EdgeRoutingService`
- `ClusterBoundaryClipper`

Responsibility:

- turn raw placement into final render-ready geometry

### 6. Viewer Layer

Already partially exists.

Classes:

- `TypeGraphWindow`
- `GraphCanvasView`
- `TypeGraphInspectorView`
- `TypeNodeElement`

Responsibility:

- request graph build/layout
- render the returned result
- handle user interaction

## Core Target Classes

## `LayoutGraph`

### Purpose

The main layout input/output object that moves through the pipeline.

### Suggested Fields

- `string Title`
- `IReadOnlyList<LayoutNode> Nodes`
- `IReadOnlyList<LayoutEdge> Edges`
- `IReadOnlyList<LayoutCluster> Clusters`
- `IReadOnlyList<LayoutSubgraph> ExtractedSubgraphs`
- `LayoutGraphMetadata Metadata`

### Suggested Methods

- `LayoutNode? FindNode(string id)`
- `LayoutCluster? FindCluster(string id)`
- `IEnumerable<LayoutEdge> GetOutgoingEdges(string nodeId)`
- `IEnumerable<LayoutEdge> GetIncomingEdges(string nodeId)`

### Notes

- This should stay generic enough to support Mermaid export, Unity viewer layout, and future debug dumps.

## `LayoutNode`

### Purpose

Represents one visual node for layout.

### Suggested Fields

- `string Id`
- `string Label`
- `string SourceNodeId`
- `string ClusterId`
- `LayoutNodeRole Role`
- `LayoutNodeKind Kind`
- `float EstimatedWidth`
- `float EstimatedHeight`
- `float MeasuredWidth`
- `float MeasuredHeight`
- `bool IsMeasured`
- `int Rank`
- `int Order`
- `bool IsVirtual`
- `bool IsPinned`
- `Rect Bounds`

### Suggested Methods

- `Vector2 GetPreferredSize()`
- `void SetMeasuredSize(float width, float height)`
- `bool HasStableSize()`

### Notes

- `Role` should distinguish real nodes from cluster anchors and self-loop helpers.
- `Bounds` should be assigned only after layout.

## `LayoutEdge`

### Purpose

Represents a layout-time edge between nodes.

### Suggested Fields

- `string Id`
- `string OriginalEdgeId`
- `string FromNodeId`
- `string ToNodeId`
- `TypeEdgeKind Kind`
- `LayoutEdgeRole Role`
- `float Weight`
- `bool ConstrainsRank`
- `bool CrossesClusterBoundary`
- `string? FromClusterId`
- `string? ToClusterId`

### Suggested Methods

- `bool IsStructural()`
- `bool IsAssociationLike()`
- `bool IsBoundaryRewrite()`

### Notes

- `OriginalEdgeId` is important once one structural edge gets rewritten into several helper edges.

## `LayoutCluster`

### Purpose

Represents a namespace/group cluster in the compound graph.

### Suggested Fields

- `string Id`
- `string Label`
- `TypeGroupKind Kind`
- `string? ParentClusterId`
- `IReadOnlyList<string> NodeIds`
- `IReadOnlyList<string> ChildClusterIds`
- `bool HasExternalConnections`
- `string? RepresentativeNodeId`
- `bool IsExtractedSubgraph`
- `Rect Bounds`

### Suggested Methods

- `bool ContainsNode(string nodeId)`
- `bool IsRootCluster()`
- `bool CanExtractAsSubgraph()`

### Notes

- `RepresentativeNodeId` mirrors Mermaid's use of a non-cluster child for cluster-touching edges.

## `LayoutSubgraph`

### Purpose

Represents an extracted internal-only cluster that gets its own local layout run.

### Suggested Fields

- `string ClusterId`
- `LayoutGraph Graph`
- `LayoutDirection Direction`
- `bool IsLaidOut`
- `Vector2 Size`

### Suggested Methods

- `void ApplyResult(LayoutResult result)`

### Notes

- This is the clean seam for Mermaid-like recursive layout.

## `LayoutGraphMetadata`

### Purpose

Stores pipeline/debug metadata for the current layout graph.

### Suggested Fields

- `string SourceDescription`
- `DateTime GeneratedAtUtc`
- `bool HasMeasuredNodes`
- `bool HasExtractedSubgraphs`
- `int RewriteCount`
- `int VirtualNodeCount`

## Pipeline Orchestration

## `LayoutPipeline`

### Purpose

Runs named passes over a `LayoutGraph`.

### Suggested Fields

- `IReadOnlyList<ILayoutPass> Passes`

### Suggested Methods

- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`
- `void AddPass(ILayoutPass pass)`

### Notes

- This should replace hidden preprocessing inside `LayeredLayoutEngine`.

## `ILayoutPass`

### Purpose

Common contract for graph-preparation and post-layout passes.

### Suggested Methods

- `string Name { get; }`
- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`

## Pre-Layout Passes

## `LayoutGraphFactory`

### Purpose

Converts `TypeGraph` into the first `LayoutGraph`.

### Suggested Fields

- `NodeMeasurementService MeasurementService`

### Suggested Methods

- `LayoutGraph Create(TypeGraph graph, LayoutOptions options)`
- `LayoutNode CreateNode(TypeNodeData sourceNode, LayoutOptions options)`
- `LayoutEdge CreateEdge(TypeEdgeData sourceEdge)`
- `LayoutCluster CreateCluster(TypeGroupData sourceGroup)`

### Responsibility

- only mapping, not graph rewriting

## `ClusterHierarchyPass`

### Purpose

Builds cluster parent/child relationships and descendant lookup data.

### Suggested Methods

- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`
- `Dictionary<string, HashSet<string>> BuildDescendantMap(LayoutGraph graph)`

### Responsibility

- normalize cluster hierarchy before edge rewriting

## `RepresentativeNodeSelectionPass`

### Purpose

Selects the real node that should represent a cluster when boundary edges need a concrete endpoint.

### Suggested Methods

- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`
- `string? FindRepresentativeNode(LayoutCluster cluster, LayoutGraph graph)`

### Responsibility

- assign `RepresentativeNodeId` for each cluster

## `BoundaryEdgeNormalizationPass`

### Purpose

Rewrite cluster-touching edges into layout-stable forms.

### Suggested Fields

- `bool UseAnchorNodes`
- `bool PreferRepresentativeNodes`

### Suggested Methods

- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`
- `IEnumerable<LayoutEdge> RewriteEdge(LayoutEdge edge, LayoutGraph graph)`
- `LayoutNode CreateInboundAnchor(...)`
- `LayoutNode CreateOutboundAnchor(...)`

### Responsibility

- mark `CrossesClusterBoundary`
- create helper nodes/edges when needed

### Notes

- This is the current path we have started, but it should become a named pass.

## `SelfLoopExpansionPass`

### Purpose

Rewrites self-loops into a helper-node chain before layout.

### Suggested Methods

- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`
- `IEnumerable<LayoutEdge> ExpandSelfLoop(LayoutEdge edge, LayoutNode node)`

### Responsibility

- mimic Mermaid's helper-node strategy for self-loops

## `InternalClusterExtractionPass`

### Purpose

Extract clusters without external connections into local subgraphs.

### Suggested Methods

- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`
- `bool ShouldExtract(LayoutCluster cluster, LayoutGraph graph)`
- `LayoutSubgraph ExtractCluster(LayoutCluster cluster, LayoutGraph graph, LayoutOptions options)`

### Responsibility

- enable Mermaid-like recursive layout

### Notes

- This is one of the biggest missing features today.

## `DisconnectedComponentPass`

### Purpose

Splits disconnected layout regions so they can be placed independently.

### Suggested Methods

- `IReadOnlyList<LayoutGraph> Split(LayoutGraph graph)`
- `LayoutGraph Run(LayoutGraph graph, LayoutOptions options)`

### Responsibility

- reduce distortion between unrelated graph regions

## Measurement Layer

## `NodeMeasurementService`

### Purpose

Measure actual UI Toolkit node sizes before final layout.

### Suggested Fields

- `MeasurementCache Cache`
- `NodeMeasurementHost Host`

### Suggested Methods

- `Vector2 Measure(TypeNodeData node, LayoutOptions options)`
- `Vector2 Measure(LayoutNode node, LayoutOptions options)`
- `void WarmCache(IEnumerable<LayoutNode> nodes, LayoutOptions options)`

### Responsibility

- provide real measured width/height instead of pure estimates

### Notes

- This should eventually use hidden/offscreen UI Toolkit visuals, not only text heuristics.

## `MeasurementCache`

### Purpose

Avoid repeated measurement of equivalent node states.

### Suggested Fields

- `Dictionary<string, Vector2> SizeByKey`

### Suggested Methods

- `bool TryGet(string key, out Vector2 size)`
- `void Store(string key, Vector2 size)`
- `string BuildKey(LayoutNode node, LayoutOptions options)`

## `NodeMeasurementHost`

### Purpose

Hidden UI Toolkit host used for measurement.

### Suggested Fields

- `VisualElement Root`
- `TypeNodeElement Prototype`

### Suggested Methods

- `Vector2 MeasureNode(TypeNodeData node, LayoutOptions options)`
- `void EnsureInitialized()`

## Layout Engine Layer

## `CompoundLayeredLayoutEngine`

### Purpose

Main engine for Mermaid-like layout.

### Suggested Fields

- `RankAssignmentService RankAssignment`
- `CrossingReductionService CrossingReduction`
- `CoordinateAssignmentService CoordinateAssignment`
- `ClusterBoundsService ClusterBounds`
- `ComponentPackingService ComponentPacking`

### Suggested Methods

- `LayoutResult Run(LayoutGraph graph, LayoutOptions options)`
- `LayoutResult RunSubgraph(LayoutSubgraph subgraph, LayoutOptions options)`

### Responsibility

- orchestrate all layout sub-services

## `RankAssignmentService`

### Purpose

Assign layer/rank indexes to nodes and clusters.

### Suggested Methods

- `Dictionary<string, int> AssignNodeRanks(LayoutGraph graph, LayoutOptions options)`
- `Dictionary<string, int> AssignClusterRanks(LayoutGraph graph, LayoutOptions options)`
- `void ApplyAssociationSpread(...)`

### Responsibility

- prioritize inheritance/implementation
- keep associations weaker but still informative

## `CrossingReductionService`

### Purpose

Reduce crossings inside neighboring ranks.

### Suggested Methods

- `void OrderNodesWithinRanks(LayoutGraph graph)`
- `float ComputeBarycenter(...)`
- `float ComputeMedian(...)`

### Responsibility

- produce a more Mermaid-like ordering before coordinates are assigned

## `CoordinateAssignmentService`

### Purpose

Convert ranks and order into concrete x/y positions.

### Suggested Methods

- `Dictionary<string, Rect> AssignNodeBounds(LayoutGraph graph, LayoutOptions options)`
- `Vector2 ComputeGraphSize(...)`

### Responsibility

- respect spacing, measurement, cluster padding, and subgraph embedding

## `ClusterBoundsService`

### Purpose

Compute cluster rectangles from child nodes and title space.

### Suggested Methods

- `Dictionary<string, Rect> ComputeClusterBounds(LayoutGraph graph, LayoutOptions options)`
- `Rect ExpandForTitle(Rect bounds, LayoutCluster cluster, LayoutOptions options)`

### Responsibility

- reserve header space and ensure child content fits

## `ComponentPackingService`

### Purpose

Pack disconnected components into the final content area.

### Suggested Methods

- `LayoutResult Pack(IReadOnlyList<LayoutResult> componentLayouts, LayoutOptions options)`

### Responsibility

- place separate components without large wasted gaps

## Post-Layout and Routing Layer

## `ClusterTitleOffsetPass`

### Purpose

Apply post-layout adjustments for cluster header/title margins.

### Suggested Methods

- `LayoutResult Run(LayoutResult result, LayoutGraph graph, LayoutOptions options)`

### Responsibility

- mirror the kind of post-layout title-margin correction Mermaid performs

## `EdgeRoutingService`

### Purpose

Build renderable edge geometry from node/cluster bounds.

### Suggested Fields

- `ClusterBoundaryClipper BoundaryClipper`

### Suggested Methods

- `IReadOnlyList<LayoutEdgePath> BuildPaths(LayoutGraph graph, LayoutResult result, LayoutOptions options)`
- `LayoutEdgePath Route(LayoutEdge edge, LayoutGraph graph, LayoutResult result)`

### Responsibility

- generate bezier or polyline routes for the viewer

## `ClusterBoundaryClipper`

### Purpose

Trim edge paths against cluster bounds.

### Suggested Methods

- `LayoutEdgePath Clip(LayoutEdgePath path, LayoutEdge edge, LayoutResult result, LayoutGraph graph)`
- `Vector2 FindIntersection(Rect bounds, Vector2 from, Vector2 to)`

### Responsibility

- make cluster-touching edges terminate at namespace borders instead of cutting through them visually

## `LayoutEdgePath`

### Purpose

Render-ready path for one logical edge.

### Suggested Fields

- `string EdgeId`
- `string OriginalEdgeId`
- `IReadOnlyList<Vector2> Points`
- `TypeEdgeKind Kind`
- `bool IsClipped`

## Coordinator Layer

## `GraphLayoutCoordinator`

### Purpose

Top-level entry point used by the viewer.

### Suggested Fields

- `LayoutGraphFactory GraphFactory`
- `LayoutPipeline Pipeline`
- `NodeMeasurementService MeasurementService`
- `IGraphLayoutEngine PrimaryEngine`
- `IGraphLayoutEngine FallbackEngine`
- `ClusterTitleOffsetPass TitleOffsetPass`
- `EdgeRoutingService EdgeRouting`

### Suggested Methods

- `LayoutResult CreateLayout(TypeGraph graph, LayoutOptions options = null)`
- `LayoutResult CreateLayout(LayoutGraph graph, LayoutOptions options = null)`

### Responsibility

- build graph
- run passes
- measure nodes
- run layout
- run post-processing
- return a final `LayoutResult`

## Viewer-Related Classes

## `GraphCanvasView`

### Current Role

- renders nodes, groups, and simple edges

### Target Role

- render only the provided `LayoutResult`
- use `LayoutEdgePath` instead of inventing simple default curves

### Methods It Should Keep

- `SetGraph(TypeGraph value, LayoutResult layout)`
- `SelectNode(string nodeId)`
- `FocusNode(string nodeId)`

### Methods It Should Not Grow

- no cluster placement logic
- no rank assignment logic
- no edge routing logic

## `TypeGraphWindow`

### Current Role

- builds graphs and owns the viewer UI

### Target Role

- request graph source
- request layout from `GraphLayoutCoordinator`
- display layout mode / debug options / relayout actions

## Relationships Between Classes

## Structural To Layout

- `TypeGraphBuilder` creates `TypeGraph`
- `LayoutGraphFactory` converts `TypeGraph` into `LayoutGraph`

## Pipeline

- `GraphLayoutCoordinator` owns `LayoutPipeline`
- `LayoutPipeline` runs `ILayoutPass` implementations over `LayoutGraph`
- `RepresentativeNodeSelectionPass`, `BoundaryEdgeNormalizationPass`, and `InternalClusterExtractionPass` all depend on cluster hierarchy data

## Measurement

- `GraphLayoutCoordinator` uses `NodeMeasurementService`
- `NodeMeasurementService` uses `MeasurementCache`
- `NodeMeasurementService` may use `NodeMeasurementHost`
- measured size updates `LayoutNode`

## Layout

- `GraphLayoutCoordinator` calls `CompoundLayeredLayoutEngine`
- `CompoundLayeredLayoutEngine` delegates to:
  - `RankAssignmentService`
  - `CrossingReductionService`
  - `CoordinateAssignmentService`
  - `ClusterBoundsService`
  - `ComponentPackingService`

## Post-Layout

- `GraphLayoutCoordinator` runs `ClusterTitleOffsetPass`
- `GraphLayoutCoordinator` runs `EdgeRoutingService`
- `EdgeRoutingService` uses `ClusterBoundaryClipper`

## Rendering

- `TypeGraphWindow` passes `LayoutResult` to `GraphCanvasView`
- `GraphCanvasView` renders node bounds, cluster bounds, and edge paths

## Suggested File Structure

Inside `Assets/Plugins/MermaidClassDiagramExporter/Editor/Layout/`:

- `LayoutModels.cs`
- `LayoutOptions.cs`
- `GraphLayoutCoordinator.cs`
- `LayoutGraphFactory.cs`
- `LayoutPipeline.cs`
- `ILayoutPass.cs`
- `Metadata/LayoutGraphMetadata.cs`
- `Passes/ClusterHierarchyPass.cs`
- `Passes/RepresentativeNodeSelectionPass.cs`
- `Passes/BoundaryEdgeNormalizationPass.cs`
- `Passes/SelfLoopExpansionPass.cs`
- `Passes/InternalClusterExtractionPass.cs`
- `Passes/DisconnectedComponentPass.cs`
- `Measurement/NodeMeasurementService.cs`
- `Measurement/MeasurementCache.cs`
- `Measurement/NodeMeasurementHost.cs`
- `Engines/CompoundLayeredLayoutEngine.cs`
- `Engines/SimpleColumnLayoutEngine.cs`
- `Services/RankAssignmentService.cs`
- `Services/CrossingReductionService.cs`
- `Services/CoordinateAssignmentService.cs`
- `Services/ClusterBoundsService.cs`
- `Services/ComponentPackingService.cs`
- `Routing/EdgeRoutingService.cs`
- `Routing/ClusterBoundaryClipper.cs`
- `Post/ClusterTitleOffsetPass.cs`

## Implementation Order

### Phase 1

- add `LayoutPipeline`
- add named preprocessing passes around the current layout graph
- keep current engine as-is behind the new coordinator

### Phase 2

- add `NodeMeasurementService`
- stop depending only on estimated node sizes

### Phase 3

- add `InternalClusterExtractionPass`
- add `LayoutSubgraph`
- run internal-only namespace layout recursively

### Phase 4

- split `LayeredLayoutEngine` into rank, ordering, coordinate, and bounds services

### Phase 5

- add `EdgeRoutingService`
- add `ClusterBoundaryClipper`
- switch viewer edge rendering to layout-produced edge paths

## Why This Is Closer To A Real Layout Pipeline

This architecture gets closer to Mermaid because it makes these things first-class:

- generic layout graph data
- cluster-aware preprocessing
- representative nodes / anchor rewriting
- recursive extracted subgraphs
- measured node sizes
- post-layout adjustment
- render-time edge clipping

That is the difference between:

- "a viewer with a custom heuristic arrangement"

and:

- "a real graph layout pipeline with a viewer on top"
