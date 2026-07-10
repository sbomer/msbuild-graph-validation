internal static class Normalization
{
    public static Dictionary<string, GraphNode> NodesById(GraphCapture capture) =>
        capture.Nodes.ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal);

    public static HashSet<NormalizedNode> ProjectNodes(IEnumerable<GraphNode> nodes)
    {
        HashSet<NormalizedNode> normalizedNodes = new(NormalizedNodeComparer.Instance);
        foreach (GraphNode node in nodes)
        {
            foreach (NormalizedNode normalizedNode in ToNormalizedNodes(node))
            {
                normalizedNodes.Add(normalizedNode);
            }
        }

        return normalizedNodes;
    }

    public static IReadOnlyList<NormalizedNode> ToNormalizedNodes(GraphNode node)
    {
        string projectPath = Path.GetFullPath(node.ProjectPath).Replace('\\', '/');
        IReadOnlyDictionary<string, string> globalProperties = NormalizedGlobalProperties(node);

        // Normalize target identity by target result rather than by requested-target batch.
        // Graph builds may schedule A;B together while dynamic builds request A and B
        // separately. Target results are what downstream MSBuild tasks consume, so split
        // multi-target requests into one normalized node per effective target.
        IReadOnlyList<string> targets = EffectiveRequestedTargets(node);
        if (targets.Count == 0)
        {
            return [new NormalizedNode(projectPath, "", globalProperties)];
        }

        return targets
            .Select(target => new NormalizedNode(projectPath, target, globalProperties))
            .ToArray();
    }

    public static HashSet<NormalizedEdge> ProjectEdges(
        IEnumerable<GraphEdge> edges,
        Dictionary<string, GraphNode> nodesById)
    {
        HashSet<NormalizedEdge> normalizedEdges = new(NormalizedEdgeComparer.Instance);
        foreach (GraphEdge edge in edges)
        {
            if (!nodesById.TryGetValue(edge.From, out GraphNode? from) ||
                !nodesById.TryGetValue(edge.To, out GraphNode? to))
            {
                continue;
            }

            foreach (NormalizedNode normalizedFrom in ToNormalizedNodes(from))
            {
                foreach (NormalizedNode normalizedTo in ToNormalizedNodes(to))
                {
                    normalizedEdges.Add(new NormalizedEdge(normalizedFrom, normalizedTo));
                }
            }
        }

        return normalizedEdges;
    }

    private static IReadOnlyList<string> EffectiveRequestedTargets(GraphNode node) =>
        node.RequestedTargets.Count > 0 ? node.RequestedTargets : node.DefaultTargets;

    private static IReadOnlyDictionary<string, string> NormalizedGlobalProperties(GraphNode node) =>
        node.GlobalProperties
            .Where(static pair => !pair.Key.Equals("IsGraphBuild", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}

internal sealed record NormalizedNode(
    string ProjectPath,
    string Target,
    IReadOnlyDictionary<string, string> GlobalProperties)
{
    public string DisplayName => ToIdentityString();

    public string ToIdentityString() =>
        $"{ProjectPath}|target={Target}|globals={string.Join(';', GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}"))}";
}

internal sealed record NormalizedEdge(NormalizedNode From, NormalizedNode To)
{
    public string DisplayName => $"{From.DisplayName} -> {To.DisplayName}";
}

internal sealed class NormalizedNodeComparer : IEqualityComparer<NormalizedNode>
{
    public static NormalizedNodeComparer Instance { get; } = new();

    public bool Equals(NormalizedNode? x, NormalizedNode? y) =>
        x is not null &&
        y is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(x.ProjectPath, y.ProjectPath) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Target, y.Target) &&
        DictionaryEquals(x.GlobalProperties, y.GlobalProperties);

    public int GetHashCode(NormalizedNode obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectPath),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Target),
            DictionaryHashCode(obj.GlobalProperties));

    private static bool DictionaryEquals(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach ((string key, string value) in left)
        {
            if (!right.TryGetValue(key, out string? rightValue) ||
                !StringComparer.OrdinalIgnoreCase.Equals(value, rightValue))
            {
                return false;
            }
        }

        return true;
    }

    private static int DictionaryHashCode(IReadOnlyDictionary<string, string> properties)
    {
        HashCode hashCode = new();
        foreach ((string key, string value) in properties.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            hashCode.Add(key, StringComparer.OrdinalIgnoreCase);
            hashCode.Add(value, StringComparer.OrdinalIgnoreCase);
        }

        return hashCode.ToHashCode();
    }
}

internal sealed class NormalizedEdgeComparer : IEqualityComparer<NormalizedEdge>
{
    public static NormalizedEdgeComparer Instance { get; } = new();

    public bool Equals(NormalizedEdge? x, NormalizedEdge? y) =>
        x is not null &&
        y is not null &&
        NormalizedNodeComparer.Instance.Equals(x.From, y.From) &&
        NormalizedNodeComparer.Instance.Equals(x.To, y.To);

    public int GetHashCode(NormalizedEdge obj) =>
        HashCode.Combine(
            NormalizedNodeComparer.Instance.GetHashCode(obj.From),
            NormalizedNodeComparer.Instance.GetHashCode(obj.To));
}
