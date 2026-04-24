# Mermaid Layout Research

## Purpose

This note captures what the local Mermaid source is doing for class-diagram layout and what parts are useful for the Unity viewer.

Local source inspected:

- `E:\Games\UnityGames\importedUnityPackagesToAIWith\mermaid-develop`

The goal is not to copy Mermaid blindly.

The goal is to understand:

- what data Mermaid gives to layout
- how namespaces and clusters are represented
- what sequence it uses before and after layout
- which ideas are worth reusing in our Unity layout engine

## Short Answer

Mermaid does not hand-place class nodes.

It converts the class diagram into a generic layout graph, marks namespaces as compound group nodes, attaches classes and notes to those groups with `parentId`, then sends that graph through a shared layout engine.

For the modern renderer path, Mermaid uses the general rendering pipeline plus a registered layout algorithm, with `dagre` as the default fallback in the inspected source.

That means the useful idea for us is:

- separate graph extraction from layout
- treat namespaces as real clusters
- give layout a proper graph with parent-child group relationships
- let a layout engine decide positions

## Key Mermaid Findings

### 1. Mermaid class diagrams are converted into generic layout data

In the newer renderer path:

- [classRenderer-v3-unified.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts:39)
- [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:649)

Important behavior:

- `classDb.getData()` returns generic `nodes` and `edges`
- namespaces are emitted as `isGroup: true`
- classes are emitted as normal nodes with `parentId`
- notes are also emitted as nodes, sometimes with `parentId`
- relations are emitted as generic edges

This is a strong confirmation that our current direction is correct:

- `TypeGraph` should stay the structural source
- the viewer should consume layout-ready data, not layout itself

### 2. Namespaces are real parent groups, not just visual decorations

In the older class renderer:

- [classRenderer-v2.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v2.ts:33)

Important behavior:

- Mermaid creates namespace nodes
- it inserts classes under those namespaces
- it calls `g.setParent(vertex.id, parent)` for classes and notes inside namespaces

This matters a lot.

Mermaid does not say:

- "draw a yellow rectangle behind these nodes"

It says:

- "these nodes belong to this compound graph parent"

That is one of the biggest reasons Mermaid gets cleaner namespace grouping than our early viewer versions.

### 3. Mermaid uses a shared layout algorithm registry

Shared rendering entry point:

- [render.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/render.ts:30)

Important behavior:

- layout algorithms are registered by name
- the inspected branch registers `dagre` by default
- rendering picks the requested algorithm or falls back to `dagre`

This is useful for us architecturally.

We should keep the same idea:

- a layout coordinator
- a layout engine interface
- more than one engine behind the same viewer contract

That validates our:

- `IGraphLayoutEngine`
- `GraphLayoutCoordinator`
- `SimpleColumnLayoutEngine`
- `LayeredLayoutEngine`

### 4. Mermaid Dagre layout runs on a compound graph

In the shared dagre renderer:

- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:273)

Important behavior:

- Mermaid creates a graphlib graph with:
  - `multigraph: true`
  - `compound: true`
- it sets:
  - `rankdir`
  - `nodesep`
  - `ranksep`
  - margins
- it inserts all nodes
- if a node has `parentId`, Mermaid calls `graph.setParent(node.id, node.parentId)`

This is another strong signal:

- compound graph structure is a first-class part of Mermaid layout
- namespace placement is not a post-process hack

## Important Mermaid Sequence

The rough sequence in the modern Dagre path is:

1. build generic nodes and edges
2. create a compound graph
3. insert nodes
4. attach child nodes to parent groups with `setParent`
5. insert edges
6. rewrite special cases like self-loops
7. adjust clusters and edges
8. run Dagre layout
9. render nodes, clusters, and edges
10. apply final position adjustments

Relevant files:

- [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:649)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:302)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:378)

That sequence is useful because it shows Mermaid does not trust a naive "layout first, cluster after" approach.

It actively massages the graph before final layout.

## What `adjustClustersAndEdges` Is Doing

The most interesting Mermaid-specific helper is:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:201)

What it does conceptually:

