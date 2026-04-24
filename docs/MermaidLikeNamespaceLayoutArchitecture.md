# Mermaid-Like Namespace Layout Architecture

## Purpose

This document defines the target architecture for making the Unity viewer behave much closer to Mermaid class-diagram layout, especially for class spacing inside namespaces.

The focus is not "better heuristics".

The focus is matching Mermaid's problem formulation:

- compound parent-child graph membership
- representative-anchor edge rewriting
- internal-only namespace extraction
- recursive subgraph layout
- measured node sizes
- post-layout cluster/title adjustments

## What We Are Trying To Match

From the Mermaid source, namespace-internal spacing is produced by this chain:

1. create real group nodes for namespaces
2. attach classes and notes with `parentId`
3. measure real rendered node sizes
4. detect `externalConnections` per namespace
5. rewrite cluster-touching edges through representative descendants
6. extract internal-only namespaces into local subgraphs
7. choose local direction for extracted subgraphs
8. run recursive layout with inherited spacing
9. expand cluster bounds and apply title offsets after layout

If we want similar results, our architecture needs to support that sequence directly.

## High-Level Shape

```text
TypeGraph
  -> LayoutGraphFactory
  -> MermaidLikeLayoutPipeline
       -> MeasurementPreparationPass
       -> CompoundHierarchyPass
       -> ExternalConnectionAnalysisPass
       -> RepresentativeAnchorSelectionPass
       -> BoundaryEdgeRewritePass
       -> InternalClusterExtractionPass
       -> SubgraphDirectionSelectionPass
       -> RecursiveSpacingPass
  -> MermaidLikeCompoundLayoutEngine
       -> RankAssignmentService
       -> CrossingReductionService
       -> CoordinateAssignmentService
       -> ClusterBoundsService
  -> PostLayoutPipeline
       -> ClusterTitleMarginPass
       -> ClusterBoundsPolishPass
       -> RoutedEdgeGeometryPass
  -> LayoutResult
  -> GraphCanvasView
```

## Design Principles

- `TypeGraph` stays the structural source of truth.
- A separate `LayoutGraph` becomes the only input to layout.
- Namespace behavior is encoded as pipeline passes, not hidden inside the viewer.
- Measurement happens before final layout, not after.
- Internal-only namespaces are solved recursively as local graphs.
- Post-layout cluster/title adjustments are first-class.
- Edge geometry is generated from layout output, not improvised by the viewer.

## Main Runtime Objects

## `LayoutGraph`

### Responsibility

The mutable layout-time graph that moves through preprocessing, recursive extraction, layout, and post-layout passes.

### Fields

- `string Title`
- `Dictionary<string, LayoutNode> NodesById`
- `Dictionary<string, LayoutEdge> EdgesById`
- `Dictionary<string, LayoutCluster> ClustersById`
- `Dictionary<string, LayoutSubgraph> ExtractedSubgraphsByClusterId`
- `LayoutGraphMetadata Metadata`

### Methods

- `LayoutNode GetNode(string id)`
- `LayoutCluster GetCluster(string id)`
- `IEnumerable<LayoutNode> GetClusterNodes(string clusterId)`
- `IEnumerable<LayoutEdge> GetIncomingEdges(string nodeId)`
- `IEnumerable<LayoutEdge> GetOutgoingEdges(string nodeId)`
- `LayoutGraph CloneForSubgraph(string clusterId)`

## `LayoutNode`

### Responsibility

Represents one layout-time visual node, including real classes, notes, helper anchors, and self-loop helpers.

### Fields

- `string Id`
- `string SourceNodeId`
- `string Label`
- `string? ClusterId`
- `LayoutNodeRole Role`
- `LayoutNodeKind Kind`
- `Vector2 EstimatedSize`
- `Vector2 MeasuredSize`
- `bool IsMeasured`
- `int Rank`
- `int Order`
- `Rect Bounds`
- `bool IsHiddenFromRender`

### Methods

- `Vector2 GetEffectiveSize()`
- `void SetMeasuredSize(float width, float height)`
- `bool IsRealContentNode()`

## `LayoutEdge`

### Responsibility

Represents a layout-time relation between two nodes after normalization and possible rewriting.

### Fields

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

### Methods

