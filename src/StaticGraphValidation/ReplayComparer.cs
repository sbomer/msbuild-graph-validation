internal static class ReplayComparer
{
    private static readonly StringComparer s_comparer = StringComparer.OrdinalIgnoreCase;

    public static ReplayComparison Compare(GraphCapture dynamicCapture, GraphCapture phase1Capture, GraphCapture staticCapture)
    {
        Dictionary<string, GraphNode> dynamicNodesById = Normalization.NodesById(dynamicCapture);
        Dictionary<string, GraphNode> phase1NodesById = Normalization.NodesById(phase1Capture);
        Dictionary<string, GraphNode> staticNodesById = Normalization.NodesById(staticCapture);

        HashSet<NormalizedNode> dynamicQuery = Normalization.ProjectNodes(dynamicCapture.Nodes.Where(Normalization.IsQueryOnlyNode), ComparisonProjection.QueryStrict);
        HashSet<NormalizedNode> dynamicNonQueryPresence = Normalization.ProjectNodes(dynamicCapture.Nodes.Where(static node => !Normalization.IsQueryOnlyNode(node)), ComparisonProjection.BuildPresence);

        HashSet<NormalizedNode> phase1Query = Normalization.ProjectNodes(phase1Capture.Nodes.Where(Normalization.IsQueryOnlyNode), ComparisonProjection.QueryStrict);
        HashSet<NormalizedNode> staticReplayPresence = Normalization.ProjectNodes(staticCapture.Nodes, ComparisonProjection.BuildPresence);

        HashSet<NormalizedEdge> dynamicQueryEdges = Normalization.ProjectEdges(dynamicCapture.Edges, dynamicNodesById, ComparisonProjection.QueryStrict, EdgeClass.Query);
        HashSet<NormalizedEdge> dynamicNonQueryEdges = Normalization.ProjectEdges(dynamicCapture.Edges, dynamicNodesById, ComparisonProjection.BuildPresence, EdgeClass.Build);
        HashSet<NormalizedEdge> phase1QueryEdges = Normalization.ProjectEdges(phase1Capture.Edges, phase1NodesById, ComparisonProjection.QueryStrict, EdgeClass.Query);
        HashSet<NormalizedEdge> staticReplayEdges = Normalization.ProjectEdges(staticCapture.Edges, staticNodesById, ComparisonProjection.BuildPresence, EdgeClass.All);

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