- discovers descendants for each cluster
- tracks parent relationships
- detects whether edges leave a cluster
- gives clusters anchor nodes for external connections
- rewrites cluster-touching edges so Dagre sees stable endpoints
- extracts some nested cluster graphs for recursive rendering/layout

This is important because compound graph layout gets messy when:

- edges connect into or out of groups
- nested groups exist
- a cluster itself should not behave like a normal node endpoint

For us, the biggest lesson is not the exact code.

The lesson is:

- cluster-aware layout needs explicit edge handling

If we ignore that, namespace boxes will always feel "painted on" instead of structurally integrated.

## What Mermaid Is Doing Inside Namespaces

The important thing to notice is that Mermaid is not laying out namespace contents by file.

It lays out:

- classes
- notes
- cluster boxes

inside a compound graph.

Relevant files:

- [classRenderer-v2.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v2.ts:40)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:31)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:174)

The practical sequence is:

1. classes and notes are inserted as normal nodes
2. nodes inside a namespace get `parentId`
3. Dagre sees the namespace as a compound cluster
4. Mermaid rewrites cluster-touching edges
5. Mermaid either:
   - lets the cluster participate in the main graph if it has external connections
   - or extracts the cluster into its own recursive subgraph if it does not

That last part matters a lot for spacing.

For clusters without external connections, Mermaid creates a nested `clusterGraph` and lays it out recursively with its own graph settings.

In the inspected source, that nested graph uses:

- `nodesep: 50`
- `ranksep: 50`

and it may flip direction relative to the parent graph unless the cluster explicitly carries its own direction.

That means the nicer spacing inside some namespace boxes is not from a special "namespace packing" algorithm.
It is mostly from:

- letting Dagre rank and order only the nodes inside that namespace
- doing that in a smaller subgraph
- isolating internal-only namespace content from unrelated outside edges

For clusters with external connections, Mermaid keeps them in the parent graph and uses cluster-edge rewriting instead of extracting them into isolated nested layout.

So the smartest part is not "how Mermaid spaces files in a namespace".

The smartest part is:

- deciding when a namespace can be treated like its own local layout problem
- and when it must stay part of the larger graph because outside relations matter

## What This Means For Our Viewer

If we want namespace internals to feel more like Mermaid, the next likely improvement is not file-aware spacing.

It is one of these:

1. Add an internal-only cluster layout mode.
   If a namespace has no cross-cluster edges, lay it out as a small local graph instead of using the same generic cluster column logic.

2. Order nodes inside a namespace by local graph structure.
   Use local ranks plus barycenter ordering, instead of just balancing columns by height.

3. Distinguish anchor nodes from real nodes more strongly.
   Cross-cluster anchor nodes should influence the boundary, but should not dominate the arrangement of the namespace interior.

4. Optionally let a namespace choose a local direction.
   Mermaid sometimes flips nested direction relative to the parent graph, which helps avoid long thin stacks.

The key takeaway is:

- Mermaid does not know about source files here
- Mermaid does know about local graph structure inside a namespace cluster
- Mermaid improves spacing by recursively laying out internal-only clusters as separate subgraphs

## Additional Findings From A Deeper Source Pass

This section captures details that were only partially covered in the first research pass.

These details matter because they explain:

- where Mermaid's layout inputs really come from
- what parts are configurable versus hardcoded
- where node size is measured
- how cluster-aware edge rewriting affects the final visual result

## How Class Data Enters The Layout Pipeline

The class-diagram database is not just storing class metadata.

It is also building the exact generic node and edge payload later consumed by the shared renderer.

Relevant file:

- [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:649)

Important details:

- namespaces are emitted as `Node` objects with `isGroup: true`
- classes are emitted as normal nodes with `parentId`
- notes are emitted as normal nodes with `parentId`
- relations become generic `Edge` objects
- class diagram direction is passed through separately

More specifically:

- namespace creation happens in [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:556)
- namespace membership is assigned in [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:588)
- namespace nodes are emitted with `isGroup: true` and class padding in [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:658)
- class nodes carry `parentId: classNode.parent` in [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:673)

That means the renderer never "discovers" namespaces later.

The cluster structure is already present in the generic layout data.

## What Parts Of Mermaid Layout Are Configurable

The class-diagram config exposes only a small number of layout-facing knobs.

