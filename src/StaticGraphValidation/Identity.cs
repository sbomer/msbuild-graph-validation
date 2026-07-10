internal static class Identity
{
    public static string NodeKey(string projectPath, IReadOnlyDictionary<string, string> globalProperties, IEnumerable<string> requestedTargets)
    {
        string path = Path.GetFullPath(projectPath).Replace('\\', '/');
        string properties = string.Join(";", globalProperties.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => $"{pair.Key}={pair.Value}"));
        string targets = string.Join(";", requestedTargets);
        return $"{path}|targets={targets}|globals={properties}";
    }
}

