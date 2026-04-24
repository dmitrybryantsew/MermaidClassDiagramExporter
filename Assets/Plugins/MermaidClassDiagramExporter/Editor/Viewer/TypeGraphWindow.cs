using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TypeGraphWindow : EditorWindow
{
    private GraphCanvasView canvasView;
    private TypeGraphInspectorView inspectorView;
    private ToolbarSearchField searchField;
    private Label statusLabel;
    private ToolbarToggle inheritanceToggle;
    private ToolbarToggle implementsToggle;
    private ToolbarToggle associationToggle;
    private ToolbarMenu traversalModeMenu;
    private ToolbarButton addSeedButton;
    private ToolbarButton removeSeedButton;
    private ToolbarButton clearSeedsButton;
    private ToolbarButton focusDepthOneButton;
    private ToolbarButton focusDepthTwoButton;
    private ToolbarButton focusDepthThreeButton;
    private ToolbarButton centerGraphButton;
    private ToolbarButton backButton;
    private ToolbarButton resetButton;
    private GraphSourceKind currentSourceKind = GraphSourceKind.Unknown;
    private string currentFolderPath = string.Empty;
    private TypeGraph currentGraph;
    private string currentSelectedNodeId = string.Empty;
    private GraphFocusTraversalMode currentTraversalMode = GraphFocusTraversalMode.UndirectedAssociations;
    private readonly GraphLayoutCoordinator layoutCoordinator = new GraphLayoutCoordinator();
    private readonly FocusedGraphNavigationController focusNavigationController = new FocusedGraphNavigationController();
    private readonly GraphSeedSelectionState seedSelectionState = new GraphSeedSelectionState();

    [MenuItem("Tools/Mermaid/Open Type Graph Viewer")]
    public static void OpenWindow()
    {
        TypeGraphWindow window = GetWindow<TypeGraphWindow>();
        window.titleContent = new GUIContent("Type Graph");
        window.minSize = new Vector2(980f, 560f);
        window.Show();
    }

    private void CreateGUI()
    {
        rootVisualElement.focusable = true;
        rootVisualElement.style.flexGrow = 1f;
        rootVisualElement.style.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 1f);
        rootVisualElement.Clear();
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);

        rootVisualElement.Add(BuildToolbar());

        var splitView = new TwoPaneSplitView(0, Mathf.Max(760, (int)(position.width - 320f)), TwoPaneSplitViewOrientation.Horizontal);
        splitView.style.flexGrow = 1f;
        rootVisualElement.Add(splitView);

        canvasView = new GraphCanvasView();
        canvasView.NodeSelected += OnNodeSelected;
        canvasView.NodeDoubleClicked += OnNodeDoubleClicked;
        splitView.Add(canvasView);

        inspectorView = new TypeGraphInspectorView();
        splitView.Add(inspectorView);

        statusLabel = new Label("Build a graph from the current selection or a project folder.");
        statusLabel.style.paddingLeft = 10f;
        statusLabel.style.paddingRight = 10f;
        statusLabel.style.paddingTop = 6f;
        statusLabel.style.paddingBottom = 6f;
        statusLabel.style.color = new Color(0.70f, 0.75f, 0.84f, 1f);
        statusLabel.style.backgroundColor = new Color(0.09f, 0.10f, 0.12f, 1f);
        rootVisualElement.Add(statusLabel);

        inspectorView.SetGraph(currentGraph);
        canvasView.SetGraph(currentGraph, BuildLayout(currentGraph));
        UpdateToolbarState();
        rootVisualElement.Focus();
    }

    private VisualElement BuildToolbar()
    {
        var toolbar = new Toolbar();

        toolbar.Add(new ToolbarButton(BuildFromSelection) { text = "From Selection" });
        toolbar.Add(new ToolbarButton(BuildFromSelectedProjectFolder) { text = "From Selected Folder" });
        toolbar.Add(new ToolbarButton(BuildFromFolderPicker) { text = "Pick Folder..." });
        toolbar.Add(new ToolbarButton(RebuildCurrentGraph) { text = "Rebuild" });
        addSeedButton = new ToolbarButton(AddCurrentSelectionAsSeed) { text = "Add Seed" };
        removeSeedButton = new ToolbarButton(RemoveCurrentSelectionFromSeeds) { text = "Remove Seed" };
        clearSeedsButton = new ToolbarButton(ClearSeedSelection) { text = "Clear Seeds" };
        toolbar.Add(addSeedButton);
        toolbar.Add(removeSeedButton);
        toolbar.Add(clearSeedsButton);
        focusDepthOneButton = new ToolbarButton(() => FocusCurrentSelection(1)) { text = "Focus D1" };
        focusDepthTwoButton = new ToolbarButton(() => FocusCurrentSelection(2)) { text = "Focus D2" };
        focusDepthThreeButton = new ToolbarButton(() => FocusCurrentSelection(3)) { text = "Focus D3" };
        toolbar.Add(focusDepthOneButton);
        toolbar.Add(focusDepthTwoButton);
        toolbar.Add(focusDepthThreeButton);

        centerGraphButton = new ToolbarButton(CenterCurrentGraph) { text = "Center Graph" };
        backButton = new ToolbarButton(GoBackToPreviousGraph) { text = "Back" };
        resetButton = new ToolbarButton(ResetToRootGraph) { text = "Reset To Root" };
        toolbar.Add(centerGraphButton);
        toolbar.Add(backButton);
        toolbar.Add(resetButton);

        traversalModeMenu = new ToolbarMenu();
        traversalModeMenu.text = BuildTraversalModeToolbarText();
        traversalModeMenu.menu.AppendAction("Traversal/Undirected Associations", _ => SetTraversalMode(GraphFocusTraversalMode.UndirectedAssociations));
        traversalModeMenu.menu.AppendAction("Traversal/Outgoing Associations Only", _ => SetTraversalMode(GraphFocusTraversalMode.OutgoingAssociationsOnly));
        traversalModeMenu.menu.AppendAction("Traversal/Incoming Associations Only", _ => SetTraversalMode(GraphFocusTraversalMode.IncomingAssociationsOnly));
        traversalModeMenu.menu.AppendAction("Traversal/All Visible Relations", _ => SetTraversalMode(GraphFocusTraversalMode.AllVisibleRelations));
        toolbar.Add(traversalModeMenu);

        inheritanceToggle = BuildEdgeToggle("Inheritance", true);
        implementsToggle = BuildEdgeToggle("Implements", true);
        associationToggle = BuildEdgeToggle("Associations", true);
        toolbar.Add(inheritanceToggle);
        toolbar.Add(implementsToggle);
        toolbar.Add(associationToggle);

        var spacer = new VisualElement();
        spacer.style.flexGrow = 1f;
        toolbar.Add(spacer);

        searchField = new ToolbarSearchField();
        searchField.style.minWidth = 220f;
        searchField.RegisterValueChangedCallback(evt =>
        {
            if (canvasView != null)
            {
                canvasView.SetSearchText(evt.newValue);
            }
        });
        toolbar.Add(searchField);

        return toolbar;
    }

    private ToolbarToggle BuildEdgeToggle(string label, bool defaultValue)
    {
        var toggle = new ToolbarToggle
        {
            text = label,
            value = defaultValue
        };

        toggle.RegisterValueChangedCallback(_ => ApplyEdgeVisibility());
        return toggle;
    }

    private void BuildFromSelection()
    {
        List<Type> selectedTypes = SelectionTypeCollector.CollectSelectedTypes();
        if (selectedTypes.Count == 0)
        {
            ShowEditorDialog("Select one or more scripts, components, ScriptableObjects, or GameObjects first.");
            return;
        }

        currentSourceKind = GraphSourceKind.Selection;
        currentFolderPath = string.Empty;
        SetRootGraph(TypeGraphBuilder.BuildGraph(selectedTypes, "Selected Classes", GraphSourceKind.Selection, "Current Unity selection"));
    }

    private void BuildFromSelectedProjectFolder()
    {
        string folderPath = FolderTypeCollector.GetSelectedProjectFolderPath();
        if (string.IsNullOrEmpty(folderPath))
        {
            ShowEditorDialog("Select a project folder in the Project window first, or use Pick Folder...");
            return;
        }

        BuildFromFolderPath(folderPath);
    }

    private void BuildFromFolderPicker()
    {
        string initialDirectory = Application.dataPath;
        string selectedFolder = EditorUtility.OpenFolderPanel("Select Folder To Build Type Graph From", initialDirectory, string.Empty);
        if (string.IsNullOrEmpty(selectedFolder))
        {
            return;
        }

        if (!FolderTypeCollector.TryGetProjectRelativePath(selectedFolder, out string projectRelativeFolder))
        {
            ShowEditorDialog("Please choose a folder inside this Unity project.");
            return;
        }

        BuildFromFolderPath(projectRelativeFolder);
    }

    private void BuildFromFolderPath(string folderPath)
    {
        List<Type> folderTypes = FolderTypeCollector.CollectTypesFromFolder(folderPath);
        if (folderTypes.Count == 0)
        {
            ShowEditorDialog("No loadable project-defined classes were found in that folder.");
            return;
        }

        currentSourceKind = GraphSourceKind.Folder;
        currentFolderPath = folderPath;
        SetRootGraph(TypeGraphBuilder.BuildGraph(folderTypes, "Classes in " + folderPath, GraphSourceKind.Folder, folderPath));
    }

    private void RebuildCurrentGraph()
    {
        switch (currentSourceKind)
        {
            case GraphSourceKind.Selection:
                BuildFromSelection();
                break;
            case GraphSourceKind.Folder:
                if (string.IsNullOrEmpty(currentFolderPath))
                {
                    ShowEditorDialog("No folder source is currently stored.");
                    return;
                }

                BuildFromFolderPath(currentFolderPath);
                break;
            default:
                ShowEditorDialog("No graph has been built yet.");
                break;
        }
    }

    private void SetRootGraph(TypeGraph graph)
    {
        focusNavigationController.SetRootGraph(graph, BuildSourceKey(graph));
        seedSelectionState.Clear();
        SetDisplayedGraph(graph, currentSelectedNodeId);
    }

    private void SetDisplayedGraph(TypeGraph graph, string selectedNodeId = "")
    {
        currentGraph = graph;
        currentSelectedNodeId = string.Empty;
        seedSelectionState.PruneToGraph(graph);

        if (canvasView != null)
        {
            canvasView.SetGraph(graph, BuildLayout(graph));
            canvasView.SetSearchText(searchField != null ? searchField.value : string.Empty);
            ApplyEdgeVisibility();
        }

        if (inspectorView != null)
        {
            inspectorView.SetGraph(graph);
            inspectorView.ShowNode(null);
        }

        if (!string.IsNullOrEmpty(selectedNodeId)
            && graph != null
            && graph.Nodes != null
            && graph.Nodes.Any(node => node.Id == selectedNodeId))
        {
            currentSelectedNodeId = selectedNodeId;
            canvasView?.SelectNode(selectedNodeId);
            canvasView?.FocusNode(selectedNodeId);
        }

        if (statusLabel != null)
        {
            statusLabel.text = BuildStatusText(graph);
        }

        UpdateToolbarState();
    }

    private void OnNodeSelected(TypeNodeData node)
    {
        currentSelectedNodeId = node != null ? node.Id : string.Empty;

        if (inspectorView != null)
        {
            inspectorView.ShowNode(node);
        }

        UpdateToolbarState();
    }

    private void OnNodeDoubleClicked(TypeNodeData node)
    {
        if (node == null || string.IsNullOrEmpty(node.AssetPath))
        {
            return;
        }

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(node.AssetPath);
        if (script != null)
        {
            AssetDatabase.OpenAsset(script);
        }
    }

    private LayoutResult BuildLayout(TypeGraph graph)
    {
        return layoutCoordinator.CreateLayout(graph);
    }

    private void FocusCurrentSelection(int depth)
    {
        IReadOnlyList<string> focusSeedIds = ResolveFocusSeedIds();
        if (!focusNavigationController.CanFocusSelection(focusSeedIds))
        {
            ShowEditorDialog("Select a node or add one or more seeds first.");
            return;
        }

        TypeGraph focusedGraph = focusNavigationController.FocusSelection(focusSeedIds, depth, currentTraversalMode);
        if (focusedGraph == null)
        {
            ShowEditorDialog("Could not build a focused graph from the current selection.");
            return;
        }

        string selectedNodeId = focusSeedIds.Count > 0 ? focusSeedIds[0] : currentSelectedNodeId;
        SetDisplayedGraph(focusedGraph, selectedNodeId);
    }

    private void GoBackToPreviousGraph()
    {
        GraphViewSnapshot snapshot = focusNavigationController.GoBack();
        if (snapshot == null)
        {
            return;
        }

        SetDisplayedGraph(snapshot.Graph, snapshot.SelectedNodeId);
    }

    private void ResetToRootGraph()
    {
        TypeGraph rootGraph = focusNavigationController.ResetToRoot();
        if (rootGraph == null)
        {
            return;
        }

        SetDisplayedGraph(rootGraph, currentSelectedNodeId);
    }

    private void CenterCurrentGraph()
    {
        canvasView?.CenterGraph();
    }

    private void UpdateToolbarState()
    {
        bool hasCurrentSelection = !string.IsNullOrEmpty(currentSelectedNodeId);
        bool isCurrentSelectionSeed = seedSelectionState.Contains(currentSelectedNodeId);
        IReadOnlyList<string> focusSeedIds = ResolveFocusSeedIds();
        bool canFocusSelection = focusNavigationController.CanFocusSelection(focusSeedIds);
        bool hasGraph = currentGraph != null && currentGraph.Nodes != null && currentGraph.Nodes.Count > 0;
        addSeedButton?.SetEnabled(hasCurrentSelection && !isCurrentSelectionSeed);
        removeSeedButton?.SetEnabled(hasCurrentSelection && isCurrentSelectionSeed);
        clearSeedsButton?.SetEnabled(seedSelectionState.HasSeeds);
        focusDepthOneButton?.SetEnabled(canFocusSelection);
        focusDepthTwoButton?.SetEnabled(canFocusSelection);
        focusDepthThreeButton?.SetEnabled(canFocusSelection);
        centerGraphButton?.SetEnabled(hasGraph);
        backButton?.SetEnabled(focusNavigationController.CanGoBack());
        resetButton?.SetEnabled(focusNavigationController.RootGraph != null
            && focusNavigationController.CurrentGraph != null
            && !ReferenceEquals(focusNavigationController.RootGraph, focusNavigationController.CurrentGraph));
        if (traversalModeMenu != null)
        {
            traversalModeMenu.text = BuildTraversalModeToolbarText();
        }
    }

    private string BuildSourceKey(TypeGraph graph)
    {
        if (graph == null)
        {
            return string.Empty;
        }

        return currentSourceKind + "|" + currentFolderPath + "|" + graph.Title;
    }

    private void ApplyEdgeVisibility()
    {
        if (canvasView == null)
        {
            return;
        }

        canvasView.SetEdgeVisibility(
            inheritanceToggle == null || inheritanceToggle.value,
            implementsToggle == null || implementsToggle.value,
            associationToggle == null || associationToggle.value);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (canvasView == null || IsTypingIntoSearchField(evt.target as VisualElement))
        {
            return;
        }

        if (evt.keyCode == KeyCode.KeypadPlus)
        {
            canvasView.ZoomIn();
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.KeypadMinus || evt.keyCode == KeyCode.Minus)
        {
            canvasView.ZoomOut();
            evt.StopPropagation();
            return;
        }

        if (evt.keyCode == KeyCode.Equals && (evt.shiftKey || evt.character == '+'))
        {
            canvasView.ZoomIn();
            evt.StopPropagation();
        }
    }

    private bool IsTypingIntoSearchField(VisualElement target)
    {
        if (searchField == null || target == null)
        {
            return false;
        }

        return target == searchField || searchField.Contains(target);
    }

    private static void ShowEditorDialog(string message)
    {
        EditorUtility.DisplayDialog("Type Graph Viewer", message, "OK");
    }

    private string BuildStatusText(TypeGraph graph)
    {
        if (graph == null)
        {
            return "No graph loaded.";
        }

        string summary = graph.Title
            + "  |  Nodes: " + graph.Nodes.Count
            + "  Edges: " + graph.Edges.Count
            + "  Groups: " + graph.Groups.Count;

        if (graph.Metadata == null || !graph.Metadata.IsDerivedView)
        {
            return AppendSeedSummary(summary);
        }

        string focusSummary = string.IsNullOrEmpty(graph.Metadata.FocusSummary)
            ? "Focused view."
            : graph.Metadata.FocusSummary;
        string parentGraphTitle = string.IsNullOrEmpty(graph.Metadata.ParentGraphTitle)
            ? "previous graph"
            : graph.Metadata.ParentGraphTitle;

        return AppendSeedSummary(summary
            + "  |  Derived from: "
            + parentGraphTitle
            + "  |  "
            + focusSummary);
    }

    private string AppendSeedSummary(string baseText)
    {
        if (!seedSelectionState.HasSeeds)
        {
            return baseText + "  |  Traversal: " + BuildTraversalModeShortLabel();
        }

        return baseText + "  |  Seed set: " + seedSelectionState.Count + "  |  Traversal: " + BuildTraversalModeShortLabel();
    }

    private void AddCurrentSelectionAsSeed()
    {
        if (string.IsNullOrEmpty(currentSelectedNodeId))
        {
            return;
        }

        seedSelectionState.Add(currentSelectedNodeId);
        UpdateToolbarState();
        if (statusLabel != null)
        {
            statusLabel.text = BuildStatusText(currentGraph);
        }
    }

    private void RemoveCurrentSelectionFromSeeds()
    {
        if (string.IsNullOrEmpty(currentSelectedNodeId))
        {
            return;
        }

        seedSelectionState.Remove(currentSelectedNodeId);
        UpdateToolbarState();
        if (statusLabel != null)
        {
            statusLabel.text = BuildStatusText(currentGraph);
        }
    }

    private void ClearSeedSelection()
    {
        seedSelectionState.Clear();
        UpdateToolbarState();
        if (statusLabel != null)
        {
            statusLabel.text = BuildStatusText(currentGraph);
        }
    }

    private IReadOnlyList<string> ResolveFocusSeedIds()
    {
        if (seedSelectionState.HasSeeds)
        {
            return seedSelectionState.SeedNodeIds;
        }

        return string.IsNullOrEmpty(currentSelectedNodeId)
            ? System.Array.Empty<string>()
            : new[] { currentSelectedNodeId };
    }

    private void SetTraversalMode(GraphFocusTraversalMode traversalMode)
    {
        currentTraversalMode = traversalMode;
        UpdateToolbarState();
        if (statusLabel != null)
        {
            statusLabel.text = BuildStatusText(currentGraph);
        }
    }

    private string BuildTraversalModeToolbarText()
    {
        return "Traversal: " + BuildTraversalModeShortLabel();
    }

    private string BuildTraversalModeShortLabel()
    {
        switch (currentTraversalMode)
        {
            case GraphFocusTraversalMode.OutgoingAssociationsOnly:
                return "Outgoing";
            case GraphFocusTraversalMode.IncomingAssociationsOnly:
                return "Incoming";
            case GraphFocusTraversalMode.AllVisibleRelations:
                return "All Visible";
            default:
                return "Undirected";
        }
    }
}
