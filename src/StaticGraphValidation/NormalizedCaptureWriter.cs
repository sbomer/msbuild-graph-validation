internal static class NormalizedCaptureWriter
{
    public static void Write(GraphCapture capture, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        Dictionary<string, GraphNode> nodesById = ReplayComparer.NodesById(capture);
        WriteProjection(capture, nodesById, outputDirectory, "query-strict", ComparisonProjection.QueryStrict, ReplayComparer.IsQueryOnlyNode, EdgeClass.Query);
        WriteProjection(capture, nodesById, outputDirectory, "full-presence", ComparisonProjection.FullPresence, static node => true, EdgeClass.All);
        WriteProjection(capture, nodesById, outputDirectory, "build-presence", ComparisonProjection.BuildPresence, static node => !ReplayComparer.IsQueryOnlyNode(node), EdgeClass.Build);
        WriteProjection(capture, nodesById, outputDirectory, "build-strict", ComparisonProjection.BuildStrict, static node => !ReplayComparer.IsQueryOnlyNode(node), EdgeClass.Build);
    }

    public static void WriteReplayInputs(GraphCapture dynamicCapture, GraphCapture phase1Capture, GraphCapture staticCapture, string outputDirectory)
    {
        WriteReplayProjection(dynamicCapture, Path.Combine(outputDirectory, "query-strict"), "dynamic", ComparisonProjection.QueryStrict, ReplayComparer.IsQueryOnlyNode, EdgeClass.Query);
        WriteReplayProjection(phase1Capture, Path.Combine(outputDirectory, "query-strict"), "phase1", ComparisonProjection.QueryStrict, ReplayComparer.IsQueryOnlyNode, EdgeClass.Query);
        WriteReplayProjection(dynamicCapture, Path.Combine(outputDirectory, "build-presence"), "dynamic", ComparisonProjection.BuildPresence, static node => !ReplayComparer.IsQueryOnlyNode(node), EdgeClass.Build);
        WriteReplayProjection(staticCapture, Path.Combine(outputDirectory, "build-presence"), "static", ComparisonProjection.BuildPresence, static node => true, EdgeClass.All);
    }

    private static void WriteReplayProjection(
        GraphCapture capture,
        string outputDirectory,
        string prefix,
        ComparisonProjection projection,
        Func<GraphNode, bool> nodePredicate,
        EdgeClass edgeClass)
    {
        Directory.CreateDirectory(outputDirectory);
        Dictionary<string, GraphNode> nodesById = ReplayComparer.NodesById(capture);
        WriteProjection(capture, nodesById, outputDirectory, prefix, projection, nodePredicate, edgeClass);
    }

    private static void WriteProjection(
        GraphCapture capture,
        Dictionary<string, GraphNode> nodesById,
        string outputDirectory,
        string name,
        ComparisonProjection projection,
        Func<GraphNode, bool> nodePredicate,
        EdgeClass edgeClass)
    {
        HashSet<NormalizedNode> nodes = ReplayComparer.ProjectNodes(capture.Nodes.Where(nodePredicate), projection);
        HashSet<NormalizedEdge> edges = ReplayComparer.ProjectEdges(capture.Edges, nodesById, projection, edgeClass);
        File.WriteAllLines(Path.Combine(outputDirectory, $"{name}.nodes.txt"), nodes.Select(NodeLine).OrderBy(static line => line, StringComparer.OrdinalIgnoreCase));
        File.WriteAllLines(Path.Combine(outputDirectory, $"{name}.edges.txt"), edges.Select(EdgeLine).OrderBy(static line => line, StringComparer.OrdinalIgnoreCase));
    }

    private static string NodeLine(NormalizedNode node) =>
        string.Join('|',
            $"Role={Escape(node.Role)}",
            $"Project={Escape(node.ProjectPath)}",
            $"Configuration={Escape(node.Configuration)}",
            $"TargetFramework={Escape(node.TargetFramework)}",
            $"RuntimeIdentifier={Escape(node.RuntimeIdentifier)}",
            $"TargetOS={Escape(node.TargetOS)}",
            $"TargetArchitecture={Escape(node.TargetArchitecture)}",
            $"TargetRid={Escape(node.TargetRid)}");

    private static string EdgeLine(NormalizedEdge edge) =>
        string.Join('|',
            $"Role={Escape(edge.Role)}",
            $"From={Escape(NodeLine(edge.From))}",
            $"To={Escape(NodeLine(edge.To))}");

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

}

