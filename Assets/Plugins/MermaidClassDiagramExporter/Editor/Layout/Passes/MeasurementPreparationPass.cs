using System.Collections.Generic;
using System.Linq;

internal sealed class MeasurementPreparationPass : ILayoutPass
{
    private readonly LayoutMeasurementService measurementService;

    public MeasurementPreparationPass(LayoutMeasurementService measurementService)
    {
        this.measurementService = measurementService ?? new LayoutMeasurementService();
    }

    public string Name => "Measurement Preparation";

    public LayoutGraph Run(LayoutGraph graph, LayoutOptions options)
    {
        if (graph == null)
        {
            return new LayoutGraph();
        }

        List<LayoutNode> nodes = graph.Nodes
            .Select(LayoutCloneUtility.CloneNode)
            .ToList();

        foreach (LayoutNode node in nodes)
        {
            if (node.Role == LayoutNodeRole.Real)
            {
                UnityEngine.Vector2 measured = measurementService.MeasureNode(node, options);
                node.MeasuredWidth = measured.x;
                node.MeasuredHeight = measured.y;
                node.Width = measured.x;
                node.Height = measured.y;
                node.IsMeasured = true;
            }
        }

        List<LayoutCluster> clusters = graph.Clusters
            .Select(LayoutCloneUtility.CloneCluster)
            .ToList();

        foreach (LayoutCluster cluster in clusters)
        {
            cluster.TitleMetrics = measurementService.MeasureClusterTitle(cluster, options);
        }

        LayoutGraph measuredGraph = new LayoutGraph
        {
            Title = graph.Title,
            Nodes = nodes,
            Edges = graph.Edges.Select(LayoutCloneUtility.CloneEdge).ToList(),
            Clusters = clusters,
            ExtractedSubgraphs = graph.ExtractedSubgraphs.Select(CloneMeasuredSubgraph).ToList(),
            Metadata = LayoutCloneUtility.CloneMetadata(graph.Metadata)
        };

        measuredGraph.Metadata.UsesMeasuredNodes = true;
        return measuredGraph;
    }

    private LayoutSubgraph CloneMeasuredSubgraph(LayoutSubgraph subgraph)
    {
        LayoutSubgraph clone = LayoutCloneUtility.CloneSubgraph(subgraph);
        clone.Graph = Run(clone.Graph, CreateSubgraphOptions(subgraph));
        return clone;
    }

    private static LayoutOptions CreateSubgraphOptions(LayoutSubgraph subgraph)
    {
        return new LayoutOptions
        {
            Direction = subgraph.Direction,
            NodeSpacing = subgraph.Spacing != null && subgraph.Spacing.NodeSeparation > 0f
                ? subgraph.Spacing.NodeSeparation
                : new LayoutOptions().NodeSpacing,
            RankSpacing = subgraph.Spacing != null && subgraph.Spacing.RankSeparation > 0f
                ? subgraph.Spacing.RankSeparation
                : new LayoutOptions().RankSpacing,
            OuterMarginX = subgraph.Spacing?.MarginX ?? 0f,
            OuterMarginY = subgraph.Spacing?.MarginY ?? 0f
        };
    }
}
