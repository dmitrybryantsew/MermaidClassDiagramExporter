using System.Linq;

internal sealed class GraphLayoutCoordinator
{
    private readonly IGraphLayoutEngine layeredLayoutEngine = new LayeredLayoutEngine();
    private readonly IGraphLayoutEngine simpleColumnLayoutEngine = new SimpleColumnLayoutEngine();
    private readonly EdgeRoutingService edgeRoutingService = new EdgeRoutingService();
    private readonly PostLayoutPipeline postLayoutPipeline = new PostLayoutPipeline()
        .AddPass(new ClusterTitleMarginPass())
        .AddPass(new ClusterBoundsPolishPass())
        .AddPass(new ClusterOverlapResolutionPass());
    private readonly LayoutPipeline pipeline = new LayoutPipeline()
        .AddPass(new MeasurementPreparationPass(new LayoutMeasurementService()))
        .AddPass(new ClusterHierarchyPass())
        .AddPass(new ExternalConnectionAnalysisPass())
        .AddPass(new RepresentativeAnchorSelectionPass())
        .AddPass(new SelfLoopExpansionPass())
        .AddPass(new InternalClusterExtractionPass())
        .AddPass(new SubgraphDirectionSelectionPass())
        .AddPass(new RecursiveSpacingPass())
        .AddPass(new BoundaryEdgeNormalizationPass());

    public LayoutResult CreateLayout(TypeGraph graph, LayoutOptions options = null)
    {
        if (graph == null)
        {
            return new LayoutResult();
        }

        LayoutOptions resolvedOptions = options ?? new LayoutOptions();
        LayoutGraph layoutGraph = LayoutGraphFactory.Create(graph, resolvedOptions);
        LayoutGraph preparedGraph = pipeline.Run(layoutGraph, resolvedOptions);
        LayoutResult layoutResult;
        if (preparedGraph.Nodes.Count == 0)
        {
            layoutResult = simpleColumnLayoutEngine.Run(preparedGraph, resolvedOptions);
        }
        else
        {
            layoutResult = layeredLayoutEngine.Run(preparedGraph, resolvedOptions);
        }

        layoutResult = postLayoutPipeline.Run(preparedGraph, layoutResult, resolvedOptions);
        layoutResult.NodeClusterIds = preparedGraph.Nodes.ToDictionary(node => node.Id, node => node.ClusterId);
        layoutResult.ClusterVisuals = preparedGraph.Clusters.ToDictionary(
            cluster => cluster.Id,
            cluster => new LayoutClusterVisual
            {
                Id = cluster.Id,
                Label = cluster.Label,
                TitleMetrics = LayoutCloneUtility.CloneTitleMetrics(cluster.TitleMetrics)
            });
        layoutResult.EdgePaths = edgeRoutingService.BuildPaths(graph, layoutResult, resolvedOptions);
        return layoutResult;
    }
}
