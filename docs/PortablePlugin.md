# Portable Plugin

## Current Packaging Goal

This subproject is now arranged so it can be copied into another Unity project as a single plugin folder:

- `Assets/Plugins/MermaidClassDiagramExporter`

That folder contains:

- `Editor/MermaidClassDiagramExporter.cs`
- `Dependencies/FoggyBalrog.MermaidDotNet.dll`
- `Dependencies/YamlDotNet.dll`

This is the intended copy unit for reuse.

## How To Reuse In Another Unity Project

1. Copy `Assets/Plugins/MermaidClassDiagramExporter` into the target project's `Assets/Plugins/` folder.
2. Open the target project in Unity.
3. Let Unity import and recompile.
4. Use the `Tools > Mermaid` menu after compilation finishes.

You do not need to:

- use OpenUPM for this plugin
- rebuild the MermaidDotNet source just to consume the exporter
- copy the `ExternalPackages/MermaidDotNet-main` source folder unless you want to rebuild or patch the DLLs yourself

## Why This Layout

The layout is intentionally simple:

- `Editor/` keeps the exporter editor-only
- `Dependencies/` keeps third-party assemblies bundled with the tool
- the whole folder can be copied as one unit

That is a better fit for quick reuse than the previous split layout where the exporter script and DLLs lived in different places.

## Compatibility Notes

- The MermaidDotNet library targets `.NET Standard 2.1`.
- This setup has been verified in this repository on Unity `6000.3.2f1`.
- A target project should use a Unity version and API compatibility profile that can consume `.NET Standard 2.1` assemblies.

## Maintenance Notes

The local maintenance source currently lives in:

- `ExternalPackages/MermaidDotNet-main`

That source copy exists because the DLLs were built locally here. It is useful for:

- rebuilding the dependency DLLs
- patching the MermaidDotNet source if needed
- tracking what version and dependency set the exporter currently depends on

It is not part of the minimum portable package.

## Good Next Packaging Step

If this tool becomes stable enough, the next step would be to turn it into a proper Unity package with:

- a `package.json`
- a cleaner namespace and assembly definition setup
- an optional sample folder
- docs shipped alongside the package

For now, the single-folder plugin copy is the simplest workable distribution format.
