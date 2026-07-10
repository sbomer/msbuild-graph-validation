internal static class StaticGraphCapture
{
    private static readonly string[] s_identityProperties =
    [
        "TargetFramework",
        "TargetFrameworks",
        "RuntimeIdentifier",
        "RuntimeIdentifiers",
        "Configuration",
        "Platform",
        "TargetOS",
        "TargetArchitecture",
        "BuildArchitecture",
        "TargetRid",
        "IsGraphBuild"
    ];

    public static GraphCapture Capture(string project, Options options)
    {
        string fullProject = Path.GetFullPath(project);
        string repoRoot = options.RepoRoot ?? Paths.FindRepoRoot(fullProject) ?? Directory.GetCurrentDirectory();
        Dictionary<string, string> globals = new(options.Properties, StringComparer.OrdinalIgnoreCase);

        List<GraphDiagnostic> diagnostics = [];
        ProjectGraph graph;
        try
        {
            graph = new ProjectGraph(
                new[] { new ProjectGraphEntryPoint(fullProject, globals) },
                ProjectCollection.GlobalProjectCollection,
                projectInstanceFactory: null);
        }
        catch (Exception ex)
        {
            return new GraphCapture
            {
                Kind = "static",
                EntryProject = fullProject,
                EntryTargets = options.Targets,
                GlobalProperties = globals,
                Diagnostics = [new GraphDiagnostic { Severity = "error", Message = ex.Message, Context = ex.ToString() }]
            };
        }

        Dictionary<ProjectGraphNode, string[]> targetLists = new();
        if (options.Targets.Length > 0)
        {
            try
            {
                foreach ((ProjectGraphNode node, IReadOnlyCollection<string> targets) in graph.GetTargetLists(options.Targets))
                {
                    targetLists[node] = targets.ToArray();
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add(new GraphDiagnostic { Severity = "error", Message = ex.Message, Context = ex.ToString() });
            }
        }

        Dictionary<ProjectGraphNode, GraphNode> nodeMap = new();
        foreach (ProjectGraphNode graphNode in graph.ProjectNodes.OrderBy(static node => node.ProjectInstance.FullPath, StringComparer.OrdinalIgnoreCase))
        {
            string[] requestedTargets = targetLists.TryGetValue(graphNode, out string[]? targets) ? targets : [];
            GraphNode node = CreateNode(graphNode.ProjectInstance, repoRoot, requestedTargets);
            nodeMap[graphNode] = node;
        }

        List<GraphEdge> edges = [];
        foreach (ProjectGraphNode source in graph.ProjectNodes)
        {
            foreach (ProjectGraphNode target in source.ProjectReferences)
            {
                ProjectItemInstance? projectReference = FindProjectReference(source.ProjectInstance, target.ProjectInstance.FullPath);
                edges.Add(new GraphEdge
                {
                    From = nodeMap[source].Id,
                    To = nodeMap[target].Id,
                    RequestedTargets = SplitTargets(projectReference?.GetMetadataValue("Targets")),
                    Metadata = projectReference is null ? new Dictionary<string, string>() : ProjectReferenceMetadata(projectReference),
                    GlobalProperties = CopyProperties(target.ProjectInstance.GlobalProperties),
                    SourceKind = "static-project-reference",
                    SourceLocation = source.ProjectInstance.FullPath
                });
            }
        }

        return new GraphCapture
        {
            Kind = "static",
            EntryProject = fullProject,
            EntryTargets = options.Targets,
            GlobalProperties = globals,
            Nodes = nodeMap.Values.OrderBy(static node => node.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray(),
            Edges = edges.OrderBy(static edge => edge.From).ThenBy(static edge => edge.To).ToArray(),
            Diagnostics = diagnostics
        };
    }

    private static GraphNode CreateNode(ProjectInstance project, string repoRoot, IReadOnlyList<string> requestedTargets)
    {
        Dictionary<string, string> evaluated = new(StringComparer.OrdinalIgnoreCase);
        foreach (string propertyName in s_identityProperties)
        {
            string value = project.GetPropertyValue(propertyName);
            if (!string.IsNullOrEmpty(value))
            {
                evaluated[propertyName] = value;
            }
        }

        Dictionary<string, string> globalProperties = CopyProperties(project.GlobalProperties);
        string key = Identity.NodeKey(project.FullPath, globalProperties, requestedTargets);
        string relativePath = Paths.Relativize(project.FullPath, repoRoot);
        string suffix = string.Join(", ", evaluated.Where(static pair => pair.Key is "TargetFramework" or "RuntimeIdentifier" or "Configuration")
            .Select(static pair => $"{pair.Key}={pair.Value}"));

        return new GraphNode
        {
            Id = key,
            ProjectPath = project.FullPath,
            RelativePath = relativePath,
            RequestedTargets = requestedTargets,
            DefaultTargets = project.DefaultTargets.ToArray(),
            GlobalProperties = globalProperties,
            EvaluatedProperties = evaluated,
            IdentityKey = key,
            DisplayName = string.IsNullOrEmpty(suffix) ? relativePath : $"{relativePath} [{suffix}]"
        };
    }

    private static ProjectItemInstance? FindProjectReference(ProjectInstance source, string targetPath)
    {
        string normalizedTarget = Path.GetFullPath(targetPath);
        return source.GetItems("ProjectReference")
            .FirstOrDefault(item => string.Equals(Path.GetFullPath(item.GetMetadataValue("FullPath")), normalizedTarget, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> CopyProperties(IDictionary<string, string> properties) =>
        properties.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> ProjectReferenceMetadata(ProjectItemInstance item)
    {
        string[] names =
        [
            "Targets",
            "AdditionalProperties",
            "GlobalPropertiesToRemove",
            "SetTargetFramework",
            "SetPlatform",
            "SetConfiguration",
            "BuildReference",
            "ReferenceOutputAssembly",
            "SkipGetTargetFrameworkProperties",
            "OutputItemType",
            "FullPath"
        ];

        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Identity"] = item.EvaluatedInclude
        };
        foreach (string name in names)
        {
            string value = item.GetMetadataValue(name);
            if (!string.IsNullOrEmpty(value))
            {
                metadata[name] = value;
            }
        }

        return metadata;
    }

    private static string[] SplitTargets(string? targets) =>
        string.IsNullOrWhiteSpace(targets)
            ? []
            : targets.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

