using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CrossingReductionService
{
    private const int DefaultSweepCount = 4;

    public void RefineRows(IList<List<LayoutNode>> rows, LayoutGraph graph, int sweepCount = DefaultSweepCount)
    {
        if (rows == null || graph == null || rows.Count <= 1)
        {
            return;
        }

        HashSet<string> includedNodeIds = new HashSet<string>(rows.SelectMany(row => row.Select(node => node.Id)));
        Dictionary<string, HashSet<string>> adjacency = BuildAdjacency(graph, includedNodeIds);
        if (adjacency.Values.All(neighbors => neighbors.Count == 0))
        {
            return;
        }

        for (int sweep = 0; sweep < sweepCount; sweep++)
        {
            for (int rowIndex = 1; rowIndex < rows.Count; rowIndex++)
            {
                rows[rowIndex] = ReorderRow(rows[rowIndex], rows[rowIndex - 1], adjacency);
            }

            for (int rowIndex = rows.Count - 2; rowIndex >= 0; rowIndex--)
            {
                rows[rowIndex] = ReorderRow(rows[rowIndex], rows[rowIndex + 1], adjacency);
            }

            ApplyTransposePass(rows, adjacency);
        }
    }

    private static Dictionary<string, HashSet<string>> BuildAdjacency(
        LayoutGraph graph,
        IReadOnlyCollection<string> includedNodeIds)
    {
        Dictionary<string, HashSet<string>> adjacency = includedNodeIds.ToDictionary(
            nodeId => nodeId,
            _ => new HashSet<string>());

        foreach (LayoutEdge edge in graph.Edges)
        {
            if (!IsOrderingEdge(edge)
                || edge.FromNodeId == edge.ToNodeId
                || !adjacency.ContainsKey(edge.FromNodeId)
                || !adjacency.ContainsKey(edge.ToNodeId))
            {
                continue;
            }

            adjacency[edge.FromNodeId].Add(edge.ToNodeId);
            adjacency[edge.ToNodeId].Add(edge.FromNodeId);
        }

        return adjacency;
    }

    private static bool IsOrderingEdge(LayoutEdge edge)
    {
        switch (edge.Role)
        {
            case LayoutEdgeRole.Direct:
            case LayoutEdgeRole.SelfLoopSourceLink:
            case LayoutEdgeRole.SelfLoopBridge:
            case LayoutEdgeRole.SelfLoopTargetLink:
                return true;
            default:
                return false;
        }
    }

    private static List<LayoutNode> ReorderRow(
        IReadOnlyList<LayoutNode> row,
        IReadOnlyList<LayoutNode> adjacentRow,
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        if (row.Count <= 1 || adjacentRow.Count == 0)
        {
            return row.ToList();
        }

        Dictionary<string, int> adjacentOrder = BuildNodeOrderIndex(adjacentRow);
        return row
            .Select((node, index) => BuildMetric(node, index, adjacentOrder, adjacency))
            .OrderBy(metric => metric.NeighborCount == 0 ? 1 : 0)
            .ThenBy(metric => metric.Median)
            .ThenBy(metric => metric.Barycenter)
            .ThenByDescending(metric => metric.NeighborCount)
            .ThenBy(metric => metric.ExistingIndex)
            .ThenBy(metric => metric.Node.Label)
            .ThenBy(metric => metric.Node.Id)
            .Select(metric => metric.Node)
            .ToList();
    }

    private static NodeOrderMetric BuildMetric(
        LayoutNode node,
        int existingIndex,
        IReadOnlyDictionary<string, int> adjacentOrder,
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        List<int> neighborOrders = adjacency.TryGetValue(node.Id, out HashSet<string> neighbors)
            ? neighbors
                .Where(adjacentOrder.ContainsKey)
                .Select(neighborId => adjacentOrder[neighborId])
                .OrderBy(value => value)
                .ToList()
            : new List<int>();

        return new NodeOrderMetric
        {
            Node = node,
            ExistingIndex = existingIndex,
            NeighborCount = neighborOrders.Count,
            Median = neighborOrders.Count > 0 ? ComputeMedian(neighborOrders) : existingIndex,
            Barycenter = neighborOrders.Count > 0 ? neighborOrders.Average() : existingIndex
        };
    }

    private static double ComputeMedian(IReadOnlyList<int> sortedValues)
    {
        if (sortedValues.Count == 0)
        {
            return 0d;
        }

        int mid = sortedValues.Count / 2;
        if ((sortedValues.Count % 2) == 1)
        {
            return sortedValues[mid];
        }

        return (sortedValues[mid - 1] + sortedValues[mid]) * 0.5d;
    }

    private static Dictionary<string, int> BuildNodeOrderIndex(IReadOnlyList<LayoutNode> row)
    {
        Dictionary<string, int> order = new Dictionary<string, int>();
        for (int index = 0; index < row.Count; index++)
        {
            order[row[index].Id] = index;
        }

        return order;
    }

    private static void ApplyTransposePass(
        IList<List<LayoutNode>> rows,
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            List<LayoutNode> row = rows[rowIndex];
            if (row.Count <= 1)
            {
                continue;
            }

            IReadOnlyList<LayoutNode> previousRow = rowIndex > 0 ? rows[rowIndex - 1] : Array.Empty<LayoutNode>();
            IReadOnlyList<LayoutNode> nextRow = rowIndex < rows.Count - 1 ? rows[rowIndex + 1] : Array.Empty<LayoutNode>();

            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < row.Count - 1; i++)
                {
                    LayoutNode first = row[i];
                    LayoutNode second = row[i + 1];

                    int currentCost = CountPairCrossings(first.Id, second.Id, previousRow, adjacency)
                        + CountPairCrossings(first.Id, second.Id, nextRow, adjacency);

                    int swappedCost = CountPairCrossings(second.Id, first.Id, previousRow, adjacency)
                        + CountPairCrossings(second.Id, first.Id, nextRow, adjacency);

                    if (swappedCost >= currentCost)
                    {
                        continue;
                    }

                    row[i] = second;
                    row[i + 1] = first;
                    changed = true;
                }
            }
            while (changed);
        }
    }

    private static int CountPairCrossings(
        string firstNodeId,
        string secondNodeId,
        IReadOnlyList<LayoutNode> adjacentRow,
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        if (adjacentRow.Count == 0)
        {
            return 0;
        }

        Dictionary<string, int> adjacentOrder = BuildNodeOrderIndex(adjacentRow);
        List<int> firstOrders = GetAdjacentOrders(firstNodeId, adjacentOrder, adjacency);
        List<int> secondOrders = GetAdjacentOrders(secondNodeId, adjacentOrder, adjacency);
        if (firstOrders.Count == 0 || secondOrders.Count == 0)
        {
            return 0;
        }

        int crossings = 0;
        foreach (int firstOrder in firstOrders)
        {
            foreach (int secondOrder in secondOrders)
            {
                if (firstOrder > secondOrder)
                {
                    crossings++;
                }
            }
        }

        return crossings;
    }

    private static List<int> GetAdjacentOrders(
        string nodeId,
        IReadOnlyDictionary<string, int> adjacentOrder,
        IReadOnlyDictionary<string, HashSet<string>> adjacency)
    {
        if (!adjacency.TryGetValue(nodeId, out HashSet<string> neighbors))
        {
            return new List<int>();
        }

        return neighbors
            .Where(adjacentOrder.ContainsKey)
            .Select(neighborId => adjacentOrder[neighborId])
            .OrderBy(value => value)
            .ToList();
    }

    private sealed class NodeOrderMetric
    {
        public LayoutNode Node { get; set; }

        public int ExistingIndex { get; set; }

        public int NeighborCount { get; set; }

        public double Median { get; set; }

        public double Barycenter { get; set; }
    }
}
