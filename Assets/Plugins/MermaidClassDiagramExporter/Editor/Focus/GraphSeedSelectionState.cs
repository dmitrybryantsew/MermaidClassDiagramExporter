using System.Collections.Generic;
using System.Linq;

internal sealed class GraphSeedSelectionState
{
    private readonly HashSet<string> seedNodeIds = new HashSet<string>();

    public int Count => seedNodeIds.Count;

    public bool HasSeeds => seedNodeIds.Count > 0;

    public IReadOnlyList<string> SeedNodeIds => seedNodeIds.OrderBy(id => id).ToArray();

    public bool Contains(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && seedNodeIds.Contains(nodeId);
    }

    public void Add(string nodeId)
    {
        if (!string.IsNullOrEmpty(nodeId))
        {
            seedNodeIds.Add(nodeId);
        }
    }

    public void Remove(string nodeId)
    {
        if (!string.IsNullOrEmpty(nodeId))
        {
            seedNodeIds.Remove(nodeId);
        }
    }

    public void Clear()
    {
        seedNodeIds.Clear();
    }

    public void PruneToGraph(TypeGraph graph)
    {
        if (graph == null || graph.Nodes == null)
        {
            seedNodeIds.Clear();
            return;
        }

        HashSet<string> validNodeIds = new HashSet<string>(graph.Nodes.Select(node => node.Id));
        seedNodeIds.RemoveWhere(nodeId => !validNodeIds.Contains(nodeId));
    }
}
