# MermaidClassDiagramExporter

Unity editor plugin for exporting Mermaid class diagrams and exploring project type graphs inside the Unity Editor.

## Status

This repository should currently be treated as a work-in-progress prototype.

That means:

- the plugin is usable
- the architecture is actively evolving
- layout and routing are still being improved
- viewer UX is still under iteration
- there may still be rough edges between releases

## What It Does

- exports Mermaid class diagrams from selected scripts, objects, or folders
- builds an in-Unity type graph viewer from the same shared graph model
- supports focused subgraph exploration by selected node or staged seed set
- bundles required dependencies so the plugin can be copied into another project

## Installation

Copy this folder into your Unity project:

- `Assets/Plugins/MermaidClassDiagramExporter`

That is the entire intended copy unit.

After copying:

1. Open the target Unity project.
2. Let Unity import and recompile.
3. Use the `Tools > Mermaid` menu.

## Included Menu Items

### Export

- `Tools > Mermaid > Export Selected Classes`
- `Tools > Mermaid > Export Classes From Folder...`
- `Tools > Mermaid > Export Classes From Selected Project Folder`
- `Tools > Mermaid > Reveal Last Export`
- `Tools > Mermaid > Copy Last Export To Clipboard`
- `Tools > Mermaid > Open Mermaid Live Editor`

### Viewer

- `Tools > Mermaid > Open Type Graph Viewer`

The viewer currently supports:

- graph build from current selection
- graph build from selected folder
- graph build from picked folder
- namespace grouping
- routed relation lines
- edge visibility toggles
- focused subgraph navigation
- multi-seed focus

## Quick Start

### Export Mermaid text

1. Select one or more scripts, components, ScriptableObjects, or GameObjects.
2. Run `Tools > Mermaid > Export Selected Classes`.
3. The plugin writes Mermaid output to a `Docs/Mermaid` folder in the Unity project.

### Explore the graph in Unity

1. Open `Tools > Mermaid > Open Type Graph Viewer`.
2. Build a graph from selection or folder.
3. Click nodes to inspect them.
4. Use `Focus D1`, `Focus D2`, or `Focus D3` to reduce the graph around the current node or staged seed set.

## Repository Layout

- `Assets/Plugins/MermaidClassDiagramExporter`
  The actual plugin.
- `docs`
  Design notes and implementation docs copied from the development project.
- `docs/BUILDING_DEPENDENCIES.md`
  Notes for rebuilding the bundled dependency DLLs.
- `THIRD_PARTY_NOTICES.md`
  Dependency attribution and license notes.

## Compatibility

- Developed against Unity `6000.3.2f1`
- Dependency DLLs target `.NET Standard 2.1`
- Best suited for newer Unity versions with `.NET Standard 2.1` support

## Known Limitations

- current extraction is still Unity-project-centric and based on `MonoScript.GetClass()`
- runtime behavior is not reconstructed
- very large graphs can still become visually heavy
- layout and routing are still under active refinement
- external-folder source parsing is not implemented yet
- this repository is still in WIP/prototype stage rather than finished production-package stage

## Third-Party Dependencies

This plugin currently bundles:

- `FoggyBalrog.MermaidDotNet`
- `YamlDotNet`

See [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) for details.

## Rebuilding Bundled Dependencies

Most users should use the bundled DLLs as-is.

If you need to rebuild them yourself, see:

- [docs/BUILDING_DEPENDENCIES.md](./docs/BUILDING_DEPENDENCIES.md)

## License

This repository is currently packaged under the MIT License. See [LICENSE](./LICENSE).
