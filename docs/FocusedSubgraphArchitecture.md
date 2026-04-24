# Focused Subgraph Architecture

## Goal

Add a viewer action that rebuilds the current graph into a smaller focused graph based on:

- the currently selected node or nodes
- association depth `N`
- optional inclusion rules for inheritance and interface edges

The focused graph should:

- be derived from the already loaded `TypeGraph`
- avoid rescanning source files
- preserve the previous graph so the user can go back immediately
- support repeated drill-down without losing navigation history

## Why This Should Not Be A Real "Rebuild From Source"

The current viewer already has a full in-memory `TypeGraph`.

That means the clean implementation is:

1. keep the loaded graph as the source graph
2. slice a smaller subgraph from it
3. push that focused graph onto a history stack
4. render the focused graph normally through the existing layout pipeline

So this feature is really:

- graph slicing
- focus navigation
- graph-view history

not:

- re-run folder scanning
- rebuild from reflection
- replace the project graph source permanently

## User Experience

Recommended toolbar actions:

- `Focus Selection`
- `Focus Depth 1`
- `Focus Depth 2`
- `Focus Depth 3`
- `Back`
- `Reset To Root`

Recommended first behavior:

- focus starts from the currently selected node
- traversal follows association edges up to depth `N`
- inheritance and implementation edges are included only when both endpoint nodes are already in the focused set

That gives the result users usually expect:

- a selected class
- the nearby dependency neighborhood
- structural inheritance lines still visible inside that neighborhood

## Design Principle

Treat each focused graph as a view state derived from a parent graph snapshot.

Do not mutate the root graph.

Do not replace the source graph cache.

Instead, add a graph-navigation stack.

## Main Runtime Objects

### `GraphViewSession`

Purpose:

- own the current root graph, current focused graph, and navigation stack for the window

Suggested fields:

- `TypeGraph RootGraph`
- `TypeGraph CurrentGraph`
- `Stack<GraphViewSnapshot> BackStack`
- `Stack<GraphViewSnapshot> ForwardStack`
- `GraphFocusState FocusState`
- `string SourceKey`

Suggested methods:

- `void Initialize(TypeGraph rootGraph, string sourceKey)`
- `void PushFocusedGraph(TypeGraph focusedGraph, GraphFocusRequest request)`
- `bool CanGoBack()`
- `bool CanGoForward()`
- `GraphViewSnapshot GoBack()`
- `GraphViewSnapshot GoForward()`
- `void ResetToRoot()`

### `GraphViewSnapshot`

Purpose:

- capture one view in the navigation stack

Suggested fields:

- `TypeGraph Graph`
- `GraphFocusRequest Request`
- `string Title`
- `DateTime CreatedAtUtc`
- `string SelectedNodeId`

Suggested methods:

- none required beyond data storage

### `GraphFocusState`

Purpose:

- describe what kind of focus is active right now

Suggested fields:

- `bool IsFocusedView`
- `GraphFocusRequest CurrentRequest`
- `string RootGraphTitle`
- `string CurrentGraphTitle`

### `GraphFocusRequest`

Purpose:

- describe how the focused graph should be sliced

Suggested fields:

- `IReadOnlyList<string> SeedNodeIds`
- `int AssociationDepth`
- `GraphFocusTraversalMode TraversalMode`
- `bool IncludeIncomingAssociations`
- `bool IncludeOutgoingAssociations`
- `bool IncludeInheritanceInsideFocusedSet`
- `bool IncludeImplementsInsideFocusedSet`
- `bool IncludeSeedNamespaces`
- `bool PreserveGroupKinds`

Recommended defaults:

- `AssociationDepth = 1`
- `TraversalMode = UndirectedAssociations`
- `IncludeIncomingAssociations = true`
- `IncludeOutgoingAssociations = true`
- `IncludeInheritanceInsideFocusedSet = true`
- `IncludeImplementsInsideFocusedSet = true`

### `GraphFocusTraversalMode`

Purpose:

- clarify how BFS expands

Suggested enum values:

- `UndirectedAssociations`
- `OutgoingAssociationsOnly`
- `IncomingAssociationsOnly`
- `AllVisibleRelations`

Recommended first implementation:

- only `UndirectedAssociations`
- keep the enum so future expansion is clean

## Graph Slicing Layer

### `FocusedSubgraphBuilder`

Purpose:

- produce a new `TypeGraph` from an existing `TypeGraph` and a `GraphFocusRequest`

Suggested methods:

