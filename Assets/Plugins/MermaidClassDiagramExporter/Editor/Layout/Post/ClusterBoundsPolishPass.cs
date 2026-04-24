using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ClusterBoundsPolishPass : IPostLayoutPass
{
    public string Name => "Cluster Bounds Polish";

    public LayoutResult Run(LayoutGraph graph, LayoutResult result, LayoutOptions options)
    {
        if (graph == null || result == null)
        {
            return result ?? new LayoutResult();
        }

        LayoutResult clone = PostLayoutResultUtility.CloneResult(result);
        Dictionary<string, Rect> nodeBounds = (Dictionary<string, Rect>)clone.NodeBounds;
        Dictionary<string, Rect> clusterBounds = (Dictionary<string, Rect>)clone.ClusterBounds;

        List<LayoutCluster> orderedClusters = graph.Clusters
            .OrderByDescending(cluster => PostLayoutResultUtility.GetDescendantClusterIds(graph, cluster.Id).Count)
            .ToList();

        foreach (LayoutCluster cluster in orderedClusters)
        {
            if (!clusterBounds.TryGetValue(cluster.Id, out Rect clusterRect))
            {
                continue;
            }

            Rect? contentBounds = null;
            foreach (string nodeId in cluster.NodeIds)
            {
                if (nodeBounds.TryGetValue(nodeId, out Rect nodeRect))
                {
                    contentBounds = contentBounds.HasValue ? Encapsulate(contentBounds.Value, nodeRect) : nodeRect;
                }
            }

            foreach (string childClusterId in cluster.ChildClusterIds)
            {
                if (clusterBounds.TryGetValue(childClusterId, out Rect childClusterRect))
                {
                    contentBounds = contentBounds.HasValue ? Encapsulate(contentBounds.Value, childClusterRect) : childClusterRect;
                }
            }

            float minWidthFromTitle = Mathf.Max(options.GroupWidth, (cluster.TitleMetrics?.LabelWidth ?? 0f) + options.ClusterTitleHorizontalPadding);
            if (contentBounds.HasValue)
            {
                Rect contents = contentBounds.Value;
                float requiredLeft = contents.xMin - options.GroupLeftPadding;
                float requiredTop = clusterRect.yMin;
                float requiredWidth = (contents.xMax - requiredLeft) + options.GroupLeftPadding;
                float requiredHeight = (contents.yMax - requiredTop) + options.GroupBottomPadding;

                clusterRect.x = Mathf.Min(clusterRect.x, requiredLeft);
                clusterRect.width = Mathf.Max(clusterRect.width, requiredWidth, minWidthFromTitle);
                clusterRect.height = Mathf.Max(clusterRect.height, requiredHeight);
            }
            else
            {
                clusterRect.width = Mathf.Max(clusterRect.width, minWidthFromTitle);
            }

            clusterBounds[cluster.Id] = clusterRect;
        }

        clone.ContentSize = RecalculateContentSize(nodeBounds.Values, clusterBounds.Values, options, clone.ContentSize);
        return clone;
    }

    private static Rect Encapsulate(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin);
        float yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax);
        float yMax = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