Relevant file:

- [config.type.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/config.type.ts:775)

For class diagrams, the layout-relevant options visible in the inspected type definitions are:

- `defaultRenderer`
- `nodeSpacing`
- `rankSpacing`
- `padding`
- `hideEmptyMembersBox`

Relevant lines:

- [config.type.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/config.type.ts:793)
- [config.type.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/config.type.ts:794)
- [config.type.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/config.type.ts:795)
- [config.type.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/config.type.ts:803)

This is useful because it tells us Mermaid does not seem to expose a special namespace layout strategy for class diagrams.

The visual cleanliness mostly comes from:

- compound graph structure
- cluster preprocessing
- Dagre layout
- measurement and post-layout adjustments

not from a large surface of class-specific layout settings.

## How The Unified Renderer Chooses The Layout Engine

The modern class renderer does not own a special class-diagram-only layout implementation.

Relevant files:

- [classRenderer-v3-unified.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts:39)
- [render.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/render.ts:39)

Important details:

- class rendering calls `diag.db.getData()`
- it resolves the layout algorithm through `getRegisteredLayoutAlgorithm`
- the shared registry falls back to `dagre`
- the shared renderer prefixes `domId` with the diagram id for uniqueness

Relevant lines:

- [classRenderer-v3-unified.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts:60)
- [classRenderer-v3-unified.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts:62)
- [classRenderer-v3-unified.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts:63)
- [render.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/render.ts:39)
- [render.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/render.ts:63)
- [render.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/render.ts:137)

Architecturally, this further validates our own plugin direction:

- generic graph extraction
- layout engine selection behind one coordinator
- renderer/viewer kept separate from graph building

## Mermaid Measures Nodes Before Dagre Lays Them Out

One especially important detail is that Mermaid does not guess final node sizes abstractly and then lay out the graph.

It inserts nodes into the DOM first, measures them, writes the measured size back into the graph node, and only then runs Dagre.

Relevant files:

- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:31)
- [dagre-wrapper/nodes.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/nodes.js:884)
- [dagre-wrapper/nodes.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/nodes.js:1141)

The class-box shape renderer:

- builds title/member/method labels
- measures them
- computes the final rectangle width and height
- calls `updateNodeBounds`

Then the Dagre renderer:

- inserts nodes
- updates abstract node bounds
- runs `dagreLayout(graph)`

Relevant lines:

- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:165)
- [dagre-wrapper/nodes.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/nodes.js:1101)

This is one reason Mermaid can look more balanced than our current Unity viewer.

Its layout engine is operating on measured node boxes, not on rough estimates.

## How Mermaid Decides Whether A Cluster Becomes A Recursive Subgraph

The earlier summary already noted that Mermaid extracts some clusters into nested graphs.

The deeper pass makes the rule clearer.

Relevant file:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:201)

What happens:

1. Mermaid collects descendants for each cluster.
2. It finds a replacement non-cluster child via `findNonClusterChild`.
3. It marks whether a cluster has `externalConnections`.
4. It rewrites cluster-touching edges to target replacement nodes.
5. It recursively extracts clusters that:
   - still have children
   - and do not have `externalConnections`

Key lines:

- replacement node search: [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:164)
- cluster preprocessing entry: [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:201)
- external connection flagging: [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:235)
- recursive extraction call: [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:290)
- extracted `clusterGraph` creation: [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:345)

This confirms that Mermaid is not treating all namespaces uniformly.

The layout behavior depends heavily on whether the namespace is:

- locally self-contained
- or entangled with the outer graph

## Mermaid Flips Direction For Extracted Cluster Graphs

One subtle but important detail:

When Mermaid extracts an internal-only cluster into a nested graph, it may flip the local layout direction relative to the parent graph.

Relevant lines:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:345)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:350)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:351)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:352)

In the inspected code:

- if the parent graph is `TB`, the extracted cluster graph defaults to `LR`
- otherwise it defaults to `TB`
- unless the cluster explicitly carries its own `dir`

This matters because it reduces the risk of nested clusters becoming long thin strips that mirror the parent orientation too literally.

## Mermaid Also Adjusts Spacing For Recursive Cluster Rendering

