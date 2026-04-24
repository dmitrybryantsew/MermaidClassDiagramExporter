# Interactive Layout Editing Architecture

## Goal

Add direct manipulation to the Unity type-graph viewer so users can:

- drag classes to new positions
- drag namespaces as grouped containers
- see related edges redraw while dragging
- keep manual layout decisions instead of losing them on rebuild
- optionally trigger local re-layout instead of recomputing the whole graph every time

This is the right next step if the viewer is becoming a real in-Unity graph tool instead of only a layout preview.

## Difficulty

Estimated difficulty:

- class dragging only: medium
- class dragging with persisted positions and live edge redraw: medium
- namespace dragging that moves all descendants cleanly: medium
- namespace dragging plus partial local re-layout of neighbors: medium-high
- full incremental compound re-layout with pinned/manual constraints: high

So yes, this is very doable, but it is not a tiny patch if we want it to feel solid.

The easiest useful slice is:

1. draggable classes
2. draggable namespaces
3. redraw routed edges live
4. persist manual positions
5. keep full rebuild as a fallback

## Design Principle

Do not store drag state inside `GraphCanvasView` as ad hoc offsets.

Instead, introduce an editable session layer:

- `TypeGraph` remains the structural graph
- `LayoutGraph` remains the layout input graph
- `LayoutResult` remains the computed geometry snapshot
- `EditableLayoutSession` becomes the live user-edit state on top of the computed layout

That gives us a clean split between:

- structural truth
- computed layout truth
- user manual overrides

## Target Flow

1. `TypeGraphWindow` builds `TypeGraph`
2. `GraphLayoutCoordinator` creates base `LayoutResult`
3. `InteractiveLayoutCoordinator` creates or refreshes `EditableLayoutSession`
4. `GraphCanvasView` renders from the editable session geometry
5. user drags node or namespace
6. session updates live positions
7. affected edges are re-routed for preview
8. on drop, manual overrides are committed
9. optional local re-layout runs for impacted region

## Main Runtime Objects

### `EditableLayoutSession`

Purpose:

- hold the current editable geometry for one loaded graph view
- track manual overrides separately from base layout
- answer "where is this node or cluster right now?"

Suggested fields:

- `TypeGraph Graph`
- `LayoutResult BaseLayout`
- `EditableGeometryState Geometry`
- `ManualLayoutOverrides Overrides`
- `InteractiveSelectionState Selection`
- `InteractiveDragState Drag`
- `EditableRoutingState Routing`
- `string SourceKey`

Suggested methods:

- `Initialize(TypeGraph graph, LayoutResult layout, string sourceKey)`
- `Rect GetNodeRect(string nodeId)`
- `Rect GetClusterRect(string clusterId)`
- `void BeginNodeDrag(string nodeId, Vector2 pointerPosition)`
- `void BeginClusterDrag(string clusterId, Vector2 pointerPosition)`
- `void UpdateDrag(Vector2 pointerPosition)`
- `void CommitDrag()`
- `void CancelDrag()`
- `void ResetManualOverrides()`
- `LayoutResult BuildRenderLayout()`

Relations:

- owned by `TypeGraphWindow`
- consumed by `GraphCanvasView`
- uses `InteractiveRelayoutCoordinator`

### `EditableGeometryState`

Purpose:

- store the mutable geometry actually rendered right now

Suggested fields:

- `Dictionary<string, Rect> NodeRects`
- `Dictionary<string, Rect> ClusterRects`
- `Dictionary<string, string> NodeClusterIds`
- `Dictionary<string, LayoutClusterVisual> ClusterVisuals`
- `Vector2 ContentSize`

Suggested methods:

- `EditableGeometryState Clone()`
- `void ApplyNodeOffset(string nodeId, Vector2 delta)`
- `void ApplyClusterOffset(string clusterId, Vector2 delta, IReadOnlyCollection<string> descendantNodeIds, IReadOnlyCollection<string> descendantClusterIds)`
- `void ExpandClusterToContain(string clusterId, Rect childRect, float padding)`
- `void RecomputeContentSize()`

### `ManualLayoutOverrides`

Purpose:

- persist user-authored positions separately from computed layout

Suggested fields:

- `Dictionary<string, Vector2> NodePositions`
- `Dictionary<string, Vector2> ClusterPositions`
- `HashSet<string> PinnedNodes`
- `HashSet<string> PinnedClusters`
- `int Version`

