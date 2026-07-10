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
        string projectPath = node.RelativePath ?? node.ProjectPath.Replace('\\', '/');
        string role = IsQueryOnlyNode(node) ? $"Query:{string.Join(";", node.RequestedTargets)}" : "Build";
        return new CanonicalNode(
            projectPath,
            role,
            node.RequestedTargets,
            GetDimension(node, "Configuration"),
            GetDimension(node, "TargetFramework"),
            GetDimension(node, "RuntimeIdentifier"),
            GetDimension(node, "TargetOS"),
            GetDimension(node, "TargetArchitecture"),
            GetDimension(node, "TargetRid"));
    }

    public static NormalizedNode Project(CanonicalNode node, ComparisonProjection projection)
    {
        string targetFramework = projection is ComparisonProjection.QueryStrict or ComparisonProjection.BuildStrict ? node.TargetFramework.Value : "";
        string runtimeIdentifier = projection == ComparisonProjection.BuildStrict && node.RuntimeIdentifier.Source == IdentitySource.Global ? node.RuntimeIdentifier.Value : "";
        string targetOS = projection == ComparisonProjection.BuildStrict && node.TargetOS.Source == IdentitySource.Global ? node.TargetOS.Value : "";
        string targetArchitecture = projection == ComparisonProjection.BuildStrict && node.TargetArchitecture.Source == IdentitySource.Global ? node.TargetArchitecture.Value : "";
        string targetRid = projection == ComparisonProjection.BuildStrict && node.TargetRid.Source == IdentitySource.Global ? node.TargetRid.Value : "";
        string role = projection == ComparisonProjection.FullPresence ? node.Role.Split(':')[0] : node.Role;
        return new NormalizedNode(node.ProjectPath, node.Configuration.Value, targetFramework, runtimeIdentifier, targetOS, targetArchitecture, targetRid, role);
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

    private static IdentityDimension GetDimension(GraphNode node, string name)
    {
        if (node.GlobalProperties.TryGetValue(name, out string? globalValue))
        {
            return new IdentityDimension(globalValue, IdentitySource.Global);
        }

        return new IdentityDimension("", IdentitySource.None);
    }
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

internal enum IdentitySource
{
    None,
    Global
}

internal sealed record IdentityDimension(string Value, IdentitySource Source);

internal sealed record CanonicalNode(
    string ProjectPath,
    string Role,
    IReadOnlyList<string> RequestedTargets,
    IdentityDimension Configuration,
    IdentityDimension TargetFramework,
    IdentityDimension RuntimeIdentifier,
    IdentityDimension TargetOS,
    IdentityDimension TargetArchitecture,
    IdentityDimension TargetRid);

internal sealed record CanonicalEdge(CanonicalNode From, CanonicalNode To, string Role);

internal sealed record NormalizedNode(
    string ProjectPath,
    string Configuration,
    string TargetFramework,
    string RuntimeIdentifier,
    string TargetOS,
    string TargetArchitecture,
    string TargetRid,
    string Role)
{
    public string DisplayName
    {
        get
        {
            List<string> properties = [];
            if (!string.IsNullOrEmpty(TargetFramework))
            {
                properties.Add($"TargetFramework={TargetFramework}");
            }

            if (!string.IsNullOrEmpty(Configuration))
            {
                properties.Add($"Configuration={Configuration}");
            }

            if (!string.IsNullOrEmpty(RuntimeIdentifier))
            {
                properties.Add($"RuntimeIdentifier={RuntimeIdentifier}");
            }

            if (!string.IsNullOrEmpty(TargetOS))
            {
                properties.Add($"TargetOS={TargetOS}");
            }

            if (!string.IsNullOrEmpty(TargetArchitecture))
            {
                properties.Add($"TargetArchitecture={TargetArchitecture}");
            }

            if (!string.IsNullOrEmpty(TargetRid))
            {
                properties.Add($"TargetRid={TargetRid}");
            }

            return properties.Count == 0 ? ProjectPath : $"{ProjectPath} [{string.Join(", ", properties)}]";
        }
    }
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
        StringComparer.OrdinalIgnoreCase.Equals(x.Configuration, y.Configuration) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetFramework, y.TargetFramework) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.RuntimeIdentifier, y.RuntimeIdentifier) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetOS, y.TargetOS) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetArchitecture, y.TargetArchitecture) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.TargetRid, y.TargetRid) &&
        StringComparer.OrdinalIgnoreCase.Equals(x.Role, y.Role);

    public int GetHashCode(NormalizedNode obj) =>
        HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProjectPath),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Configuration),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetFramework),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RuntimeIdentifier),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetOS),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetArchitecture),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TargetRid),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Role));
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
