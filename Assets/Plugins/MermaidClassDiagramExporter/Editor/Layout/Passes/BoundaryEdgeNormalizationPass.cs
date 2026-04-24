internal sealed class BoundaryEdgeNormalizationPass : ILayoutPass
{
    public string Name => "Boundary Edge Normalization";

    public LayoutGraph Run(LayoutGraph graph, LayoutOptions options)
    {
        return ClusterBoundaryEdgeNormalizer.Normalize(graph, options);
    }
}