The recursive Dagre renderer does not just reuse the exact same spacing values blindly.

Relevant line:

- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:81)

When rendering a nested `clusterNode`, Mermaid keeps `nodesep` but increases `ranksep` by `25`.

That means recursive cluster graphs are not merely copied into smaller boxes.
They are given slightly more separation along the rank axis.

This is another small but meaningful contributor to the more readable spacing inside some namespace clusters.

## Mermaid Does Extra Post-Layout Position Adjustment For Cluster Titles

After Dagre returns coordinates, Mermaid still adjusts node and edge positions to account for subgraph title margins.

Relevant files:

- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:170)
- [edges.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/edges.js:176)
- [subGraphTitleMargins.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/utils/subGraphTitleMargins.ts:1)

Important details:

- cluster nodes get extra vertical offset
- regular nodes inside clusters are nudged by half the total subgraph title margin
- edge control points and edge labels are also shifted by that title margin

Relevant lines:

- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:184)
- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:224)
- [dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:253)
- [edges.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/edges.js:180)
- [edges.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/edges.js:204)

So even after layout, Mermaid still performs diagram-specific cleanup for cluster title space.

## Mermaid's Edge Cleanup Is About Both Layout And Final Rendering

The cluster edge rewriting serves two distinct purposes:

1. make Dagre accept a cleaner graph
2. make the final rendered edge visually terminate at the cluster border

The second part is easy to miss.

Relevant file:

- [edges.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/edges.js:352)

Important behavior:

- if an edge has `toCluster`, Mermaid trims the path at the target cluster boundary
- if an edge has `fromCluster`, Mermaid trims the path at the source cluster boundary
- this uses geometric intersection against the actual cluster box

Relevant lines:

- [edges.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/edges.js:400)
- [edges.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/edges.js:407)

This is important for our Unity viewer because it means Mermaid’s nicer cluster-edge presentation is not coming from layout alone.

Part of the visual cleanliness is a rendering-time path trim against the cluster boundary.

## The Mermaid Tests Confirm The Intended Cluster Semantics

The local spec file is worth taking seriously because it shows what Mermaid's maintainers consider the intended behavior.

Relevant file:

- [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:53)

The inspected tests cover:

- cluster-edge validation after adjustment
- replacing cluster endpoints with internal representatives
- extracting internal-only clusters into nested graphs
- preserving internal links inside extracted graphs
- handling nested cluster hierarchies

Useful examples:

- validation after adjustment: [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:53)
- cluster extraction with external links present: [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:128)
- multiple external links: [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:183)
- nested extracted graphs: [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:220)
- preserved internal links inside extracted graphs: [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:318)
- nested hierarchy handling: [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/mermaid-graphlib.spec.js:376)

These tests strongly support the conclusion that Mermaid’s cluster handling is not incidental.

It is a deliberate preprocessing and recursive-layout subsystem.

## Updated Practical Takeaways For Our Unity Viewer

After this deeper pass, the most useful Mermaid-inspired ideas for our project now look like this:

1. Keep treating namespaces as real clusters in the layout graph.

2. Keep cluster-boundary normalization as a pre-layout rewrite, not a render-only trick.

3. Distinguish between:
   - externally connected namespaces
   - internal-only namespaces

4. Give internal-only namespaces their own local layout pass, potentially with local direction changes.

5. Improve node measurement quality, because Mermaid is laying out measured boxes, not estimated ones.

6. Add cluster-boundary edge trimming in the renderer, not just anchor-based layout normalization.

7. Reserve space for cluster titles explicitly in both layout and edge rendering.

## Revised Recommendation

The best Mermaid-inspired next steps for our Unity viewer are now:

1. improve node measurement fidelity
2. add cluster-boundary edge trimming in rendering
3. add stronger within-rank crossing reduction
4. experiment with local direction switching for internal-only namespaces

That order now looks more accurate than the earlier version, because the deeper source pass shows Mermaid’s readability is coming from:

- measured node boxes
- cluster-aware preprocessing
- recursive subgraph extraction
- post-layout cluster-title and edge cleanup

not just from generic layered layout alone.

## What Mermaid Does With Self-Loops

In the Dagre renderer:

- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:307)

