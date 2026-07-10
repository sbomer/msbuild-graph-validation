internal static class NormalizedCaptureWriter
{
    public static void Write(GraphCapture capture, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);

        Dictionary<string, GraphNode> nodesById = Normalization.NodesById(capture);
        WriteGraph(capture, nodesById, outputDirectory);
    }

    private static void WriteGraph(GraphCapture capture, Dictionary<string, GraphNode> nodesById, string outputDirectory)
    {
        HashSet<NormalizedNode> nodes = Normalization.ProjectNodes(capture.Nodes);
        HashSet<NormalizedEdge> edges = Normalization.ProjectEdges(capture.Edges, nodesById);
        File.WriteAllLines(Path.Combine(outputDirectory, "nodes.txt"), NodeLines(nodes));
        File.WriteAllLines(Path.Combine(outputDirectory, "edges.txt"), edges.Select(EdgeLine).OrderBy(static line => line, StringComparer.OrdinalIgnoreCase));
    }

    private static string EdgeLine(NormalizedEdge edge) =>
        string.Join('|',
            $"From={Escape(FlatNodeLine(edge.From))}",
            $"To={Escape(FlatNodeLine(edge.To))}");

    private static string FlatNodeLine(NormalizedNode node) =>
        string.Join('|',
            $"Project={Escape(node.ProjectPath)}",
            $"Target={Escape(node.Target)}",
            $"GlobalProperties={Escape(string.Join(';', node.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")))}");

    private static IEnumerable<string> NodeLines(IEnumerable<NormalizedNode> nodes)
    {
        foreach (IGrouping<string, NormalizedNode> projectGroup in nodes
            .GroupBy(static node => node.ProjectPath)
            .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            yield return $"Project: {Escape(projectGroup.Key)}";

            foreach (IGrouping<string, NormalizedNode> targetGroup in projectGroup
                .GroupBy(static node => node.Target)
                .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"  Target: {Escape(targetGroup.Key)}";

                foreach (string properties in targetGroup
                    .Select(static node => string.Join(';', node.GlobalProperties.Select(static pair => $"{pair.Key}={pair.Value}")))
                    .OrderBy(static line => line, StringComparer.OrdinalIgnoreCase))
                {
                    yield return $"    Globals: {Escape(properties)}";
                }
            }
        }
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

}
