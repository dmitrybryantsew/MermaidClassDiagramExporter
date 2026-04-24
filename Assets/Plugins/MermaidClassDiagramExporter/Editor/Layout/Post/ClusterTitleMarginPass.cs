using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ClusterTitleMarginPass : IPostLayoutPass
{
    public string Name => "Cluster Title Margin";

    public LayoutResult Run(LayoutGraph graph, LayoutResult result, LayoutOptions options)
    {
        if (graph == null || result == null)
        {
            return result ?? new LayoutResult();
        }

        LayoutResult clone = PostLayoutResultUtility.CloneResult(result);
        Dictionary<string, Rect> nodeBounds = (Dictionary<string, Rect>)clone.NodeBounds;
        Dictionary<string, Rect> clusterBounds = (Dictionary<string, Rect>)clone.ClusterBounds;

        foreach (LayoutCluster cluster in graph.Clusters)
        {
            if (!clusterBounds.TryGetValue(cluster.Id, out Rect clusterRect))
            {
                continue;
            }

            float desiredInset = Mathf.Max(options.GroupTopPadding, (cluster.TitleMetrics?.TotalMargin ?? 0f) + 12f);
            float currentInset = CalculateCurrentTopInset(graph, cluster, clusterRect, nodeBounds, clusterBounds);
            float delta = desiredInset - currentInset;
            if (delta <= 0.01f)
            {
                continue;
            }

            ShiftClusterContents(graph, cluster, delta, nodeBounds, clusterBounds);
            clusterRect.height += delta;
            clusterBounds[cluster.Id] = clusterRect;
        }

        clone.ContentSize = RecalculateContentSize(nodeBounds.Values, clusterBounds.Values, options, clone.ContentSize);
        return clone;
    }

    private static float CalculateCurrentTopInset(
        LayoutGraph graph,
        LayoutCluster cluster,
        Rect clusterRect,
        IReadOnlyDictionary<string, Rect> nodeBounds,
        IReadOnlyDictionary<string, Rect> clusterBounds)
    {
        float minY = float.PositiveInfinity;

        foreach (string nodeId in cluster.NodeIds)
        {
            if (nodeBounds.TryGetValue(nodeId, out Rect nodeRect))
            {
                minY = Mathf.Min(minY, nodeRect.yMin);
            }
        }

        foreach (string childClusterId in cluster.ChildClusterIds)
        {
            if (clusterBounds.TryGetValue(childClusterId, out Rect childClusterRect))
            {
                minY = Mathf.Min(minY, childClusterRect.yMin);
            }
        }

        if (float.IsPositiveInfinity(minY))
        {
            return clusterRect.height;
        }

        return minY - clusterRect.yMin;
    }

    private static void ShiftClusterContents(
        LayoutGraph graph,
        LayoutCluster cluster,
        float deltaY,
        IDictionary<string, Rect> nodeBounds,
        IDictionary<string, Rect> clusterBounds)
    {
        HashSet<string> descendantClusterIds = PostLayoutResultUtility.GetDescendantClusterIds(graph, cluster.Id);

        foreach (string nodeId in cluster.NodeIds)
        {
            if (nodeBounds.TryGetValue(nodeId, out Rect nodeRect))
            {
                nodeRect.y += deltaY;
                nodeBounds[nodeId] = nodeRect;
            }
        }

        foreach (string descendantClusterId in descendantClusterIds)
        {
            LayoutCluster descendantCluster = graph.Clusters.FirstOrDefault(candidate => candidate.Id == descendantClusterId);
            if (descendantCluster == null)
            {
                continue;
            }

            if (clusterBounds.TryGetValue(descendantClusterId, out Rect descendantClusterRect))
            {
                descendantClusterRect.y += deltaY;
                clusterBounds[descendantClusterId] = descendantClusterRect;
            }

            foreach (string nodeId in descendantCluster.NodeIds)
            {
                if (nodeBounds.TryGetValue(nodeId, out Rect nodeRect))
                {
                    nodeRect.y += deltaY;
                    nodeBounds[nodeId] = nodeRect;
                }
            }
        }
    }

    private static Vector2 RecalculateContentSize(
        IEnumerable<Rect> nodeRects,
        IEnumerable<Rect> clusterRects,
        LayoutOptions options,
        Vector2 current)
    {
        float maxX = 0f;
        float maxY = 0f;

        foreach (Rect rect in nodeRects.Concat(clusterRects))
        {
            maxX = Mathf.Max(maxX, rect.xMax);
            maxY = Mathf.Max(maxY, rect.yMax);
        }

        return new Vector2(
            Mathf.Max(current.x, maxX + options.OuterMarginX),
            Mathf.Max(current.y, maxY + options.OuterMarginY));
    }
}
