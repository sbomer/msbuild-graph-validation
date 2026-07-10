internal static class DynamicBinlogCapture
{
    public static GraphCapture Capture(string binlogPath, Options options)
    {
        string fullPath = Path.GetFullPath(binlogPath);
        string repoRoot = options.RepoRoot ?? Paths.FindRepoRoot(Directory.GetCurrentDirectory()) ?? Directory.GetCurrentDirectory();
        List<GraphDiagnostic> diagnostics = [];

        object? build;
        try
        {
            Type binaryLogType = Type.GetType("Microsoft.Build.Logging.StructuredLogger.BinaryLog, StructuredLogger", throwOnError: true)!;
            MethodInfo readBuild = binaryLogType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(method => method.Name == "ReadBuild" && method.GetParameters().Length >= 1 && method.GetParameters()[0].ParameterType == typeof(string));
            build = readBuild.Invoke(null, [fullPath]);
        }
        catch (Exception ex)
        {
            return new GraphCapture
            {
                Kind = "dynamic",
                EntryProject = fullPath,
                Diagnostics = [new GraphDiagnostic { Severity = "error", Message = $"Unable to read binlog '{fullPath}': {ex.Message}", Context = ex.ToString() }]
            };
        }

        Dictionary<string, GraphNode> nodes = new(StringComparer.Ordinal);
        List<GraphEdge> edges = [];
        HashSet<string> defaultTargetDiagnostics = new(StringComparer.OrdinalIgnoreCase);
        Stack<GraphNode> parentStack = new();
        Stack<string> targetStack = new();
        Stack<string> taskStack = new();

        void Walk(object node)
        {
            string typeName = node.GetType().Name;
            bool isProject = typeName.Equals("Project", StringComparison.OrdinalIgnoreCase);
            bool isTarget = typeName.Equals("Target", StringComparison.OrdinalIgnoreCase);
            bool isTask = typeName.EndsWith("Task", StringComparison.OrdinalIgnoreCase);

            if (isTarget)
            {
                targetStack.Push(GetString(node, "Name") ?? "");
            }
            else if (isTask)
            {
                taskStack.Push(GetString(node, "Name") ?? typeName);
            }

            GraphNode? graphNode = null;
            if (isProject)
            {
                graphNode = CreateDynamicNode(node, repoRoot, diagnostics, defaultTargetDiagnostics);
                nodes.TryAdd(graphNode.Id, graphNode);

                if (parentStack.TryPeek(out GraphNode? parent))
                {
                    edges.Add(new GraphEdge
                    {
                        From = parent.Id,
                        To = graphNode.Id,
                        RequestedTargets = graphNode.RequestedTargets,
                        Metadata = CreateEdgeMetadata(targetStack, taskStack),
                        GlobalProperties = graphNode.GlobalProperties,
                        SourceKind = taskStack.TryPeek(out string? taskName) && taskName.Equals("MSBuild", StringComparison.OrdinalIgnoreCase)
                            ? "dynamic-msbuild-task"
                            : "dynamic-binlog-parent",
                        SourceLocation = targetStack.TryPeek(out string? targetName) && !string.IsNullOrEmpty(targetName)
                            ? targetName
                            : null
                    });
                }

                parentStack.Push(graphNode);
            }

            foreach (object child in GetChildren(node))
            {
                Walk(child);
            }

            if (isProject)
            {
                parentStack.Pop();
            }

            if (isTarget)
            {
                targetStack.Pop();
            }
            else if (isTask)
            {
                taskStack.Pop();
            }
        }

        Walk(build!);

        diagnostics.Add(new GraphDiagnostic
        {
            Severity = "warning",
            Message = "Dynamic capture is reconstructed from StructuredLogger tree shape. Parent edges are best-effort; verify suspicious edges against the original binlog."
        });

        return new GraphCapture
        {
            Kind = "dynamic",
            EntryProject = fullPath,
            Nodes = nodes.Values.OrderBy(static node => node.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            Edges = edges.OrderBy(static edge => edge.From).ThenBy(static edge => edge.To).ToArray(),
            Diagnostics = diagnostics
        };
    }

    private static GraphNode CreateDynamicNode(
        object project,
        string repoRoot,
        List<GraphDiagnostic> diagnostics,
        HashSet<string> defaultTargetDiagnostics)
    {
        string projectPath = GetString(project, "ProjectFile") ?? GetString(project, "File") ?? GetString(project, "Name") ?? "<unknown>";
        if (File.Exists(projectPath))
        {
            projectPath = Path.GetFullPath(projectPath);
        }

        Dictionary<string, string> properties = ExtractProperties(project);
        string[] targets = ExtractTargets(project);
        string[] defaultTargets = ExtractDefaultTargets(projectPath, properties, diagnostics, defaultTargetDiagnostics);
        AddUnresolvedDefaultTargetsDiagnostic(project, projectPath, targets, defaultTargets, diagnostics, defaultTargetDiagnostics);
        string key = Identity.NodeKey(projectPath, properties, targets);
        string relativePath = File.Exists(projectPath) ? Paths.Relativize(projectPath, repoRoot) : projectPath;

        return new GraphNode
        {
            Id = key,
            ProjectPath = projectPath,
            RelativePath = relativePath,
            RequestedTargets = targets,
            DefaultTargets = defaultTargets,
            GlobalProperties = properties,
            EvaluatedProperties = ExtractInterestingProperties(properties),
            IdentityKey = key,
            DisplayName = targets.Length == 0 ? relativePath : $"{relativePath} /t:{string.Join(";", targets)}"
        };
    }

    private static Dictionary<string, string> ExtractProperties(object node)
    {
        Dictionary<string, string> properties = new(StringComparer.OrdinalIgnoreCase);
        foreach (string propertyName in new[] { "GlobalProperties", "Properties" })
        {
            object? value = GetProperty(node, propertyName);
            if (value is null)
            {
                continue;
            }

            if (value is System.Collections.IDictionary dictionary)
            {
                foreach (object? key in dictionary.Keys)
                {
                    if (key is not null && dictionary[key] is not null)
                    {
                        properties[key.ToString()!] = dictionary[key]!.ToString()!;
                    }
                }
            }
        }

        return properties;
    }

    private static Dictionary<string, string> ExtractInterestingProperties(Dictionary<string, string> properties)
    {
        string[] names = ["TargetFramework", "RuntimeIdentifier", "Configuration", "TargetOS", "TargetArchitecture"];
        return properties.Where(pair => names.Contains(pair.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static string[] ExtractTargets(object project)
    {
        if (GetProperty(project, "EntryTargets") is System.Collections.IEnumerable entryTargets)
        {
            string[] targets = entryTargets
                .Cast<object?>()
                .Select(static target => target?.ToString())
                .Where(static target => !string.IsNullOrWhiteSpace(target))
                .Select(static target => target!)
                .ToArray();
            if (targets.Length > 0)
            {
                return targets;
            }
        }

        string? text = GetString(project, "TargetsText")
            ?? GetString(project, "TargetNames")
            ?? GetString(project, "Targets")
            ?? GetString(project, "RequestedTargets");

        return string.IsNullOrWhiteSpace(text)
            ? []
            : text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void AddUnresolvedDefaultTargetsDiagnostic(
        object project,
        string projectPath,
        IReadOnlyList<string> requestedTargets,
        IReadOnlyList<string> defaultTargets,
        List<GraphDiagnostic> diagnostics,
        HashSet<string> defaultTargetDiagnostics)
    {
        if (requestedTargets.Count > 0 ||
            defaultTargets.Count > 0 ||
            !File.Exists(projectPath) ||
            !GetChildren(project).Any(static child => child.GetType().Name.Equals("Target", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        string key = $"{projectPath}|unresolved-default-targets";
        if (defaultTargetDiagnostics.Add(key))
        {
            diagnostics.Add(new GraphDiagnostic
            {
                Severity = "warning",
                Message = $"Unable to resolve default targets for '{projectPath}'. The binlog project invocation has no requested targets and project re-evaluation returned no default targets.",
                Context = "Default-target normalization will preserve an empty target list for this project."
            });
        }
    }

    private static string[] ExtractDefaultTargets(
        string projectPath,
        Dictionary<string, string> properties,
        List<GraphDiagnostic> diagnostics,
        HashSet<string> defaultTargetDiagnostics)
    {
        if (!File.Exists(projectPath))
        {
            return [];
        }

        try
        {
            ProjectInstance project = new(projectPath, properties, toolsVersion: null);
            return project.DefaultTargets.ToArray();
        }
        catch (Exception ex)
        {
            string key = $"{projectPath}|{ex.GetType().FullName}|{ex.Message}";
            if (defaultTargetDiagnostics.Add(key))
            {
                diagnostics.Add(new GraphDiagnostic
                {
                    Severity = "warning",
                    Message = $"Unable to extract default targets for '{projectPath}': {ex.Message}",
                    Context = ex.ToString()
                });
            }

            return [];
        }
    }

    private static Dictionary<string, string> CreateEdgeMetadata(Stack<string> targetStack, Stack<string> taskStack)
    {
        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase);
        if (targetStack.TryPeek(out string? targetName) && !string.IsNullOrEmpty(targetName))
        {
            metadata["ParentTarget"] = targetName;
        }

        if (taskStack.TryPeek(out string? taskName) && !string.IsNullOrEmpty(taskName))
        {
            metadata["ParentTask"] = taskName;
        }

        return metadata;
    }

    private static IEnumerable<object> GetChildren(object node)
    {
        object? children = GetProperty(node, "Children");
        if (children is System.Collections.IEnumerable enumerable)
        {
            foreach (object? child in enumerable)
            {
                if (child is not null)
                {
                    yield return child;
                }
            }
        }
    }

    private static string? GetString(object obj, string propertyName) => GetProperty(obj, propertyName)?.ToString();

    private static object? GetProperty(object obj, string propertyName) =>
        obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.GetValue(obj);
}
