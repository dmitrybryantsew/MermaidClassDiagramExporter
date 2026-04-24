using System.Collections.Generic;

internal sealed class GraphViewSession
{
    private readonly Stack<GraphViewSnapshot> backStack = new Stack<GraphViewSnapshot>();

    public TypeGraph RootGraph { get; private set; }

    public TypeGraph CurrentGraph { get; private set; }

    public string SourceKey { get; private set; } = string.Empty;

    public void Initialize(TypeGraph rootGraph, string sourceKey)
    {
        RootGraph = rootGraph;
        CurrentGraph = rootGraph;
        SourceKey = sourceKey ?? string.Empty;
        backStack.Clear();
    }

    public void PushFocusedGraph(TypeGraph focusedGraph, GraphFocusRequest request, string selectedNodeId)
    {
        if (CurrentGraph != null)
        {
            backStack.Push(new GraphViewSnapshot
            {
                Graph = CurrentGraph,
                Request = request,
                Title = CurrentGraph.Title,
                SelectedNodeId = selectedNodeId ?? string.Empty
            });
        }

        CurrentGraph = focusedGraph;
    }

    public bool CanGoBack()
    {
        return backStack.Count > 0;
    }

    public GraphViewSnapshot GoBack()
    {
        if (backStack.Count == 0)
        {
            return null;
        }

        GraphViewSnapshot snapshot = backStack.Pop();
        CurrentGraph = snapshot.Graph;
        return snapshot;
    }

    public void ResetToRoot()
    {
        CurrentGraph = RootGraph;
        backStack.Clear();
    }
}
