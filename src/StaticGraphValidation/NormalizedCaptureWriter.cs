internal static class NormalizedCaptureWriter
{
    public static void Write(GraphCapture capture, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        Dictionary<string, GraphNode> nodesById = Normalization.NodesById(capture);
        WriteProjection(capture, nodesById, outputDirectory, "query-strict", ComparisonProjection.QueryStrict, Normalization.IsQueryOnlyNode, EdgeClass.Query);
        WriteProjection(capture, nodesById, outputDirectory, "full-presence", ComparisonProjection.FullPresence, static node => true, EdgeClass.All);
        WriteProjection(capture, nodesById, outputDirectory, "build-presence", ComparisonProjection.BuildPresence, static node => !Normalization.IsQueryOnlyNode(node), EdgeClass.Build);
        WriteProjection(capture, nodesById, outputDirectory, "build-strict", ComparisonProjection.BuildStrict, static node => !Normalization.IsQueryOnlyNode(node), EdgeClass.Build);
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
        HashSet<NormalizedNode> nodes = Normalization.ProjectNodes(capture.Nodes.Where(nodePredicate), projection);
        HashSet<NormalizedEdge> edges = Normalization.ProjectEdges(capture.Edges, nodesById, projection, edgeClass);
        File.WriteAllLines(Path.Combine(outputDirectory, $"{name}.nodes.txt"), nodes.Select(NodeLine).OrderBy(static line => line, StringComparer.OrdinalIgnoreCase));
        File.WriteAllLines(Path.Combine(outputDirectory, $"{name}.edges.txt"), edges.Select(EdgeLine).OrderBy(static line => line, StringComparer.OrdinalIgnoreCase));
    }

    private static string NodeLine(NormalizedNode node) =>
        string.Join('|',
            $"Role={Escape(node.Role)}",
            $"Project={Escape(node.ProjectPath)}",
            $"Targets={Escape(string.Join(';', node.EffectiveTargets))}",
            $"GlobalProperties={Escape(string.Join(';', node.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")))}");

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
