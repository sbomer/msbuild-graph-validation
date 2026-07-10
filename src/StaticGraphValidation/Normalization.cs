internal static class Normalization
{
    public static Dictionary<string, GraphNode> NodesById(GraphCapture capture) =>
        capture.Nodes.ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal);

    public static HashSet<NormalizedNode> ProjectNodes(IEnumerable<GraphNode> nodes)
    {
        HashSet<NormalizedNode> normalizedNodes = new(NormalizedNodeComparer.Instance);
        foreach (GraphNode node in nodes)
        {
            normalizedNodes.Add(ToNormalizedNode(node));
        }

        return normalizedNodes;
    }

    public static NormalizedNode ToNormalizedNode(GraphNode node) =>
        new(
            Path.GetFullPath(node.ProjectPath).Replace('\\', '/'),
            EffectiveRequestedTargets(node),
            NormalizedGlobalProperties(node));

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

            normalizedEdges.Add(new NormalizedEdge(ToNormalizedNode(from), ToNormalizedNode(to)));
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
    IReadOnlyList<string> EffectiveTargets,
    IReadOnlyDictionary<string, string> GlobalProperties)
{
    public string DisplayName => ToIdentityString();

    public string ToIdentityString() =>
        $"{ProjectPath}|targets={string.Join(';', EffectiveTargets)}|globals={string.Join(';', GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}"))}";
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
        x.EffectiveTargets.SequenceEqual(y.EffectiveTargets, StringComparer.OrdinalIgnoreCase) &&
        DictionaryEquals(x.GlobalProperties, y.GlobalProperties);

    public int GetHashCode(NormalizedNode obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectPath),
            SequenceHashCode(obj.EffectiveTargets),
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

    private static int SequenceHashCode(IEnumerable<string> values)
    {
        HashCode hashCode = new();
        foreach (string value in values)
        {
            hashCode.Add(value, StringComparer.OrdinalIgnoreCase);
        }

        return hashCode.ToHashCode();
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
