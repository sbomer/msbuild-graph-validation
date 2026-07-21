internal static class GraphComparisonReportWriter
{
    public static string Write(GraphComparison comparison)
    {
        StringWriter writer = new();
        writer.WriteLine("Normalized graph comparison");
        writer.WriteLine();
        writer.WriteLine("Raw node counts:");
        writer.WriteLine($"  left: {comparison.LeftNodeCount}");
        writer.WriteLine($"  right: {comparison.RightNodeCount}");
        writer.WriteLine("Raw edge counts:");
        writer.WriteLine($"  left: {comparison.LeftEdgeCount}");
        writer.WriteLine($"  right: {comparison.RightEdgeCount}");
        writer.WriteLine();
        writer.WriteLine("Normalized counts:");
        writer.WriteLine($"  left evaluated project instances: {comparison.LeftEvaluatedProjectInstanceCount}");
        writer.WriteLine($"  right evaluated project instances: {comparison.RightEvaluatedProjectInstanceCount}");
        writer.WriteLine($"  left nodes: {comparison.LeftNormalizedNodeCount}");
        writer.WriteLine($"  right nodes: {comparison.RightNormalizedNodeCount}");
        writer.WriteLine($"  left edges: {comparison.LeftNormalizedEdgeCount}");
        writer.WriteLine($"  right edges: {comparison.RightNormalizedEdgeCount}");
        writer.WriteLine();
        writer.WriteLine("Normalization:");
        writer.WriteLine("  Node identity: project path + individual effective target + global properties");
        writer.WriteLine("  Effective targets: requested targets, otherwise resolved default targets; multi-target requests are split");
        writer.WriteLine("  Ignored global properties: IsGraphBuild");
        writer.WriteLine();
        WriteSection(writer, "Left-only nodes", comparison.LeftOnlyNodes);
        WriteSection(writer, "Right-only nodes", comparison.RightOnlyNodes);
        WriteSection(writer, "Left-only edges", comparison.LeftOnlyEdges);
        WriteSection(writer, "Right-only edges", comparison.RightOnlyEdges);
        return writer.ToString();
    }

    private static void WriteSection(StringWriter writer, string title, IReadOnlyList<NormalizedNode> nodes)
    {
        writer.WriteLine($"{title}: {nodes.Count}");
        foreach (NormalizedNode node in nodes.Take(80))
        {
            writer.WriteLine($"  {node.DisplayName}");
        }

        if (nodes.Count > 80)
        {
            writer.WriteLine("  ...");
        }

        writer.WriteLine();
    }

    private static void WriteSection(StringWriter writer, string title, IReadOnlyList<NormalizedEdge> edges)
    {
        writer.WriteLine($"{title}: {edges.Count}");
        foreach (NormalizedEdge edge in edges.Take(80))
        {
            writer.WriteLine($"  {edge.DisplayName}");
        }

        if (edges.Count > 80)
        {
            writer.WriteLine("  ...");
        }

        writer.WriteLine();
    }
}
