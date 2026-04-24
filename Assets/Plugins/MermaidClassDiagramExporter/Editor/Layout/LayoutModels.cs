using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
internal sealed class LayoutGraph
{
    public string Title { get; set; } = string.Empty;

    public IReadOnlyList<LayoutNode> Nodes { get; set; } = Array.Empty<LayoutNode>();

    public IReadOnlyList<LayoutEdge> Edges { get; set; } = Array.Empty<LayoutEdge>();

    public IReadOnlyList<LayoutCluster> Clusters { get; set; } = Array.Empty<LayoutCluster>();

    public IReadOnlyList<LayoutSubgraph> ExtractedSubgraphs { get; set; } = Array.Empty<LayoutSubgraph>();

    public LayoutGraphMetadata Metadata { get; set; } = new LayoutGraphMetadata();
}

[Serializable]
internal sealed class LayoutNode
{
    public string Id { get; set; } = string.Empty;

    public string ClusterId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public LayoutNodeRole Role { get; set; } = LayoutNodeRole.Real;

    public string SourceNodeId { get; set; } = string.Empty;

    public string BadgeText { get; set; } = string.Empty;

    public IReadOnlyList<string> MemberLines { get; set; } = Array.Empty<string>();

    public float EstimatedWidth { get; set; }

    public float EstimatedHeight { get; set; }

    public float MeasuredWidth { get; set; }

    public float MeasuredHeight { get; set; }

    public bool IsMeasured { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }
}

[Serializable]
internal sealed class LayoutEdge
{
    public string Id { get; set; } = string.Empty;

    public string OriginalEdgeId { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public TypeEdgeKind Kind { get; set; } = TypeEdgeKind.Association;

    public LayoutEdgeRole Role { get; set; } = LayoutEdgeRole.Direct;
}

[Serializable]
internal sealed class LayoutCluster
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public TypeGroupKind Kind { get; set; } = TypeGroupKind.Namespace;

    public string ParentClusterId { get; set; } = string.Empty;

    public IReadOnlyList<string> NodeIds { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> ChildClusterIds { get; set; } = Array.Empty<string>();

    public bool HasExternalConnections { get; set; }

    public string RepresentativeNodeId { get; set; } = string.Empty;

    public bool IsExtractedSubgraph { get; set; }

    public ClusterTitleMetrics TitleMetrics { get; set; } = new ClusterTitleMetrics();
}

[Serializable]
internal sealed class LayoutSubgraph
{
    public string ClusterId { get; set; } = string.Empty;

    public LayoutGraph Graph { get; set; } = new LayoutGraph();

    public LayoutDirection Direction { get; set; } = LayoutDirection.LeftToRight;

    public LayoutSpacingProfile Spacing { get; set; } = new LayoutSpacingProfile();
}

[Serializable]
internal sealed class LayoutResult
{
    public IReadOnlyDictionary<string, Rect> NodeBounds { get; set; } = new Dictionary<string, Rect>();

    public IReadOnlyDictionary<string, Rect> ClusterBounds { get; set; } = new Dictionary<string, Rect>();

    public IReadOnlyDictionary<string, string> NodeClusterIds { get; set; } = new Dictionary<string, string>();

    public IReadOnlyDictionary<string, LayoutClusterVisual> ClusterVisuals { get; set; } = new Dictionary<string, LayoutClusterVisual>();

    public IReadOnlyList<LayoutEdgePath> EdgePaths { get; set; } = Array.Empty<LayoutEdgePath>();

    public Vector2 ContentSize { get; set; }
}

[Serializable]
internal sealed class LayoutEdgePath
{
    public string EdgeId { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public TypeEdgeKind Kind { get; set; } = TypeEdgeKind.Association;

    public bool IsClippedToClusters { get; set; }

    public IReadOnlyList<Vector2> Points { get; set; } = Array.Empty<Vector2>();
}

[Serializable]
internal sealed class ClusterTitleMetrics
{
    public float LabelWidth { get; set; }

    public float LabelHeight { get; set; }

    public float TopMargin { get; set; }

    public float BottomMargin { get; set; }

    public float TotalMargin => TopMargin + LabelHeight + BottomMargin;
}

[Serializable]
internal sealed class LayoutSpacingProfile
{
    public float NodeSeparation { get; set; }

    public float RankSeparation { get; set; }

    public float MarginX { get; set; }

    public float MarginY { get; set; }
}

[Serializable]
internal sealed class LayoutGraphMetadata
{
    public string SourceDescription { get; set; } = string.Empty;

    public LayoutDirection Direction { get; set; } = LayoutDirection.LeftToRight;

    public bool UsesMeasuredNodes { get; set; }

    public LayoutSpacingProfile Spacing { get; set; } = new LayoutSpacingProfile();
}

[Serializable]
internal sealed class LayoutClusterVisual
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public ClusterTitleMetrics TitleMetrics { get; set; } = new ClusterTitleMetrics();
}

internal enum LayoutNodeRole
{
    Real,
    ClusterInboundAnchor,
    ClusterOutboundAnchor,
    SelfLoopHelper
}

internal enum LayoutEdgeRole
{
    Direct,
    BoundarySourceLink,
    BoundaryBridge,
    BoundaryTargetLink,
    SelfLoopSourceLink,
    SelfLoopBridge,
    SelfLoopTargetLink
}