- `bool IsStructuralEdge()`
- `bool IsBoundaryHelper()`
- `bool IsAssociationLike()`

## `LayoutCluster`

### Responsibility

Represents one namespace/group cluster in the compound graph.

### Fields

- `string Id`
- `string Label`
- `string? ParentClusterId`
- `List<string> ChildClusterIds`
- `List<string> NodeIds`
- `bool HasExternalConnections`
- `string? RepresentativeNodeId`
- `bool IsExtractedSubgraph`
- `LayoutDirection? PreferredLocalDirection`
- `Rect Bounds`
- `ClusterTitleMetrics TitleMetrics`

### Methods

- `bool CanExtractAsSubgraph()`
- `bool IsRootCluster()`
- `bool ContainsNode(string nodeId)`

## `LayoutSubgraph`

### Responsibility

Owns an extracted internal-only namespace layout problem plus its local settings.

### Fields

- `string ClusterId`
- `LayoutGraph Graph`
- `LayoutDirection Direction`
- `LayoutSpacingProfile Spacing`
- `LayoutResult? SolvedLayout`

### Methods

- `bool HasSolvedLayout()`
- `void SetSolvedLayout(LayoutResult result)`

## `LayoutResult`

### Responsibility

The final render-ready output of layout.

### Fields

- `Dictionary<string, Rect> NodeBoundsById`
- `Dictionary<string, Rect> ClusterBoundsById`
- `Dictionary<string, RoutedEdgePath> RoutedEdgesById`
- `Rect OverallBounds`
- `LayoutResultMetadata Metadata`

### Methods

- `Rect GetNodeBounds(string id)`
- `Rect GetClusterBounds(string id)`
- `RoutedEdgePath? TryGetEdgePath(string id)`

## Shared Settings Objects

## `LayoutSpacingProfile`

### Responsibility

Encodes Mermaid-like spacing inputs for one graph solve.

### Fields

- `float NodeSeparation`
- `float RankSeparation`
- `float MarginX`
- `float MarginY`
- `float ClusterPadding`
- `float ClusterTitleTopMargin`
- `float ClusterTitleBottomMargin`
- `float RecursiveRankSeparationBonus`

### Notes

This object exists because Mermaid does not use one global spacing forever.

Extracted subgraphs inherit parent spacing and then increase rank spacing.

## `ClusterTitleMetrics`

### Responsibility

Stores measured title information that changes visible cluster spacing after layout.

### Fields

- `float LabelWidth`
- `float LabelHeight`
- `float TopMargin`
- `float BottomMargin`
- `float TotalMargin`

## `LayoutGraphMetadata`

### Responsibility

Stores graph-wide settings and source context.

### Fields

- `LayoutDirection Direction`
- `LayoutSpacingProfile Spacing`
- `string SourceDescription`
- `bool UsesMeasuredNodes`

## Pipeline Architecture

## `MermaidLikeLayoutPipeline`

### Responsibility

Owns the ordered preprocessing sequence that transforms a raw `LayoutGraph` into a compound-layout-ready graph.

### Fields

- `IReadOnlyList<ILayoutPass> Passes`

### Methods

- `LayoutGraph Run(LayoutGraph graph, MermaidLikeLayoutContext context)`

## `ILayoutPass`

### Responsibility

Common interface for a named graph-preparation step.

### Methods

- `string Name { get; }`
- `LayoutGraph Execute(LayoutGraph graph, MermaidLikeLayoutContext context)`

## `MermaidLikeLayoutContext`

### Responsibility

Shared context object for one full solve.

### Fields

- `LayoutMeasurementService MeasurementService`
- `LayoutOptions Options`
- `LayoutThemeMetrics ThemeMetrics`
- `bool AllowRecursiveExtraction`
- `bool UseMeasuredNodes`

## Pre-Layout Passes

## `MeasurementPreparationPass`

### Responsibility

Measures real node and title sizes before layout.

### Fields

- `LayoutMeasurementService MeasurementService`

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- renders or simulates each node card offscreen
- measures real width and height
- measures cluster title width and height
- writes results into `LayoutNode.MeasuredSize` and `LayoutCluster.TitleMetrics`

## `CompoundHierarchyPass`

### Responsibility

Builds the explicit cluster parent-child structure.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- attaches nodes to clusters
- attaches nested clusters to parents
- validates compound hierarchy

