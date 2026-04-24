using System.Collections.Generic;
using System.Linq;

internal static class ComponentSplitter
{
    public static List<List<LayoutCluster>> SplitClusters(LayoutGraph graph)
    {
        Dictionary<string, LayoutCluster> clustersById = graph.Clusters.ToDictionary(cluster => cluster.Id);
        Dictionary<string, HashSet<string>> adjacency = graph.Clusters.ToDictionary(cluster => cluster.Id, _ => new HashSet<string>());
        Dictionary<string, string> clusterByNodeId = graph.Nodes.ToDictionary(node => node.Id, node => node.ClusterId);

        foreach (LayoutEdge edge in graph.Edges)
        {
            if (!clusterByNodeId.TryGetValue(edge.FromNodeId, out string fromClusterId)
                || !clusterByNodeId.TryGetValue(edge.ToNodeId, out string toClusterId))
            {
                continue;
            }

            if (fromClusterId == toClusterId)
            {
                continue;
            }

            adjacency[fromClusterId].Add(toClusterId);
            adjacency[toClusterId].Add(fromClusterId);
        }

        HashSet<string> visited = new HashSet<string>();
        List<List<LayoutCluster>> components = new List<List<LayoutCluster>>();

        foreach (LayoutCluster cluster in graph.Clusters)
        {
            if (!visited.Add(cluster.Id))
            {
                continue;
            }

            Queue<string> queue = new Queue<string>();
            List<LayoutCluster> component = new List<LayoutCluster>();
            queue.Enqueue(cluster.Id);

            while (queue.Count > 0)
            {
                string currentId = queue.Dequeue();
                component.Add(clustersById[currentId]);

                foreach (string neighborId in adjacency[currentId])
                {
                    if (visited.Add(neighborId))
                    {
                        queue.Enqueue(neighborId);
                    }
                }
            }

            components.Add(component);
        }

        return components
            .OrderByDescending(component => component.Sum(cluster => cluster.NodeIds.Count))
            .ThenBy(component => component.First().Label)
            .ToList();
    }
}
