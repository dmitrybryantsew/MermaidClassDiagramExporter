using System.Collections.Generic;
using System.Linq;

internal static class NamespaceClusterBuilder
{
    public static List<LayoutCluster> Build(TypeGraph graph)
    {
        if (graph.Groups.Count > 0)
        {
            return graph.Groups
                .Select(group => new LayoutCluster
                {
                    Id = group.Id,
                    Label = group.Label,
                    Kind = group.Kind,
                    NodeIds = group.NodeIds
                })
                .ToList();
        }

        return new List<LayoutCluster>
        {
            new LayoutCluster
            {
                Id = "fallback",
                Label = "Ungrouped",
                Kind = TypeGroupKind.Namespace,
                NodeIds = graph.Nodes.Select(node => node.Id).ToArray()
            }
        };
    }
}
