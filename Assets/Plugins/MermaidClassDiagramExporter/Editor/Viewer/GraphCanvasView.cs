using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class GraphCanvasView : VisualElement
{
    private const float MinZoom = 0.18f;
    private const float MaxZoom = 2.25f;
    private const float ZoomStep = 0.10f;

    private readonly VisualElement viewport;
    private readonly VisualElement groupLayer;
    private readonly VisualElement edgeLayer;
    private readonly VisualElement nodeLayer;
    private readonly Label emptyStateLabel;
    private readonly Dictionary<string, Rect> nodeRects = new Dictionary<string, Rect>();
    private readonly Dictionary<string, TypeNodeElement> nodeElements = new Dictionary<string, TypeNodeElement>();
    private readonly Dictionary<string, VisualElement> groupElements = new Dictionary<string, VisualElement>();
    private TypeGraph graph;
    private LayoutResult layoutResult;
    private string selectedNodeId = string.Empty;
    private string searchText = string.Empty;
    private bool showInheritanceEdges = true;
    private bool showImplementsEdges = true;
    private bool showAssociationEdges = true;
    private Vector2 pan = new Vector2(40f, 40f);
    private float zoom = 1f;
    private bool isPanning;
    private Vector2 lastPointerPosition;
    private string pendingFocusNodeId = string.Empty;
    private bool pendingCenterGraph;

    public GraphCanvasView()
    {
        style.flexGrow = 1f;
        style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        style.position = Position.Relative;
        style.overflow = Overflow.Hidden;

        viewport = new VisualElement();
        viewport.style.position = Position.Absolute;
        viewport.style.left = 0f;
        viewport.style.top = 0f;
        viewport.style.width = 6400f;
        viewport.style.height = 6400f;

        groupLayer = new VisualElement();
        groupLayer.style.position = Position.Absolute;
        groupLayer.style.left = 0f;
        groupLayer.style.top = 0f;
        groupLayer.style.width = 6400f;
        groupLayer.style.height = 6400f;

        edgeLayer = new VisualElement();
        edgeLayer.style.position = Position.Absolute;
        edgeLayer.style.left = 0f;
        edgeLayer.style.top = 0f;
        edgeLayer.style.width = 6400f;
        edgeLayer.style.height = 6400f;
        edgeLayer.pickingMode = PickingMode.Ignore;
        edgeLayer.generateVisualContent += OnGenerateEdgeVisuals;

        nodeLayer = new VisualElement();
        nodeLayer.style.position = Position.Absolute;
        nodeLayer.style.left = 0f;
        nodeLayer.style.top = 0f;
        nodeLayer.style.width = 6400f;
        nodeLayer.style.height = 6400f;

        viewport.Add(groupLayer);
        viewport.Add(edgeLayer);
        viewport.Add(nodeLayer);
        Add(viewport);

        emptyStateLabel = new Label("Build a graph from the toolbar to start exploring.");
        emptyStateLabel.style.position = Position.Absolute;
        emptyStateLabel.style.left = 16f;
        emptyStateLabel.style.top = 16f;
        emptyStateLabel.style.color = new Color(0.75f, 0.78f, 0.84f, 1f);
        Add(emptyStateLabel);

        RegisterCallback<WheelEvent>(OnWheel);
        RegisterCallback<MouseDownEvent>(OnMouseDown);
        RegisterCallback<MouseMoveEvent>(OnMouseMove);
        RegisterCallback<MouseUpEvent>(OnMouseUp);
        RegisterCallback<MouseLeaveEvent>(OnMouseLeave);
        RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    public event Action<TypeNodeData> NodeSelected;

    public event Action<TypeNodeData> NodeDoubleClicked;

    public void SetGraph(TypeGraph value, LayoutResult layout)
    {
        graph = value;
        layoutResult = layout;
        selectedNodeId = string.Empty;
        pendingFocusNodeId = string.Empty;
        pendingCenterGraph = false;
        nodeRects.Clear();
        nodeElements.Clear();
        groupElements.Clear();
        groupLayer.Clear();
        nodeLayer.Clear();
        emptyStateLabel.style.display = value == null || value.Nodes.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

        if (value == null || value.Nodes.Count == 0 || layout == null)
        {
            edgeLayer.MarkDirtyRepaint();
            return;
        }

        ApplyLayout(value, layout);
        ApplySearchState();
        ApplyTransform();
        edgeLayer.MarkDirtyRepaint();
    }

    public void SetSearchText(string value)
    {
        searchText = value ?? string.Empty;
        ApplySearchState();
    }

    public void SetEdgeVisibility(bool showInheritance, bool showImplements, bool showAssociations)
    {
        showInheritanceEdges = showInheritance;
        showImplementsEdges = showImplements;
        showAssociationEdges = showAssociations;
        edgeLayer.MarkDirtyRepaint();
    }

    public void ZoomIn()
    {
        ZoomAroundViewportCenter(ZoomStep);
    }

    public void ZoomOut()
    {
        ZoomAroundViewportCenter(-ZoomStep);
    }

    public void FocusNode(string nodeId)
    {
        pendingCenterGraph = false;
        pendingFocusNodeId = nodeId ?? string.Empty;
        TryFocusPendingNode();
    }

    public void CenterGraph()
    {
        pendingFocusNodeId = string.Empty;
        pendingCenterGraph = true;
        TryCenterGraph();
    }

    private bool TryFocusPendingNode()
    {
        if (string.IsNullOrEmpty(pendingFocusNodeId) || !nodeRects.TryGetValue(pendingFocusNodeId, out Rect rect))
        {
            return false;
        }

        CenterPointInViewport(rect.center);
        pendingFocusNodeId = string.Empty;
        return true;
    }

    private bool TryCenterGraph()
    {
        if (!pendingCenterGraph || !TryGetGraphBounds(out Rect graphBounds))
        {
            return false;
        }

        CenterPointInViewport(graphBounds.center);
        pendingCenterGraph = false;
        return true;
    }

    public void SelectNode(string nodeId)
    {
        selectedNodeId = nodeId ?? string.Empty;

        foreach (KeyValuePair<string, TypeNodeElement> pair in nodeElements)
        {
            pair.Value.SetSelected(pair.Key == selectedNodeId);
        }

        if (graph == null)
        {
            return;
        }

        TypeNodeData node = graph.Nodes.FirstOrDefault(candidate => candidate.Id == selectedNodeId);
        NodeSelected?.Invoke(node);
        edgeLayer.MarkDirtyRepaint();
    }

    private void ApplyLayout(TypeGraph value, LayoutResult layout)
    {
        foreach (KeyValuePair<string, LayoutClusterVisual> pair in layout.ClusterVisuals)
        {
            if (layout.ClusterBounds.TryGetValue(pair.Key, out Rect groupRect))
            {
                VisualElement groupElement = BuildGroupElement(pair.Value, groupRect);
                groupLayer.Add(groupElement);
                groupElements[pair.Key] = groupElement;
            }
        }

        foreach (TypeNodeData node in value.Nodes)
        {
            if (!layout.NodeBounds.TryGetValue(node.Id, out Rect rect))
            {
                continue;
            }

            nodeRects[node.Id] = rect;

            var nodeElement = new TypeNodeElement();
            nodeElement.Bind(node);
            nodeElement.style.left = rect.x;
            nodeElement.style.top = rect.y;
            nodeElement.style.width = rect.width;
            nodeElement.style.height = rect.height;
            nodeElement.Clicked += OnNodeClicked;
            nodeElement.DoubleClicked += OnNodeDoubleClicked;
            nodeLayer.Add(nodeElement);
            nodeElements[node.Id] = nodeElement;
        }

        float viewportWidth = layout.ContentSize.x;
        float viewportHeight = layout.ContentSize.y;

        viewport.style.width = viewportWidth;
        viewport.style.height = viewportHeight;
        groupLayer.style.width = viewportWidth;
        groupLayer.style.height = viewportHeight;
        edgeLayer.style.width = viewportWidth;
        edgeLayer.style.height = viewportHeight;
        nodeLayer.style.width = viewportWidth;
        nodeLayer.style.height = viewportHeight;
    }

    private VisualElement BuildGroupElement(LayoutClusterVisual group, Rect rect)
    {
        var container = new VisualElement();
        container.style.position = Position.Absolute;
        container.style.left = rect.x;
        container.style.top = rect.y;
        container.style.width = rect.width;
        container.style.height = rect.height;
        container.style.backgroundColor = new Color(0.95f, 0.90f, 0.63f, 0.08f);
        container.style.borderTopWidth = 1f;
        container.style.borderBottomWidth = 1f;
        container.style.borderLeftWidth = 1f;
        container.style.borderRightWidth = 1f;
        container.style.borderTopColor = new Color(0.92f, 0.83f, 0.35f, 0.25f);
        container.style.borderBottomColor = new Color(0.92f, 0.83f, 0.35f, 0.25f);
        container.style.borderLeftColor = new Color(0.92f, 0.83f, 0.35f, 0.25f);
        container.style.borderRightColor = new Color(0.92f, 0.83f, 0.35f, 0.25f);
        container.style.borderTopLeftRadius = 10f;
        container.style.borderTopRightRadius = 10f;
        container.style.borderBottomLeftRadius = 10f;
        container.style.borderBottomRightRadius = 10f;

        var label = new Label(group.Label);
        label.style.position = Position.Absolute;
        label.style.left = 12f;
        label.style.top = group.TitleMetrics != null ? group.TitleMetrics.TopMargin : 8f;
        label.style.color = new Color(0.97f, 0.92f, 0.72f, 0.95f);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 11f;
        container.Add(label);

        return container;
    }

    private void ApplySearchState()
    {
        bool hasSearchText = !string.IsNullOrWhiteSpace(searchText);
        foreach (TypeNodeElement nodeElement in nodeElements.Values)
        {
            bool isMatch = !hasSearchText
                || nodeElement.Node.DisplayName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                || nodeElement.Node.FullName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;

            nodeElement.SetSearchMatchState(hasSearchText, isMatch);
        }
    }

    private void ApplyTransform()
    {
        viewport.transform.position = new Vector3(pan.x, pan.y, 0f);
        viewport.transform.scale = new Vector3(zoom, zoom, 1f);
    }

    private void OnNodeClicked(TypeNodeData node)
    {
        SelectNode(node != null ? node.Id : string.Empty);
    }

    private void OnNodeDoubleClicked(TypeNodeData node)
    {
        if (node == null)
        {
            return;
        }

        SelectNode(node.Id);
        NodeDoubleClicked?.Invoke(node);
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 2)
        {
            return;
        }

        isPanning = true;
        lastPointerPosition = evt.localMousePosition;
        evt.StopPropagation();
    }

    private void OnMouseMove(MouseMoveEvent evt)
    {
        if (!isPanning)
        {
            return;
        }

        Vector2 delta = evt.localMousePosition - lastPointerPosition;
        lastPointerPosition = evt.localMousePosition;
        pan += delta;
        ApplyTransform();
        evt.StopPropagation();
    }

    private void OnMouseUp(MouseUpEvent evt)
    {
        if (!isPanning || evt.button != 2)
        {
            return;
        }

        isPanning = false;
        evt.StopPropagation();
    }

    private void OnMouseLeave(MouseLeaveEvent evt)
    {
        isPanning = false;
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        if (!TryFocusPendingNode())
        {
            TryCenterGraph();
        }
    }

    private void OnWheel(WheelEvent evt)
    {
        if (!TryApplyZoom(evt.delta.y > 0f ? -0.08f : 0.08f, evt.localMousePosition))
        {
            return;
        }

        evt.StopPropagation();
    }

    private void ZoomAroundViewportCenter(float zoomDelta)
    {
        Vector2 pivot = new Vector2(layout.width * 0.5f, layout.height * 0.5f);
        TryApplyZoom(zoomDelta, pivot);
    }

    private bool TryApplyZoom(float zoomDelta, Vector2 pivot)
    {
        float previousZoom = zoom;
        zoom = Mathf.Clamp(zoom + zoomDelta, MinZoom, MaxZoom);

        if (Mathf.Approximately(previousZoom, zoom))
        {
            return false;
        }

        Vector2 transformOrigin = GetViewportTransformOrigin();
        Vector2 contentPosition = transformOrigin + ((pivot - pan - transformOrigin) / previousZoom);
        pan = pivot - transformOrigin - ((contentPosition - transformOrigin) * zoom);
        ApplyTransform();
        edgeLayer.MarkDirtyRepaint();
        return true;
    }

    private Vector2 GetViewportTransformOrigin()
    {
        float width = viewport.resolvedStyle.width;
        float height = viewport.resolvedStyle.height;

        if (width <= 0f)
        {
            width = 6400f;
        }

        if (height <= 0f)
        {
            height = 6400f;
        }

        return new Vector2(width * 0.5f, height * 0.5f);
    }

    private void CenterPointInViewport(Vector2 targetCenter)
    {
        if (layout.width <= 0f || layout.height <= 0f)
        {
            return;
        }

        Vector2 viewportCenter = new Vector2(layout.width * 0.5f, layout.height * 0.5f);
        Vector2 transformOrigin = GetViewportTransformOrigin();
        pan = viewportCenter - transformOrigin - ((targetCenter - transformOrigin) * zoom);
        ApplyTransform();
    }

    private bool TryGetGraphBounds(out Rect bounds)
    {
        bounds = default;
        bool hasBounds = false;

        foreach (Rect rect in nodeRects.Values)
        {
            bounds = hasBounds ? Encapsulate(bounds, rect) : rect;
            hasBounds = true;
        }

        if (layoutResult != null && layoutResult.ClusterBounds != null)
        {
            foreach (Rect rect in layoutResult.ClusterBounds.Values)
            {
                bounds = hasBounds ? Encapsulate(bounds, rect) : rect;
                hasBounds = true;
            }
        }

        if (!hasBounds && layoutResult != null && layoutResult.ContentSize != Vector2.zero)
        {
            bounds = new Rect(Vector2.zero, layoutResult.ContentSize);
            hasBounds = true;
        }

        return hasBounds;
    }

    private static Rect Encapsulate(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin);
        float yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax);
        float yMax = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void OnGenerateEdgeVisuals(MeshGenerationContext context)
    {
        if (graph == null || layoutResult == null)
        {
            return;
        }

        var painter = context.painter2D;
        painter.lineWidth = zoom < 0.7f ? 1f : 1.5f;

        if (layoutResult.EdgePaths != null && layoutResult.EdgePaths.Count > 0)
        {
            foreach (LayoutEdgePath path in layoutResult.EdgePaths)
            {
                if (path.Points == null || path.Points.Count < 2)
                {
                    continue;
                }

                if (!IsEdgeKindVisible(path.Kind))
                {
                    continue;
                }

                bool isHighlighted = selectedNodeId == path.FromNodeId || selectedNodeId == path.ToNodeId;
                painter.strokeColor = BuildEdgeColor(path.Kind, isHighlighted);
                DrawPath(painter, path.Points);
                DrawEdgeMarker(painter, path.Points, path.Kind, isHighlighted, zoom);
            }

            return;
        }

        foreach (TypeEdgeData edge in graph.Edges)
        {
            if (!IsEdgeKindVisible(edge.Kind))
            {
                continue;
            }

            if (!nodeRects.TryGetValue(edge.FromNodeId, out Rect fromRect)
                || !nodeRects.TryGetValue(edge.ToNodeId, out Rect toRect))
            {
                continue;
            }

            bool isHighlighted = selectedNodeId == edge.FromNodeId || selectedNodeId == edge.ToNodeId;
            painter.strokeColor = BuildEdgeColor(edge.Kind, isHighlighted);

            Vector2 from = new Vector2(fromRect.xMax, fromRect.center.y);
            Vector2 to = new Vector2(toRect.xMin, toRect.center.y);
            DrawPath(painter, new[]
            {
                from,
                from + new Vector2(Mathf.Clamp((to.x - from.x) * 0.5f, 32f, 120f), 0f),
                to - new Vector2(Mathf.Clamp((to.x - from.x) * 0.5f, 32f, 120f), 0f),
                to
            });
            DrawEdgeMarker(
                painter,
                new[]
                {
                    from,
                    from + new Vector2(Mathf.Clamp((to.x - from.x) * 0.5f, 32f, 120f), 0f),
                    to - new Vector2(Mathf.Clamp((to.x - from.x) * 0.5f, 32f, 120f), 0f),
                    to
                },
                edge.Kind,
                isHighlighted,
                zoom);
        }
    }

    private bool IsEdgeKindVisible(TypeEdgeKind kind)
    {
        switch (kind)
        {
            case TypeEdgeKind.Inheritance:
                return showInheritanceEdges;
            case TypeEdgeKind.Implements:
                return showImplementsEdges;
            default:
                return showAssociationEdges;
        }
    }

    private static void DrawPath(Painter2D painter, IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count < 2)
        {
            return;
        }

        painter.BeginPath();
        painter.MoveTo(points[0]);
        for (int index = 1; index < points.Count; index++)
        {
            painter.LineTo(points[index]);
        }

        painter.Stroke();
    }

    private static void DrawEdgeMarker(
        Painter2D painter,
        IReadOnlyList<Vector2> points,
        TypeEdgeKind kind,
        bool isHighlighted,
        float currentZoom)
    {
        if (kind == TypeEdgeKind.Association || points == null || points.Count < 2)
        {
            return;
        }

        Vector2 tip = points[points.Count - 1];
        Vector2 basePoint = tip;
        for (int index = points.Count - 2; index >= 0; index--)
        {
            if (Vector2.Distance(points[index], tip) > 0.5f)
            {
                basePoint = points[index];
                break;
            }
        }

        Vector2 direction = (tip - basePoint).normalized;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float markerLength = currentZoom < 0.55f ? 8f : 12f;
        float markerWidth = currentZoom < 0.55f ? 4.5f : 7f;

        Vector2 left = tip - (direction * markerLength) + (perpendicular * markerWidth);
        Vector2 right = tip - (direction * markerLength) - (perpendicular * markerWidth);

        if (kind == TypeEdgeKind.Inheritance || kind == TypeEdgeKind.Implements)
        {
            Color strokeColor = painter.strokeColor;
            painter.fillColor = new Color(0.08f, 0.09f, 0.11f, isHighlighted ? 0.98f : 1f);
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(left);
            painter.LineTo(right);
            painter.ClosePath();
            painter.Fill();

            painter.strokeColor = strokeColor;
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(left);
            painter.LineTo(right);
            painter.ClosePath();
            painter.Stroke();
        }
    }

    private static Color BuildEdgeColor(TypeEdgeKind kind, bool isHighlighted)
    {
        Color baseColor;
        switch (kind)
        {
            case TypeEdgeKind.Inheritance:
                baseColor = new Color(0.92f, 0.71f, 0.28f, 0.75f);
                break;
            case TypeEdgeKind.Implements:
                baseColor = new Color(0.45f, 0.79f, 0.90f, 0.72f);
                break;
            default:
                baseColor = new Color(0.78f, 0.80f, 0.84f, 0.26f);
                break;
        }

        if (!isHighlighted)
        {
            return baseColor;
        }

        return new Color(
            Mathf.Min(baseColor.r + 0.12f, 1f),
            Mathf.Min(baseColor.g + 0.12f, 1f),
            Mathf.Min(baseColor.b + 0.12f, 1f),
            0.96f);
    }
}