## `ExternalConnectionAnalysisPass`

### Responsibility

Mirrors Mermaid's `externalConnections` detection.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- computes descendants for each cluster
- checks whether any edge crosses a cluster boundary
- marks `LayoutCluster.HasExternalConnections`

## `RepresentativeAnchorSelectionPass`

### Responsibility

Selects the best real descendant node to stand in for cluster-touching edges.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- finds candidate real child nodes
- scores them by edge conflict and hierarchy depth
- writes `LayoutCluster.RepresentativeNodeId`

## `BoundaryEdgeRewritePass`

### Responsibility

Rewrites cluster-touching edges through representative anchors or helper nodes.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- replaces direct cluster edges with node-to-node helper edges
- marks edge metadata like `FromClusterId`, `ToClusterId`, `Role`
- reduces distortion of namespace interiors by external traffic

## `InternalClusterExtractionPass`

### Responsibility

Extracts internal-only namespaces into separate recursive subgraph solves.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- detects clusters with no external connections
- copies only local nodes and internal edges into a new `LayoutSubgraph`
- replaces the original cluster with a `clusterNode`-style container

## `SubgraphDirectionSelectionPass`

### Responsibility

Assigns local direction for extracted namespaces.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- defaults opposite to parent direction, Mermaid-style
- respects cluster overrides if present
- stores result in `LayoutSubgraph.Direction`

## `RecursiveSpacingPass`

### Responsibility

Applies Mermaid-like recursive spacing inheritance.

### Methods

- `LayoutGraph Execute(...)`

### What It Does

- copies parent `NodeSeparation`
- copies parent `RankSeparation`
- applies `RecursiveRankSeparationBonus`
- preserves cluster margins

This pass is where we model the Mermaid behavior of "child graph inherits spacing, then rank spacing gets larger".

## Layout Engine Layer

## `MermaidLikeCompoundLayoutEngine`

### Responsibility

Solves the prepared `LayoutGraph` and any extracted `LayoutSubgraph` instances using a compound layered layout pipeline.

### Fields

- `RankAssignmentService RankAssignment`
- `CrossingReductionService CrossingReduction`
- `CoordinateAssignmentService CoordinateAssignment`
- `ClusterBoundsService ClusterBounds`

### Methods

- `LayoutResult Layout(LayoutGraph graph, MermaidLikeLayoutContext context)`
- `LayoutResult LayoutSubgraph(LayoutSubgraph subgraph, MermaidLikeLayoutContext context)`

### Notes

This is not the viewer.

It should only solve coordinates and bounds.

## `RankAssignmentService`

### Responsibility

Assigns ranks to nodes and helper anchors.

### Methods

- `void AssignRanks(LayoutGraph graph)`

## `CrossingReductionService`

### Responsibility

Orders nodes within ranks and local subgraphs to reduce edge crossings.

### Methods

- `void Reduce(LayoutGraph graph)`

## `CoordinateAssignmentService`

### Responsibility

Turns ranks and ordering into concrete node positions.

### Methods

- `void AssignCoordinates(LayoutGraph graph, LayoutSpacingProfile spacing)`

## `ClusterBoundsService`

### Responsibility

Builds cluster rectangles around laid-out child content.

### Methods

- `void ResolveClusterBounds(LayoutGraph graph)`

### What It Must Respect

- measured title width
- title margins
- cluster padding
- child node bounds
- nested cluster bounds

## Post-Layout Passes

## `PostLayoutPipeline`

### Responsibility

Runs render-facing cleanup after coordinates are solved.

### Fields

- `IReadOnlyList<IPostLayoutPass> Passes`

### Methods

- `LayoutResult Run(LayoutGraph graph, LayoutResult result, MermaidLikeLayoutContext context)`

## `ClusterTitleMarginPass`

### Responsibility

Applies Mermaid-like title-driven vertical offset after layout.

### Methods

- `LayoutResult Execute(...)`

### What It Does

- expands cluster bounds for title space
- shifts visible child content if needed
- updates edge attachment points

## `ClusterBoundsPolishPass`

### Responsibility

Final cluster rectangle cleanup.

### Methods

- `LayoutResult Execute(...)`

### What It Does

