using System.Collections.Generic;
using System.Linq;

internal sealed class ClusterHierarchyPass : ILayoutPass
{
    public string Name => "Cluster Hierarchy";

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
        Dictionary<string, HashSet<string>> childClusterIdsByParent = new Dictionary<string, HashSet<string>>();

        List<LayoutCluster> clusters = graph.Clusters
            .Select(cluster =>
            {
                string parentClusterId = ResolveParentClusterId(cluster, clusterIdByNodeId, graph.Clusters);
                if (!string.IsNullOrEmpty(parentClusterId))
                {
                    if (!childClusterIdsByParent.TryGetValue(parentClusterId, out HashSet<string> childClusterIds))
                    {
                        childClusterIds = new HashSet<string>();
                        childClusterIdsByParent[parentClusterId] = childClusterIds;
                    }

                    childClusterIds.Add(cluster.Id);
                }

                return new LayoutCluster
                {
                    Id = cluster.Id,
                    Label = cluster.Label,
                    Kind = cluster.Kind,
                    ParentClusterId = parentClusterId,
                    NodeIds = cluster.NodeIds.ToArray(),
                    ChildClusterIds = System.Array.Empty<string>(),
                    HasExternalConnections = cluster.HasExternalConnections,
                    RepresentativeNodeId = cluster.RepresentativeNodeId,
                    IsExtractedSubgraph = cluster.IsExtractedSubgraph,
                    TitleMetrics = LayoutCloneUtility.CloneTitleMetrics(cluster.TitleMetrics)
                };
            })
            .ToList();

        foreach (LayoutCluster cluster in clusters)
        {
            if (childClusterIdsByParent.TryGetValue(cluster.Id, out HashSet<string> childClusterIds))
            {
                cluster.ChildClusterIds = childClusterIds.OrderBy(id => id).ToArray();
            }
        }

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

    private static string ResolveParentClusterId(
        LayoutCluster cluster,
        IReadOnlyDictionary<string, string> clusterIdByNodeId,
        IReadOnlyList<LayoutCluster> clusters)
    {
        if (string.IsNullOrEmpty(cluster.Id) || cluster.NodeIds.Count == 0)
        {
            return string.Empty;
        }

        HashSet<string> clusterNodeIds = new HashSet<string>(cluster.NodeIds);
        foreach (LayoutCluster candidateParent in clusters)
        {
            if (candidateParent.Id == cluster.Id || candidateParent.NodeIds.Count == 0)
            {
                continue;
            }

            bool isParent = clusterNodeIds.All(nodeId =>
                clusterIdByNodeId.TryGetValue(nodeId, out string ownerClusterId)
                && ownerClusterId == cluster.Id
                && candidateParent.NodeIds.Contains(nodeId));

            if (isParent)
            {
                return candidateParent.Id;
            }
        }

        return string.Empty;
    }
}
