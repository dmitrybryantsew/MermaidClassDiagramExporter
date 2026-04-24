using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

internal static class TypeGraphBuilder
{
    public static TypeGraph BuildGraph(
        IReadOnlyList<Type> types,
        string title,
        GraphSourceKind sourceKind,
        string sourceDescription,
        GraphBuildOptions options = null)
    {
        GraphBuildOptions resolvedOptions = options ?? new GraphBuildOptions();
        List<Type> orderedTypes = ProjectTypeUtility.OrderTypes(types ?? Array.Empty<Type>());
        List<TypeNodeData> nodes = new List<TypeNodeData>(orderedTypes.Count);
        Dictionary<Type, TypeNodeData> nodeByType = new Dictionary<Type, TypeNodeData>();

        foreach (Type type in orderedTypes)
        {
            TypeNodeData node = BuildNode(type, resolvedOptions);
            nodes.Add(node);
            nodeByType[type] = node;
        }

        List<TypeGroupData> groups = BuildGroups(orderedTypes, nodeByType, resolvedOptions);
        List<TypeEdgeData> edges = BuildEdges(orderedTypes, nodeByType, resolvedOptions);

        TypeGraphMetadata metadata = new TypeGraphMetadata
        {
            GeneratedAtUtc = DateTime.UtcNow,
            SourceDescription = sourceDescription ?? string.Empty,
            SourceKind = sourceKind,
            Options = resolvedOptions,
            IsDerivedView = false,
            ParentGraphTitle = string.Empty,
            FocusSummary = string.Empty,
            SeedNodeIds = Array.Empty<string>(),
            FocusDepth = 0
        };

        return new TypeGraph(title, nodes, edges, groups, metadata);
    }

    private static TypeNodeData BuildNode(Type type, GraphBuildOptions options)
    {
        List<TypeMemberData> members = BuildMembers(type, options);

        return new TypeNodeData
        {
            Id = TypeNameUtility.BuildTypeId(type),
            DisplayName = TypeNameUtility.BuildTypeDisplayName(type),
            FullName = type.FullName ?? type.Name,
            Namespace = type.Namespace ?? string.Empty,
            AssemblyName = type.Assembly.GetName().Name ?? string.Empty,
            AssetPath = TypeAssetPathResolver.GetAssetPath(type),
            Kind = TypeNameUtility.BuildNodeKind(type),
            IsProjectType = ProjectTypeUtility.ShouldIncludeType(type),
            IsMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(type),
            IsScriptableObject = typeof(ScriptableObject).IsAssignableFrom(type),
            Members = members
        };
    }

    private static List<TypeMemberData> BuildMembers(Type type, GraphBuildOptions options)
    {
        BindingFlags memberFlags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic;

        if (options.IncludeDeclaredMembersOnly)
        {
            memberFlags |= BindingFlags.DeclaredOnly;
        }

        List<TypeMemberData> members = new List<TypeMemberData>();

        if (options.IncludeFields)
        {
            IEnumerable<FieldInfo> fields = type
                .GetFields(memberFlags)
                .Where(field => !field.IsSpecialName && TypeNameUtility.ShouldListMember(field))
                .OrderBy(field => field.Name);

            foreach (FieldInfo field in fields)
            {
                members.Add(new TypeMemberData
                {
                    Name = field.Name,
                    TypeName = TypeNameUtility.BuildTypeName(field.FieldType, true),
                    Kind = TypeMemberKind.Field,
                    Visibility = field.IsPublic ? TypeVisibility.Public : TypeVisibility.Private,
                    IsStatic = field.IsStatic
                });
            }
        }

        if (options.IncludeProperties)
        {
            IEnumerable<PropertyInfo> properties = type
                .GetProperties(memberFlags)
                .Where(property => !property.IsSpecialName && TypeNameUtility.ShouldListMember(property))
                .OrderBy(property => property.Name);

            foreach (PropertyInfo property in properties)
            {
                MethodInfo accessor = property.GetMethod ?? property.SetMethod;
                members.Add(new TypeMemberData
                {
                    Name = property.Name,
                    TypeName = TypeNameUtility.BuildTypeName(property.PropertyType, true),
                    Kind = TypeMemberKind.Property,
                    Visibility = accessor != null ? TypeNameUtility.BuildVisibility(accessor) : TypeVisibility.Public,
                    IsStatic = accessor != null && accessor.IsStatic
                });
            }
        }

        if (options.IncludeMethods)
        {
            IEnumerable<MethodInfo> methods = type
                .GetMethods(memberFlags)
                .Where(method => !method.IsSpecialName && TypeNameUtility.ShouldListMember(method))
                .OrderBy(method => method.Name);

            foreach (MethodInfo method in methods)
            {
                members.Add(new TypeMemberData
                {
                    Name = method.Name,
                    TypeName = TypeNameUtility.BuildTypeName(method.ReturnType, true),
                    Kind = TypeMemberKind.Method,
                    Visibility = TypeNameUtility.BuildVisibility(method),
                    IsStatic = method.IsStatic,
                    IsAbstract = method.IsAbstract,
                    Parameters = method
                        .GetParameters()
                        .Select(parameter => new TypeMemberParameterData
                        {
                            Name = parameter.Name ?? "arg",
                            TypeName = TypeNameUtility.BuildTypeName(parameter.ParameterType, true)
                        })
                        .ToArray()
                });
            }
        }

        if (options.MaxMemberCountPerNode > 0 && members.Count > options.MaxMemberCountPerNode)
        {
            members = members.Take(options.MaxMemberCountPerNode).ToList();
        }

        return members;
    }

