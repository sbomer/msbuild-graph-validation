internal static class CommandLine
{
    public static int Run(string[] args)
    {
        try
        {
            string command = args[0].ToLowerInvariant();
            string[] rest = args.Skip(1).ToArray();

            return command switch
            {
                "dynamic" => RunDynamic(rest),
                "static" => RunStatic(rest),
                "normalize" => RunNormalize(rest),
                "normalize-replay" => RunNormalizeReplay(rest),
                "compare-replay" => RunCompareReplay(rest),
                _ => Unknown(command)
            };
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    public static void PrintUsage()
    {
        Console.Error.WriteLine("""
            usage:
              StaticGraphValidation dynamic <binlog> [options]
              StaticGraphValidation static <project> [options]
              StaticGraphValidation normalize <capture.json> --output-dir directory
              StaticGraphValidation normalize-replay --dynamic baseline.json --phase1 query.json --static replay.json --output-dir directory
              StaticGraphValidation compare-replay --dynamic baseline.json --phase1 query.json --static replay.json [options]

            options:
              -p|--property Name=Value   global property for static capture (repeatable)
              -t|--target Target[;...]   entry targets for static target-list capture
              -o|--output path           output path (default: stdout)
              --repo-root path           path used for relative display
              --output-dir path          directory for normalized text files
              --format text|json         output format for compare-replay (default: text)
            """);
    }

    private static int RunDynamic(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        Options options = Options.Parse(args.Skip(1));
        GraphCapture capture = DynamicBinlogCapture.Capture(args[0], options);
        WriteText(GraphJson.Serialize(capture), options.OutputPath);
        return DiagnosticsExitCode(capture);
    }

    private static int RunStatic(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        Options options = Options.Parse(args.Skip(1));
        GraphCapture capture = StaticGraphCapture.Capture(args[0], options);
        WriteText(GraphJson.Serialize(capture), options.OutputPath);
        return DiagnosticsExitCode(capture);
    }

    private static int RunNormalize(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        Options options = Options.Parse(args.Skip(1));
        if (options.OutputDirectory is null)
        {
            throw new ArgumentException("--output-dir is required for normalize.");
        }

        GraphCapture capture = GraphJson.Read(args[0]);
        NormalizedCaptureWriter.Write(capture, options.OutputDirectory);
        return 0;
    }

    private static int RunNormalizeReplay(string[] args)
    {
        Options options = Options.Parse(args);
        if (options.DynamicPath is null || options.Phase1Path is null || options.StaticPath is null)
        {
            PrintUsage();
            return 1;
        }

        if (options.OutputDirectory is null)
        {
            throw new ArgumentException("--output-dir is required for normalize-replay.");
        }

        NormalizedCaptureWriter.WriteReplayInputs(
            GraphJson.Read(options.DynamicPath),
            GraphJson.Read(options.Phase1Path),
            GraphJson.Read(options.StaticPath),
            options.OutputDirectory);
        return 0;
    }

    private static int RunCompareReplay(string[] args)
    {
        Options options = Options.Parse(args);
        if (options.DynamicPath is null || options.Phase1Path is null || options.StaticPath is null)
        {
            PrintUsage();
            return 1;
        }

        ReplayComparison comparison = ReplayComparer.Compare(
            GraphJson.Read(options.DynamicPath),
            GraphJson.Read(options.Phase1Path),
            GraphJson.Read(options.StaticPath));

        string report = options.Format.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? GraphJson.Serialize(comparison)
            : ReplayReportWriter.Write(comparison);
        WriteText(report, options.OutputPath);
        return comparison.QueryDynamicOnly.Count == 0 &&
            comparison.QueryPhase1Only.Count == 0 &&
            comparison.BuildPresenceDynamicOnly.Count == 0 &&
            comparison.BuildPresenceStaticOnly.Count == 0 &&
            comparison.QueryEdgeDynamicOnly.Count == 0 &&
            comparison.QueryEdgePhase1Only.Count == 0 &&
            comparison.BuildPresenceEdgeDynamicOnly.Count == 0 &&
            comparison.BuildPresenceEdgeStaticOnly.Count == 0
                ? 0
                : 2;
    }

    private static void WriteText(string text, string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(text);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, text);
    }

    private static int DiagnosticsExitCode(GraphCapture capture) =>
        capture.Diagnostics.Any(static d => d.Severity.Equals("error", StringComparison.OrdinalIgnoreCase)) ? 2 : 0;

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"unknown command '{command}'");
        PrintUsage();
        return 1;
    }
}

