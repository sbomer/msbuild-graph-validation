internal static class ReplayComparer
{
    private static readonly StringComparer s_comparer = StringComparer.OrdinalIgnoreCase;
    private static readonly HashSet<string> s_queryTargets = new(s_comparer)
    {
        "GetTargetFrameworks",
        "GetTargetFrameworksWithPlatformForSingleTargetFramework"
    };

    public static ReplayComparison Compare(GraphCapture dynamicCapture, GraphCapture phase1Capture, GraphCapture staticCapture)
    {
        Dictionary<string, GraphNode> dynamicNodesById = NodesById(dynamicCapture);
        Dictionary<string, GraphNode> phase1NodesById = NodesById(phase1Capture);
        Dictionary<string, GraphNode> staticNodesById = NodesById(staticCapture);

        HashSet<NormalizedNode> dynamicQuery = ProjectNodes(dynamicCapture.Nodes.Where(IsQueryOnlyNode), ComparisonProjection.QueryStrict);
        HashSet<NormalizedNode> dynamicNonQueryPresence = ProjectNodes(dynamicCapture.Nodes.Where(static node => !IsQueryOnlyNode(node)), ComparisonProjection.BuildPresence);

        HashSet<NormalizedNode> phase1Query = ProjectNodes(phase1Capture.Nodes.Where(IsQueryOnlyNode), ComparisonProjection.QueryStrict);
        HashSet<NormalizedNode> staticReplayPresence = ProjectNodes(staticCapture.Nodes, ComparisonProjection.BuildPresence);

        HashSet<NormalizedEdge> dynamicQueryEdges = ProjectEdges(dynamicCapture.Edges, dynamicNodesById, ComparisonProjection.QueryStrict, EdgeClass.Query);
        HashSet<NormalizedEdge> dynamicNonQueryEdges = ProjectEdges(dynamicCapture.Edges, dynamicNodesById, ComparisonProjection.BuildPresence, EdgeClass.Build);
        HashSet<NormalizedEdge> phase1QueryEdges = ProjectEdges(phase1Capture.Edges, phase1NodesById, ComparisonProjection.QueryStrict, EdgeClass.Query);
        HashSet<NormalizedEdge> staticReplayEdges = ProjectEdges(staticCapture.Edges, staticNodesById, ComparisonProjection.BuildPresence, EdgeClass.All);

        return new ReplayComparison
        {
            DynamicNodeCount = dynamicCapture.Nodes.Count,
            Phase1NodeCount = phase1Capture.Nodes.Count,
            StaticNodeCount = staticCapture.Nodes.Count,
            DynamicEdgeCount = dynamicCapture.Edges.Count,
            Phase1EdgeCount = phase1Capture.Edges.Count,
            StaticEdgeCount = staticCapture.Edges.Count,
            DynamicQueryCount = dynamicQuery.Count,
            Phase1QueryCount = phase1Query.Count,
            DynamicNonQueryCount = dynamicNonQueryPresence.Count,
            StaticReplayCount = staticReplayPresence.Count,
            DynamicQueryEdgeCount = dynamicQueryEdges.Count,
            Phase1QueryEdgeCount = phase1QueryEdges.Count,
            DynamicNonQueryEdgeCount = dynamicNonQueryEdges.Count,
            StaticReplayEdgeCount = staticReplayEdges.Count,
            QueryDynamicOnly = Except(dynamicQuery, phase1Query),
            QueryPhase1Only = Except(phase1Query, dynamicQuery),
            BuildPresenceDynamicOnly = Except(dynamicNonQueryPresence, staticReplayPresence),
            BuildPresenceStaticOnly = Except(staticReplayPresence, dynamicNonQueryPresence),
            QueryEdgeDynamicOnly = Except(dynamicQueryEdges, phase1QueryEdges),
            QueryEdgePhase1Only = Except(phase1QueryEdges, dynamicQueryEdges),
            BuildPresenceEdgeDynamicOnly = Except(dynamicNonQueryEdges, staticReplayEdges),
            BuildPresenceEdgeStaticOnly = Except(staticReplayEdges, dynamicNonQueryEdges)
        };
    }

    internal static Dictionary<string, GraphNode> NodesById(GraphCapture capture) =>
        capture.Nodes.ToDictionary(static node => node.Id, static node => node, StringComparer.Ordinal);

    internal static HashSet<NormalizedNode> ProjectNodes(IEnumerable<GraphNode> nodes, ComparisonProjection projection)
    {
        HashSet<NormalizedNode> normalizedNodes = new(NormalizedNodeComparer.Instance);
        foreach (GraphNode node in nodes)
        {
            normalizedNodes.Add(Project(ToCanonicalNode(node), projection));
        }

        return normalizedNodes;
    }

    internal static CanonicalNode ToCanonicalNode(GraphNode node)
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

    internal static NormalizedNode Project(CanonicalNode node, ComparisonProjection projection)
    {
        string targetFramework = projection is ComparisonProjection.QueryStrict or ComparisonProjection.BuildStrict ? node.TargetFramework.Value : "";
        string runtimeIdentifier = projection == ComparisonProjection.BuildStrict && node.RuntimeIdentifier.Source == IdentitySource.Global ? node.RuntimeIdentifier.Value : "";
        string targetOS = projection == ComparisonProjection.BuildStrict && node.TargetOS.Source == IdentitySource.Global ? node.TargetOS.Value : "";
        string targetArchitecture = projection == ComparisonProjection.BuildStrict && node.TargetArchitecture.Source == IdentitySource.Global ? node.TargetArchitecture.Value : "";
        string targetRid = projection == ComparisonProjection.BuildStrict && node.TargetRid.Source == IdentitySource.Global ? node.TargetRid.Value : "";
        string role = projection == ComparisonProjection.FullPresence ? node.Role.Split(':')[0] : node.Role;
        return new NormalizedNode(node.ProjectPath, node.Configuration.Value, targetFramework, runtimeIdentifier, targetOS, targetArchitecture, targetRid, role);
    }

    internal static HashSet<NormalizedEdge> ProjectEdges(
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

    private static IdentityDimension GetDimension(GraphNode node, string name)
    {
        if (node.GlobalProperties.TryGetValue(name, out string? globalValue))
        {
            return new IdentityDimension(globalValue, IdentitySource.Global);
        }

        return new IdentityDimension("", IdentitySource.None);
    }

    internal static bool IsQueryOnlyNode(GraphNode node)
    {
        if (node.RequestedTargets.Count == 0)
        {
            return false;
        }

        return node.RequestedTargets.All(s_queryTargets.Contains);
    }

    private static List<NormalizedNode> Except(HashSet<NormalizedNode> left, HashSet<NormalizedNode> right) =>
        left.Except(right, NormalizedNodeComparer.Instance)
            .OrderBy(static node => node.ProjectPath, s_comparer)
            .ThenBy(static node => node.Configuration, s_comparer)
            .ThenBy(static node => node.TargetFramework, s_comparer)
            .ThenBy(static node => node.Role, s_comparer)
            .ToList();

    private static List<NormalizedEdge> Except(HashSet<NormalizedEdge> left, HashSet<NormalizedEdge> right) =>
        left.Except(right, NormalizedEdgeComparer.Instance)
            .OrderBy(static edge => edge.From.ProjectPath, s_comparer)
            .ThenBy(static edge => edge.To.ProjectPath, s_comparer)
            .ThenBy(static edge => edge.Role, s_comparer)
            .ThenBy(static edge => edge.From.Configuration, s_comparer)
            .ThenBy(static edge => edge.To.Configuration, s_comparer)
            .ThenBy(static edge => edge.From.TargetFramework, s_comparer)
            .ThenBy(static edge => edge.To.TargetFramework, s_comparer)
            .ToList();
}

