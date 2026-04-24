using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class TypeGraphInspectorView : VisualElement
{
    private readonly Label titleLabel;
    private readonly Label subtitleLabel;
    private readonly Label pathLabel;
    private readonly Label summaryLabel;
    private readonly VisualElement membersContainer;
    private readonly VisualElement outgoingContainer;
    private readonly VisualElement incomingContainer;
    private readonly Button pingButton;
    private readonly Button openButton;
    private TypeGraph graph;
    private TypeNodeData currentNode;

    public TypeGraphInspectorView()
    {
        style.minWidth = 280f;
        style.backgroundColor = new Color(0.10f, 0.11f, 0.14f, 1f);
        style.borderLeftWidth = 1f;
        style.borderLeftColor = new Color(0.18f, 0.20f, 0.25f, 1f);

        var scrollView = new ScrollView();
        scrollView.style.flexGrow = 1f;
        Add(scrollView);

        var content = new VisualElement();
        content.style.paddingLeft = 14f;
        content.style.paddingRight = 14f;
        content.style.paddingTop = 14f;
        content.style.paddingBottom = 18f;
        scrollView.Add(content);

        titleLabel = new Label("No Selection");
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.fontSize = 14f;
        titleLabel.style.color = new Color(0.96f, 0.96f, 0.99f, 1f);
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        content.Add(titleLabel);

        subtitleLabel = new Label();
        subtitleLabel.style.color = new Color(0.72f, 0.76f, 0.84f, 1f);
        subtitleLabel.style.marginTop = 4f;
        subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        content.Add(subtitleLabel);

        pathLabel = new Label();
        pathLabel.style.color = new Color(0.62f, 0.66f, 0.74f, 1f);
        pathLabel.style.fontSize = 10f;
        pathLabel.style.marginTop = 8f;
        pathLabel.style.whiteSpace = WhiteSpace.Normal;
        content.Add(pathLabel);

        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.marginTop = 10f;
        buttonRow.style.marginBottom = 10f;
        content.Add(buttonRow);

        pingButton = new Button(PingCurrentNode) { text = "Ping Script" };
        pingButton.style.marginRight = 8f;
        buttonRow.Add(pingButton);

        openButton = new Button(OpenCurrentNode) { text = "Open Script" };
        buttonRow.Add(openButton);

        summaryLabel = BuildSectionLabel(content, "Graph Summary");
        membersContainer = BuildSectionContainer(content, "Members");
        outgoingContainer = BuildSectionContainer(content, "Outgoing Relations");
        incomingContainer = BuildSectionContainer(content, "Incoming Relations");

        SetGraph(null);
        ShowNode(null);
    }

    public void SetGraph(TypeGraph value)
    {
        graph = value;
        currentNode = null;
        RefreshGraphSummary();
    }

    public void ShowNode(TypeNodeData node)
    {
        currentNode = node;

        if (node == null)
        {
            titleLabel.text = "No Selection";
            subtitleLabel.text = "Choose a node in the graph to inspect its members and relations.";
            pathLabel.text = string.Empty;
            pingButton.SetEnabled(false);
            openButton.SetEnabled(false);
            FillContainer(membersContainer, new[] { "No node selected." });
            FillContainer(outgoingContainer, new[] { "No node selected." });
            FillContainer(incomingContainer, new[] { "No node selected." });
            RefreshGraphSummary();
            return;
        }

        titleLabel.text = node.DisplayName;
        subtitleLabel.text = BuildNodeSubtitle(node);
        pathLabel.text = string.IsNullOrEmpty(node.AssetPath) ? "Script path not resolved." : node.AssetPath;
        pingButton.SetEnabled(!string.IsNullOrEmpty(node.AssetPath));
        openButton.SetEnabled(!string.IsNullOrEmpty(node.AssetPath));

        FillContainer(
            membersContainer,
            node.Members.Count > 0
                ? node.Members.Select(BuildMemberLine)
                : new[] { "No visible members in the current graph options." });

        FillContainer(outgoingContainer, BuildRelationLines(node.Id, outgoing: true));
        FillContainer(incomingContainer, BuildRelationLines(node.Id, outgoing: false));
        RefreshGraphSummary();
    }

    private static Label BuildSectionLabel(VisualElement content, string label)
    {
        var sectionLabel = new Label(label);
        sectionLabel.style.marginTop = 12f;
        sectionLabel.style.marginBottom = 6f;
        sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        sectionLabel.style.color = new Color(0.92f, 0.93f, 0.97f, 1f);
        content.Add(sectionLabel);
        return sectionLabel;
    }

    private static VisualElement BuildSectionContainer(VisualElement content, string label)
    {
        BuildSectionLabel(content, label);

        var container = new VisualElement();
        container.style.paddingBottom = 4f;
        content.Add(container);
        return container;
    }

    private void RefreshGraphSummary()
    {
        if (graph == null || graph.Nodes == null || graph.Edges == null || graph.Groups == null)
        {
            summaryLabel.text = "Graph Summary\nNo graph loaded yet.";
            return;
        }

        string text =
            "Graph Summary\n"
            + graph.Title
            + "\nNodes: " + graph.Nodes.Count
            + "  Edges: " + graph.Edges.Count
            + "  Groups: " + graph.Groups.Count;

        if (graph.Metadata != null && graph.Metadata.IsDerivedView)
        {
            text += "\nDerived from: " + graph.Metadata.ParentGraphTitle;

            if (!string.IsNullOrEmpty(graph.Metadata.FocusSummary))
            {
                text += "\n" + graph.Metadata.FocusSummary;
            }
        }

        summaryLabel.text = text;
    }

    private IEnumerable<string> BuildRelationLines(string nodeId, bool outgoing)
    {
        if (graph == null || graph.Edges == null || graph.Nodes == null)
        {
            return new[] { "No graph loaded." };
        }

        IEnumerable<TypeEdgeData> relations = outgoing
            ? graph.Edges.Where(edge => edge.FromNodeId == nodeId)
            : graph.Edges.Where(edge => edge.ToNodeId == nodeId);

        List<string> lines = relations
            .Select(edge => BuildRelationLine(edge, outgoing))
            .ToList();

        return lines.Count > 0 ? lines : new[] { "None." };
    }

    private string BuildRelationLine(TypeEdgeData edge, bool outgoing)
    {
        string relatedNodeId = outgoing ? edge.ToNodeId : edge.FromNodeId;
        TypeNodeData relatedNode = graph != null && graph.Nodes != null
            ? graph.Nodes.FirstOrDefault(node => node.Id == relatedNodeId)
            : null;
        string relatedLabel = relatedNode != null ? relatedNode.DisplayName : relatedNodeId;
        return edge.Kind + " -> " + relatedLabel;
    }

    private static string BuildNodeSubtitle(TypeNodeData node)
    {
        return node.Kind + " in " + (string.IsNullOrEmpty(node.Namespace) ? "Global Namespace" : node.Namespace);
    }

    private static string BuildMemberLine(TypeMemberData member)
    {
        if (member.Kind == TypeMemberKind.Method)
        {
            string parameters = string.Join(", ", member.Parameters.Select(parameter => parameter.TypeName + " " + parameter.Name));
            return member.Visibility + " " + member.Name + "(" + parameters + ") : " + member.TypeName;
        }

        return member.Visibility + " " + member.Name + " : " + member.TypeName;
    }

    private static void FillContainer(VisualElement container, IEnumerable<string> lines)
    {
        container.Clear();
        foreach (string line in lines)
        {
            var label = new Label(line);
            label.style.fontSize = 11f;
            label.style.color = new Color(0.77f, 0.80f, 0.87f, 1f);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 3f;
            container.Add(label);
        }
    }

    private void PingCurrentNode()
    {
        if (currentNode == null || string.IsNullOrEmpty(currentNode.AssetPath))
        {
            return;
        }

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(currentNode.AssetPath);
        if (script != null)
        {
            EditorGUIUtility.PingObject(script);
        }
    }

    private void OpenCurrentNode()
    {
        if (currentNode == null || string.IsNullOrEmpty(currentNode.AssetPath))
        {
            return;
        }

        MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(currentNode.AssetPath);
        if (script != null)
        {
            AssetDatabase.OpenAsset(script);
        }
    }
}
