using System.Collections.Generic;
using System.Linq;

internal sealed class ExternalConnectionAnalysisPass : ILayoutPass
{
    public string Name => "External Connection Analysis";

    public LayoutGraph Run(LayoutGraph graph, LayoutOptions options)
    {
        if (graph == null)
        {
            return new LayoutGraph();
        }

        List<LayoutNode> nodes = graph.Nodes
            .Select(LayoutCloneUtility.CloneNode)
            .ToList();

        Dictionary<string, string> clusterIdByNodeId = nodes.ToDictionary(node => node.Id, node => node.ClusterId);
        List<LayoutCluster> clusters = graph.Clusters
            .Select(cluster =>
            {
                LayoutCluster clone = LayoutCloneUtility.CloneCluster(cluster);
                clone.HasExternalConnections = HasExternalConnections(cluster.Id, graph.Edges, clusterIdByNodeId);
                return clone;
            })
            .ToList();

        return new LayoutGraph
        {
            Title = graph.Title,
            Nodes = nodes,
            Edges = graph.Edges.Select(LayoutCloneUtility.CloneEdge).ToList(),
            Clusters = clusters,
            ExtractedSubgraphs = graph.ExtractedSubgraphs.Select(LayoutCloneUtility.CloneSubgraph).ToList(),
            Metadata = LayoutCloneUtility.CloneMetadata(graph.Metadata)
        };
    }

    private static bool HasExternalConnections(
        string clusterId,
        IEnumerable<LayoutEdge> edges,
        IReadOnlyDictionary<string, string> clusterIdByNodeId)
    {
        foreach (LayoutEdge edge in edges)
        {
            if (!clusterIdByNodeId.TryGetValue(edge.FromNodeId, out string fromClusterId)
                || !clusterIdByNodeId.TryGetValue(edge.ToNodeId, out string toClusterId))
            {
                continue;
            }

            if (fromClusterId != toClusterId
                && (fromClusterId == clusterId || toClusterId == clusterId))
            {
                return true;
            }
        }

        return false;
    }
}
