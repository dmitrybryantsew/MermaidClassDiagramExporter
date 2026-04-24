using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class TypeNodeElement : VisualElement
{
    private readonly Label titleLabel;
    private readonly Label badgeLabel;
    private readonly VisualElement memberContainer;
    private TypeNodeData node;

    public TypeNodeElement()
    {
        style.position = Position.Absolute;
        style.width = 280f;
        style.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 0.96f);
        style.borderTopWidth = 1f;
        style.borderBottomWidth = 1f;
        style.borderLeftWidth = 1f;
        style.borderRightWidth = 1f;
        style.borderTopColor = new Color(0.33f, 0.37f, 0.46f, 1f);
        style.borderBottomColor = new Color(0.33f, 0.37f, 0.46f, 1f);
        style.borderLeftColor = new Color(0.33f, 0.37f, 0.46f, 1f);
        style.borderRightColor = new Color(0.33f, 0.37f, 0.46f, 1f);
        style.borderTopLeftRadius = 8f;
        style.borderTopRightRadius = 8f;
        style.borderBottomLeftRadius = 8f;
        style.borderBottomRightRadius = 8f;
        style.paddingLeft = 10f;
        style.paddingRight = 10f;
        style.paddingTop = 8f;
        style.paddingBottom = 8f;

        var header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.Center;

        titleLabel = new Label();
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.color = new Color(0.96f, 0.96f, 0.99f, 1f);
        titleLabel.style.flexShrink = 1f;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.fontSize = 12f;
        header.Add(titleLabel);

        badgeLabel = new Label();
        badgeLabel.style.fontSize = 10f;
        badgeLabel.style.paddingLeft = 6f;
        badgeLabel.style.paddingRight = 6f;
        badgeLabel.style.paddingTop = 2f;
        badgeLabel.style.paddingBottom = 2f;
        badgeLabel.style.borderTopLeftRadius = 999f;
        badgeLabel.style.borderTopRightRadius = 999f;
        badgeLabel.style.borderBottomLeftRadius = 999f;
        badgeLabel.style.borderBottomRightRadius = 999f;
        badgeLabel.style.backgroundColor = new Color(0.23f, 0.29f, 0.39f, 1f);
        badgeLabel.style.color = new Color(0.92f, 0.95f, 1f, 1f);
        badgeLabel.style.marginLeft = 8f;
        header.Add(badgeLabel);

        Add(header);

        memberContainer = new VisualElement();
        memberContainer.style.marginTop = 8f;
        memberContainer.style.borderTopWidth = 1f;
        memberContainer.style.borderTopColor = new Color(0.22f, 0.25f, 0.31f, 1f);
        memberContainer.style.paddingTop = 6f;
        Add(memberContainer);

        RegisterCallback<MouseDownEvent>(OnMouseDown);
    }

    public event Action<TypeNodeData> Clicked;

    public event Action<TypeNodeData> DoubleClicked;

    public TypeNodeData Node => node;

    public void Bind(TypeNodeData value)
    {
        node = value;
        titleLabel.text = value.DisplayName;
        badgeLabel.text = BuildBadgeText(value);
        badgeLabel.style.backgroundColor = BuildBadgeColor(value.Kind);

        memberContainer.Clear();
        foreach (TypeMemberData member in BuildVisibleMembers(value.Members))
        {
            memberContainer.Add(BuildMemberLabel(member));
        }

        style.height = EstimateHeight(value.Members.Count);
    }

    public void SetSelected(bool isSelected)
    {
        Color borderColor = isSelected
            ? new Color(0.96f, 0.71f, 0.22f, 1f)
            : new Color(0.33f, 0.37f, 0.46f, 1f);

        style.borderTopColor = borderColor;
        style.borderBottomColor = borderColor;
        style.borderLeftColor = borderColor;
        style.borderRightColor = borderColor;
        style.backgroundColor = isSelected
            ? new Color(0.16f, 0.18f, 0.23f, 0.98f)
            : new Color(0.12f, 0.13f, 0.16f, 0.96f);
    }

    public void SetSearchMatchState(bool hasSearchText, bool isMatch)
    {
        style.opacity = hasSearchText && !isMatch ? 0.24f : 1f;
    }

    public static float EstimateHeight(int memberCount)
    {
        int visibleMemberCount = Mathf.Min(memberCount, 6);
        float memberSectionHeight = visibleMemberCount <= 0 ? 0f : 28f + (visibleMemberCount * 16f);
        return 46f + memberSectionHeight;
    }

    private static IEnumerable<TypeMemberData> BuildVisibleMembers(IReadOnlyList<TypeMemberData> members)
    {
        const int maxVisibleMembers = 6;
        return members.Take(maxVisibleMembers);
    }

    private static Label BuildMemberLabel(TypeMemberData member)
    {
        var label = new Label(BuildMemberSummary(member));
        label.style.fontSize = 10f;
        label.style.color = new Color(0.78f, 0.81f, 0.88f, 1f);
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.marginBottom = 2f;
        return label;
    }

    private static string BuildMemberSummary(TypeMemberData member)
    {
        if (member.Kind == TypeMemberKind.Method)
        {
            string parameterSummary = string.Join(", ", member.Parameters.Select(parameter => parameter.TypeName));
            return member.Name + "(" + parameterSummary + ") : " + member.TypeName;
        }

        return member.Name + " : " + member.TypeName;
    }

    private static string BuildBadgeText(TypeNodeData value)
    {
        switch (value.Kind)
        {
            case TypeNodeKind.Interface:
                return "Interface";
            case TypeNodeKind.Enum:
                return "Enum";
            case TypeNodeKind.Struct:
                return "Struct";
            case TypeNodeKind.StaticClass:
                return "Static";
            case TypeNodeKind.AbstractClass:
                return "Abstract";
            default:
                return value.IsMonoBehaviour ? "Mono" : value.IsScriptableObject ? "SO" : "Class";
        }
    }

    private static Color BuildBadgeColor(TypeNodeKind kind)
    {
        switch (kind)
        {
            case TypeNodeKind.Interface:
                return new Color(0.20f, 0.45f, 0.54f, 1f);
            case TypeNodeKind.Enum:
                return new Color(0.52f, 0.33f, 0.18f, 1f);
            case TypeNodeKind.Struct:
                return new Color(0.36f, 0.31f, 0.56f, 1f);
            case TypeNodeKind.StaticClass:
                return new Color(0.46f, 0.24f, 0.24f, 1f);
            case TypeNodeKind.AbstractClass:
                return new Color(0.38f, 0.33f, 0.18f, 1f);
            default:
                return new Color(0.23f, 0.29f, 0.39f, 1f);
        }
    }

    private void OnMouseDown(MouseDownEvent evt)
    {
        if (evt.button != 0 || node == null)
        {
            return;
        }

        Clicked?.Invoke(node);
        if (evt.clickCount == 2)
        {
            DoubleClicked?.Invoke(node);
        }

        evt.StopPropagation();
    }
}
