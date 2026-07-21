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

    public static HashSet<EvaluatedProjectInstance> ProjectInstances(IEnumerable<GraphNode> nodes)
    {
        HashSet<EvaluatedProjectInstance> instances = new(EvaluatedProjectInstanceComparer.Instance);
        foreach (GraphNode node in nodes)
        {
            instances.Add(ToEvaluatedProjectInstance(node));
        }

        return instances;
    }

    private static EvaluatedProjectInstance ToEvaluatedProjectInstance(GraphNode node) =>
        new(
            Path.GetFullPath(node.ProjectPath).Replace('\\', '/'),
            NormalizedGlobalProperties(node));

    public static IReadOnlyList<NormalizedNode> ToNormalizedNodes(GraphNode node)
    {
        string projectPath = Path.GetFullPath(node.ProjectPath).Replace('\\', '/');
        IReadOnlyDictionary<string, string> globalProperties = NormalizedGlobalProperties(node);

        // Normalize target identity by target result rather than by requested-target batch.
        // Graph builds may schedule A;B together while dynamic builds request A and B
        // separately. Target results are what downstream MSBuild tasks consume, so split
        // multi-target requests into one normalized node per effective target.
        IReadOnlyList<string> effectiveTargets = EffectiveRequestedTargets(node);
        string[] targets = effectiveTargets
            .Where(static target => !IsIgnoredTarget(target))
            .ToArray();
        if (targets.Length == 0)
        {
            return effectiveTargets.Count == 0
                ? [new NormalizedNode(projectPath, "", globalProperties)]
                : [];
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

    private static bool IsIgnoredTarget(string target) =>
        // TODO: Keep this out of the main diff but report ignored-target
        // differences separately. Fully filtering it can hide a real mismatch
        // if this query target is the only differing target for a project
        // instance, even though replay-pruned TFMs make many such differences
        // expected.
        target.Equals("GetTargetFrameworksWithPlatformForSingleTargetFramework", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> NormalizedGlobalProperties(GraphNode node) =>
        node.GlobalProperties
            .Where(static pair => !IsIgnoredGlobalProperty(pair.Key))
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static bool IsIgnoredGlobalProperty(string name) =>
        name.Equals("IsGraphBuild", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("ProjectReferenceReplayTargets", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("UseProjectReferenceReplay", StringComparison.OrdinalIgnoreCase);
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

internal sealed record EvaluatedProjectInstance(
    string ProjectPath,
    IReadOnlyDictionary<string, string> GlobalProperties);

internal sealed class EvaluatedProjectInstanceComparer : IEqualityComparer<EvaluatedProjectInstance>
{
    public static EvaluatedProjectInstanceComparer Instance { get; } = new();

    public bool Equals(EvaluatedProjectInstance? x, EvaluatedProjectInstance? y) =>
        x is not null &&
        y is not null &&
        StringComparer.OrdinalIgnoreCase.Equals(x.ProjectPath, y.ProjectPath) &&
        DictionaryEquals(x.GlobalProperties, y.GlobalProperties);

    public int GetHashCode(EvaluatedProjectInstance obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectPath),
            DictionaryHashCode(obj.GlobalProperties));

    private static bool DictionaryEquals(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        NormalizedNodeComparer.DictionaryEquals(left, right);

    private static int DictionaryHashCode(IReadOnlyDictionary<string, string> properties) =>
        NormalizedNodeComparer.DictionaryHashCode(properties);
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

    internal static bool DictionaryEquals(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
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

    internal static int DictionaryHashCode(IReadOnlyDictionary<string, string> properties)
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
