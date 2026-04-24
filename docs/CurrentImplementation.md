# Current Implementation

## What Has Been Done

The current exporter is a Unity editor-only tool built around the `FoggyBalrog.MermaidDotNet` library.

What was added:

- built `FoggyBalrog.MermaidDotNet` locally from source
- bundled the built dependency DLLs into a portable Unity plugin folder
- added a Unity editor menu tool that exports Mermaid class diagrams
- added recursive folder scanning for `.cs` files inside the project
- added convenience actions for revealing, copying, and opening exports
- added a UI Toolkit type-graph viewer that consumes the same shared graph model
- refactored layout into a dedicated layout subsystem with simple and layered engines
- started Mermaid-inspired cluster-boundary edge normalization for cross-namespace relations

The active plugin code now lives at:

- `Assets/Plugins/MermaidClassDiagramExporter/Editor/MermaidClassDiagramExporter.cs`
- `Assets/Plugins/MermaidClassDiagramExporter/Editor/Viewer/TypeGraphWindow.cs`
- `Assets/Plugins/MermaidClassDiagramExporter/Editor/Layout/`

The bundled DLL dependencies are:

- `Assets/Plugins/MermaidClassDiagramExporter/Dependencies/FoggyBalrog.MermaidDotNet.dll`
- `Assets/Plugins/MermaidClassDiagramExporter/Dependencies/YamlDotNet.dll`

## Current Unity Menu

The plugin currently adds these menu items:

- `Tools > Mermaid > Export Selected Classes`
- `Tools > Mermaid > Export Classes From Folder...`
- `Tools > Mermaid > Export Classes From Selected Project Folder`
- `Tools > Mermaid > Reveal Last Export`
- `Tools > Mermaid > Copy Last Export To Clipboard`
- `Tools > Mermaid > Open Mermaid Live Editor`

The type graph viewer toolbar now also includes relation-visibility toggles for:

- `Inheritance`
- `Implements`
- `Associations`
- `Focus D1`
- `Focus D2`
- `Focus D3`
- `Add Seed`
- `Remove Seed`
- `Clear Seeds`
- `Traversal: Undirected / Outgoing / Incoming / All Visible`
- `Back`
- `Reset To Root`

## How It Works Now

### Selected-object export

`Export Selected Classes` collects types from:

- selected `MonoScript` assets
- selected `GameObject` components
- selected `Component` objects
- selected `ScriptableObject` instances

### Folder export

`Export Classes From Folder...` and `Export Classes From Selected Project Folder`:

- scan the chosen project folder recursively for `.cs` files
- load each file as a `MonoScript`
- call `MonoScript.GetClass()` to resolve the main type Unity exposes for that script
- skip scripts that do not resolve to a loadable class

## What The Diagram Includes

For each collected type, the exporter currently writes:

- the class name
- an annotation when the type is an interface, enum, abstract class, or static class
- public fields
- public properties without indexers
- public, protected, and internal methods

The exporter currently infers these relationships when both sides are present in the exported set:

- inheritance from base classes
- implemented interfaces
- associations from field and property types
- associations through arrays, nullable wrappers, and generic arguments

## Current Viewer And Layout State

The in-Unity viewer now builds layout from the shared `TypeGraph` model instead of laying nodes out directly inside the canvas.

Current layout pipeline:

- `LayoutGraphFactory` converts `TypeGraph` into layout nodes, edges, namespace clusters, member-line payloads, and badge/title metadata
- `LayoutPipeline` runs named layout-preparation passes
- `MeasurementPreparationPass` now performs a real pre-layout measurement stage using `LayoutMeasurementService` so real node widths/heights and cluster title metrics exist before the graph is ranked
- `ClusterHierarchyPass` now focuses on explicit compound hierarchy data such as parent and child cluster relationships
- `ExternalConnectionAnalysisPass` now computes Mermaid-like `HasExternalConnections` facts for each cluster
- `RepresentativeAnchorSelectionPass` now selects a stable real class inside each cluster to act as the boundary representative when cross-cluster relations need rewriting
- `SelfLoopExpansionPass` rewrites self-loop edges into helper-node chains before layout
- `InternalClusterExtractionPass` now focuses on identifying internal-only namespace clusters and materializing them as `LayoutSubgraph` records with their own root cluster
- `SubgraphDirectionSelectionPass` now assigns Mermaid-like opposite-axis local direction to extracted namespace subgraphs
- `RecursiveSpacingPass` now applies inherited recursive spacing so extracted subgraphs keep parent node separation and gain extra rank separation
- `BoundaryEdgeNormalizationPass` now rewrites cross-cluster edges through hidden anchor nodes and, when available, through the selected representative real nodes so external traffic pulls less directly on arbitrary interior classes
- `GraphLayoutCoordinator` runs the normalized graph through the active layout engine
- `LayeredLayoutEngine` can now recurse into extracted internal-only subgraphs, reuse their nested result as the cluster's internal layout, and reserve namespace space using measured title metrics instead of only fixed top padding
- `PostLayoutPipeline` now runs `ClusterTitleMarginPass`, `ClusterBoundsPolishPass`, and `ClusterOverlapResolutionPass` after the engine so title-driven inset, final namespace bounds, and sibling namespace reflow are adjusted before edge routing
- `CrossingReductionService` now owns rank-neighbor row ordering and transpose-style crossing reduction instead of keeping that logic hidden inside the engine
- `EdgeRoutingService` now builds renderable edge paths from node and cluster bounds after layout
- `ClusterBoundaryClipper` provides the first pass of cluster-boundary intersection logic for routed edges
- `GraphCanvasView` now renders routed edge paths from `LayoutResult` instead of inventing all edge geometry on its own
- `GraphCanvasView` now adds relation markers for inheritance and implementation edges
- `TypeGraphWindow` and `GraphCanvasView` now support per-edge-kind visibility toggles for inheritance, implementation, and association relations
- `GraphCanvasView` now renders namespace boxes from `LayoutResult.ClusterVisuals` instead of rebuilding group visuals from `TypeGraph.Groups`
- `EdgeRoutingService` now also reads node-to-cluster ownership from `LayoutResult` so routing follows the resolved layout graph rather than the original structural group list
- `FocusedGraphNavigationController` now lets the viewer derive focused subgraphs from the currently loaded graph without rescanning source files
- `FocusedSubgraphBuilder` now slices a new `TypeGraph` from the currently displayed graph using the selected node as a seed and association depth `1`, `2`, or `3`
- `GraphViewSession` now keeps the root graph plus a back stack so focused views can be undone instantly
- `TypeGraphMetadata` now carries explicit focused-view metadata such as whether the graph is derived, the parent graph title, the focus summary, seed node ids, and focus depth
- `TypeGraphWindow` now enables or disables focus actions based on whether a node is selected, and the status bar now explains when the current graph is a derived focused view
- `TypeGraphInspectorView` now shows focused-view summary details when the current graph is derived from a parent view
- `GraphSeedSelectionState` now allows multi-seed focus by staging several selected nodes as focus seeds before building a union neighborhood subgraph
- `FocusedGraphNavigationController` now supports multi-seed focus requests, not only single-node focus
- the status bar now also shows the staged seed-set size when one exists
- focused subgraph traversal can now be switched between undirected associations, outgoing-only associations, incoming-only associations, and all visible relations

The current normalization pass is intentionally modest.

It does not yet try to fully reproduce Mermaid or Dagre cluster handling.
It does give cross-namespace edges stable cluster entry and exit points so later ranking and crossing-reduction work can build on a cleaner graph.
The new pass-based pipeline also means future steps like cluster extraction, self-loop expansion, and measurement-aware rewrites can now be added without hiding more behavior inside the layout engine itself.

The layered engine now also distinguishes between two namespace cases:

- namespaces with external connections keep the anchor-aware boundary layout
- internal-only namespaces can use a local subgraph-style layout that spreads nodes by local ranks and row ordering instead of only height-balanced columns
- extracted internal-only namespaces can now be laid out through a recursive layout pass instead of only through direct one-off cluster heuristics

Externally connected namespaces no longer have to place their real classes with the old height-balanced grid packing.
Their anchor nodes still stay at the namespace boundaries, but the core classes can now use the same local rank and row-ordering logic in the middle of the cluster.

The current structured namespace layout also includes:

- weak association-based rank spreading to break up flat single-rank rows
- explicit rank-band preservation so rows that come from different local ranks get larger vertical separation
- max-nodes-per-row wrapping so namespace contents stop collapsing into dense horizontal blocks
- structured-only horizontal spacing values that are looser than the general node spacing
- width-based row wrapping so dense namespaces do not stretch into one long strip
- lighter row offsetting instead of rigid full centering
- service-based crossing reduction before and after row wrapping so local and recursive layouts get the same ordering improvements
- post-layout edge routing that can add cluster exit and entry waypoints for cross-namespace relations
- orthogonal edge-path simplification so routed lines drop redundant intermediate bends before rendering
- cluster-pair corridor bundling so cross-namespace edges can share a common horizontal or vertical routing lane instead of each using a separate midpoint
- late fan-out routing so bundled cross-namespace edges stay together longer and only split near source and target cluster boundaries

## Output Files

Each export currently writes two files into `docs/Mermaid`:

- a raw Mermaid file: `.mmd`
- a Markdown wrapper with a fenced `mermaid` block: `.md`

The exporter also:

- copies the raw Mermaid text to the clipboard
- reveals the generated `.mmd` file in Explorer

Example output already generated in this project:

- `docs/Mermaid/Folder_Assets__TestTDTowerAndMeshCreation_Scripts.mmd`
- `docs/Mermaid/Folder_Assets__TestTDTowerAndMeshCreation_Scripts.md`

## Current Limitations

- It is a static code-structure exporter, not a runtime behavior tracer.
- It does not currently reconstruct actual method call flow.
- It does not yet read scene wiring, prefab references, DI container bindings, or serialized object graphs.
- `MonoScript.GetClass()` is not a full Roslyn analysis pass, so multi-type files and unusual script layouts may not be represented completely.
- Large graphs become visually heavy quickly, even when the export itself succeeds.
- The new cluster-boundary normalization is a first pass and still needs follow-up work for crossing reduction and better cluster ordering.