- `TypeGraph BuildFocusedGraph(TypeGraph sourceGraph, GraphFocusRequest request)`
- `HashSet<string> CollectFocusedNodeIds(TypeGraph sourceGraph, GraphFocusRequest request)`
- `IReadOnlyList<TypeEdgeData> FilterEdges(TypeGraph sourceGraph, IReadOnlyCollection<string> nodeIds, GraphFocusRequest request)`
- `IReadOnlyList<TypeGroupData> FilterGroups(TypeGraph sourceGraph, IReadOnlyCollection<string> nodeIds, GraphFocusRequest request)`
- `TypeGraphMetadata BuildFocusedMetadata(TypeGraph sourceGraph, GraphFocusRequest request)`

Responsibilities:

- breadth-first traversal from selected seeds
- node filtering
- edge filtering
- group filtering
- focused graph title / metadata

### `GraphNeighborhoodIndex`

Purpose:

- cache adjacency so repeated focus operations are cheap

Suggested fields:

- `Dictionary<string, TypeNodeData> NodesById`
- `Dictionary<string, List<TypeEdgeData>> OutgoingEdgesByNodeId`
- `Dictionary<string, List<TypeEdgeData>> IncomingEdgesByNodeId`
- `Dictionary<string, List<TypeEdgeData>> AssociationEdgesByNodeId`
- `Dictionary<string, TypeGroupData> GroupsById`
- `Dictionary<string, List<string>> GroupIdsByNodeId`

Suggested methods:

- `static GraphNeighborhoodIndex Build(TypeGraph graph)`
- `IReadOnlyList<TypeEdgeData> GetOutgoing(string nodeId)`
- `IReadOnlyList<TypeEdgeData> GetIncoming(string nodeId)`
- `IReadOnlyList<TypeEdgeData> GetAssociations(string nodeId)`

Why it matters:

- once the user starts drilling in repeatedly, rebuilding adjacency every time becomes wasteful

### `FocusedTraversalService`

Purpose:

- own the BFS / depth traversal logic

Suggested methods:

- `HashSet<string> Traverse(TypeGraph graph, GraphNeighborhoodIndex index, GraphFocusRequest request)`
- `void ExpandAssociations(...)`
- `void ExpandAllVisibleRelations(...)`

Recommended first algorithm:

1. start with seed nodes
2. run BFS over association edges only
3. stop expansion when depth exceeds `AssociationDepth`
4. after node set is known, add inheritance and implementation edges only if both endpoints are already included

This keeps association depth semantics predictable.

## Group Handling

### `FocusedGroupProjector`

Purpose:

- rebuild group list for the focused graph without leaving empty namespaces behind

Suggested methods:

- `IReadOnlyList<TypeGroupData> ProjectGroups(TypeGraph sourceGraph, IReadOnlyCollection<string> includedNodeIds, GraphFocusRequest request)`
- `TypeGroupData ProjectGroup(TypeGroupData sourceGroup, IReadOnlyCollection<string> includedNodeIds)`

Recommended behavior:

- preserve namespace / assembly grouping labels
- keep only groups that still contain included nodes
- preserve group ids when possible

This is important because the existing layout pipeline depends on stable group data.

## Metadata Layer

### `FocusedGraphMetadataFactory`

Purpose:

- mark focused graphs as derived views

Suggested additions to `TypeGraphMetadata`:

- `bool IsDerivedView`
- `string ParentGraphTitle`
- `string FocusSummary`
- `IReadOnlyList<string> SeedNodeIds`
- `int FocusDepth`

If you want to avoid changing `TypeGraphMetadata` immediately, a simpler first step is:

- encode focus info into `SourceDescription`

Recommended better long-term version:

- add explicit metadata fields

## Window / Navigation Layer

### `FocusedGraphNavigationController`

Purpose:

- own focus actions and history transitions for `TypeGraphWindow`

Suggested fields:

- `GraphViewSession Session`
- `FocusedSubgraphBuilder FocusedSubgraphBuilder`
- `GraphNeighborhoodIndex CurrentIndex`

Suggested methods:

- `void SetRootGraph(TypeGraph graph, string sourceKey)`
- `bool CanFocusSelection(string selectedNodeId)`
- `void FocusSelection(string selectedNodeId, int depth)`
- `void FocusSelection(IReadOnlyList<string> selectedNodeIds, int depth)`
- `void GoBack()`
- `void GoForward()`
- `void ResetToRoot()`

Responsibilities:

- create focused graph
- push snapshots
- manage back stack
- rebuild neighborhood index when current graph changes

### `TypeGraphWindow`

Current role:

- owns current graph
- rebuilds from selection/folder
- owns viewer and inspector

New responsibilities:

