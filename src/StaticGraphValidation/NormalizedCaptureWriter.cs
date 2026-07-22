internal static class NormalizedCaptureWriter
{
    public static void Write(GraphCapture capture, string outputDirectory, string? repoRoot = null)
    {
        Directory.CreateDirectory(outputDirectory);

        Dictionary<string, GraphNode> nodesById = Normalization.NodesById(capture);
        WriteGraph(capture, nodesById, outputDirectory, repoRoot);
    }

    private static void WriteGraph(
        GraphCapture capture,
        Dictionary<string, GraphNode> nodesById,
        string outputDirectory,
        string? repoRoot)
    {
        HashSet<NormalizedNode> nodes = Normalization.ProjectNodes(capture.Nodes);
        HashSet<NormalizedEdge> edges = Normalization.ProjectEdges(capture.Edges, nodesById);
        File.WriteAllLines(Path.Combine(outputDirectory, "nodes.txt"), NodeLines(nodes, repoRoot));
        File.WriteAllLines(
            Path.Combine(outputDirectory, "edges.txt"),
            edges.Select(edge => EdgeLine(edge, repoRoot)).OrderBy(static line => line, StringComparer.OrdinalIgnoreCase));
    }

    private static string EdgeLine(NormalizedEdge edge, string? repoRoot) =>
        string.Join('|',
            $"From={Escape(FlatNodeLine(edge.From, repoRoot))}",
            $"To={Escape(FlatNodeLine(edge.To, repoRoot))}");

    private static string FlatNodeLine(NormalizedNode node, string? repoRoot) =>
        string.Join('|',
            $"Project={Escape(Paths.Relativize(node.ProjectPath, repoRoot))}",
            $"Target={Escape(node.Target)}",
            $"GlobalProperties={Escape(string.Join(';', node.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")))}");

    private static IEnumerable<string> NodeLines(IEnumerable<NormalizedNode> nodes, string? repoRoot)
    {
        foreach (IGrouping<string, NormalizedNode> projectGroup in nodes
            .GroupBy(static node => node.ProjectPath)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return $"Project: {Escape(Paths.Relativize(projectGroup.Key, repoRoot))}";

            foreach (IGrouping<string, NormalizedNode> globalPropertiesGroup in projectGroup
                .GroupBy(static node => string.Join(';', node.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")))
                .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                yield return LabelLine("  Globals:", globalPropertiesGroup.Key);

                foreach (string target in globalPropertiesGroup
                    .Select(static node => node.Target)
                    .OrderBy(static line => line, StringComparer.OrdinalIgnoreCase))
                {
                    yield return LabelLine("    Target:", target);
                }
            }
        }
    }

    private static string LabelLine(string label, string value) =>
        value.Length == 0 ? label : $"{label} {Escape(value)}";

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

}