Suggested methods:

- `bool TryGetNodePosition(string nodeId, out Vector2 position)`
- `bool TryGetClusterPosition(string clusterId, out Vector2 position)`
- `void SetNodePosition(string nodeId, Vector2 position)`
- `void SetClusterPosition(string clusterId, Vector2 position)`
- `void RemoveNodePosition(string nodeId)`
- `void RemoveClusterPosition(string clusterId)`
- `void Clear()`

Persistence options:

- best editor-only option: `ScriptableSingleton<ManualLayoutOverrideStore>`
- portable option: JSON under `Library/MermaidClassDiagramExporter/`
- shareable option: asset under `Assets/Editor Default Resources/` or `ProjectSettings/`

Recommended default:

- store in `Library/` first so the project is not polluted with layout noise

### `InteractiveSelectionState`

Purpose:

- hold selected node, selected namespace, and later multi-selection

Suggested fields:

- `string SelectedNodeId`
- `string SelectedClusterId`
- `HashSet<string> SelectedNodeIds`
- `HashSet<string> SelectedClusterIds`

Suggested methods:

- `void SelectNode(string nodeId)`
- `void SelectCluster(string clusterId)`
- `void Clear()`

### `InteractiveDragState`

Purpose:

- track active drag gesture

Suggested fields:

- `bool IsDragging`
- `EditableDragTargetKind TargetKind`
- `string TargetId`
- `Vector2 DragStartPointer`
- `Vector2 LastPointer`
- `Vector2 TotalDelta`
- `Rect OriginalNodeRect`
- `Rect OriginalClusterRect`
- `Dictionary<string, Rect> SnapshotNodeRects`
- `Dictionary<string, Rect> SnapshotClusterRects`

Suggested methods:

- `void BeginNodeDrag(...)`
- `void BeginClusterDrag(...)`
- `void Update(Vector2 pointerPosition)`
- `void End()`
- `void Reset()`

## Interaction Layer

### `GraphInteractionController`

Purpose:

- centralize hit-testing and drag gestures
- keep raw interaction logic out of `GraphCanvasView`

Suggested fields:

- `EditableLayoutSession Session`
- `GraphCanvasView Canvas`
- `NamespaceHitTestService NamespaceHitTest`
- `PointerGestureClassifier GestureClassifier`

Suggested methods:

- `void Attach(GraphCanvasView canvas, EditableLayoutSession session)`
- `void OnMouseDown(MouseDownEvent evt)`
- `void OnMouseMove(MouseMoveEvent evt)`
- `void OnMouseUp(MouseUpEvent evt)`
- `void OnMouseLeave(MouseLeaveEvent evt)`

Responsibilities:

- decide whether the pointer is dragging a node, a cluster, or panning the viewport
- capture drag intent
- forward drag updates into the session

### `NamespaceHitTestService`

Purpose:

- detect when the cursor is over a namespace body vs over a node inside it

Suggested methods:

- `string HitTestCluster(IReadOnlyDictionary<string, Rect> clusterRects, Vector2 contentPosition)`
- `bool IsClusterHeaderHit(Rect clusterRect, ClusterTitleMetrics titleMetrics, Vector2 localPosition)`
- `bool IsBodyHit(Rect clusterRect, Vector2 localPosition)`

Recommended behavior:

- dragging a namespace should preferably start from its title/header region
- this avoids accidental namespace drags when the user intended to drag a class inside it

## Layout / Relayout Layer

### `InteractiveLayoutCoordinator`

Purpose:

- own the bridge between base layout and manual editing
- apply overrides on top of newly rebuilt layouts

Suggested fields:

- `GraphLayoutCoordinator BaseCoordinator`
- `ManualLayoutOverrideStore OverrideStore`
- `InteractiveRelayoutCoordinator RelayoutCoordinator`

Suggested methods:

- `EditableLayoutSession CreateSession(TypeGraph graph, string sourceKey)`
- `void ApplyStoredOverrides(EditableLayoutSession session)`
- `void SaveOverrides(EditableLayoutSession session)`
- `void ResetOverrides(string sourceKey)`

### `InteractiveRelayoutCoordinator`

Purpose:

- decide how much of the graph to recompute after a drag

Suggested methods:

