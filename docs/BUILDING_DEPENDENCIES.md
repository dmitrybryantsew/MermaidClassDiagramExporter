# Building Dependencies

## Purpose

This repository ships with prebuilt dependency DLLs so users can normally just copy:

- `Assets/Plugins/MermaidClassDiagramExporter`

into another Unity project.

If you need to rebuild the dependency DLLs yourself, this note explains the expected workflow.

## Bundled DLLs

The plugin currently bundles:

- `FoggyBalrog.MermaidDotNet.dll`
- `YamlDotNet.dll`

They live in:

- `Assets/Plugins/MermaidClassDiagramExporter/Dependencies`

## Normal Reuse Path

For normal use, do not rebuild the dependencies.

Just keep the bundled DLLs and their `.meta` files together.

## Rebuild Workflow

If you need your own build:

1. Get the `FoggyBalrog.MermaidDotNet` source.
2. Restore and build it with the local `dotnet` SDK.
3. Collect the built `FoggyBalrog.MermaidDotNet.dll`.
4. Collect required dependency DLLs, especially `YamlDotNet.dll`.
5. Replace the files in:
   - `Assets/Plugins/MermaidClassDiagramExporter/Dependencies`
6. Reopen Unity and let it reimport.

## Example Commands

From the MermaidDotNet source folder:

```powershell
dotnet restore
dotnet build -c Release
```

The built DLLs are typically under a `bin/Release/...` output folder.

## Important Notes

### 1. You need both DLLs

Do not copy only:

- `FoggyBalrog.MermaidDotNet.dll`

You also need its dependency chain, especially:

- `YamlDotNet.dll`

### 2. Keep the Unity `.meta` files

The bundled DLL `.meta` files matter because Unity must import these as editor plugins rather than analyzers.

### 3. MermaidDotNet source may need a small project tweak

During local integration, the copied MermaidDotNet source needed a minor project-file adjustment because the installed SDK did not accept the exact language-version setting used by the source at that time.

So if `dotnet build` fails, inspect the MermaidDotNet `.csproj` first before assuming the Unity plugin code is at fault.

### 4. Unity compatibility

The current dependency setup expects a Unity version that can consume `.NET Standard 2.1` assemblies.

## Recommendation

Use the bundled dependency DLLs unless you specifically need:

- a newer MermaidDotNet version
- a patched MermaidDotNet build
- a different dependency version
- to verify the build chain yourself

