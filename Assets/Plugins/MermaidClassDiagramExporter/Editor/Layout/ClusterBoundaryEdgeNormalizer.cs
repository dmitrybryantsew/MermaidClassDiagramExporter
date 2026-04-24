using System.Collections.Generic;
using System.Linq;

internal static class ClusterBoundaryEdgeNormalizer
{
    public static LayoutGraph Normalize(LayoutGraph graph, LayoutOptions options)
    {
        if (graph == null)
        {
            return new LayoutGraph();
        }

        List<LayoutCluster> clusters = graph.Clusters
            .Select(LayoutCloneUtility.CloneCluster)
            .ToList();

        Dictionary<string, LayoutCluster> clusterById = clusters.ToDictionary(cluster => cluster.Id);
        List<LayoutNode> nodes = graph.Nodes
            .Select(LayoutCloneUtility.CloneNode)
            .ToList();

        Dictionary<string, string> clusterIdByNodeId = nodes.ToDictionary(node => node.Id, node => node.ClusterId);
        List<LayoutEdge> normalizedEdges = new List<LayoutEdge>();
        Dictionary<string, string> outboundAnchorIds = new Dictionary<string, string>();
        Dictionary<string, string> inboundAnchorIds = new Dictionary<string, string>();

        foreach (LayoutEdge edge in graph.Edges)
        {
            if (!clusterIdByNodeId.TryGetValue(edge.FromNodeId, out string fromClusterId)
                || !clusterIdByNodeId.TryGetValue(edge.ToNodeId, out string toClusterId)
                || fromClusterId == toClusterId)
            {
                normalizedEdges.Add(LayoutCloneUtility.CloneEdge(edge));
                continue;
            }

            string outboundAnchorId = GetOrCreateAnchor(
                outboundAnchorIds,
                nodes,
                clusterById,
                fromClusterId,
                toClusterId,
                LayoutNodeRole.ClusterOutboundAnchor,
                options);

            string inboundAnchorId = GetOrCreateAnchor(
                inboundAnchorIds,
                nodes,
                clusterById,
                toClusterId,
                fromClusterId,
                LayoutNodeRole.ClusterInboundAnchor,
                options);

            string sourceBoundaryNodeId = ResolveBoundaryNodeId(
                clusterById,
                fromClusterId,
                edge.FromNodeId);

            string targetBoundaryNodeId = ResolveBoundaryNodeId(
                clusterById,
                toClusterId,
                edge.ToNodeId);

            if (sourceBoundaryNodeId != edge.FromNodeId)
            {
                normalizedEdges.Add(new LayoutEdge
                {
                    Id = edge.Id + "::representative-source",
                    OriginalEdgeId = string.IsNullOrEmpty(edge.OriginalEdgeId) ? edge.Id : edge.OriginalEdgeId,
                    FromNodeId = edge.FromNodeId,
                    ToNodeId = sourceBoundaryNodeId,
                    Kind = TypeEdgeKind.Association,
                    Role = LayoutEdgeRole.BoundarySourceLink
                });
            }

            normalizedEdges.Add(new LayoutEdge
            {
                Id = edge.Id + "::source",
                OriginalEdgeId = string.IsNullOrEmpty(edge.OriginalEdgeId) ? edge.Id : edge.OriginalEdgeId,
                FromNodeId = sourceBoundaryNodeId,
                ToNodeId = outboundAnchorId,
                Kind = TypeEdgeKind.Association,
                Role = LayoutEdgeRole.BoundarySourceLink
            });

            normalizedEdges.Add(new LayoutEdge
            {
                Id = edge.Id + "::bridge",
                OriginalEdgeId = string.IsNullOrEmpty(edge.OriginalEdgeId) ? edge.Id : edge.OriginalEdgeId,
                FromNodeId = outboundAnchorId,
                ToNodeId = inboundAnchorId,
                Kind = edge.Kind,
                Role = LayoutEdgeRole.BoundaryBridge
            });

            normalizedEdges.Add(new LayoutEdge
            {
                Id = edge.Id + "::target",
                OriginalEdgeId = string.IsNullOrEmpty(edge.OriginalEdgeId) ? edge.Id : edge.OriginalEdgeId,
                FromNodeId = inboundAnchorId,
                ToNodeId = targetBoundaryNodeId,
                Kind = TypeEdgeKind.Association,
                Role = LayoutEdgeRole.BoundaryTargetLink
            });

            if (targetBoundaryNodeId != edge.ToNodeId)
            {
                normalizedEdges.Add(new LayoutEdge
                {
                    Id = edge.Id + "::representative-target",
                    OriginalEdgeId = string.IsNullOrEmpty(edge.OriginalEdgeId) ? edge.Id : edge.OriginalEdgeId,
                    FromNodeId = targetBoundaryNodeId,
                    ToNodeId = edge.ToNodeId,
                    Kind = TypeEdgeKind.Association,
                    Role = LayoutEdgeRole.BoundaryTargetLink
                });
            }
        }

        return new LayoutGraph
        {
            Title = graph.Title,
            Nodes = nodes,
            Edges = normalizedEdges,
            Clusters = clusters,
            ExtractedSubgraphs = graph.ExtractedSubgraphs.Select(LayoutCloneUtility.CloneSubgraph).ToList(),
            Metadata = LayoutCloneUtility.CloneMetadata(graph.Metadata)
        };
    }
    
    private static string ResolveBoundaryNodeId(
        IReadOnlyDictionary<string, LayoutCluster> clusterById,
        string clusterId,
        string fallbackNodeId)
    {
        if (clusterById.TryGetValue(clusterId, out LayoutCluster cluster)
            && cluster.HasExternalConnections
            && !string.IsNullOrEmpty(cluster.RepresentativeNodeId))
        {
            return cluster.RepresentativeNodeId;
        }

        return fallbackNodeId;
    }

    private static string GetOrCreateAnchor(
        IDictionary<string, string> anchorIds,
        ICollection<LayoutNode> nodes,
        IReadOnlyDictionary<string, LayoutCluster> clusterById,
        string ownerClusterId,
        string peerClusterId,
        LayoutNodeRole role,
        LayoutOptions options)
    {
        string key = ownerClusterId + "=>" + peerClusterId + ":" + role;
        if (anchorIds.TryGetValue(key, out string existingAnchorId))
        {
            return existingAnchorId;
        }

        string anchorId = "anchor::" + ownerClusterId + "::" + peerClusterId + "::" + role;
        LayoutNode anchorNode = new LayoutNode
        {
            Id = anchorId,
            ClusterId = ownerClusterId,
            Label = peerClusterId,
            Role = role,
            SourceNodeId = string.Empty,
            Width = options.ClusterAnchorWidth,
            Height = options.ClusterAnchorHeight,
            EstimatedWidth = options.ClusterAnchorWidth,
            EstimatedHeight = options.ClusterAnchorHeight,
            MeasuredWidth = options.ClusterAnchorWidth,
            MeasuredHeight = options.ClusterAnchorHeight,
            IsMeasured = true
        };

        nodes.Add(anchorNode);

        if (clusterById.TryGetValue(ownerClusterId, out LayoutCluster cluster))
        {
            cluster.NodeIds = cluster.NodeIds.Concat(new[] { anchorId }).ToArray();
        }

        anchorIds[key] = anchorId;
        return anchorId;
    }
}