Important behavior:

- Mermaid does not leave self-loops as a direct single edge
- it creates temporary helper nodes
- it replaces the self-loop with a small chain of edges

Why this matters:

- layout engines often handle self-loops poorly if left naive
- the workaround is graph rewriting before layout

We do not need this immediately for our Unity viewer, but it is a useful future tactic.

## What Mermaid Does For Cluster Rendering

Cluster rendering code:

- [clusters.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/clusters.js:1)

Important behavior:

- cluster rectangles and labels are measured after insertion
- cluster size is not purely guessed in advance
- title margins are treated specially

This is useful for us in a softer way:

- our layout should be informed by node and group measurement
- title/header space in group boxes should be part of layout, not an afterthought

## What Mermaid Seems To Prioritize

From the code path, Mermaid seems to prioritize:

- structural grouping first
- general graph layout second
- cluster/edge cleanup around compound graphs
- final rendering adjustments after layout

It does not appear to hardcode a domain-specific class-diagram placement strategy like:

- "inheritance on the left"
- "services in the middle"
- "data on the right"

Instead it builds a well-formed compound graph and lets the layout engine work on it.

That means our best path is not:

- more and more custom hand-placement rules

It is:

- better graph formulation
- better rank/order heuristics
- better cluster-aware edge handling

## What We Should Reuse In Spirit

These Mermaid ideas are worth reusing:

### 1. Generic layout graph

Keep layout input generic and reusable.

We already started this with:

- `LayoutGraph`
- `LayoutNode`
- `LayoutEdge`
- `LayoutCluster`

### 2. Parent-child cluster membership

Treat namespaces as actual layout parents, not just background boxes.

For us this means:

- cluster membership should influence rank and ordering
- edges should know when they cross cluster boundaries

### 3. Pre-layout graph rewriting

Before layout, normalize the graph for difficult cases:

- cross-cluster edges
- self-loops
- maybe later collapsed groups

### 4. Multi-engine layout architecture

Mermaid’s renderer registry validates our own direction:

- keep simple fallback layout
- keep better layered layout
- maybe later experiment with another engine

## Deep Dive: Internal Namespace Spacing

This is the part that matters most for matching the Mermaid look.

The internal spacing of classes inside a class-diagram namespace is not produced by one special "namespace packing" rule.

It is the result of several mechanisms working together:

1. real measured class box sizes
2. compound parent-child graph membership
3. cluster extraction only when the namespace is internally isolated
4. recursive local layout with direction changes and spacing overrides
5. post-layout title-margin and cluster-box adjustments

If even one of those pieces is missing, the result starts to look like:

- a neat grid
- a manual card packer
- or a layered graph with a painted namespace background

instead of the more natural Mermaid layout.

### 1. Mermaid measures the real class node before layout

Relevant files:

- [nodes.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/rendering-elements/nodes.ts:14)
- [classBox.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/rendering-elements/shapes/classBox.ts:1)
- [util.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/rendering-elements/shapes/util.ts:141)

Important behavior:

- Mermaid inserts the actual SVG node first
- the class shape computes its own internal padding and section dividers
- `updateNodeBounds(...)` writes the rendered `getBBox()` width and height back onto the node
- Dagre then runs on those measured sizes

That means the layout is responding to:

- actual class title width
- actual member/method section height
- actual empty-box behavior
- actual note size

not to a guessed size.

This is one of the biggest remaining differences from our Unity viewer.

Mermaid's class boxes are not "same kind of cards, then arranged".

They are measured diagram objects first, then arranged.

### 2. Namespace membership is structural, not decorative

Relevant files:

- [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:649)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:301)

Important behavior:

- namespaces are emitted as `isGroup: true`
- classes and notes carry `parentId`
- Mermaid calls `graph.setParent(node.id, node.parentId)`

So classes inside a namespace are not "later assigned to a box".

They are part of a compound graph from the beginning.

That matters because Dagre can then optimize:

- ranks
- ordering
- cluster bounds
- edge endpoints

while already knowing that the class belongs inside that namespace.

### 3. Mermaid first decides whether a namespace is internally isolated

Relevant files:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:201)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:235)

Important behavior:

- Mermaid computes descendants for each cluster
- it checks each edge against those descendants
- if exactly one endpoint is inside the cluster, the cluster is marked with `externalConnections = true`

This is the critical fork.

Once a namespace has external connections, Mermaid stops treating it like a self-contained local spacing problem.

That means two namespaces with the same classes can get different internal spacing depending on whether:

- they connect outside
- their children connect outside
- nested clusters connect outside

So if our Unity layout is only looking at local namespace content, it will still miss Mermaid behavior for externally connected namespaces.

### 4. Mermaid rewrites cluster-touching edges before layout

Relevant files:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:165)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:201)

Important behavior:

- Mermaid picks a representative non-cluster descendant for a cluster
- cluster-touching edges are rewritten to those representative anchors
- `fromCluster` and `toCluster` metadata is attached to the edge

This is a big part of why namespace interiors do not get distorted as much as a naive compound layout would.

The interior nodes are not all directly fighting external edges.

Instead, the namespace gets a more stable boundary interaction model.

So the internal spacing is partly a side effect of external-edge simplification.

### 5. Internal-only namespaces are extracted into their own recursive subgraph

Relevant files:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:318)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:345)
- [mermaid-graphlib.spec.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.spec.js:220)

Important behavior:

- if a cluster has no `externalConnections` and has children, Mermaid extracts it
- it builds a fresh compound `clusterGraph`
- it copies only the nodes and internal edges that belong to that cluster
- it replaces the original cluster node with a `clusterNode` that owns the extracted graph

This is the single most important namespace-internal-spacing behavior in Mermaid.

It means internal-only namespaces are not laid out in the parent graph at all.

They get their own local graph problem.

That is why Mermaid often looks much cleaner inside a namespace than our current viewer:

- the cluster interior is no longer competing with the whole diagram
- local ranks can form naturally
- local crossings can reduce more cleanly
- unrelated external traffic cannot flatten the local arrangement

### 6. The local extracted namespace can flip direction relative to the parent

Relevant files:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:339)

Important behavior:

- if the parent graph uses `TB`, the extracted cluster defaults to `LR`
- otherwise it defaults to `TB`
- if the cluster data already carries a direction, Mermaid uses that instead

This is extremely relevant to the look you are chasing.

Mermaid is not always trying to preserve the parent flow inside the namespace.

It will happily rotate the local problem to avoid a tall or awkward result.

That means some namespace boxes look more balanced because Mermaid intentionally solves them on the opposite axis.

### 7. The extracted namespace graph starts with its own spacing defaults

Relevant files:

- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:345)

Important behavior:

The new local cluster graph is created with:

- `rankdir: dir`
- `nodesep: 50`
- `ranksep: 50`
- `marginx: 8`
- `marginy: 8`

This is the first level of local namespace spacing.

But it is not the whole story.

### 8. Recursive render overrides child spacing again

Relevant files:

- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:77)

Important behavior:

Before Mermaid recursively renders a `clusterNode`, it does:

- read the current parent graph's `ranksep` and `nodesep`
- apply those values to the child graph
- keep `nodesep` the same
- bump `ranksep` by `25`

So the extracted namespace graph is not simply left at its original `50/50`.

It is reconfigured to:

- inherit parent `nodesep`
- use a larger `ranksep` than the parent

This is a crucial detail.

It means Mermaid often makes internal namespace ranks airier than the surrounding graph.

That extra vertical or horizontal breathing room is part of the look.

For nested clusters, this effect compounds recursively.

### 9. Namespace label and title margins change the final perceived spacing

Relevant files:

- [clusters.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/rendering-elements/clusters.js:1)
- [subGraphTitleMargins.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/utils/subGraphTitleMargins.ts:1)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:170)

Important behavior:

- clusters compute a real label bounding box
- cluster width may grow if the title is wider than the laid-out content
- cluster `offsetY` depends on label height and padding
- after Dagre layout, Mermaid shifts cluster nodes, child nodes, and edge points by `subGraphTitleTotalMargin`

So even if the raw Dagre positions were the same, the visible spacing can still feel different because:

- the title pushes content down
- the cluster box grows
- edge points are moved
- the top margin becomes part of the visual rhythm

