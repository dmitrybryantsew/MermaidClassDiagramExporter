# Mermaid Class Diagram Exporter

This folder documents the Unity Mermaid class-diagram exporter subproject that now lives in:

- `Assets/Plugins/MermaidClassDiagramExporter`

The goal of this subproject is to make it easy to:

- export Mermaid class diagrams from selected scripts or folders inside Unity
- keep the exporter portable so it can be copied into another Unity project
- evolve the exporter later into a richer in-Unity node or graph viewer

Generated diagram outputs currently go to:

- `docs/Mermaid`

Recommended reading order:

- [CurrentImplementation.md](./CurrentImplementation.md)
- [UnityViewerTechnicalDesign.md](./UnityViewerTechnicalDesign.md)
- [LayoutEngineTechnicalDesign.md](./LayoutEngineTechnicalDesign.md)
- [RealLayoutPipelineTechnicalDesign.md](./RealLayoutPipelineTechnicalDesign.md)
- [MermaidLayoutResearch.md](./MermaidLayoutResearch.md)
- [MermaidLikeNamespaceLayoutArchitecture.md](./MermaidLikeNamespaceLayoutArchitecture.md)
- [InteractiveLayoutEditingArchitecture.md](./InteractiveLayoutEditingArchitecture.md)
- [FocusedSubgraphArchitecture.md](./FocusedSubgraphArchitecture.md)
- [PortablePlugin.md](./PortablePlugin.md)
- [SimilarToolsAndResearch.md](./SimilarToolsAndResearch.md)
- [Roadmap.md](./Roadmap.md)

Current plugin layout:

- `Assets/Plugins/MermaidClassDiagramExporter/Editor/MermaidClassDiagramExporter.cs`
- `Assets/Plugins/MermaidClassDiagramExporter/Dependencies/FoggyBalrog.MermaidDotNet.dll`
- `Assets/Plugins/MermaidClassDiagramExporter/Dependencies/YamlDotNet.dll`

Notes:

- The local source copy used to build the DLLs during setup is in `ExternalPackages/MermaidDotNet-main`.
- That source copy is for maintenance and rebuilds here. It is not required when copying the plugin into another Unity project.