- own `GraphViewSession`
- own `FocusedGraphNavigationController`
- expose toolbar buttons for focus and back navigation
- keep selection when possible across focused views

Suggested new fields:

- `GraphViewSession graphSession`
- `FocusedGraphNavigationController focusNavigationController`
- `ToolbarButton backButton`
- `ToolbarButton resetButton`

Suggested new methods:

- `void FocusCurrentSelection(int depth)`
- `void GoBackToPreviousGraph()`
- `void ResetToRootGraph()`
- `void UpdateNavigationButtons()`

## Selection Dependency

The feature depends on stable current selection.

Current state:

- `GraphCanvasView` supports single selected node
- `TypeGraphWindow` receives selection through `OnNodeSelected`

Recommended first version:

- support only one selected node as the focus seed

Later:

- allow multi-selection and use all selected nodes as BFS seeds

## Focused Graph Title Rules

Suggested focused title format:

- `Focused: TowerBlockService (depth 1)`
- `Focused: TowerBlockService + 2 more (depth 2)`

This helps the user immediately understand that they are not viewing the root graph anymore.

## Back Navigation Rules

When the user presses `Focus Selection`:

1. push current graph snapshot onto back stack
2. clear forward stack
3. set focused graph as current
4. layout/render current graph normally

When the user presses `Back`:

1. push current snapshot onto forward stack
2. restore previous snapshot from back stack
3. layout/render restored graph

When the user presses `Reset To Root`:

1. restore root graph
2. clear focus state
3. preserve root graph cache

## Caching Strategy

Recommended cache layers:

### Root graph cache

- keep the root `TypeGraph`
- do not rebuild it unless the user explicitly rebuilds from selection/folder

### Neighborhood cache

- cache adjacency for the current root graph
- invalidate only when the root graph changes

### Focused graph cache

Optional later optimization:

- cache focused results by `(seed ids, depth, traversal mode)`

This is nice later, but not needed for the first version because focused subgraph generation should already be cheap.

## Layout Behavior

Focused graphs should go through the normal path:

1. focused `TypeGraph`
2. `GraphLayoutCoordinator`
3. `LayoutResult`
4. viewer render

Do not special-case layout for focused graphs initially.

That keeps the feature low-risk and consistent with the rest of the viewer.

## Recommended File Structure

Under `Assets/Plugins/MermaidClassDiagramExporter/Editor/Focus/`:

- `GraphViewSession.cs`
- `GraphViewSnapshot.cs`
- `GraphFocusState.cs`
- `GraphFocusRequest.cs`
- `GraphFocusTraversalMode.cs`
- `FocusedSubgraphBuilder.cs`
- `FocusedTraversalService.cs`
- `GraphNeighborhoodIndex.cs`
- `FocusedGroupProjector.cs`
- `FocusedGraphNavigationController.cs`

## Suggested Implementation Phases

### Phase 1: Single-Node Focus

- add `GraphViewSession`
- add `GraphFocusRequest`
- add `FocusedSubgraphBuilder`
- add `GraphNeighborhoodIndex`
- add `Back` and `Reset To Root`
- add `Focus Depth 1/2/3` for selected node

This is the right first slice.

### Phase 2: Better Metadata And UX

- add explicit focused-view metadata
- improve toolbar state
- show focused-view summary in status label
- preserve last selected node when possible

### Phase 3: Multi-Seed Focus

- allow several selected nodes as seeds
- support union neighborhoods

### Phase 4: Advanced Traversal Options

- incoming-only or outgoing-only association traversal
- include all visible relations in traversal
- pin root nodes in focused layout

## Recommended First Behavior

For the first working version, keep it simple:

- single selected node
- association depth 1/2/3
- inheritance and implementation included only when already inside the focused node set
- back button
- reset to root button

That gives you the useful "show me this class and its nearby dependency graph" flow without turning the viewer into a query engine immediately.

## Why This Fits The Current Code

This architecture matches the plugin well because:

- `TypeGraph` already has stable node ids
- the graph already exists in memory after build
- the layout pipeline already accepts any `TypeGraph`
- the viewer already handles graph replacement cleanly through `SetGraph(...)`
- the current selection pipeline in `TypeGraphWindow` already gives us the seed node we need

## Bottom Line

Yes, this feature fits very naturally.

The proper architecture is:

- keep the root graph
- derive focused subgraphs from it
- push them onto a navigation stack
- run the normal layout pipeline on the focused result

That gives us:

- fast focus-by-selection
- depth-based association neighborhood views
- easy back navigation
- no expensive source rebuilds

and it leaves room for later multi-node focus and richer traversal rules.
