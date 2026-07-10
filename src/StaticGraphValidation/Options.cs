internal sealed class Options
{
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string[] Targets { get; private set; } = [];
    public string? OutputPath { get; private set; }
    public string? OutputDirectory { get; private set; }
    public string? RepoRoot { get; private set; }
    public string Format { get; private set; } = "text";
    public string? DynamicPath { get; private set; }
    public string? StaticPath { get; private set; }
    public string? LeftPath { get; private set; }
    public string? RightPath { get; private set; }

    public static Options Parse(IEnumerable<string> args)
    {
        Options options = new();
        string[] array = args.ToArray();
        for (int i = 0; i < array.Length; i++)
        {
            string arg = array[i];
            string Next()
            {
                if (++i >= array.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}");
                }

                return array[i];
            }

            switch (arg)
            {
                case "-p":
                case "--property":
                    AddProperty(options.Properties, Next());
                    break;
                case "-t":
                case "--target":
                    options.Targets = SplitTargets(Next());
                    break;
                case "-o":
                case "--output":
                    options.OutputPath = Next();
                    break;
                case "--repo-root":
                    options.RepoRoot = Path.GetFullPath(Next());
                    break;
                case "--output-dir":
                    options.OutputDirectory = Path.GetFullPath(Next());
                    break;
                case "--format":
                    options.Format = Next();
                    break;
                case "--dynamic":
                    options.DynamicPath = Next();
                    break;
                case "--static":
                    options.StaticPath = Next();
                    break;
                case "--left":
                    options.LeftPath = Next();
                    break;
                case "--right":
                    options.RightPath = Next();
                    break;
                default:
                    if (arg.StartsWith("-p:", StringComparison.OrdinalIgnoreCase))
                    {
                        AddProperty(options.Properties, arg[3..]);
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown option '{arg}'");
                    }
                    break;
            }
        }

        return options;
    }

    private static string[] SplitTargets(string value) =>
        value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    private static void AddProperty(Dictionary<string, string> properties, string value)
    {
        int equals = value.IndexOf('=');
        if (equals <= 0)
        {
            throw new ArgumentException($"Property must be Name=Value: '{value}'");
        }

        properties[value[..equals]] = value[(equals + 1)..];
    }
}
