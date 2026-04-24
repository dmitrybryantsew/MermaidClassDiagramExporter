# Unity Viewer Technical Design

## Purpose

This document defines the first concrete architecture for a Unity-native graph viewer that grows out of the current Mermaid exporter plugin.

The core idea is:

- the viewer should not depend on Mermaid as its internal model
- the project should have one shared graph data model
- both the Unity viewer and Mermaid export should be built on top of that shared model

That lets the subproject stay portable, easier to extend, and better suited for large graphs than a browser-based Mermaid workflow.

## Design Goals

- keep the subproject portable between Unity projects
- reuse the current class and relation discovery logic instead of re-inventing it in the UI layer
- support large graphs better than Mermaid browser rendering
- allow future enrichment with scene, prefab, and workflow-style relations
- preserve Mermaid export as a documentation and sharing format

## Non-Goals For V1

- no runtime tracing
- no full Roslyn semantic analysis
- no method-call graph reconstruction
- no dependency on xNode or Unity Experimental GraphView as the core architecture
- no requirement for perfect automatic layout before the viewer becomes useful

## Architecture Overview

The viewer should be split into four layers:

1. Graph domain
2. Graph extraction
3. Graph export
4. Graph viewer

The main rule is that data extraction and rendering stay separate.

### Data Flow

1. Unity selection or folder input is collected.
2. Types are resolved from scripts and loaded assemblies.
3. A shared `TypeGraph` is built from those types.
4. The graph is either:
   - rendered in the Unity viewer
   - exported to Mermaid
   - later saved as JSON or a snapshot asset

## Proposed Plugin Structure

Inside `Assets/Plugins/MermaidClassDiagramExporter`:

- `Core/`
- `Editor/Extraction/`
- `Editor/Export/`
- `Editor/Viewer/`
- `Editor/State/`
- `Dependencies/`

### Responsibilities

`Core/`

- graph data contracts
- graph build options
- graph filtering rules
- view-state contracts

`Editor/Extraction/`

- collect Unity objects, scripts, and folders
- resolve `Type` values
- build nodes, edges, and groups

`Editor/Export/`

- Mermaid output from shared graph data
- later JSON or debug export

`Editor/Viewer/`

- `EditorWindow`
- UI Toolkit layout
- canvas interaction
- node and edge rendering

`Editor/State/`

- serialized layout state
- user preferences
- last-open graph metadata

## Core Data Model

### `TypeGraph`

The root immutable-ish graph model used by exporters and viewers.

Suggested fields:

- `string Title`
- `IReadOnlyList<TypeNodeData> Nodes`
- `IReadOnlyList<TypeEdgeData> Edges`
- `IReadOnlyList<TypeGroupData> Groups`
- `TypeGraphMetadata Metadata`

### `TypeNodeData`

Represents one exported type.

Suggested fields:

- `string Id`
- `string DisplayName`
- `string FullName`
- `string Namespace`
- `string AssemblyName`
- `string AssetPath`
- `TypeNodeKind Kind`
- `bool IsProjectType`
- `bool IsMonoBehaviour`
- `bool IsScriptableObject`
- `IReadOnlyList<TypeMemberData> Members`

### `TypeMemberData`

Represents a field, property, or method.

Suggested fields:

- `string Name`
- `string Signature`
- `TypeMemberKind Kind`
- `TypeVisibility Visibility`
- `bool IsStatic`

### `TypeEdgeData`

Represents a relation between two nodes.

Suggested fields:

- `string FromNodeId`
- `string ToNodeId`
- `TypeEdgeKind Kind`
- `string Label`
- `bool IsStrongRelation`

Initial `TypeEdgeKind` values:

- `Inheritance`
- `Implements`
- `Association`

Planned future kinds:

- `SerializedReference`
- `SceneReference`
- `PrefabReference`
- `PublishesEvent`
- `SubscribesEvent`
- `ResolvesDependency`

### `TypeGroupData`

Represents a visual/logical grouping.

Suggested fields:

- `string Id`
- `string Label`
- `TypeGroupKind Kind`
- `string ParentGroupId`
- `IReadOnlyList<string> NodeIds`