This is another place where our viewer still differs, because we are not yet doing a faithful post-layout cluster-title adjustment pass.

### 10. Namespace spacing is mostly emergent, not hand-authored

The final namespace-internal spacing in Mermaid emerges from:

- measured class sizes
- compound graph membership
- extracted local subgraphs when allowed
- representative-anchor edge rewriting
- local direction selection
- inherited-plus-expanded recursive spacing
- post-layout title and cluster adjustments

This is why it is hard to reproduce with just:

- row wrapping
- local barycenter sorting
- a few spacing constants

Those help, but they are downstream approximations.

Mermaid is solving a different problem formulation.

## Practical Diagnosis For Our Viewer

Compared to Mermaid, our current Unity namespace spacing still misses several structural ingredients:

1. We still estimate node sizes instead of measuring the real rendered class card before layout.
2. Our extracted-subgraph behavior is only partially Mermaid-like and does not yet mirror the recursive spacing override exactly.
3. Our externally connected namespaces are still arranged by our own heuristic engine rather than a true compound Dagre solve with representative anchors.
4. We do not have Mermaid's post-layout cluster title offset behavior, so our namespace interiors can feel too tight against the top or too uniformly packed.
5. We do not yet allow local direction flipping as aggressively and automatically as Mermaid does for extracted namespaces.

## Best Mermaid-Inspired Fixes For Us

If the goal is specifically "make classes inside namespaces look like Mermaid", the most valuable next steps are:

1. Measure real UI Toolkit node sizes before layout.
2. Make extracted namespace subgraphs inherit parent `nodesep` and use parent `ranksep + extra`, instead of our current approximate spacing rules.
3. Add a real post-layout cluster-title margin pass.
4. Let extracted namespaces choose a local opposite-axis direction by default.
5. Keep improving representative-anchor handling for externally connected namespaces, because those namespaces will never look right from local packing alone.

## What We Should Not Copy Directly

These Mermaid parts are not a good direct port target:

- browser DOM/SVG insertion code
- D3-specific measurement and transform code
- graphlib-specific cluster extraction code as-is
- the exact recursive render pipeline

Why:

- our environment is Unity UI Toolkit, not SVG
- our rendering constraints are different
- direct code porting would import a lot of browser assumptions we do not want

So the right goal is:

- Mermaid-inspired architecture and graph preparation
- not Mermaid renderer transplantation

## Practical Implications For Our Unity Viewer

### Immediate layout lessons

We should move toward:

- explicit cluster-aware edge handling
- stronger pre-layout graph normalization
- better rank ordering using cluster connectivity
- better handling of disconnected components

### Next likely upgrades

1. Add cross-cluster edge normalization.
   Similar in spirit to Mermaid's anchor-node rewriting.

2. Add crossing reduction within ranks.
   Mermaid gets a lot of readability from using a real layered layout engine rather than a simple heuristic spread.

3. Add optional nested cluster support later.
   Mermaid’s compound graph and recursive handling show this is possible, but we do not need it immediately.

4. Add node/group measurement as a first-class input.
   Group title height and node size should explicitly influence layout.

## Recommended Action For Our Project

The most useful Mermaid-inspired next step is not:

- port Dagre wrapper code directly

The most useful next step is:

1. keep our current `LayeredLayoutEngine`
2. add cluster-boundary edge handling
3. add rank crossing reduction
4. add better ordering of clusters by connected-neighbor barycenter

That would move us much closer to Mermaid’s clarity without dragging browser rendering code into Unity.

## Files Worth Revisiting Later

If we return to Mermaid research later, these are the most valuable files:

- [classDb.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classDb.ts:649)
- [classRenderer-v2.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v2.ts:33)
- [classRenderer-v3-unified.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/diagrams/class/classRenderer-v3-unified.ts:39)
- [render.ts](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/render.ts:30)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:273)
- [layout-algorithms/dagre/index.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/index.js:378)
- [mermaid-graphlib.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/rendering-util/layout-algorithms/dagre/mermaid-graphlib.js:201)
- [clusters.js](e:/Games/UnityGames/importedUnityPackagesToAIWith/mermaid-develop/packages/mermaid/src/dagre-wrapper/clusters.js:1)