- `void PreviewNodeDrag(EditableLayoutSession session, string nodeId, Vector2 delta)`
- `void PreviewClusterDrag(EditableLayoutSession session, string clusterId, Vector2 delta)`
- `void CommitNodeDrag(EditableLayoutSession session, string nodeId)`
- `void CommitClusterDrag(EditableLayoutSession session, string clusterId)`
- `void RelayoutImpactedRegion(EditableLayoutSession session, LocalRelayoutRequest request)`

Recommended modes:

- `LiveTranslate`
  - move dragged geometry directly
  - re-route only affected edges
- `CommitLocalRelayout`
  - after drop, re-layout impacted cluster or neighborhood
- `FullRebuildFallback`
  - if constraints become invalid, rebuild everything and reapply manual overrides

### `LocalRelayoutRequest`

Purpose:

- describe the area that should be recomputed after a drag

Suggested fields:

- `EditableDragTargetKind TargetKind`
- `string TargetId`
- `HashSet<string> ImpactedNodeIds`
- `HashSet<string> ImpactedClusterIds`
- `bool PreserveDraggedElementAbsolutePosition`
- `bool PreservePinnedNeighbors`

### `ConstraintOverlay`

Purpose:

- represent manual positions as layout constraints the engine can respect

Suggested fields:

- `Dictionary<string, Rect> FixedNodeRects`
- `Dictionary<string, Rect> FixedClusterRects`
- `HashSet<string> PinnedNodeIds`
- `HashSet<string> PinnedClusterIds`

Suggested methods:

- `bool IsNodePinned(string nodeId)`
- `bool IsClusterPinned(string clusterId)`

This is the bridge we eventually need if partial re-layout should feel intentional instead of fighting the user.

## Routing / Redraw Layer

### `EditableRoutingState`

Purpose:

- cache routed edges for the current editable geometry

Suggested fields:

- `List<LayoutEdgePath> EdgePaths`
- `HashSet<string> DirtyEdgeIds`

Suggested methods:

- `void InvalidateAll()`
- `void InvalidateForNode(string nodeId)`
- `void InvalidateForCluster(string clusterId)`

### `InteractiveEdgeRoutingService`

Purpose:

- reroute only the affected edges during drag

Suggested fields:

- `EdgeRoutingService BaseRoutingService`

Suggested methods:

- `IReadOnlyList<LayoutEdgePath> RouteAll(TypeGraph graph, EditableGeometryState geometry)`
- `void RerouteAffectedEdges(TypeGraph graph, EditableGeometryState geometry, EditableRoutingState routing, IReadOnlyCollection<string> affectedNodeIds, IReadOnlyCollection<string> affectedClusterIds)`

Recommended behavior:

- during drag, reroute only impacted edges
- on drop, optionally reroute all edges if cheaper than tracking diff complexity

## Viewer Layer Changes

### `GraphCanvasView`

Current role:

- mostly renderer with panning, zooming, selection, and edge drawing

Target role:

- renderer plus drag event surface
- not the owner of editable geometry rules

Changes needed:

- replace `SetGraph(TypeGraph, LayoutResult)` with `SetSession(EditableLayoutSession)`
- render from `session.Geometry` and `session.Routing`
- expose cluster hit events in addition to node hit events
- keep panning and zooming
- stop owning drag business logic except maybe pointer forwarding

Suggested new events:

- `event Action<string> ClusterSelected`
- `event Action<GraphDragEvent> DragStarted`
- `event Action<GraphDragEvent> DragUpdated`
- `event Action<GraphDragEvent> DragEnded`

### `TypeNodeElement`

Current role:

- render a class card and raise click/double-click

Changes needed:

- add drag affordance
- optionally expose drag handle semantics later

Suggested additions:

- `event Action<TypeNodeData, Vector2> DragStarted`
- `event Action<TypeNodeData, Vector2> DragUpdated`
- `event Action<TypeNodeData, Vector2> DragEnded`

Recommended first version:

- keep node dragging coordinated at `GraphCanvasView` level using node rect hit-testing
- do not push full gesture logic into each node element yet

### `TypeGraphWindow`

Current role:

- build graph
- own `GraphCanvasView`
- own inspector

Target role:

- own `InteractiveLayoutCoordinator`
- create session after each graph build
- save and restore manual layout overrides
- offer UI actions like:
  - `Reset Manual Layout`
  - `Pin Selection`
  - `Unpin Selection`
  - `Relayout Selected Namespace`

Suggested fields:

- `InteractiveLayoutCoordinator interactiveLayoutCoordinator`
- `EditableLayoutSession currentSession`