Initial `TypeGroupKind` values:

- `Namespace`
- `Folder`
- `Assembly`

### `TypeGraphMetadata`

Suggested fields:

- `DateTime GeneratedAtUtc`
- `string SourceDescription`
- `GraphSourceKind SourceKind`
- `GraphBuildOptions Options`

## View State Model

Graph data and graph presentation should be separate.

### `GraphViewState`

This stores how the user is currently looking at a graph.

Suggested fields:

- `Vector2 Pan`
- `float Zoom`
- `Dictionary<string, NodeLayoutState> NodeLayouts`
- `HashSet<string> CollapsedGroupIds`
- `HashSet<TypeEdgeKind> HiddenEdgeKinds`
- `HashSet<TypeMemberKind> HiddenMemberKinds`
- `string SelectedNodeId`
- `string SearchText`

### `NodeLayoutState`

Suggested fields:

- `Vector2 Position`
- `bool IsPinned`
- `bool IsExpanded`

The viewer should be able to save and reload `GraphViewState` without changing the underlying graph data.

## Extraction Layer

The current exporter already contains most of the extraction logic, but it is inside one editor utility class.

That logic should be moved into dedicated services.

### `SelectionTypeCollector`

Responsibilities:

- collect selected Unity objects
- resolve project-defined `Type` values
- normalize and deduplicate results

### `FolderTypeCollector`

Responsibilities:

- scan project folders recursively
- load `MonoScript` assets
- resolve `MonoScript.GetClass()`
- deduplicate and sort discovered types

### `TypeGraphBuilder`

Responsibilities:

- build `TypeNodeData`
- infer `TypeEdgeData`
- create groups
- apply graph build options

### `GraphBuildOptions`

Initial options:

- include fields
- include properties
- include methods
- include inherited members or declared-only members
- include interfaces
- include associations
- group by namespace or folder
- member count cap per node

This should become the shared configuration object for both Mermaid export and the future viewer.

## Export Layer

### `MermaidGraphExporter`

The Mermaid exporter should consume `TypeGraph` rather than raw `Type` collections.

Responsibilities:

- convert graph nodes and groups into Mermaid class diagram structures
- map edge kinds to Mermaid relations
- apply export-specific formatting rules
- write `.mmd` and `.md` output

This refactor matters because it makes the Unity viewer and Mermaid exporter equally first-class outputs.

## Viewer Layer

The viewer should be built as a custom `EditorWindow` using UI Toolkit.

### `TypeGraphWindow`

The main editor window.

Responsibilities:

- build the visual tree
- own toolbar, canvas, and details panel
- request graph builds
- load and save view state

Initial commands:

- build from selected classes
- build from selected project folder
- rebuild current graph
- focus selected node
- reset layout

### `GraphCanvasElement`

The main pannable and zoomable surface.

Responsibilities:

- host node visuals
- host edge layer
- translate pan and zoom
- manage selection and drag interactions

### `NodeElement`

Represents a single visible node.

Responsibilities:

- render title, type badges, and compact member list
- expose expand/collapse behavior
- support click, double-click, and drag

### `EdgeLayer`

Dedicated layer for drawing edges.

Responsibilities:

- draw relations efficiently
- hide detail when zoomed out
- highlight hovered or selected relations

### `InspectorPanel`

Details panel for the selected node or group.

Responsibilities:

- show script path
- show full member list
- show incoming and outgoing relations
- provide buttons to ping or open the script asset

## Visual Behavior For V1

The first viewer should optimize for exploration, not full fidelity.

### Node Display

At normal zoom:

- node name
- type kind badge
- a small limited member preview

At low zoom:

- node name only

On selection or expand:

- more members
- relation summary
- quick actions

### Group Display

V1 should support at least one grouping mode:

- namespace grouping

Folder grouping can be added immediately after if extraction metadata is already available.

Collapsed groups should hide internal nodes and summarize relation counts at the group boundary.

### Edge Display

V1 edges should support:

- separate colors or styles by edge kind
- hide/show by edge kind
- stronger visual priority for inheritance
- reduced detail at low zoom

## Initial Layout Strategy

The first layout algorithm should be intentionally simple and stable.

Recommended approach:

- group nodes by namespace
- place groups in columns
- place inheritance chains vertically inside groups
- place remaining nodes in a compact flow layout
- allow manual drag correction
- persist manual layout in `GraphViewState`

This is preferable to a complex auto-layout pass early on because predictable and editable layout is more useful than a fragile "smart" layout.

## Performance Strategy

Performance should be part of the design from the first implementation.

### Must-Have V1 Safeguards

- do not rebuild the graph on every UI interaction
- do not recreate all node visuals when selection changes
- keep edge rendering in its own lightweight layer
- cap visible member count per node by default
- hide labels and secondary detail at low zoom
- support group collapse from the beginning

### Planned Optimizations

- viewport-based node virtualization
- cached edge geometry
- incremental redraw when only a subset changes
- background graph build where safe in editor context
- optional deferred rendering for dense relation layers

## Persistence Strategy

Two persistence layers are useful:

### Short-Term Session State

Use `EditorPrefs` for:

- last graph source
- last folder path
- last active filters

### Durable Layout State

Use a small serialized asset or JSON sidecar for:

- node positions
- collapsed groups
- hidden relation kinds
- named saved views

This allows multiple reusable layouts for the same subsystem.

## Integration With Current Exporter

Refactoring path:

1. move type collection out of `MermaidClassDiagramExporter`
2. introduce `TypeGraphBuilder`
3. change Mermaid export to consume `TypeGraph`
4. add viewer window that consumes the same `TypeGraph`

The existing menu items can remain, but internally they should delegate to the shared graph services.

## Suggested Implementation Phases

### Phase 1: Shared Graph Core

- add `Core` graph data classes
- extract folder and selection collection into reusable services
- add `TypeGraphBuilder`
- refactor Mermaid export to use shared graph data

Deliverable:

- existing Mermaid export still works
- graph data can be built independently of Mermaid

### Phase 2: Basic UI Toolkit Viewer

- create `TypeGraphWindow`
- render nodes in a simple pannable and zoomable canvas
- support selection, search, and script ping/open
- build graph from selected folder

Deliverable:

- useful in-editor graph exploration for medium-sized graphs

### Phase 3: Readability And Filtering

- edge-kind toggles
- member-kind toggles
- group collapse
- saved layout state
- initial details panel

Deliverable:

- graph becomes usable on larger subsystems

### Phase 4: Better Layout And Scale

- improved initial placement
- cached layout
- viewport-based optimization
- folder and assembly grouping

Deliverable:

- stable viewer behavior on large project slices

### Phase 5: Graph Enrichment

- scene references
- prefab references
- ScriptableObject asset links
- event and DI overlays

Deliverable:

- graph moves from class structure into architecture exploration

## Technical Risks

### 1. Large Graph Density

Even a fast viewer can become unreadable if too much is shown at once.

Mitigation:

- filtering
- collapsed groups
- member caps
- edge-kind toggles

### 2. Incomplete Type Resolution

`MonoScript.GetClass()` does not provide full source analysis.

Mitigation:

- document the limitation clearly
- keep the graph builder tolerant of missing data
- consider optional Roslyn-based enrichment only later if needed

### 3. UI Toolkit Graph Complexity

A custom canvas is more work than using a ready-made node framework.

Mitigation:

- keep V1 interaction simple
- separate graph model from rendering
- prototype layout and interaction incrementally

## Why This Path Instead Of xNode

`xNode` is a good tool for authoring custom node graphs, but our primary problem is generated architecture exploration.

This design favors:

- a shared graph model
- portable extraction logic
- control over large-graph performance
- future non-class relation layers

That makes a custom UI Toolkit viewer the better long-term fit, even if it takes a little more setup at the start.

## Immediate Next Step

The next implementation task should be Phase 1:

- create the shared graph data model
- extract the current collection and relation logic out of the exporter
- keep current Mermaid export behavior working while preparing for the viewer window
