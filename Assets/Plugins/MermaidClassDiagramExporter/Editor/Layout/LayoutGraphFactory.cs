using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class LayoutGraphFactory
{
    public static LayoutGraph Create(TypeGraph graph, LayoutOptions options)
    {
        List<LayoutCluster> clusters = NamespaceClusterBuilder.Build(graph);
        Dictionary<string, string> clusterIdByNodeId = BuildClusterMap(clusters);
        HashSet<string> referencedClusterIds = new HashSet<string>();

        List<LayoutNode> nodes = graph.Nodes
            .Select(node =>
            {
                string clusterId = ResolveClusterId(clusterIdByNodeId, node.Id);
                referencedClusterIds.Add(clusterId);
                float estimatedHeight = TypeNodeElement.EstimateHeight(node.Members.Count);
                string badgeText = BuildBadgeText(node);
                string[] memberLines = BuildVisibleMemberLines(node).ToArray();
                return new LayoutNode
                {
                    Id = node.Id,
                    ClusterId = clusterId,
                    Label = node.DisplayName,
                    Role = LayoutNodeRole.Real,
                    SourceNodeId = node.Id,
                    BadgeText = badgeText,
                    MemberLines = memberLines,
                    EstimatedWidth = options.NodeWidth,
                    EstimatedHeight = estimatedHeight,
                    Width = options.NodeWidth,
                    Height = estimatedHeight
                };
            })
            .ToList();

        EnsureReferencedClustersExist(clusters, referencedClusterIds, nodes);

        List<LayoutEdge> edges = graph.Edges
            .Select(edge => new LayoutEdge
            {
                Id = edge.FromNodeId + "->" + edge.ToNodeId + ":" + edge.Kind,
                OriginalEdgeId = edge.FromNodeId + "->" + edge.ToNodeId + ":" + edge.Kind,
                FromNodeId = edge.FromNodeId,
                ToNodeId = edge.ToNodeId,
                Kind = edge.Kind,
                Role = LayoutEdgeRole.Direct
            })
            .ToList();

        return new LayoutGraph
        {
            Title = graph.Title,
            Nodes = nodes,
            Edges = edges,
            Clusters = clusters,
            ExtractedSubgraphs = new LayoutSubgraph[0],
            Metadata = new LayoutGraphMetadata
            {
                SourceDescription = graph.Metadata?.SourceDescription ?? graph.Title,
                Direction = options.Direction,
                UsesMeasuredNodes = false,
                Spacing = new LayoutSpacingProfile
                {
                    NodeSeparation = options.NodeSpacing,
                    RankSeparation = options.RankSpacing,
                    MarginX = options.OuterMarginX,
                    MarginY = options.OuterMarginY
                }
            }
        };
    }

    private static void EnsureReferencedClustersExist(
        ICollection<LayoutCluster> clusters,
        IEnumerable<string> referencedClusterIds,
        IEnumerable<LayoutNode> nodes)
    {
        HashSet<string> existingClusterIds = new HashSet<string>(clusters.Select(cluster => cluster.Id));
        foreach (string clusterId in referencedClusterIds)
        {
            if (existingClusterIds.Contains(clusterId))
            {
                continue;
            }

            clusters.Add(new LayoutCluster
            {
                Id = clusterId,
                Label = clusterId == "fallback" ? "Ungrouped" : clusterId,
                Kind = TypeGroupKind.Namespace,
                NodeIds = nodes.Where(node => node.ClusterId == clusterId).Select(node => node.Id).ToArray()
            });
            existingClusterIds.Add(clusterId);
        }
    }

    private static IEnumerable<string> BuildVisibleMemberLines(TypeNodeData node)
    {
        return node.Members
            .Take(6)
            .Select(member =>
            {
                if (member.Kind == TypeMemberKind.Method)
                {
                    string parameterSummary = string.Join(", ", member.Parameters.Select(parameter => parameter.TypeName));
                    return member.Name + "(" + parameterSummary + ") : " + member.TypeName;
                }

                return member.Name + " : " + member.TypeName;
            });
    }

    private static string BuildBadgeText(TypeNodeData node)
    {
        switch (node.Kind)
        {
            case TypeNodeKind.Interface:
                return "Interface";
            case TypeNodeKind.Enum:
                return "Enum";
            case TypeNodeKind.Struct:
                return "Struct";
            case TypeNodeKind.StaticClass:
                return "Static";
            case TypeNodeKind.AbstractClass:
                return "Abstract";
            default:
                return node.IsMonoBehaviour ? "Mono" : node.IsScriptableObject ? "SO" : "Class";
        }
    }

    private static string ResolveClusterId(IReadOnlyDictionary<string, string> clusterIdByNodeId, string nodeId)
    {
        if (clusterIdByNodeId.TryGetValue(nodeId, out string clusterId))
        {
            return clusterId;
        }

        return "fallback";
    }

    private static Dictionary<string, string> BuildClusterMap(IEnumerable<LayoutCluster> clusters)
    {
        Dictionary<string, string> clusterIdByNodeId = new Dictionary<string, string>();
        foreach (LayoutCluster cluster in clusters)
        {
            foreach (string nodeId in cluster.NodeIds)
            {
                clusterIdByNodeId[nodeId] = cluster.Id;
            }
        }

        return clusterIdByNodeId;
    }
}
