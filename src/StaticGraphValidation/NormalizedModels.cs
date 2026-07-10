internal enum ComparisonProjection
{
    FullPresence,
    QueryStrict,
    BuildPresence,
    BuildStrict
}

internal enum EdgeClass
{
    All,
    Query,
    Build
}

internal sealed record NormalizedNode(
    string ProjectPath,
    string Configuration,
    string TargetFramework,
    string RuntimeIdentifier,
    string TargetOS,
    string TargetArchitecture,
    string TargetRid,
    string Role)
{
    public string DisplayName
    {
        get
        {
            List<string> properties = [];
            if (!string.IsNullOrEmpty(TargetFramework))
            {
                properties.Add($"TargetFramework={TargetFramework}");
            }

            if (!string.IsNullOrEmpty(Configuration))
            {
                properties.Add($"Configuration={Configuration}");
            }

            if (!string.IsNullOrEmpty(RuntimeIdentifier))
            {
                properties.Add($"RuntimeIdentifier={RuntimeIdentifier}");
            }

            if (!string.IsNullOrEmpty(TargetOS))
            {
                properties.Add($"TargetOS={TargetOS}");
            }

            if (!string.IsNullOrEmpty(TargetArchitecture))
            {
                properties.Add($"TargetArchitecture={TargetArchitecture}");
            }

            if (!string.IsNullOrEmpty(TargetRid))
            {
                properties.Add($"TargetRid={TargetRid}");
            }

            return properties.Count == 0 ? ProjectPath : $"{ProjectPath} [{string.Join(", ", properties)}]";
        }
    }
}

internal sealed record NormalizedEdge(NormalizedNode From, NormalizedNode To, string Role)
{
    public string DisplayName => $"{From.DisplayName} -> {To.DisplayName} [{Role}]";
}