- enforces minimum width from title
- enforces minimum padding around contents
- smooths nested-cluster bounds

## `RoutedEdgeGeometryPass`

### Responsibility

Produces final render paths after node and cluster bounds are known.

### Fields

- `EdgeRoutingService Router`
- `ClusterBoundaryClipper Clipper`

### Methods

- `LayoutResult Execute(...)`

## Measurement Architecture

## `LayoutMeasurementService`

### Responsibility

Provides real measured sizes for nodes and cluster titles.

### Fields

- `MeasurementCache Cache`
- `NodeMeasurementRenderer Renderer`

### Methods

- `Vector2 MeasureNode(LayoutNode node)`
- `ClusterTitleMetrics MeasureClusterTitle(LayoutCluster cluster)`

## `NodeMeasurementRenderer`

### Responsibility

Creates hidden UI Toolkit elements that match the viewer's real cards.

### Methods

- `VisualElement BuildNodeProbe(LayoutNode node)`
- `VisualElement BuildClusterTitleProbe(LayoutCluster cluster)`

## `MeasurementCache`

### Responsibility

Caches measurement results so relayouts stay fast.

### Fields

- `Dictionary<string, Vector2> NodeSizeCache`
- `Dictionary<string, ClusterTitleMetrics> ClusterTitleCache`

## Rendering Layer

## `GraphCanvasView`

### Responsibility

Consumes `LayoutResult` only.

### Notes

It should not:

- rank nodes
- choose namespace internals
- rewrite edges
- guess cluster bounds

Its job is to render:

- node cards
- cluster rectangles
- routed edge geometry

## Class Relationships

```text
TypeGraphBuilder
  -> LayoutGraphFactory
  -> LayoutGraph

MermaidLikeLayoutCoordinator
  -> MermaidLikeLayoutPipeline
  -> MermaidLikeCompoundLayoutEngine
  -> PostLayoutPipeline

MermaidLikeLayoutPipeline
  -> MeasurementPreparationPass
  -> CompoundHierarchyPass
  -> ExternalConnectionAnalysisPass
  -> RepresentativeAnchorSelectionPass
  -> BoundaryEdgeRewritePass
  -> InternalClusterExtractionPass
  -> SubgraphDirectionSelectionPass
  -> RecursiveSpacingPass

MermaidLikeCompoundLayoutEngine
  -> RankAssignmentService
  -> CrossingReductionService
  -> CoordinateAssignmentService
  -> ClusterBoundsService

PostLayoutPipeline
  -> ClusterTitleMarginPass
  -> ClusterBoundsPolishPass
  -> RoutedEdgeGeometryPass

RoutedEdgeGeometryPass
  -> EdgeRoutingService
  -> ClusterBoundaryClipper
```

## How This Differs From Our Current System

Today we still rely too much on one heuristic engine plus manual spacing rules.

To get closer to Mermaid, the architecture needs these specific upgrades:

1. real measurement before layout
2. explicit `HasExternalConnections` analysis
3. representative-anchor edge rewrite before layout
4. recursive extracted-subgraph solve with inherited spacing
5. post-layout cluster title margin pass

Those five changes matter more than any single spacing constant.

## Recommended Implementation Order

### Phase 1

- add `MeasurementPreparationPass`
- add `LayoutMeasurementService`
- start measuring real node cards and cluster titles

### Phase 2

- split external-connection detection from generic cluster hierarchy
- add `RepresentativeAnchorSelectionPass`
- upgrade `BoundaryEdgeRewritePass`

### Phase 3

- make extracted subgraphs use `SubgraphDirectionSelectionPass`
- implement `RecursiveSpacingPass`
- make recursive layout inherit parent spacing and add rank bonus

### Phase 4

- add `ClusterTitleMarginPass`
- add `ClusterBoundsPolishPass`

### Phase 5

- tighten viewer rendering so `GraphCanvasView` only consumes `LayoutResult`
- remove any remaining layout logic from the viewer layer

## Success Criteria

We should consider this architecture successful when:

- internal-only namespaces no longer look manually packed
- externally connected namespaces still preserve readable interiors
- cluster titles visibly affect final spacing like Mermaid
- the same graph can be relaid out without changing viewer code
- spacing changes happen by modifying passes and spacing profiles, not the canvas renderer
