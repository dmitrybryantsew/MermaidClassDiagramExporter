using System;
using System.Linq;

internal sealed class FocusedGraphNavigationController
{
    private readonly GraphViewSession session = new GraphViewSession();
    private readonly FocusedSubgraphBuilder focusedSubgraphBuilder = new FocusedSubgraphBuilder();

    public TypeGraph RootGraph => session.RootGraph;

    public TypeGraph CurrentGraph => session.CurrentGraph;

    public bool IsFocusedView =>
        session.RootGraph != null
        && session.CurrentGraph != null
        && !ReferenceEquals(session.RootGraph, session.CurrentGraph);

    public void SetRootGraph(TypeGraph graph, string sourceKey)
    {
        session.Initialize(graph, sourceKey);
    }

    public bool CanFocusSelection(string selectedNodeId)
    {
        return CurrentGraph != null && !string.IsNullOrEmpty(selectedNodeId);
    }

    public bool CanFocusSelection(System.Collections.Generic.IReadOnlyCollection<string> selectedNodeIds)
    {
        return CurrentGraph != null && selectedNodeIds != null && selectedNodeIds.Count > 0;
    }

    public bool CanGoBack()
    {
        return session.CanGoBack();
    }

    public TypeGraph FocusSelection(string selectedNodeId, int depth, GraphFocusTraversalMode traversalMode)
    {
        if (!CanFocusSelection(selectedNodeId))
        {
            return null;
        }

        GraphFocusRequest request = new GraphFocusRequest
        {
            SeedNodeIds = new[] { selectedNodeId },
            AssociationDepth = depth,
            TraversalMode = traversalMode
        };

        TypeGraph focusedGraph = focusedSubgraphBuilder.BuildFocusedGraph(CurrentGraph, request);
        if (focusedGraph == null)
        {
            return null;
        }

        session.PushFocusedGraph(focusedGraph, request, selectedNodeId);
        return focusedGraph;
    }

    public TypeGraph FocusSelection(System.Collections.Generic.IReadOnlyList<string> selectedNodeIds, int depth, GraphFocusTraversalMode traversalMode)
    {
        if (!CanFocusSelection(selectedNodeIds))
        {
            return null;
        }

        GraphFocusRequest request = new GraphFocusRequest
        {
            SeedNodeIds = selectedNodeIds.ToArray(),
            AssociationDepth = depth,
            TraversalMode = traversalMode
        };

        TypeGraph focusedGraph = focusedSubgraphBuilder.BuildFocusedGraph(CurrentGraph, request);
        if (focusedGraph == null)
        {
            return null;
        }

        string primarySelectedNodeId = selectedNodeIds.Count > 0 ? selectedNodeIds[0] : string.Empty;
        session.PushFocusedGraph(focusedGraph, request, primarySelectedNodeId);
        return focusedGraph;
    }

    public GraphViewSnapshot GoBack()
    {
        return session.GoBack();
    }

    public TypeGraph ResetToRoot()
    {
        session.ResetToRoot();
        return session.CurrentGraph;
    }
}
