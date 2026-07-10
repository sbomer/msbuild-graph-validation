internal sealed record GraphComparison
{
    public int LeftNodeCount { get; init; }
    public int RightNodeCount { get; init; }
    public int LeftEdgeCount { get; init; }
    public int RightEdgeCount { get; init; }
    public int LeftNormalizedNodeCount { get; init; }
    public int RightNormalizedNodeCount { get; init; }
    public int LeftNormalizedEdgeCount { get; init; }
    public int RightNormalizedEdgeCount { get; init; }
    public IReadOnlyList<NormalizedNode> LeftOnlyNodes { get; init; } = [];
    public IReadOnlyList<NormalizedNode> RightOnlyNodes { get; init; } = [];
    public IReadOnlyList<NormalizedEdge> LeftOnlyEdges { get; init; } = [];
    public IReadOnlyList<NormalizedEdge> RightOnlyEdges { get; init; } = [];
}
