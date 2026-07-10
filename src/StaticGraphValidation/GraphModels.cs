internal sealed record GraphCapture
{
    public string Schema { get; init; } = "static-graph-validation.v1";
    public string Kind { get; init; } = "";
    public string? EntryProject { get; init; }
    public IReadOnlyList<string> EntryTargets { get; init; } = [];
    public IReadOnlyDictionary<string, string> GlobalProperties { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<GraphNode> Nodes { get; init; } = [];
    public IReadOnlyList<GraphEdge> Edges { get; init; } = [];
    public IReadOnlyList<GraphDiagnostic> Diagnostics { get; init; } = [];
}

internal sealed record GraphNode
{
    public string Id { get; init; } = "";
    public string ProjectPath { get; init; } = "";
    public string? RelativePath { get; init; }
    public IReadOnlyList<string> RequestedTargets { get; init; } = [];
    public IReadOnlyList<string> DefaultTargets { get; init; } = [];
    public IReadOnlyDictionary<string, string> GlobalProperties { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> EvaluatedProperties { get; init; } = new Dictionary<string, string>();
    public string IdentityKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
}

internal sealed record GraphEdge
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public IReadOnlyList<string> RequestedTargets { get; init; } = [];
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> GlobalProperties { get; init; } = new Dictionary<string, string>();
    public string SourceKind { get; init; } = "";
    public string? SourceLocation { get; init; }
}

internal sealed record GraphDiagnostic
{
    public string Severity { get; init; } = "info";
    public string Message { get; init; } = "";
    public string? Context { get; init; }
}
