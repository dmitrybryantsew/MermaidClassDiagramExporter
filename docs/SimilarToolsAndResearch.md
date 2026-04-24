# Similar Tools And Research

## Short Answer

There are already Unity tools in the same general space, but they split into different categories:

- graph-editor frameworks
- visual scripting / behavior tree tools
- dependency or class-diagram viewers
- asset relationship explorers

Our current exporter is closest to the dependency or class-diagram side, but the future in-Unity viewer would overlap with graph-editor frameworks too.

## Unity-Native Or Unity-Centric Tools Worth Knowing

### Official Unity graph APIs

Unity exposes `Experimental.GraphView`, which provides graph, node, edge, zoom, selection, and related editor UI pieces. Unity's own docs explicitly mark it as experimental, so it is good for prototypes and internal tools, but riskier as a long-term foundation.

Relevant official docs:

- `Experimental.GraphView.Node`
- `Experimental.GraphView.GraphView`

### xNode

`xNode` is a popular open-source Unity node-editor framework for building custom graph tools. It is not a class-diagram exporter by itself, but it is very relevant if we decide to build a custom in-Unity graph viewer instead of relying on Mermaid rendering in a browser.

What makes it relevant:

- built specifically for Unity
- focused on node editor UX
- lightweight
- proven by many custom graph tools

### Asset Relations Viewer

`Asset Relations Viewer` is aimed at visualizing dependencies between Unity assets, files, asset groups, and similar content in the Unity editor. It is more asset-oriented than code-oriented, but it is useful as a reference for project-scale dependency visualization inside Unity.

### Class Diagram Generator / Smart Dependency Diagram / Script Dependency Visualizer

These are closer to the current goal than node frameworks:

- `Class Diagram Generator` on the Unity Asset Store
- `Smart Dependency Diagram` on the Unity Asset Store
- `Script Dependency Visualizer` on the Unity Asset Store

These suggest there is real demand for:

- class and dependency inspection inside Unity
- visual architecture exploration
- editor tools that help understand large codebases

## Mermaid Performance Note

Your observation about browser lag makes sense.

Mermaid's official docs describe Mermaid as a JavaScript-based diagram system, and their API returns SVG output. In practice that means large diagrams can become expensive because:

- layout work happens before rendering
- the result is a large SVG tree
- browser DOM and SVG interaction costs rise with graph size

That does not automatically mean a Unity-native viewer will be fast, but it does mean we are not locked to browser SVG performance if we later build our own viewer.

## What A Unity Viewer Could Improve

A custom Unity viewer could eventually do things Mermaid in the browser is not optimized for in this specific workflow:

- incremental redraws instead of full browser re-rendering
- cached node positions
- collapsed namespaces or groups
- filtering by folder, assembly, namespace, or type role
- click-through to source files
- highlighting only the neighborhood around a selected node
- hiding relation categories on demand

If we keep the exported graph data separate from the rendering layer, we could preserve Mermaid export as the portable text format while also adding a faster Unity-native visualization path later.

## Suggested Positioning For This Subproject

The most useful niche for this tool is probably:

- export-first today
- Unity-native viewer second
- architecture exploration rather than full UML purity

That plays to Unity's strengths better than trying to reproduce every browser-based Mermaid feature inside the editor.

## Source Links

- Unity GraphView `Node` API: https://docs.unity3d.com/kr/2022.3/ScriptReference/Experimental.GraphView.Node.html
- Unity GraphView `GraphView` API: https://docs.unity3d.com/es/current/ScriptReference/Experimental.GraphView.GraphView.html
- xNode: https://github.com/Siccity/xNode
- Asset Relations Viewer: https://github.com/innogames/asset-relations-viewer
- Class Diagram Generator: https://assetstore.unity.com/packages/tools/utilities/class-diagram-generator-323124
- Smart Dependency Diagram: https://assetstore.unity.com/packages/tools/utilities/smart-dependency-diagram-326818
- Script Dependency Visualizer: https://assetstore.unity.com/packages/tools/utilities/script-dependency-visualizer-57647
- Mermaid usage docs: https://mermaid.js.org/config/usage
- Mermaid render result docs: https://mermaid.js.org/config/setup/mermaid/interfaces/RenderResult.html
