internal static class Normalization
{
    private static readonly StringComparer s_comparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> s_queryTargets = new(s_comparer)
    {
        "GetTargetFrameworks",
        "GetTargetFrameworksWithPlatformForSingleTargetFramework"
    };

    public static Dictionary<string, GraphNode> NodesById(GraphCapture capture) =>
        capture.Nodes.ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal);

    public static HashSet<NormalizedNode> ProjectNodes(IEnumerable<GraphNode> nodes, ComparisonProjection projection)
    {
        HashSet<NormalizedNode> normalizedNodes = new(NormalizedNodeComparer.Instance);
        foreach (GraphNode node in nodes)
        {
            normalizedNodes.Add(Project(ToCanonicalNode(node), projection));
        }

        return normalizedNodes;
    }

    public static CanonicalNode ToCanonicalNode(GraphNode node)
    {
        string projectPath = Path.GetFullPath(node.ProjectPath).Replace('\\', '/');
        string role = IsQueryOnlyNode(node) ? $"Query:{string.Join(";", node.RequestedTargets)}" : "Build";
        return new CanonicalNode(
            projectPath,
            role,
            EffectiveRequestedTargets(node),
            NormalizedGlobalProperties(node));
    }

    public static NormalizedNode Project(CanonicalNode node, ComparisonProjection projection)
    {
        string role = projection == ComparisonProjection.FullPresence ? node.Role.Split(':')[0] : node.Role;
        return new NormalizedNode(node.ProjectPath, node.EffectiveTargets, node.GlobalProperties, role);
    }

    public static HashSet<NormalizedEdge> ProjectEdges(
        IEnumerable<GraphEdge> edges,
        Dictionary<string, GraphNode> nodesById,
        ComparisonProjection projection,
        EdgeClass edgeClass)
    {
        HashSet<NormalizedEdge> normalizedEdges = new(NormalizedEdgeComparer.Instance);
        foreach (GraphEdge edge in edges)
        {
            if (!nodesById.TryGetValue(edge.From, out GraphNode? from) ||
                !nodesById.TryGetValue(edge.To, out GraphNode? to))
            {
                continue;
            }

            bool isQueryEdge = IsQueryOnlyNode(to);
            if (edgeClass == EdgeClass.Query && !isQueryEdge)
            {
                continue;
            }

            if (edgeClass == EdgeClass.Build && isQueryEdge)
            {
                continue;
            }

            NormalizedNode normalizedFrom = Project(ToCanonicalNode(from), projection);
            NormalizedNode normalizedTo = Project(ToCanonicalNode(to), projection);
            string role = isQueryEdge ? "Query" : "Build";
            normalizedEdges.Add(new NormalizedEdge(normalizedFrom, normalizedTo, role));
        }

        return normalizedEdges;
    }

    public static bool IsQueryOnlyNode(GraphNode node)
    {
        if (node.RequestedTargets.Count == 0)
        {
            return false;
        }

        return node.RequestedTargets.All(s_queryTargets.Contains);
    }

    private static IReadOnlyList<string> EffectiveRequestedTargets(GraphNode node) =>
        node.RequestedTargets.Count > 0 ? node.RequestedTargets : node.DefaultTargets;

    private static IReadOnlyDictionary<string, string> NormalizedGlobalProperties(GraphNode node) =>
        node.GlobalProperties
            .Where(static pair => !pair.Key.Equals("IsGraphBuild", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);
}

internal enum ComparisonProjection
{
    FullPresence,
    QueryStrict,
    BuildPresence,
    BuildStrict
}

internal enum EdgeClass
{
    All,
    Query,
    Build
}

internal sealed record CanonicalNode(
    string ProjectPath,
    string Role,
    IReadOnlyList<string> EffectiveTargets,
    IReadOnlyDictionary<string, string> GlobalProperties);

internal sealed record CanonicalEdge(CanonicalNode From, CanonicalNode To, string Role);

internal sealed record NormalizedNode(
    string ProjectPath,
    IReadOnlyList<string> EffectiveTargets,
    IReadOnlyDictionary<string, string> GlobalProperties,
    string Role)
{
    public string DisplayName => ToIdentityString();

    public string ToIdentityString() =>
        $"{ProjectPath}|targets={string.Join(';', EffectiveTargets)}|globals={string.Join(';', GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}"))}";
}

internal sealed record NormalizedEdge(NormalizedNode From, NormalizedNode To, string Role)
{
    public string DisplayName => $"{From.DisplayName} -> {To.DisplayName} [{Role}]";
}

internal sealed class NormalizedNodeComparer : IEqualityComparer<NormalizedNode>
{
    public static NormalizedNodeComparer Instance { get; } = new();

    public bool Equals(NormalizedNode? x, NormalizedNode? y) =>
        x is not null &&
        y is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(x.ProjectPath, y.ProjectPath) &&
        x.EffectiveTargets.SequenceEqual(y.EffectiveTargets, StringComparer.OrdinalIgnoreCase) &&
        DictionaryEquals(x.GlobalProperties, y.GlobalProperties) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Role, y.Role);

    public int GetHashCode(NormalizedNode obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectPath),
            SequenceHashCode(obj.EffectiveTargets),
            DictionaryHashCode(obj.GlobalProperties),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Role));

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
        NormalizedNodeComparer.Instance.Equals(x.To, y.To) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Role, y.Role);

    public int GetHashCode(NormalizedEdge obj) =>
        HashCode.Combine(
            NormalizedNodeComparer.Instance.GetHashCode(obj.From),
            NormalizedNodeComparer.Instance.GetHashCode(obj.To),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Role));
}
