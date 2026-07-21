internal static class GraphComparer
{
    private static readonly StringComparer s_comparer = StringComparer.OrdinalIgnoreCase;

    public static GraphComparison Compare(GraphCapture left, GraphCapture right)
    {
        Dictionary<string, GraphNode> leftNodesById = Normalization.NodesById(left);
        Dictionary<string, GraphNode> rightNodesById = Normalization.NodesById(right);

        HashSet<NormalizedNode> leftNodes = Normalization.ProjectNodes(left.Nodes);
        HashSet<NormalizedNode> rightNodes = Normalization.ProjectNodes(right.Nodes);
        HashSet<EvaluatedProjectInstance> leftInstances = Normalization.ProjectInstances(left.Nodes);
        HashSet<EvaluatedProjectInstance> rightInstances = Normalization.ProjectInstances(right.Nodes);
        HashSet<NormalizedEdge> leftEdges = Normalization.ProjectEdges(left.Edges, leftNodesById);
        HashSet<NormalizedEdge> rightEdges = Normalization.ProjectEdges(right.Edges, rightNodesById);

        return new GraphComparison
        {
            LeftNodeCount = left.Nodes.Count,
            RightNodeCount = right.Nodes.Count,
            LeftEdgeCount = left.Edges.Count,
            RightEdgeCount = right.Edges.Count,
            LeftNormalizedNodeCount = leftNodes.Count,
            RightNormalizedNodeCount = rightNodes.Count,
            LeftEvaluatedProjectInstanceCount = leftInstances.Count,
            RightEvaluatedProjectInstanceCount = rightInstances.Count,
            LeftNormalizedEdgeCount = leftEdges.Count,
            RightNormalizedEdgeCount = rightEdges.Count,
            LeftOnlyNodes = Except(leftNodes, rightNodes),
            RightOnlyNodes = Except(rightNodes, leftNodes),
            LeftOnlyEdges = Except(leftEdges, rightEdges),
            RightOnlyEdges = Except(rightEdges, leftEdges)
        };
    }

    private static List<NormalizedNode> Except(HashSet<NormalizedNode> left, HashSet<NormalizedNode> right) =>
        left.Except(right, NormalizedNodeComparer.Instance)
            .OrderBy(static node => node.ProjectPath, s_comparer)
            .ThenBy(static node => node.Target, s_comparer)
            .ThenBy(static node => string.Join(';', node.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")), s_comparer)
            .ToList();

    private static List<NormalizedEdge> Except(HashSet<NormalizedEdge> left, HashSet<NormalizedEdge> right) =>
        left.Except(right, NormalizedEdgeComparer.Instance)
            .OrderBy(static edge => edge.From.ProjectPath, s_comparer)
            .ThenBy(static edge => edge.To.ProjectPath, s_comparer)
            .ThenBy(static edge => edge.From.Target, s_comparer)
            .ThenBy(static edge => edge.To.Target, s_comparer)
            .ThenBy(static edge => string.Join(';', edge.From.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")), s_comparer)
            .ThenBy(static edge => string.Join(';', edge.To.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")), s_comparer)
            .ToList();
}
