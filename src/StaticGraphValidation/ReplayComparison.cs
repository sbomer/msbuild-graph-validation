internal sealed record ReplayComparison
{
    public int DynamicNodeCount { get; init; }
    public int Phase1NodeCount { get; init; }
    public int StaticNodeCount { get; init; }
    public int DynamicEdgeCount { get; init; }
    public int Phase1EdgeCount { get; init; }
    public int StaticEdgeCount { get; init; }
    public int DynamicQueryCount { get; init; }
    public int Phase1QueryCount { get; init; }
    public int DynamicNonQueryCount { get; init; }
    public int StaticReplayCount { get; init; }
    public int DynamicQueryEdgeCount { get; init; }
    public int Phase1QueryEdgeCount { get; init; }
    public int DynamicNonQueryEdgeCount { get; init; }
    public int StaticReplayEdgeCount { get; init; }
    public IReadOnlyList<NormalizedNode> QueryDynamicOnly { get; init; } = [];
    public IReadOnlyList<NormalizedNode> QueryPhase1Only { get; init; } = [];
    public IReadOnlyList<NormalizedNode> BuildPresenceDynamicOnly { get; init; } = [];
    public IReadOnlyList<NormalizedNode> BuildPresenceStaticOnly { get; init; } = [];
    public IReadOnlyList<NormalizedEdge> QueryEdgeDynamicOnly { get; init; } = [];
    public IReadOnlyList<NormalizedEdge> QueryEdgePhase1Only { get; init; } = [];
    public IReadOnlyList<NormalizedEdge> BuildPresenceEdgeDynamicOnly { get; init; } = [];
    public IReadOnlyList<NormalizedEdge> BuildPresenceEdgeStaticOnly { get; init; } = [];
}

