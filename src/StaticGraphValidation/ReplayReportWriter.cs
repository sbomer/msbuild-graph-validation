internal static class ReplayReportWriter
{
    public static string Write(ReplayComparison comparison)
    {
        StringWriter writer = new();
        writer.WriteLine("Static graph replay comparison");
        writer.WriteLine();
        writer.WriteLine("Raw node counts:");
        writer.WriteLine($"  dynamic: {comparison.DynamicNodeCount}");
        writer.WriteLine($"  phase1: {comparison.Phase1NodeCount}");
        writer.WriteLine($"  static replay: {comparison.StaticNodeCount}");
        writer.WriteLine("Raw edge counts:");
        writer.WriteLine($"  dynamic: {comparison.DynamicEdgeCount}");
        writer.WriteLine($"  phase1: {comparison.Phase1EdgeCount}");
        writer.WriteLine($"  static replay: {comparison.StaticEdgeCount}");
        writer.WriteLine();
        writer.WriteLine("Normalized node counts:");
        writer.WriteLine($"  dynamic query: {comparison.DynamicQueryCount}");
        writer.WriteLine($"  phase1 query: {comparison.Phase1QueryCount}");
        writer.WriteLine($"  dynamic non-query: {comparison.DynamicNonQueryCount}");
        writer.WriteLine($"  static replay: {comparison.StaticReplayCount}");
        writer.WriteLine("Normalized edge counts:");
        writer.WriteLine($"  dynamic query: {comparison.DynamicQueryEdgeCount}");
        writer.WriteLine($"  phase1 query: {comparison.Phase1QueryEdgeCount}");
        writer.WriteLine($"  dynamic non-query: {comparison.DynamicNonQueryEdgeCount}");
        writer.WriteLine($"  static replay: {comparison.StaticReplayEdgeCount}");
        writer.WriteLine();
        writer.WriteLine("Projection dimensions:");
        writer.WriteLine("  QueryStrict: project + configuration + target framework + query target");
        writer.WriteLine("  BuildPresence: project + configuration + build role");
        writer.WriteLine("  Identity dimensions use global properties only; evaluated properties are diagnostic data and do not participate.");
        writer.WriteLine();
        WriteSection(writer, "QueryStrict dynamic-only", comparison.QueryDynamicOnly);
        WriteSection(writer, "QueryStrict phase1-only", comparison.QueryPhase1Only);
        WriteSection(writer, "BuildPresence dynamic-only", comparison.BuildPresenceDynamicOnly);
        WriteSection(writer, "BuildPresence static-only", comparison.BuildPresenceStaticOnly);
        WriteSection(writer, "QueryStrict edge dynamic-only", comparison.QueryEdgeDynamicOnly);
        WriteSection(writer, "QueryStrict edge phase1-only", comparison.QueryEdgePhase1Only);
        WriteSection(writer, "BuildPresence edge dynamic-only", comparison.BuildPresenceEdgeDynamicOnly);
        WriteSection(writer, "BuildPresence edge static-only", comparison.BuildPresenceEdgeStaticOnly);
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
