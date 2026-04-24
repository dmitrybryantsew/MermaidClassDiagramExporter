using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal sealed class LayoutMeasurementService
{
    private readonly Dictionary<string, Vector2> nodeSizeCache = new Dictionary<string, Vector2>();
    private readonly Dictionary<string, ClusterTitleMetrics> clusterTitleCache = new Dictionary<string, ClusterTitleMetrics>();

    public Vector2 MeasureNode(LayoutNode node, LayoutOptions options)
    {
        if (node == null)
        {
            return Vector2.zero;
        }

        string cacheKey = node.Id + "|" + node.Label + "|" + node.BadgeText + "|" + string.Join("\n", node.MemberLines);
        if (nodeSizeCache.TryGetValue(cacheKey, out Vector2 cachedSize))
        {
            return cachedSize;
        }

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            wordWrap = true,
            richText = false
        };
        GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontSize = 10,
            padding = new RectOffset(6, 6, 2, 2),
            margin = new RectOffset(0, 0, 0, 0)
        };
        GUIStyle memberStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 10,
            wordWrap = true,
            richText = false
        };

        Vector2 badgeSize = string.IsNullOrEmpty(node.BadgeText)
            ? Vector2.zero
            : badgeStyle.CalcSize(new GUIContent(node.BadgeText));

        float paddingHorizontal = 20f;
        float paddingVertical = 16f;
        float memberSectionChrome = node.MemberLines.Count > 0 ? 15f : 0f;
        float badgeAndGapWidth = badgeSize.x > 0f ? badgeSize.x + 8f : 0f;

        float preferredContentWidth = titleStyle.CalcSize(new GUIContent(node.Label)).x + badgeAndGapWidth;
        if (node.MemberLines.Count > 0)
        {
            preferredContentWidth = Mathf.Max(
                preferredContentWidth,
                node.MemberLines.Max(line => memberStyle.CalcSize(new GUIContent(line)).x));
        }

        float width = Mathf.Clamp(
            preferredContentWidth + paddingHorizontal,
            options.NodeWidth,
            options.MaxMeasuredNodeWidth);

        float titleWidth = Mathf.Max(96f, width - paddingHorizontal - badgeAndGapWidth);
        float titleHeight = Mathf.Max(18f, titleStyle.CalcHeight(new GUIContent(node.Label), titleWidth));
        float headerHeight = Mathf.Max(titleHeight, badgeSize.y);

        float memberHeight = 0f;
        if (node.MemberLines.Count > 0)
        {
            float memberWidth = Mathf.Max(96f, width - paddingHorizontal);
            memberHeight = node.MemberLines
                .Take(6)
                .Sum(line => memberStyle.CalcHeight(new GUIContent(line), memberWidth) + 2f);
        }

        float height = paddingVertical + headerHeight + memberSectionChrome + memberHeight;
        Vector2 measuredSize = new Vector2(width, Mathf.Max(node.EstimatedHeight, height));
        nodeSizeCache[cacheKey] = measuredSize;
        return measuredSize;
    }

    public ClusterTitleMetrics MeasureClusterTitle(LayoutCluster cluster, LayoutOptions options)
    {
        if (cluster == null)
        {
            return new ClusterTitleMetrics();
        }

        string cacheKey = cluster.Id + "|" + cluster.Label;
        if (clusterTitleCache.TryGetValue(cacheKey, out ClusterTitleMetrics cachedMetrics))
        {
            return CloneClusterTitleMetrics(cachedMetrics);
        }

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            wordWrap = false,
            richText = false
        };

        Vector2 labelSize = labelStyle.CalcSize(new GUIContent(cluster.Label ?? string.Empty));
        ClusterTitleMetrics metrics = new ClusterTitleMetrics
        {
            LabelWidth = labelSize.x,
            LabelHeight = Mathf.Max(14f, labelSize.y),
            TopMargin = options.ClusterTitleTopMargin,
            BottomMargin = options.ClusterTitleBottomMargin
        };

        clusterTitleCache[cacheKey] = CloneClusterTitleMetrics(metrics);
        return metrics;
    }

    private static ClusterTitleMetrics CloneClusterTitleMetrics(ClusterTitleMetrics metrics)
    {
        return new ClusterTitleMetrics
        {
            LabelWidth = metrics.LabelWidth,
            LabelHeight = metrics.LabelHeight,
            TopMargin = metrics.TopMargin,
            BottomMargin = metrics.BottomMargin
        };
    }
}
