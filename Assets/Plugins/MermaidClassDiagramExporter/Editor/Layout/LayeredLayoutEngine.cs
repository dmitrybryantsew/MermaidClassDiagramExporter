using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class LayeredLayoutEngine : IGraphLayoutEngine
{
    private static readonly CrossingReductionService crossingReductionService = new CrossingReductionService();

    public LayoutResult Run(LayoutGraph graph, LayoutOptions options)
    {
        Dictionary<string, Rect> nodeBounds = new Dictionary<string, Rect>();
        Dictionary<string, Rect> clusterBounds = new Dictionary<string, Rect>();

        Dictionary<string, LayoutNode> nodeById = graph.Nodes.ToDictionary(node => node.Id);
        Dictionary<string, string> clusterIdByNodeId = graph.Nodes.ToDictionary(node => node.Id, node => node.ClusterId);

        List<List<LayoutCluster>> components = ComponentSplitter.SplitClusters(graph);
        float currentX = options.OuterMarginX;
        float currentY = options.OuterMarginY;
        float rowHeight = 0f;
        float maxContentWidth = options.MinimumContentWidth;

        foreach (List<LayoutCluster> component in components)
        {
            ComponentLayout componentLayout = BuildComponentLayout(component, graph, nodeById, clusterIdByNodeId, options);

            if (currentX > options.OuterMarginX && currentX + componentLayout.Size.x > options.TargetRowWidth)
            {
                currentX = options.OuterMarginX;
                currentY += rowHeight + options.ComponentSpacing;
                rowHeight = 0f;
            }

            OffsetLayout(componentLayout, new Vector2(currentX, currentY), nodeBounds, clusterBounds);

            currentX += componentLayout.Size.x + options.ComponentSpacing;
            rowHeight = Mathf.Max(rowHeight, componentLayout.Size.y);
            maxContentWidth = Mathf.Max(maxContentWidth, currentX + options.OuterMarginX);
        }

        float totalHeight = currentY + rowHeight + options.OuterMarginY;

        return new LayoutResult
        {
            NodeBounds = nodeBounds,
            ClusterBounds = clusterBounds,
            ContentSize = new Vector2(
                Mathf.Max(options.MinimumContentWidth, maxContentWidth),
                Mathf.Max(options.MinimumContentHeight, totalHeight))
        };
    }

    private static ComponentLayout BuildComponentLayout(
        IReadOnlyList<LayoutCluster> component,
        LayoutGraph graph,
        IReadOnlyDictionary<string, LayoutNode> nodeById,
        IReadOnlyDictionary<string, string> clusterIdByNodeId,
        LayoutOptions options)
    {
        Dictionary<string, ClusterMetric> metrics = BuildClusterMetrics(component, graph, clusterIdByNodeId);
        Dictionary<string, int> ranks = AssignClusterRanks(component, graph, clusterIdByNodeId, metrics);
        Dictionary<int, List<LayoutCluster>> clustersByRank = GroupClustersByRank(component, ranks, metrics);

        Dictionary<string, Rect> clusterBounds = new Dictionary<string, Rect>();
        Dictionary<string, Rect> nodeBounds = new Dictionary<string, Rect>();

        float x = 0f;
        float componentHeight = 0f;
        foreach (int rank in clustersByRank.Keys.OrderBy(value => value))
        {
            List<LayoutCluster> rankClusters = clustersByRank[rank];
            float maxRankWidth = 0f;
            float y = 0f;

            foreach (LayoutCluster cluster in rankClusters)
            {
                ClusterLayout clusterLayout = BuildClusterLayout(cluster, graph, nodeById, options);
                clusterBounds[cluster.Id] = new Rect(x, y, clusterLayout.Bounds.width, clusterLayout.Bounds.height);

                foreach (KeyValuePair<string, Rect> pair in clusterLayout.NestedClusterBounds)
                {
                    Rect rect = pair.Value;
                    rect.position += new Vector2(x, y);
                    clusterBounds[pair.Key] = rect;
                }

                foreach (KeyValuePair<string, Rect> pair in clusterLayout.NodeBounds)
                {
                    Rect rect = pair.Value;
                    rect.position += new Vector2(x, y);
                    nodeBounds[pair.Key] = rect;
                }

                maxRankWidth = Mathf.Max(maxRankWidth, clusterLayout.Bounds.width);
                y += clusterLayout.Bounds.height + options.ClusterSpacing;
                componentHeight = Mathf.Max(componentHeight, y);
            }

            x += maxRankWidth + options.RankSpacing;
        }

        float componentWidth = clusterBounds.Count > 0
            ? clusterBounds.Values.Max(rect => rect.xMax)
            : 0f;

        return new ComponentLayout
        {
            NodeBounds = nodeBounds,
            ClusterBounds = clusterBounds,
            Size = new Vector2(componentWidth, Mathf.Max(0f, componentHeight - options.ClusterSpacing))
        };
    }

    private static Dictionary<string, ClusterMetric> BuildClusterMetrics(
        IReadOnlyList<LayoutCluster> component,
        LayoutGraph graph,
        IReadOnlyDictionary<string, string> clusterIdByNodeId)
    {
        HashSet<string> componentClusterIds = new HashSet<string>(component.Select(cluster => cluster.Id));
        Dictionary<string, ClusterMetric> metrics = component.ToDictionary(
            cluster => cluster.Id,
            cluster => new ClusterMetric
            {
                Label = cluster.Label,
                NodeCount = cluster.NodeIds.Count
            });

        foreach (LayoutEdge edge in graph.Edges)
        {
            if (!clusterIdByNodeId.TryGetValue(edge.FromNodeId, out string fromClusterId)
                || !clusterIdByNodeId.TryGetValue(edge.ToNodeId, out string toClusterId))
            {
                continue;
            }

            if (!componentClusterIds.Contains(fromClusterId) || !componentClusterIds.Contains(toClusterId))
            {
                continue;
            }

            float weight = GetEdgeWeight(edge.Kind);
            metrics[fromClusterId].OutWeight += weight;
            metrics[toClusterId].InWeight += weight;
            metrics[fromClusterId].ConnectedClusterIds.Add(toClusterId);
            metrics[toClusterId].ConnectedClusterIds.Add(fromClusterId);
        }

        return metrics;
    }

    private static Dictionary<string, int> AssignClusterRanks(
        IReadOnlyList<LayoutCluster> component,
        LayoutGraph graph,
        IReadOnlyDictionary<string, string> clusterIdByNodeId,
        IReadOnlyDictionary<string, ClusterMetric> metrics)
    {
        Dictionary<string, int> ranks = BuildBaselineRanks(component, metrics);
        HashSet<string> componentClusterIds = new HashSet<string>(component.Select(cluster => cluster.Id));

        for (int i = 0; i < component.Count * 2; i++)
        {
            bool changed = false;

            foreach (LayoutEdge edge in graph.Edges.OrderByDescending(candidate => GetEdgeWeight(candidate.Kind)))
            {
                if (!clusterIdByNodeId.TryGetValue(edge.FromNodeId, out string fromClusterId)
                    || !clusterIdByNodeId.TryGetValue(edge.ToNodeId, out string toClusterId)
                    || fromClusterId == toClusterId
                    || !componentClusterIds.Contains(fromClusterId)
                    || !componentClusterIds.Contains(toClusterId))
                {
                    continue;
                }

                int rankDelta = edge.Kind == TypeEdgeKind.Association ? 0 : 1;
                int proposedRank = ranks[fromClusterId] + rankDelta;
                if (proposedRank > ranks[toClusterId])
                {
                    ranks[toClusterId] = proposedRank;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        int minRank = ranks.Values.Min();
        if (minRank != 0)
        {
            foreach (string clusterId in component.Select(cluster => cluster.Id).ToList())
            {
                ranks[clusterId] -= minRank;
            }
        }

        return ranks;
    }

    private static Dictionary<string, int> BuildBaselineRanks(
        IReadOnlyList<LayoutCluster> component,
        IReadOnlyDictionary<string, ClusterMetric> metrics)
    {
        List<LayoutCluster> orderedClusters = component
            .OrderBy(cluster => metrics[cluster.Id].InWeight - metrics[cluster.Id].OutWeight)
            .ThenByDescending(cluster => metrics[cluster.Id].ConnectedClusterIds.Count)
            .ThenBy(cluster => cluster.Label)
            .ToList();

        int targetRankCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(component.Count)));
        int clustersPerRank = Mathf.Max(1, Mathf.CeilToInt(component.Count / (float)targetRankCount));
        Dictionary<string, int> ranks = new Dictionary<string, int>();

        for (int i = 0; i < orderedClusters.Count; i++)
        {
            ranks[orderedClusters[i].Id] = i / clustersPerRank;
        }

        return ranks;
    }

    private static Dictionary<int, List<LayoutCluster>> GroupClustersByRank(
        IReadOnlyList<LayoutCluster> component,
        IReadOnlyDictionary<string, int> ranks,
        IReadOnlyDictionary<string, ClusterMetric> metrics)
    {
        return component
            .GroupBy(cluster => ranks[cluster.Id])
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(cluster => metrics[cluster.Id].ConnectedClusterIds.Count)
                    .ThenByDescending(cluster => metrics[cluster.Id].OutWeight + metrics[cluster.Id].InWeight)
                    .ThenBy(cluster => cluster.Label)
                    .ToList());
    }

    private static ClusterLayout BuildClusterLayout(
        LayoutCluster cluster,
        LayoutGraph graph,
        IReadOnlyDictionary<string, LayoutNode> nodeById,
        LayoutOptions options)
    {
        if (TryBuildExtractedSubgraphClusterLayout(cluster, graph, options, out ClusterLayout extractedLayout))
        {
            return extractedLayout;
        }

        List<LayoutNode> nodes = cluster.NodeIds
            .Select(nodeId => nodeById.TryGetValue(nodeId, out LayoutNode node) ? node : null)
            .Where(node => node != null)
            .ToList();

        Dictionary<string, int> localRanks = AssignLocalNodeRanks(nodes, graph);
        List<LayoutNode> orderedNodes = nodes
            .OrderBy(node => localRanks[node.Id])
            .ThenByDescending(node => node.Height)
            .ThenBy(node => node.Id)
            .ToList();

        List<LayoutNode> inboundAnchors = orderedNodes
            .Where(node => node.Role == LayoutNodeRole.ClusterInboundAnchor)
            .ToList();

        List<LayoutNode> coreNodes = orderedNodes
            .Where(node => node.Role == LayoutNodeRole.Real || node.Role == LayoutNodeRole.SelfLoopHelper)
            .ToList();

        List<LayoutNode> outboundAnchors = orderedNodes
            .Where(node => node.Role == LayoutNodeRole.ClusterOutboundAnchor)
            .ToList();

        float clusterTopPadding = GetClusterTopPadding(cluster, options);
        float minimumClusterWidth = GetMinimumClusterWidth(cluster, options);

        if (ShouldUseInternalSubgraphLayout(coreNodes, inboundAnchors, outboundAnchors, graph))
        {
            return BuildInternalOnlyClusterLayout(cluster, coreNodes, graph, options, clusterTopPadding, minimumClusterWidth);
        }

        StructuredCoreLayout coreLayout = BuildStructuredCoreLayout(coreNodes, graph, options);

        Dictionary<string, Rect> nodeBounds = new Dictionary<string, Rect>();
        float currentX = options.GroupLeftPadding;
        float contentHeight = Mathf.Max(0f, coreLayout.Height);

        if (inboundAnchors.Count > 0)
        {
            float currentY = clusterTopPadding;
            float columnWidth = 0f;

            foreach (LayoutNode node in inboundAnchors)
            {
                Rect rect = new Rect(currentX, currentY, node.Width, node.Height);
                nodeBounds[node.Id] = rect;
                currentY += node.Height + options.NodeSpacing;
                columnWidth = Mathf.Max(columnWidth, node.Width);
                contentHeight = Mathf.Max(contentHeight, currentY);
            }

            currentX += columnWidth + options.NodeColumnSpacing;
        }

        foreach (KeyValuePair<string, Rect> pair in coreLayout.NodeBounds)
        {
            Rect rect = pair.Value;
            rect.position += new Vector2(currentX, clusterTopPadding);
            nodeBounds[pair.Key] = rect;
        }

        currentX += coreLayout.Width;
        if (coreLayout.NodeBounds.Count > 0 && outboundAnchors.Count > 0)
        {
            currentX += options.NodeColumnSpacing;
        }

        if (outboundAnchors.Count > 0)
        {
            float currentY = clusterTopPadding;
            float columnWidth = 0f;

            foreach (LayoutNode node in outboundAnchors)
            {
                Rect rect = new Rect(currentX, currentY, node.Width, node.Height);
                nodeBounds[node.Id] = rect;
                currentY += node.Height + options.NodeSpacing;
                columnWidth = Mathf.Max(columnWidth, node.Width);
                contentHeight = Mathf.Max(contentHeight, currentY);
            }

            currentX += columnWidth;
        }

        float clusterWidth = Mathf.Max(
            minimumClusterWidth,
            currentX + options.GroupLeftPadding);

        float clusterHeight = Mathf.Max(
            clusterTopPadding + options.GroupBottomPadding + 24f,
            contentHeight + options.GroupBottomPadding);

        return new ClusterLayout
        {
            Bounds = new Rect(0f, 0f, clusterWidth, clusterHeight),
            NodeBounds = nodeBounds
        };
    }

    private static bool TryBuildExtractedSubgraphClusterLayout(
        LayoutCluster cluster,
        LayoutGraph graph,
        LayoutOptions options,
        out ClusterLayout clusterLayout)
    {
        clusterLayout = null;

        LayoutSubgraph extractedSubgraph = graph.ExtractedSubgraphs
            .FirstOrDefault(candidate => candidate.ClusterId == cluster.Id);

        if (extractedSubgraph == null
            || extractedSubgraph.Graph == null
            || extractedSubgraph.Graph.Nodes.Count == 0
            || extractedSubgraph.Graph.Clusters.Count == 0)
        {
            return false;
        }

        float clusterTopPadding = GetClusterTopPadding(cluster, options);
        float minimumClusterWidth = GetMinimumClusterWidth(cluster, options);
        LayoutOptions subgraphOptions = CreateSubgraphOptions(options, extractedSubgraph);
        LayoutResult subgraphLayout = new LayeredLayoutEngine().Run(extractedSubgraph.Graph, subgraphOptions);
        if (subgraphLayout == null || subgraphLayout.NodeBounds.Count == 0)
        {
            return false;
        }

        Dictionary<string, Rect> rawNestedClusterBounds = subgraphLayout.ClusterBounds
            .Where(pair => pair.Key != cluster.Id)
            .ToDictionary(
                pair => pair.Key,
                pair => pair.Value);

        Rect contentBounds = BuildContentBounds(
            subgraphLayout.NodeBounds.Values.Concat(rawNestedClusterBounds.Values));
        Vector2 normalizationOffset = contentBounds.position;

        Dictionary<string, Rect> nodeBounds = subgraphLayout.NodeBounds.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                Rect rect = pair.Value;
                rect.position = new Vector2(
                    (rect.x - normalizationOffset.x) + options.GroupLeftPadding,
                    (rect.y - normalizationOffset.y) + clusterTopPadding);
                return rect;
            });

        Dictionary<string, Rect> nestedClusterBounds = rawNestedClusterBounds.ToDictionary(
            pair => pair.Key,
            pair =>
            {
                Rect rect = pair.Value;
                rect.position = new Vector2(
                    (rect.x - normalizationOffset.x) + options.GroupLeftPadding,
                    (rect.y - normalizationOffset.y) + clusterTopPadding);
                return rect;
            });

        float contentWidth = Mathf.Max(0f, contentBounds.width);
        float contentHeight = Mathf.Max(0f, contentBounds.height);
        float clusterWidth = Mathf.Max(
            minimumClusterWidth,
            (options.GroupLeftPadding * 2f) + contentWidth);

        float clusterHeight = Mathf.Max(
            clusterTopPadding + options.GroupBottomPadding + 24f,
            clusterTopPadding + contentHeight + options.GroupBottomPadding);

        clusterLayout = new ClusterLayout
        {
            Bounds = new Rect(0f, 0f, clusterWidth, clusterHeight),
            NodeBounds = nodeBounds,
            NestedClusterBounds = nestedClusterBounds
        };

        return true;
    }

    private static LayoutOptions CreateSubgraphOptions(LayoutOptions options, LayoutSubgraph subgraph)
    {
        return new LayoutOptions
        {
            Direction = subgraph.Direction,
            RankSpacing = subgraph.Spacing != null && subgraph.Spacing.RankSeparation > 0f
                ? subgraph.Spacing.RankSeparation
                : options.RankSpacing + options.RecursiveRankSpacingBonus,
            ClusterSpacing = options.ClusterSpacing,
            ComponentSpacing = options.ComponentSpacing,
            GroupLeftPadding = options.GroupLeftPadding,
            GroupTopPadding = options.GroupTopPadding,
            GroupWidth = options.GroupWidth,
            GroupSpacing = options.GroupSpacing,
            NodeSpacing = subgraph.Spacing != null && subgraph.Spacing.NodeSeparation > 0f
                ? subgraph.Spacing.NodeSeparation
                : options.NodeSpacing,
            OuterMarginX = 0f,
            OuterMarginY = 0f,
            NodeWidth = options.NodeWidth,
            MaxMeasuredNodeWidth = options.MaxMeasuredNodeWidth,
            GroupBottomPadding = options.GroupBottomPadding,
            ClusterTitleHorizontalPadding = options.ClusterTitleHorizontalPadding,
            ClusterTitleTopMargin = options.ClusterTitleTopMargin,
            ClusterTitleBottomMargin = options.ClusterTitleBottomMargin,
            NodeColumnSpacing = options.NodeColumnSpacing,
            MaxClusterColumns = options.MaxClusterColumns,
            TargetRowWidth = Mathf.Max(options.GroupWidth, options.StructuredClusterMaxRowWidth),
            StructuredClusterMaxRowWidth = options.StructuredClusterMaxRowWidth,
            StructuredClusterMaxNodesPerRow = options.StructuredClusterMaxNodesPerRow,
            StructuredNodeColumnSpacing = options.StructuredNodeColumnSpacing,
            StructuredRankGap = options.StructuredRankGap,
            StructuredWrappedRowGap = options.StructuredWrappedRowGap,
            StructuredRowIndentStep = options.StructuredRowIndentStep,
            StructuredRowMaxIndent = options.StructuredRowMaxIndent,
            StructuredRowCenteringBias = options.StructuredRowCenteringBias,
            ClusterAnchorWidth = options.ClusterAnchorWidth,
            ClusterAnchorHeight = options.ClusterAnchorHeight,
            RecursiveRankSpacingBonus = options.RecursiveRankSpacingBonus,
            MinimumContentWidth = 0f,
            MinimumContentHeight = 0f
        };
    }

    private static Rect BuildContentBounds(IEnumerable<Rect> rects)
    {
        Rect? aggregate = null;

        foreach (Rect rect in rects)
        {
            aggregate = aggregate.HasValue ? Encapsulate(aggregate.Value, rect) : rect;
        }

        return aggregate ?? new Rect(0f, 0f, 0f, 0f);
    }

    private static Rect Encapsulate(Rect a, Rect b)
    {
        float minX = Mathf.Min(a.xMin, b.xMin);
        float minY = Mathf.Min(a.yMin, b.yMin);
        float maxX = Mathf.Max(a.xMax, b.xMax);
        float maxY = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private static StructuredCoreLayout BuildStructuredCoreLayout(
        IReadOnlyList<LayoutNode> coreNodes,
        LayoutGraph graph,
        LayoutOptions options)
    {
        if (coreNodes.Count == 0)
        {
            return new StructuredCoreLayout();
        }

        Dictionary<string, int> ranks = AssignLocalNodeRanks(coreNodes, graph);
        NormalizeRanks(ranks);
        ApplyWeakAssociationRankSpread(coreNodes, graph, ranks);
        NormalizeRanks(ranks);

        List<StructuredRow> structuredRows = BuildStructuredRows(coreNodes, ranks, graph, options);

        Dictionary<string, Rect> nodeBounds = new Dictionary<string, Rect>();
        Dictionary<int, float> rowWidths = new Dictionary<int, float>();
        Dictionary<int, List<string>> rowNodeIds = new Dictionary<int, List<string>>();

        float currentY = 0f;
        float maxContentWidth = 0f;

        int previousRank = int.MinValue;
        for (int rowIndex = 0; rowIndex < structuredRows.Count; rowIndex++)
        {
            StructuredRow structuredRow = structuredRows[rowIndex];
            List<LayoutNode> row = structuredRow.Nodes;
            if (rowIndex > 0)
            {
                currentY += structuredRow.Rank == previousRank
                    ? options.StructuredWrappedRowGap
                    : options.StructuredRankGap;
            }

            float currentX = 0f;
            float rowHeight = 0f;
            rowNodeIds[rowIndex] = new List<string>();

            foreach (LayoutNode node in row)
            {
                Rect rect = new Rect(currentX, currentY, node.Width, node.Height);
                nodeBounds[node.Id] = rect;
                rowNodeIds[rowIndex].Add(node.Id);
                currentX += node.Width + options.StructuredNodeColumnSpacing;
                rowHeight = Mathf.Max(rowHeight, node.Height);
            }

            float rowWidth = row.Count > 0 ? currentX - options.StructuredNodeColumnSpacing : 0f;
            rowWidths[rowIndex] = rowWidth;
            maxContentWidth = Mathf.Max(maxContentWidth, rowWidth);
            currentY += rowHeight;
            previousRank = structuredRow.Rank;
        }

        OffsetStructuredRows(nodeBounds, rowNodeIds, rowWidths, maxContentWidth, options);

        float contentHeight = Mathf.Max(0f, currentY);
        return new StructuredCoreLayout
        {
            NodeBounds = nodeBounds,
            Width = maxContentWidth,
            Height = contentHeight
        };
    }

    private static bool ShouldUseInternalSubgraphLayout(
        IReadOnlyList<LayoutNode> coreNodes,
        IReadOnlyList<LayoutNode> inboundAnchors,
        IReadOnlyList<LayoutNode> outboundAnchors,
        LayoutGraph graph)
    {
        if (coreNodes.Count < 3 || inboundAnchors.Count > 0 || outboundAnchors.Count > 0)
        {
            return false;
        }

        HashSet<string> coreNodeIds = new HashSet<string>(coreNodes.Select(node => node.Id));
        int structuralEdgeCount = graph.Edges.Count(edge =>
            coreNodeIds.Contains(edge.FromNodeId)
            && coreNodeIds.Contains(edge.ToNodeId)
            && edge.FromNodeId != edge.ToNodeId
            && edge.Role == LayoutEdgeRole.Direct);

        return structuralEdgeCount >= 2;
    }

    private static ClusterLayout BuildInternalOnlyClusterLayout(
        LayoutCluster cluster,
        IReadOnlyList<LayoutNode> nodes,
        LayoutGraph graph,
        LayoutOptions options,
        float clusterTopPadding,
        float minimumClusterWidth)
    {
        Dictionary<string, int> ranks = AssignLocalNodeRanks(nodes, graph);
        NormalizeRanks(ranks);
        ApplyWeakAssociationRankSpread(nodes, graph, ranks);
        NormalizeRanks(ranks);

        List<StructuredRow> structuredRows = BuildStructuredRows(nodes, ranks, graph, options);

        Dictionary<string, Rect> nodeBounds = new Dictionary<string, Rect>();
        Dictionary<int, float> rowWidths = new Dictionary<int, float>();
        Dictionary<int, List<string>> rowNodeIds = new Dictionary<int, List<string>>();

        float currentY = clusterTopPadding;
        float maxContentWidth = 0f;

        int previousRank = int.MinValue;
        for (int rowIndex = 0; rowIndex < structuredRows.Count; rowIndex++)
        {
            StructuredRow structuredRow = structuredRows[rowIndex];
            List<LayoutNode> row = structuredRow.Nodes;
            if (rowIndex > 0)
            {
                currentY += structuredRow.Rank == previousRank
                    ? options.StructuredWrappedRowGap
                    : options.StructuredRankGap;
            }

            float currentX = options.GroupLeftPadding;
            float rowHeight = 0f;
            rowNodeIds[rowIndex] = new List<string>();

            foreach (LayoutNode node in row)
            {
                Rect rect = new Rect(currentX, currentY, node.Width, node.Height);
                nodeBounds[node.Id] = rect;
                rowNodeIds[rowIndex].Add(node.Id);
                currentX += node.Width + options.StructuredNodeColumnSpacing;
                rowHeight = Mathf.Max(rowHeight, node.Height);
            }

            float rowWidth = row.Count > 0
                ? currentX - options.StructuredNodeColumnSpacing - options.GroupLeftPadding
                : 0f;

            rowWidths[rowIndex] = rowWidth;
            maxContentWidth = Mathf.Max(maxContentWidth, rowWidth);
            currentY += rowHeight;
            previousRank = structuredRow.Rank;
        }

        float clusterWidth = Mathf.Max(
            minimumClusterWidth,
            (options.GroupLeftPadding * 2f) + maxContentWidth);

        OffsetRowsWithinCluster(nodeBounds, rowNodeIds, rowWidths, clusterWidth, options);

        float clusterHeight = Mathf.Max(
            clusterTopPadding + options.GroupBottomPadding + 24f,
            currentY + options.GroupBottomPadding);

        return new ClusterLayout
        {
            Bounds = new Rect(0f, 0f, clusterWidth, clusterHeight),
            NodeBounds = nodeBounds
        };
    }

    private static List<StructuredRow> BuildStructuredRows(
        IReadOnlyList<LayoutNode> nodes,
        IReadOnlyDictionary<string, int> ranks,
        LayoutGraph graph,
        LayoutOptions options)
    {
        List<StructuredRow> structuredRows = nodes
            .GroupBy(node => ranks[node.Id])
            .OrderBy(group => group.Key)
            .Select(group => new StructuredRow
            {
                Rank = group.Key,
                Nodes = group
                    .OrderByDescending(node => GetNodeConnectivity(node.Id, ranks.Keys, graph))
                    .ThenBy(node => node.Label)
                    .ThenBy(node => node.Id)
                    .ToList()
            })
            .ToList();

        RefineStructuredRows(structuredRows, graph);

        List<StructuredRow> wrappedRows = new List<StructuredRow>();
        foreach (StructuredRow row in structuredRows)
        {
            foreach (List<LayoutNode> wrappedRow in WrapStructuredRow(row.Nodes, options))
            {
                wrappedRows.Add(new StructuredRow
                {
                    Rank = row.Rank,
                    Nodes = wrappedRow
                });
            }
        }

        RefineStructuredRows(wrappedRows, graph);
        return wrappedRows;
    }

    private static float GetClusterTopPadding(LayoutCluster cluster, LayoutOptions options)
    {
        float titleMargin = cluster?.TitleMetrics?.TotalMargin ?? 0f;
        return Mathf.Max(options.GroupTopPadding, titleMargin + 12f);
    }

    private static float GetMinimumClusterWidth(LayoutCluster cluster, LayoutOptions options)
    {
        float titleWidth = cluster?.TitleMetrics?.LabelWidth ?? 0f;
        return Mathf.Max(options.GroupWidth, titleWidth + options.ClusterTitleHorizontalPadding);
    }

    private static void RefineStructuredRows(IList<StructuredRow> structuredRows, LayoutGraph graph)
    {
        List<List<LayoutNode>> rowLists = structuredRows.Select(row => row.Nodes).ToList();
        crossingReductionService.RefineRows(rowLists, graph);
        for (int index = 0; index < structuredRows.Count; index++)
        {
            structuredRows[index].Nodes = rowLists[index];
        }
    }

    private static Dictionary<string, int> AssignLocalNodeRanks(IReadOnlyList<LayoutNode> nodes, LayoutGraph graph)
    {
        Dictionary<string, LayoutNode> nodeById = nodes.ToDictionary(node => node.Id);
        Dictionary<string, int> ranks = nodes.ToDictionary(node => node.Id, GetInitialLocalRank);
        HashSet<string> nodeIds = new HashSet<string>(nodes.Select(node => node.Id));

        for (int i = 0; i < nodes.Count * 2; i++)
        {
            bool changed = false;

            foreach (LayoutEdge edge in graph.Edges.OrderByDescending(candidate => GetEdgeWeight(candidate.Kind)))
            {
                if (!nodeIds.Contains(edge.FromNodeId) || !nodeIds.Contains(edge.ToNodeId) || edge.FromNodeId == edge.ToNodeId)
                {
                    continue;
                }

                int rankDelta = GetLocalRankDelta(nodeById[edge.FromNodeId], nodeById[edge.ToNodeId], edge);
                int proposedRank = ranks[edge.FromNodeId] + rankDelta;
                if (proposedRank > ranks[edge.ToNodeId])
                {
                    ranks[edge.ToNodeId] = proposedRank;
                    changed = true;
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return ranks;
    }

    private static void ApplyWeakAssociationRankSpread(
        IReadOnlyList<LayoutNode> nodes,
        LayoutGraph graph,
        IDictionary<string, int> ranks)
    {
        List<LayoutNode> realNodes = nodes
            .Where(node => node.Role == LayoutNodeRole.Real || node.Role == LayoutNodeRole.SelfLoopHelper)
            .ToList();

        if (realNodes.Count < 6 || ranks.Count == 0)
        {
            return;
        }

        int currentSpan = ranks.Values.Max() - ranks.Values.Min();
        if (currentSpan >= 2)
        {
            return;
        }

        HashSet<string> nodeIds = new HashSet<string>(realNodes.Select(node => node.Id));
        Dictionary<string, HashSet<string>> adjacency = realNodes.ToDictionary(
            node => node.Id,
            _ => new HashSet<string>());

        foreach (LayoutEdge edge in graph.Edges)
        {
            if (edge.Role != LayoutEdgeRole.Direct
                || edge.FromNodeId == edge.ToNodeId
                || !nodeIds.Contains(edge.FromNodeId)
                || !nodeIds.Contains(edge.ToNodeId))
            {
                continue;
            }

            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
            adjacency[edge.ToNodeId].Add(edge.FromNodeId);
        }

        LayoutNode seedNode = realNodes
            .OrderByDescending(node => adjacency[node.Id].Count)
            .ThenBy(node => node.Label)
            .ThenBy(node => node.Id)
            .FirstOrDefault();

        if (seedNode == null || adjacency[seedNode.Id].Count == 0)
        {
            return;
        }

        Dictionary<string, int> distances = new Dictionary<string, int>
        {
            [seedNode.Id] = 0
        };

        Queue<string> queue = new Queue<string>();
        queue.Enqueue(seedNode.Id);

        while (queue.Count > 0)
        {
            string currentId = queue.Dequeue();
            int nextDistance = distances[currentId] + 1;
            foreach (string neighborId in adjacency[currentId])
            {
                if (distances.ContainsKey(neighborId))
                {
                    continue;
                }

                distances[neighborId] = nextDistance;
                queue.Enqueue(neighborId);
            }
        }

        int maxDistance = distances.Values.Max();
        if (maxDistance <= 0)
        {
            return;
        }

        int targetRankSpan = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(realNodes.Count)), 2, 4) - 1;
        foreach (LayoutNode node in realNodes)
        {
            if (!distances.TryGetValue(node.Id, out int distance))
            {
                continue;
            }

            int scaledRank = Mathf.RoundToInt((distance / (float)maxDistance) * targetRankSpan);
            ranks[node.Id] = Mathf.Max(ranks[node.Id], scaledRank);
        }
    }

    private static void NormalizeRanks(IDictionary<string, int> ranks)
    {
        if (ranks.Count == 0)
        {
            return;
        }

        int minRank = ranks.Values.Min();
        if (minRank == 0)
        {
            return;
        }

        foreach (string nodeId in ranks.Keys.ToList())
        {
            ranks[nodeId] -= minRank;
        }
    }

    private static List<List<LayoutNode>> WrapStructuredRow(
        IReadOnlyList<LayoutNode> row,
        LayoutOptions options)
    {
        List<List<LayoutNode>> wrappedRows = new List<List<LayoutNode>>();
        List<LayoutNode> currentRow = new List<LayoutNode>();
        float currentWidth = 0f;

        foreach (LayoutNode node in row)
        {
            float nodeWidth = node.Width + (currentRow.Count > 0 ? options.StructuredNodeColumnSpacing : 0f);
            bool exceedsWidth = currentRow.Count > 0
                && currentWidth + nodeWidth > options.StructuredClusterMaxRowWidth;
            bool exceedsCount = currentRow.Count >= options.StructuredClusterMaxNodesPerRow;

            if (exceedsWidth || exceedsCount)
            {
                wrappedRows.Add(currentRow);
                currentRow = new List<LayoutNode>();
                currentWidth = 0f;
                nodeWidth = node.Width;
            }

            currentRow.Add(node);
            currentWidth += nodeWidth;
        }

        if (currentRow.Count > 0)
        {
            wrappedRows.Add(currentRow);
        }

        return wrappedRows;
    }

    private static int GetNodeConnectivity(
        string nodeId,
        IEnumerable<string> nodeIds,
        LayoutGraph graph)
    {
        HashSet<string> nodeIdSet = new HashSet<string>(nodeIds);
        int connectivity = 0;
        foreach (LayoutEdge edge in graph.Edges)
        {
            if (edge.Role != LayoutEdgeRole.Direct)
            {
                continue;
            }

            if (edge.FromNodeId == nodeId && nodeIdSet.Contains(edge.ToNodeId))
            {
                connectivity++;
            }
            else if (edge.ToNodeId == nodeId && nodeIdSet.Contains(edge.FromNodeId))
            {
                connectivity++;
            }
        }

        return connectivity;
    }

    private static void OffsetRowsWithinCluster(
        IDictionary<string, Rect> nodeBounds,
        IReadOnlyDictionary<int, List<string>> rowNodeIds,
        IReadOnlyDictionary<int, float> rowWidths,
        float clusterWidth,
        LayoutOptions options)
    {
        float contentWidth = Mathf.Max(0f, clusterWidth - (options.GroupLeftPadding * 2f));
        foreach (KeyValuePair<int, List<string>> row in rowNodeIds)
        {
            if (!rowWidths.TryGetValue(row.Key, out float rowWidth))
            {
                continue;
            }

            float slack = Mathf.Max(0f, contentWidth - rowWidth);
            float offsetX = Mathf.Min(
                options.StructuredRowMaxIndent,
                (slack * options.StructuredRowCenteringBias) + (row.Key * options.StructuredRowIndentStep));
            foreach (string nodeId in row.Value)
            {
                Rect rect = nodeBounds[nodeId];
                rect.x += offsetX;
                nodeBounds[nodeId] = rect;
            }
        }
    }

    private static void OffsetStructuredRows(
        IDictionary<string, Rect> nodeBounds,
        IReadOnlyDictionary<int, List<string>> rowNodeIds,
        IReadOnlyDictionary<int, float> rowWidths,
        float contentWidth,
        LayoutOptions options)
    {
        foreach (KeyValuePair<int, List<string>> row in rowNodeIds)
        {
            if (!rowWidths.TryGetValue(row.Key, out float rowWidth))
            {
                continue;
            }

            float slack = Mathf.Max(0f, contentWidth - rowWidth);
            float offsetX = Mathf.Min(
                options.StructuredRowMaxIndent,
                (slack * options.StructuredRowCenteringBias) + (row.Key * options.StructuredRowIndentStep));
            foreach (string nodeId in row.Value)
            {
                Rect rect = nodeBounds[nodeId];
                rect.x += offsetX;
                nodeBounds[nodeId] = rect;
            }
        }
    }

    private static List<List<LayoutNode>> DistributeNodesAcrossColumns(IReadOnlyList<LayoutNode> nodes, int columnCount)
    {
        List<List<LayoutNode>> columns = new List<List<LayoutNode>>();
        if (nodes.Count == 0)
        {
            return columns;
        }

        for (int i = 0; i < columnCount; i++)
        {
            columns.Add(new List<LayoutNode>());
        }

        float[] heights = new float[columnCount];
        foreach (LayoutNode node in nodes)
        {
            int targetColumn = 0;
            float minHeight = heights[0];
            for (int i = 1; i < columnCount; i++)
            {
                if (heights[i] < minHeight)
                {
                    minHeight = heights[i];
                    targetColumn = i;
                }
            }

            columns[targetColumn].Add(node);
            heights[targetColumn] += node.Height;
        }

        return columns;
    }

    private static int DetermineColumnCount(int nodeCount, int maxColumns)
    {
        if (nodeCount <= 0)
        {
            return 1;
        }

        if (nodeCount <= 4)
        {
            return 1;
        }

        if (nodeCount <= 10)
        {
            return Mathf.Min(2, maxColumns);
        }

        return Mathf.Min(3, maxColumns);
    }

    private static float GetEdgeWeight(TypeEdgeKind kind)
    {
        switch (kind)
        {
            case TypeEdgeKind.Inheritance:
                return 3f;
            case TypeEdgeKind.Implements:
                return 2.5f;
            default:
                return 1f;
        }
    }

    private static int GetInitialLocalRank(LayoutNode node)
    {
        switch (node.Role)
        {
            case LayoutNodeRole.ClusterInboundAnchor:
                return 0;
            case LayoutNodeRole.ClusterOutboundAnchor:
                return 2;
            case LayoutNodeRole.SelfLoopHelper:
                return 2;
            default:
                return 1;
        }
    }

    private static int GetLocalRankDelta(LayoutNode fromNode, LayoutNode toNode, LayoutEdge edge)
    {
        if (fromNode.Role == LayoutNodeRole.ClusterInboundAnchor && toNode.Role == LayoutNodeRole.Real)
        {
            return 1;
        }

        if (fromNode.Role == LayoutNodeRole.Real && toNode.Role == LayoutNodeRole.ClusterOutboundAnchor)
        {
            return 1;
        }

        if ((fromNode.Role == LayoutNodeRole.Real || fromNode.Role == LayoutNodeRole.SelfLoopHelper)
            && (toNode.Role == LayoutNodeRole.Real || toNode.Role == LayoutNodeRole.SelfLoopHelper)
            && (edge.Role == LayoutEdgeRole.SelfLoopSourceLink
                || edge.Role == LayoutEdgeRole.SelfLoopBridge
                || edge.Role == LayoutEdgeRole.SelfLoopTargetLink))
        {
            return 1;
        }

        return edge.Kind == TypeEdgeKind.Association ? 0 : 1;
    }

    private static void OffsetLayout(
        ComponentLayout componentLayout,
        Vector2 offset,
        IDictionary<string, Rect> nodeBounds,
        IDictionary<string, Rect> clusterBounds)
    {
        foreach (KeyValuePair<string, Rect> pair in componentLayout.ClusterBounds)
        {
            Rect rect = pair.Value;
            rect.position += offset;
            clusterBounds[pair.Key] = rect;
        }

        foreach (KeyValuePair<string, Rect> pair in componentLayout.NodeBounds)
        {
            Rect rect = pair.Value;
            rect.position += offset;
            nodeBounds[pair.Key] = rect;
        }
    }

    private sealed class ClusterMetric
    {
        public string Label { get; set; } = string.Empty;

        public int NodeCount { get; set; }

        public float InWeight { get; set; }

        public float OutWeight { get; set; }

        public HashSet<string> ConnectedClusterIds { get; } = new HashSet<string>();
    }

    private sealed class ClusterLayout
    {
        public Rect Bounds { get; set; }

        public Dictionary<string, Rect> NodeBounds { get; set; } = new Dictionary<string, Rect>();

        public Dictionary<string, Rect> NestedClusterBounds { get; set; } = new Dictionary<string, Rect>();
    }

    private sealed class ComponentLayout
    {
        public Dictionary<string, Rect> NodeBounds { get; set; } = new Dictionary<string, Rect>();

        public Dictionary<string, Rect> ClusterBounds { get; set; } = new Dictionary<string, Rect>();

        public Vector2 Size { get; set; }
    }

    private sealed class StructuredCoreLayout
    {
        public Dictionary<string, Rect> NodeBounds { get; set; } = new Dictionary<string, Rect>();

        public float Width { get; set; }

        public float Height { get; set; }
    }

    private sealed class StructuredRow
    {
        public int Rank { get; set; }

        public List<LayoutNode> Nodes { get; set; } = new List<LayoutNode>();
    }
}
