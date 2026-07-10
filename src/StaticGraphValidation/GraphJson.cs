internal static class GraphJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static GraphCapture Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<GraphCapture>(stream, Options)
            ?? throw new InvalidOperationException($"Unable to read graph capture '{path}'.");
    }
}