## Drag Behavior Rules

### Dragging a class

Expected behavior:

- dragged class follows cursor
- connected edges redraw live
- namespace may expand if needed
- other nodes stay where they are during live drag
- on drop, class position becomes a manual override

Optional later behavior:

- local sibling compaction inside the namespace
- snap-to-guides

### Dragging a namespace

Expected behavior:

- all descendant classes and child namespaces move together
- cross-namespace edges redraw live
- namespace keeps its internal relative arrangement during drag
- on drop, cluster position becomes a manual override

Recommended rule:

- moving a namespace should not rewrite the internal layout of its children during live drag
- it should behave like moving a grouped frame

### Rebuild behavior

When the user rebuilds the graph from the same source:

- structural graph is refreshed
- base layout is recomputed
- stored overrides are matched by stable ids
- surviving nodes/clusters get their manual positions reapplied
- removed ids are discarded
- new ids use computed layout positions

## Redraw Strategy

### During drag

Use cheap live preview:

- update dragged node or cluster rects immediately
- update dependent cluster bounds if needed
- reroute only affected edges
- repaint canvas

### On drop

Use stable commit path:

- write override positions
- normalize cluster containment
- optionally run local re-layout for impacted neighborhood
- reroute all impacted edges
- repaint canvas

This gives a responsive feel without forcing a full layout solve on every mouse move.

## Persistence Model

Recommended key:

- hash of graph source kind + source path/selection signature + graph title

Store format:

- `ManualLayoutOverrideStore`
  - `Dictionary<string, ManualLayoutOverrides> Sessions`

Why this matters:

- dragging is only truly useful if the layout survives window close, recompiles, and rebuilds

## Suggested File Structure

Under `Assets/Plugins/MermaidClassDiagramExporter/Editor/Viewer/Interaction/`:

- `GraphInteractionController.cs`
- `NamespaceHitTestService.cs`
- `GraphDragEvent.cs`
- `InteractiveSelectionState.cs`
- `InteractiveDragState.cs`

Under `Assets/Plugins/MermaidClassDiagramExporter/Editor/Layout/Interactive/`:

- `EditableLayoutSession.cs`
- `EditableGeometryState.cs`
- `EditableRoutingState.cs`
- `InteractiveLayoutCoordinator.cs`
- `InteractiveRelayoutCoordinator.cs`
- `LocalRelayoutRequest.cs`
- `ConstraintOverlay.cs`
- `ManualLayoutOverrides.cs`
- `ManualLayoutOverrideStore.cs`

## Implementation Phases

### Phase 1: Manual Dragging Foundation

- add `EditableLayoutSession`
- add `ManualLayoutOverrides`
- add node dragging
- add namespace dragging from cluster header
- redraw all edges live
- save overrides

This is the minimum valuable version.

### Phase 2: Smarter Live Redraw

- reroute only affected edges
- auto-expand namespace bounds while dragging a node
- preserve cluster containment rules

### Phase 3: Incremental Local Relayout

- add `ConstraintOverlay`
- add local re-layout for impacted namespace or neighborhood
- preserve dragged element absolute position after drop

### Phase 4: Editing UX

- pin/unpin
- reset manual layout
- relayout selected namespace
- snap guides
- multi-select drag

## Recommended First Implementation Choice

Do this first:

1. `EditableLayoutSession`
2. `ManualLayoutOverrides`
3. node drag
4. namespace drag
5. redraw all edges on drag
6. persist positions

Do not start with:

- full incremental compound re-layout
- multi-select
- fancy snapping
- physics or animation

Those are good later, but they will slow down the first usable version.

## Why This Fits The Current Code

This architecture matches the current codebase well because:

- `TypeGraphWindow` already owns graph rebuilds
- `GraphLayoutCoordinator` already produces a reusable `LayoutResult`
- `GraphCanvasView` is already close to a pure renderer
- routed edges already exist, so live redraw is an incremental step rather than a brand-new subsystem
- cluster visuals already come from layout data, which makes namespace dragging much easier than if the viewer were reconstructing them ad hoc

## Bottom Line

Movable classes and namespaces are very achievable.

The important design choice is to add:

- an editable session layer
- a manual override store
- an interaction controller

instead of trying to bolt drag offsets straight into the canvas.

That path gives us a first usable drag-and-drop editor quickly, and it leaves room for the harder but valuable next step later: partial constrained re-layout that respects user-pinned positions.