    private static List<TypeGroupData> BuildGroups(
        IEnumerable<Type> orderedTypes,
        IReadOnlyDictionary<Type, TypeNodeData> nodeByType,
        GraphBuildOptions options)
    {
        TypeGroupKind groupKind = options.PrimaryGroupKind;
        if (groupKind == TypeGroupKind.Folder)
        {
            groupKind = TypeGroupKind.Namespace;
        }

        IEnumerable<IGrouping<string, Type>> groupedTypes = groupKind == TypeGroupKind.Assembly
            ? orderedTypes.GroupBy(type => type.Assembly.GetName().Name ?? string.Empty)
            : orderedTypes.GroupBy(type => type.Namespace ?? string.Empty);

        return groupedTypes
            .Select(group => new TypeGroupData
            {
                Id = groupKind + ":" + (string.IsNullOrEmpty(group.Key) ? "global" : group.Key),
                Label = string.IsNullOrEmpty(group.Key)
                    ? groupKind == TypeGroupKind.Assembly ? "Unnamed Assembly" : "Global Namespace"
                    : group.Key,
                Kind = groupKind,
                NodeIds = group.Select(type => nodeByType[type].Id).ToArray()
            })
            .OrderBy(group => group.Label)
            .ToList();
    }

    private static List<TypeEdgeData> BuildEdges(
        IEnumerable<Type> orderedTypes,
        IReadOnlyDictionary<Type, TypeNodeData> nodeByType,
        GraphBuildOptions options)
    {
        HashSet<string> edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        List<TypeEdgeData> edges = new List<TypeEdgeData>();

        foreach (Type type in orderedTypes)
        {
            TypeNodeData currentNode = nodeByType[type];

            if (type.BaseType != null && nodeByType.ContainsKey(type.BaseType))
            {
                TryAddEdge(
                    edges,
                    edgeKeys,
                    nodeByType[type.BaseType].Id,
                    currentNode.Id,
                    TypeEdgeKind.Inheritance,
                    string.Empty,
                    true);
            }

            if (options.IncludeInterfaces)
            {
                foreach (Type implementedInterface in type.GetInterfaces().Where(nodeByType.ContainsKey))
                {
                    TryAddEdge(
                        edges,
                        edgeKeys,
                        nodeByType[implementedInterface].Id,
                        currentNode.Id,
                        TypeEdgeKind.Implements,
                        "implements",
                        true);
                }
            }

            if (options.IncludeAssociations)
            {
                foreach (Type relatedType in GetAssociatedTypes(type).Where(nodeByType.ContainsKey))
                {
                    if (relatedType == type)
                    {
                        continue;
                    }

                    TryAddEdge(
                        edges,
                        edgeKeys,
                        currentNode.Id,
                        nodeByType[relatedType].Id,
                        TypeEdgeKind.Association,
                        string.Empty,
                        false);
                }
            }
        }

        return edges
            .OrderBy(edge => edge.FromNodeId)
            .ThenBy(edge => edge.ToNodeId)
            .ThenBy(edge => edge.Kind)
            .ToList();
    }

    private static void TryAddEdge(
        ICollection<TypeEdgeData> edges,
        ISet<string> edgeKeys,
        string fromNodeId,
        string toNodeId,
        TypeEdgeKind kind,
        string label,
        bool isStrongRelation)
    {
        string key = fromNodeId + "|" + toNodeId + "|" + kind + "|" + label;
        if (!edgeKeys.Add(key))
        {
            return;
        }

        edges.Add(new TypeEdgeData
        {
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            Kind = kind,
            Label = label ?? string.Empty,
            IsStrongRelation = isStrongRelation
        });
    }

    private static IEnumerable<Type> GetAssociatedTypes(Type type)
    {
        const BindingFlags relationshipFlags =
            BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.DeclaredOnly;

        IEnumerable<Type> fieldTypes = type
            .GetFields(relationshipFlags)
            .Where(field => !field.IsSpecialName)
            .SelectMany(field => ExpandRelatedTypes(field.FieldType));

        IEnumerable<Type> propertyTypes = type
            .GetProperties(relationshipFlags)
            .Where(property => !property.IsSpecialName)
            .SelectMany(property => ExpandRelatedTypes(property.PropertyType));

        return fieldTypes
            .Concat(propertyTypes)
            .Where(relatedType => relatedType != null && !relatedType.IsGenericParameter)
            .Distinct();
    }

    private static IEnumerable<Type> ExpandRelatedTypes(Type type)
    {
        if (type == null)
        {
            yield break;
        }

        Type nullableType = Nullable.GetUnderlyingType(type);
        if (nullableType != null)
        {
            foreach (Type relatedType in ExpandRelatedTypes(nullableType))
            {
                yield return relatedType;
            }

            yield break;
        }

        if (type.IsArray || type.IsByRef || type.IsPointer)
        {
            foreach (Type relatedType in ExpandRelatedTypes(type.GetElementType()))
            {
                yield return relatedType;
            }

            yield break;
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                foreach (Type relatedType in ExpandRelatedTypes(argument))
                {
                    yield return relatedType;
                }
            }
        }

        yield return type;
    }
}
