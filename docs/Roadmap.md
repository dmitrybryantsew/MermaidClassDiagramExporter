# Roadmap

## Near-Term Goals

### 1. Stabilize the exporter

- verify the reorganized portable plugin folder in Unity after reimport
- test copying `Assets/Plugins/MermaidClassDiagramExporter` into another Unity project
- confirm the menu appears and exports work without the local source tree

### 2. Improve graph readability

- add options to hide methods, properties, or fields
- add namespace collapse or folder grouping options
- add filters for inheritance only, associations only, or interfaces only
- add limits on exported member count per class

### 3. Improve export targeting

- export by assembly definition
- export by namespace prefix
- export by dependency radius from a chosen root type
- export from scene or prefab selections, not only scripts and folders

## Medium-Term Goals

### 4. Add a Unity-native viewer

The next major step should be an in-editor viewer window that reads the exporter data and renders it inside Unity.

Candidate approaches:

- UI Toolkit with a custom graph canvas
- GraphView as an internal prototype path
- xNode-inspired or xNode-backed custom viewer if needed

### 5. Add interaction

- click node to ping or open script
- search and focus a type
- highlight incoming and outgoing relations
- save and restore manual layout
- color nodes by category such as MonoBehaviour, ScriptableObject, service, installer, data model

## Long-Term Goals

### 6. Move beyond static class structure

The current export is structural. A later version could enrich the graph with:

- scene references
- prefab references
- ScriptableObject asset links
- DI registrations and resolutions
- event publishers and subscribers
- state-machine or workflow edges

### 7. Support a hybrid workflow

The best long-term setup may be:

- Mermaid export for text-based sharing and documentation
- Unity-native graph viewer for day-to-day exploration

That would let the project keep a portable interchange format while also avoiding some of the browser performance pain on large diagrams.

## Practical Performance Direction

If we build the Unity viewer, performance should be treated as a first-class design requirement from the start:

- virtualize rendering where possible
- avoid redrawing the entire graph on every interaction
- cache layout results
- support collapsed groups
- allow hiding low-value edges
- load relation layers progressively

The image generated from `docs/Mermaid/Folder_Assets__TestTDTowerAndMeshCreation_Scripts.mmd` already shows the core issue clearly: the data is useful, but the full graph is dense enough that readability and interaction now matter as much as raw export correctness.


# below is user written donotforget staff
rebuild graph with only selected nodes and its assotiations up to n assotiations

option to better represent public - private protected text in class cards with + and etc - mermaid also writes full class descr in card and they can get quite large yet it good to add as optional

drag and drop actions on namespaces(groups that move all related classes inside around) and drag and drop for classes inside group (moving it out of boundary will enlarge groub boundaries to said class pos) then as we wont rebuild it - save or if rebuild save non defauilt position and after default rebuild put related classes on their nondefault positions. binary or json...

create  actions this will allow to create hierarchy with groups and classes and their relations and members and then transfer it to actual scripts (and also we can add llm here to help in development like chatgpt or chutes or whatever) inside Asset/_ProjectName/Scripts; need to define typical folder names and their role to be able to easily put them into it and make it configurable; need a lot of thought to it all actually

draw how data flows inside app to see waht happening on each stage, ability to configure what actually to draw in the end image as we might need full flow of system


add to be able to trace not only project code
(For an external folder, we have 3 realistic options:

Easiest, least powerful: allow an external folder picker, copy or mirror the .cs files into a temporary project area, then use the current Unity path.
Good for quick experiments, bad for clean tooling.

Better long-term: parse the external .cs files directly with Roslyn and build our own type graph.
This is the right solution if you want “draw class connection graph from arbitrary source folder.”
It would no longer depend on Unity being able to import the scripts.

Harder but stronger: compile/load the external code or referenced assemblies, then analyze symbols/types.
Best accuracy, most setup pain.

My recommendation:

for external folders, switch to a Roslyn-based extraction path
keep the current Unity/MonoScript path for project-internal folders
feed both into the same TypeGraphBuilder / layout pipeline
That would let us support:

external source folders
non-Unity C# code
multiple classes per file
better namespace/type resolution than MonoScript.GetClass()
Difficulty:

basic external folder graph with simple parsing: medium
good symbol resolution with references/usings/generics: medium-high
production-grade cross-project analysis: high)

