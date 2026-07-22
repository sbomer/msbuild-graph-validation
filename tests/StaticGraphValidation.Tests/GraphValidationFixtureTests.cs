using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace StaticGraphValidation.Tests;

public sealed class GraphValidationFixtureTests
{
    private sealed record GraphCounts(int DynamicNodes, int DynamicEdges, int StaticNodes, int StaticEdges);
    private sealed record DiffLine(char Prefix, string Text);

    [Fact]
    public async Task SingleProjectWithNoProjectReferencesExplicitBuildMatchesStaticGraph()
    {
        GraphCounts counts = await RunGraphValidationFixture(
            "single-project-no-references",
            "build.proj",
            nameof(SingleProjectWithNoProjectReferencesExplicitBuildMatchesStaticGraph),
            entryTargets: ["Build"]);

        Assert.Equal(new GraphCounts(1, 0, 1, 0), counts);
    }

    [Fact]
    public async Task SingleProjectWithNoProjectReferencesDefaultTargetsMatchStaticGraph()
    {
        GraphCounts counts = await RunGraphValidationFixture(
            "single-project-no-references",
            "build.proj",
            nameof(SingleProjectWithNoProjectReferencesDefaultTargetsMatchStaticGraph),
            entryTargets: []);

        Assert.Equal(new GraphCounts(1, 0, 1, 0), counts);
    }

    [Fact]
    public async Task EmptyEntryTargetsNormalizeToDefaultTargets()
    {
        (string dynamicJson, string staticJson, _) = await RunGraphValidationFixture(
            "single-project-no-references",
            "build.proj",
            nameof(EmptyEntryTargetsNormalizeToDefaultTargets),
            dynamicEntryTargets: [],
            staticEntryTargets: ["Build"],
            expectedComparisonExitCode: 0);

        Assert.Equal([], RequestedTargets(dynamicJson, "build.proj"));
        Assert.Equal(["Build"], RequestedTargets(staticJson, "build.proj"));
    }

    [Fact]
    public async Task GraphBuildDefaultTargetsAreRecordedAsExplicitBuildTargets()
    {
        (string dynamicJson, string graphBuildJson, _) = await RunGraphBuildBinlogComparison(
            "single-project-no-references",
            "build.proj",
            nameof(GraphBuildDefaultTargetsAreRecordedAsExplicitBuildTargets),
            isolateProjects: true,
            expectedComparisonExitCode: 0);

        Assert.Equal([], RequestedTargets(dynamicJson, "build.proj"));
        Assert.Equal(["Build"], RequestedTargets(graphBuildJson, "build.proj"));
    }

    [Fact]
    public async Task GraphBuildDefaultTargetsAreNotPropagatedToNestedDefaultTargetBuilds()
    {
        (string dynamicJson, string graphBuildJson, _) = await RunGraphBuildBinlogComparison(
            "single-project-reference",
            "root.proj",
            nameof(GraphBuildDefaultTargetsAreNotPropagatedToNestedDefaultTargetBuilds),
            isolateProjects: false,
            expectedComparisonExitCode: 0);

        Assert.Equal([], RequestedTargets(dynamicJson, "root.proj"));
        Assert.Equal([], RequestedTargets(dynamicJson, "referenced.proj"));
        Assert.Equal(["Build"], RequestedTargets(graphBuildJson, "root.proj"));
        Assert.Equal([], RequestedTargets(graphBuildJson, "referenced.proj"));
    }

    [Fact]
    public async Task IsolatedGraphBuildDefaultProjectReferenceTargetsAddExplicitReferencedBuildNode()
    {
        (string dynamicJson, string graphBuildJson, _) = await RunGraphBuildBinlogComparison(
            "graph-build-reference-default-target",
            "root.proj",
            nameof(IsolatedGraphBuildDefaultProjectReferenceTargetsAddExplicitReferencedBuildNode),
            isolateProjects: true,
            expectedComparisonExitCode: 0);

        Assert.Equal([TargetList()], RequestedTargetLists(dynamicJson, "root.proj"));
        Assert.Equal([TargetList()], RequestedTargetLists(dynamicJson, "referenced.proj"));
        Assert.Equal([TargetList("Build")], RequestedTargetLists(graphBuildJson, "root.proj"));
        Assert.Equal([TargetList(), TargetList("Build")], RequestedTargetLists(graphBuildJson, "referenced.proj"));
    }

    [Fact]
    public async Task DynamicProjectReferenceAdditionalPropertiesAreNotVisibleToGraphBuild()
    {
        string output = await RunIsolatedGraphBuildExpectingFailure(
            "dynamic-project-reference-additional-properties",
            "root.proj",
            nameof(DynamicProjectReferenceAdditionalPropertiesAreNotVisibleToGraphBuild));

        Assert.Contains("MSB4252", output);
        Assert.Contains("DynamicProperty=from-target", output);
        Assert.Contains("build result for the built project is not in the engine cache", output);
    }

    [Fact]
    public async Task DynamicallyAddedProjectReferenceIsNotVisibleToIsolatedGraphBuild()
    {
        string output = await RunIsolatedGraphBuildExpectingFailure(
            "dynamic-project-reference-item",
            "root.proj",
            nameof(DynamicallyAddedProjectReferenceIsNotVisibleToIsolatedGraphBuild));

        Assert.Contains("MSB4252", output);
        Assert.Contains("referenced.proj", output);
        Assert.Contains("not explicitly specified as a ProjectReference item", output);
    }

    [Fact]
    public async Task SeparateDynamicTargetInvocationsDifferFromBatchedGraphTargets()
    {
        (string dynamicJson, string graphBuildJson, string comparisonText) = await RunGraphBuildBinlogComparison(
            "target-batching-difference",
            "root.proj",
            nameof(SeparateDynamicTargetInvocationsDifferFromBatchedGraphTargets),
            isolateProjects: true,
            expectedComparisonExitCode: 0);

        string comparison = await File.ReadAllTextAsync(comparisonText);
        Assert.Contains("Left-only nodes: 0", comparison);
        Assert.Contains("Right-only nodes: 0", comparison);

        // TargetA depends on TargetB, so the explicit TargetB request is a cache hit that must still be captured.
        Assert.Equal([TargetList("TargetA"), TargetList("TargetB")], RequestedTargetLists(dynamicJson, "referenced.proj"));
        Assert.Equal([TargetList("TargetA"), TargetList("TargetA", "TargetB"), TargetList("TargetB")], RequestedTargetLists(graphBuildJson, "referenced.proj"));
    }

    [Fact]
    public async Task StaticGraphExpandsMultitargetedProjectReferenceBeyondRequestedParentTargetFramework()
    {
        (string dynamicJson, string graphBuildJson, string comparisonText) = await RunGraphBuildBinlogComparison(
            "multitargeted-project-reference",
            "root.proj",
            nameof(StaticGraphExpandsMultitargetedProjectReferenceBeyondRequestedParentTargetFramework),
            isolateProjects: true,
            expectedComparisonExitCode: 2,
            restoreProject: "parent/parent.csproj");

        string comparison = await File.ReadAllTextAsync(comparisonText);
        Assert.Contains("Right-only nodes: 5", comparison);
        Assert.Contains("child/child.csproj|target=Build|globals=Restore=false;TargetFramework=net9.0", comparison);
        Assert.Contains("child/child.csproj|target=GetCopyToOutputDirectoryItems|globals=Restore=false;TargetFramework=net9.0", comparison);
        Assert.Contains("child/child.csproj|target=GetNativeManifest|globals=Restore=false;TargetFramework=net9.0", comparison);

        Assert.Contains(("child/child.csproj", "Build", "net8.0"), CapturedTargets(dynamicJson));
        Assert.DoesNotContain(("child/child.csproj", "Build", "net9.0"), CapturedTargets(dynamicJson));
        Assert.Contains(("child/child.csproj", "Build", "net9.0"), CapturedTargets(graphBuildJson));
    }

    [Fact]
    public async Task ExplicitSetTargetFrameworkIsCapturedButStaticGraphStillCreatesOuterBuild()
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", "multitargeted-project-reference");
        string projectPath = Path.Combine(fixtureRoot, "explicit/explicit.csproj");
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(ExplicitSetTargetFrameworkIsCapturedButStaticGraphStillCreatesOuterBuild));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);
        string captureFile = Path.Combine(outputRoot, "project-references.txt");

        await Run(repoRoot, "dotnet", ["msbuild", projectPath, "/t:Restore", "/nologo", "/v:minimal"]);
        await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                projectPath,
                "/t:CaptureProjectReferencesRecursive",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false",
                "/p:CaptureProjectReferences=true",
                $"/p:CaptureProjectReferencesFile={captureFile}",
                $"/p:CustomAfterMicrosoftCommonTargets={Path.Combine(fixtureRoot, "capture-project-references.targets")}"
            ]);

        Assert.Contains(
            await File.ReadAllLinesAsync(captureFile),
            line => line.Contains("child/child.csproj", StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith("|TargetFramework=net8.0", StringComparison.Ordinal));

        (string dynamicJson, string staticJson, _) = await RunGraphValidationFixture(
            "multitargeted-project-reference",
            "explicit/explicit.csproj",
            nameof(ExplicitSetTargetFrameworkIsCapturedButStaticGraphStillCreatesOuterBuild),
            dynamicEntryTargets: ["Build"],
            staticEntryTargets: ["Build"],
            expectedComparisonExitCode: 2);

        HashSet<(string Project, string Target, string TargetFramework)> dynamicTargets = CapturedTargets(dynamicJson);
        HashSet<(string Project, string Target, string TargetFramework)> staticTargets = CapturedTargets(staticJson);

        Assert.Contains(("child/child.csproj", "Build", "net8.0"), dynamicTargets);
        Assert.DoesNotContain(("child/child.csproj", "GetTargetFrameworks", ""), dynamicTargets);
        Assert.Contains(("child/child.csproj", "Build", "net8.0"), staticTargets);
        Assert.Contains(("child/child.csproj", "GetTargetFrameworks", ""), staticTargets);
    }

    [Fact]
    public async Task CaptureProjectReferencesRecordsSdkSelectedTargetFramework()
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", "multitargeted-project-reference");
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(CaptureProjectReferencesRecordsSdkSelectedTargetFramework));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);
        string captureFile = Path.Combine(outputRoot, "project-references.txt");

        await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, "parent/parent.csproj"), "/t:Restore", "/nologo", "/v:minimal"]);
        await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                Path.Combine(fixtureRoot, "parent/parent.csproj"),
                "/t:CaptureProjectReferencesRecursive",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false",
                "/p:TargetFramework=net8.0",
                "/p:CaptureProjectReferences=true",
                $"/p:CaptureProjectReferencesFile={captureFile}",
                $"/p:CustomAfterMicrosoftCommonTargets={Path.Combine(fixtureRoot, "capture-project-references.targets")}"
            ]);

        string[] lines = await File.ReadAllLinesAsync(captureFile);
        Assert.Contains(
            lines,
            line => line.Contains("child/child.csproj", StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith("|TargetFramework=net8.0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StaticGraphCanUseCapturedProjectReferencesToConstrainTargetFrameworks()
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", "multitargeted-project-reference");
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(StaticGraphCanUseCapturedProjectReferencesToConstrainTargetFrameworks));
        string replayRoot = Path.Combine(repoRoot, "artifacts", "obj", "static-graph", "tests", nameof(StaticGraphCanUseCapturedProjectReferencesToConstrainTargetFrameworks));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        if (Directory.Exists(replayRoot))
        {
            Directory.Delete(replayRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);
        Directory.CreateDirectory(replayRoot);
        string captureFile = Path.Combine(replayRoot, "project-references.raw.txt");
        string replayTargets = Path.Combine(replayRoot, "project-references.replay.targets");
        string graphBuildBinlog = Path.Combine(outputRoot, "graph-build.binlog");
        string graphBuildJson = Path.Combine(outputRoot, "graph-build.json");

        await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, "parent/parent.csproj"), "/t:Restore", "/nologo", "/v:minimal"]);
        string output = await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                Path.Combine(fixtureRoot, "parent/parent.csproj"),
                "/t:CaptureProjectReferencesRecursive",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false",
                "/p:TargetFramework=net8.0",
                "/p:CaptureProjectReferences=true",
                $"/p:CaptureProjectReferencesFile={captureFile}",
                $"/p:CustomAfterMicrosoftCommonTargets={Path.Combine(fixtureRoot, "capture-project-references.targets")}"
            ]);

        string generatorProject = Path.Combine(replayRoot, "generate.proj");
        await File.WriteAllTextAsync(generatorProject, $"""
            <Project>
              <Import Project="{Path.Combine(repoRoot, "targets/staticGraphTargetFrameworkSelection.targets")}" />
            </Project>
            """);

        await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                generatorProject,
                "/t:GenerateStaticGraphSelectedTargetFrameworks",
                "/nologo",
                "/v:minimal",
                $"/p:StaticGraphSelectedTargetFrameworksRawFile={captureFile}",
                $"/p:StaticGraphSelectedTargetFrameworksTargets={replayTargets}",
                $"/p:StaticGraphTasksAssemblyPath={StaticGraphTasksAssemblyPath(repoRoot)}"
            ]);

        Assert.Contains("TargetFrameworks>net8.0</TargetFrameworks", await File.ReadAllTextAsync(replayTargets));

        await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, "parent/parent.csproj"), "/t:Restore", "/nologo", "/v:minimal", $"/p:ProjectReferenceReplayTargets={replayTargets}"]);
        await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                Path.Combine(fixtureRoot, "root.proj"),
                "/graphBuild",
                "/isolateProjects",
                $"/bl:{graphBuildBinlog}",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false",
                $"/p:ProjectReferenceReplayTargets={replayTargets}"
            ]);

        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "dynamic", graphBuildBinlog, "--repo-root", fixtureRoot, "-o", graphBuildJson]);

        Assert.Contains(("child/child.csproj", "Build", "net8.0"), CapturedTargets(graphBuildJson));
        Assert.DoesNotContain(("child/child.csproj", "Build", "net9.0"), CapturedTargets(graphBuildJson));
    }

    [Fact]
    public async Task StaticGraphBuildsAllTargetFrameworksForMultitargetedReferenceTree()
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", "multitargeted-tree");
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(StaticGraphBuildsAllTargetFrameworksForMultitargetedReferenceTree));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);
        string graphBuildBinlog = Path.Combine(outputRoot, "graph-build.binlog");
        string graphBuildJson = Path.Combine(outputRoot, "graph-build.json");

        await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, "root/root.csproj"), "/t:Restore", "/nologo", "/v:minimal"]);
        string output = await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                Path.Combine(fixtureRoot, "root/root.csproj"),
                "/graphBuild",
                "/isolateProjects",
                $"/bl:{graphBuildBinlog}",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false"
            ]);

        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "dynamic", graphBuildBinlog, "--repo-root", fixtureRoot, "-o", graphBuildJson]);

        HashSet<(string Project, string Target, string TargetFramework)> targets = CapturedTargets(graphBuildJson);
        Assert.Contains(("middle/middle.csproj", "Build", "net8.0"), targets);
        Assert.Contains(("middle/middle.csproj", "Build", "net9.0"), targets);
        Assert.Contains(("middle/middle.csproj", "Build", "netstandard2.0"), targets);
        Assert.Contains(("leaf/leaf.csproj", "Build", "net8.0"), targets);
        Assert.Contains(("leaf/leaf.csproj", "Build", "net9.0"), targets);
        Assert.Contains(("leaf/leaf.csproj", "Build", "netstandard2.0"), targets);
    }

    [Fact]
    public async Task CaptureProjectReferencesRecordsSharedTransitiveReferenceEdges()
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", "multitargeted-tree");
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(CaptureProjectReferencesRecordsSharedTransitiveReferenceEdges));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);
        string captureFile = Path.Combine(outputRoot, "project-references.txt");

        await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, "root/root.csproj"), "/t:Restore", "/nologo", "/v:minimal"]);
        await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                Path.Combine(fixtureRoot, "root/root.csproj"),
                "/t:CaptureProjectReferencesRecursive",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false",
                "/p:CaptureProjectReferences=true",
                $"/p:CaptureProjectReferencesTargets={Path.Combine(repoRoot, "tests/StaticGraphValidation.Tests/TestProjects/multitargeted-project-reference/capture-project-references.targets")}",
                $"/p:CaptureProjectReferencesFile={captureFile}"
            ]);

        string[] lines = await File.ReadAllLinesAsync(captureFile);
        Assert.Equal(
            0,
            lines.Length - lines.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            2,
            lines.Count(line => line.Contains("leaf/leaf.csproj", StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith("|TargetFramework=net8.0", StringComparison.Ordinal)));
        Assert.Equal(
            2,
            lines.Count(line => line.Contains("leaf/leaf.csproj", StringComparison.OrdinalIgnoreCase) &&
                line.EndsWith("|TargetFramework=net9.0", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CaptureProjectReferencesDeduplicatesReturnedItemsAtEachRecursiveStep()
    {
        string repoRoot = FindRepoRoot();
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(CaptureProjectReferencesDeduplicatesReturnedItemsAtEachRecursiveStep));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);
        string fixtureRoot = Path.Combine(outputRoot, "fixture");
        CreateDiamondFixture(fixtureRoot, depth: 24);

        string captureFile = Path.Combine(outputRoot, "project-references.txt");
        await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, "root/root.csproj"), "/t:Restore", "/nologo", "/v:minimal"]);
        string output = await Run(
            repoRoot,
            "dotnet",
            [
                "msbuild",
                Path.Combine(fixtureRoot, "root/root.csproj"),
                "/t:CaptureProjectReferencesRecursive",
                "/nologo",
                "/v:minimal",
                "/p:Restore=false",
                "/p:CaptureProjectReferences=true",
                "/p:CaptureProjectReferencesLogCounts=true",
                $"/p:CaptureProjectReferencesTargets={Path.Combine(repoRoot, "tests/StaticGraphValidation.Tests/TestProjects/multitargeted-project-reference/capture-project-references.targets")}",
                $"/p:CaptureProjectReferencesFile={captureFile}"
            ]);

        string[] lines = await File.ReadAllLinesAsync(captureFile);
        Assert.Equal(lines.Distinct(StringComparer.OrdinalIgnoreCase).Count(), lines.Length);
        Assert.Equal(1201, lines.Length);
        Assert.All(
            CaptureProjectReferencesCounts(output),
            count => Assert.InRange(count, 0, 1201));
    }

    [Fact]
    public async Task SingleProjectReferenceMatchesStaticGraph()
    {
        GraphCounts counts = await RunGraphValidationFixture(
            "single-project-reference",
            "root.proj",
            nameof(SingleProjectReferenceMatchesStaticGraph),
            entryTargets: ["Build"]);

        Assert.Equal(new GraphCounts(2, 1, 2, 1), counts);
    }

    private static async Task<GraphCounts> RunGraphValidationFixture(
        string fixtureName,
        string entryProject,
        string testName,
        string[] entryTargets)
    {
        (string dynamicJson, string staticJson, _) = await RunGraphValidationFixture(
            fixtureName,
            entryProject,
            testName,
            dynamicEntryTargets: entryTargets,
            staticEntryTargets: entryTargets,
            expectedComparisonExitCode: 0);

        using JsonDocument dynamicCapture = JsonDocument.Parse(await File.ReadAllTextAsync(dynamicJson));
        using JsonDocument staticCapture = JsonDocument.Parse(await File.ReadAllTextAsync(staticJson));

        return new GraphCounts(
            NodeCount(dynamicCapture),
            EdgeCount(dynamicCapture),
            NodeCount(staticCapture),
            EdgeCount(staticCapture));
    }

    private static async Task<(string DynamicJson, string GraphBuildJson, string ComparisonText)> RunGraphBuildBinlogComparison(
        string fixtureName,
        string entryProject,
        string testName,
        bool isolateProjects,
        int expectedComparisonExitCode,
        string? restoreProject = null)
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", fixtureName);
        string projectPath = Path.Combine(fixtureRoot, entryProject);
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", testName);

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);

        string dynamicBinlog = Path.Combine(outputRoot, "dynamic.binlog");
        string graphBuildBinlog = Path.Combine(outputRoot, "graph-build.binlog");
        string dynamicJson = Path.Combine(outputRoot, "dynamic.json");
        string graphBuildJson = Path.Combine(outputRoot, "graph-build.json");
        string comparisonText = Path.Combine(outputRoot, "comparison.txt");

        if (restoreProject is not null)
        {
            await Run(repoRoot, "dotnet", ["msbuild", Path.Combine(fixtureRoot, restoreProject), "/t:Restore", "/nologo", "/v:minimal"]);
        }

        await Run(repoRoot, "dotnet", ["msbuild", projectPath, $"/bl:{dynamicBinlog}", "/nologo", "/v:minimal", "/p:Restore=false"]);

        List<string> graphBuildArguments = ["msbuild", projectPath, "/graphBuild", $"/bl:{graphBuildBinlog}", "/nologo", "/v:minimal", "/p:Restore=false"];
        if (isolateProjects)
        {
            graphBuildArguments.Insert(3, "/isolateProjects");
        }

        await Run(repoRoot, "dotnet", graphBuildArguments);
        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "dynamic", dynamicBinlog, "--repo-root", fixtureRoot, "-o", dynamicJson]);
        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "dynamic", graphBuildBinlog, "--repo-root", fixtureRoot, "-o", graphBuildJson]);
        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "compare", "--left", dynamicJson, "--right", graphBuildJson, "-o", comparisonText], expectedComparisonExitCode);
        await WriteAndVerifyExpectedDiffs(
            repoRoot,
            fixtureRoot,
            testName,
            outputRoot,
            dynamicJson,
            "dynamic",
            graphBuildJson,
            "graph-build");

        return (dynamicJson, graphBuildJson, comparisonText);
    }

    private static async Task<string> RunIsolatedGraphBuildExpectingFailure(
        string fixtureName,
        string entryProject,
        string testName)
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", fixtureName);
        string projectPath = Path.Combine(fixtureRoot, entryProject);
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", testName);

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);

        string graphBuildBinlog = Path.Combine(outputRoot, "graph-build.binlog");
        return await Run(
            repoRoot,
            "dotnet",
            ["msbuild", projectPath, "/graphBuild", "/isolateProjects", $"/bl:{graphBuildBinlog}", "/nologo", "/v:minimal"],
            expectedExitCode: 1);
    }

    private static async Task<(string DynamicJson, string StaticJson, string ComparisonText)> RunGraphValidationFixture(
        string fixtureName,
        string entryProject,
        string testName,
        string[] dynamicEntryTargets,
        string[] staticEntryTargets,
        int expectedComparisonExitCode)
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", fixtureName);
        string projectPath = Path.Combine(fixtureRoot, entryProject);
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", testName);

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);

        string dynamicBinlog = Path.Combine(outputRoot, "dynamic.binlog");
        string dynamicJson = Path.Combine(outputRoot, "dynamic.json");
        string staticJson = Path.Combine(outputRoot, "static.json");
        string comparisonText = Path.Combine(outputRoot, "comparison.txt");

        List<string> dynamicArguments = ["msbuild", projectPath, $"/bl:{dynamicBinlog}", "/nologo", "/v:minimal"];
        if (dynamicEntryTargets.Length > 0)
        {
            dynamicArguments.Insert(2, $"/t:{string.Join(';', dynamicEntryTargets)}");
        }

        await Run(repoRoot, "dotnet", dynamicArguments);
        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "dynamic", dynamicBinlog, "--repo-root", fixtureRoot, "-o", dynamicJson]);

        List<string> staticArguments = ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "static", projectPath, "--repo-root", fixtureRoot, "-o", staticJson];
        if (staticEntryTargets.Length > 0)
        {
            staticArguments.InsertRange(staticArguments.Count - 2, ["-t", string.Join(';', staticEntryTargets)]);
        }

        await Run(repoRoot, "dotnet", staticArguments);

        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "compare", "--left", dynamicJson, "--right", staticJson, "-o", comparisonText], expectedComparisonExitCode);
        await WriteAndVerifyExpectedDiffs(
            repoRoot,
            fixtureRoot,
            testName,
            outputRoot,
            dynamicJson,
            "dynamic",
            staticJson,
            "static");

        return (dynamicJson, staticJson, comparisonText);
    }

    private static async Task WriteAndVerifyExpectedDiffs(
        string repoRoot,
        string fixtureRoot,
        string testName,
        string outputRoot,
        string leftCapture,
        string leftName,
        string rightCapture,
        string rightName)
    {
        string leftNormalized = Path.Combine(outputRoot, $"{leftName}-normalized");
        string rightNormalized = Path.Combine(outputRoot, $"{rightName}-normalized");

        await Run(
            repoRoot,
            "dotnet",
            [
                "run",
                "--no-build",
                "--project",
                Path.Combine(repoRoot, "src", "StaticGraphValidation"),
                "--",
                "normalize",
                leftCapture,
                "--repo-root",
                fixtureRoot,
                "--output-dir",
                leftNormalized
            ]);
        await Run(
            repoRoot,
            "dotnet",
            [
                "run",
                "--no-build",
                "--project",
                Path.Combine(repoRoot, "src", "StaticGraphValidation"),
                "--",
                "normalize",
                rightCapture,
                "--repo-root",
                fixtureRoot,
                "--output-dir",
                rightNormalized
            ]);

        foreach (string fileName in new[] { "nodes.txt", "edges.txt" })
        {
            string actualDiffPath = Path.Combine(outputRoot, Path.ChangeExtension(fileName, ".diff"));
            string actualDiff = CreateDiff(
                await File.ReadAllLinesAsync(Path.Combine(leftNormalized, fileName)),
                await File.ReadAllLinesAsync(Path.Combine(rightNormalized, fileName)),
                $"{leftName}-normalized/{fileName}",
                $"{rightName}-normalized/{fileName}");
            await File.WriteAllTextAsync(actualDiffPath, actualDiff);

            string expectedDiffPath = Path.Combine(
                repoRoot,
                "tests",
                "StaticGraphValidation.Tests",
                "ExpectedDiffs",
                testName,
                Path.ChangeExtension(fileName, ".diff"));

            if (Environment.GetEnvironmentVariable("UPDATE_EXPECTED_DIFFS") == "1")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(expectedDiffPath)!);
                await File.WriteAllTextAsync(expectedDiffPath, actualDiff);
            }

            Assert.True(
                File.Exists(expectedDiffPath),
                $"Missing expected diff '{expectedDiffPath}'. Run with UPDATE_EXPECTED_DIFFS=1 to create it.");
            Assert.Equal(
                NormalizeLineEndings(await File.ReadAllTextAsync(expectedDiffPath)),
                NormalizeLineEndings(actualDiff));
        }
    }

    private static string CreateDiff(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right,
        string leftName,
        string rightName)
    {
        int[,] commonSuffixLengths = new int[left.Count + 1, right.Count + 1];
        for (int leftIndex = left.Count - 1; leftIndex >= 0; leftIndex--)
        {
            for (int rightIndex = right.Count - 1; rightIndex >= 0; rightIndex--)
            {
                commonSuffixLengths[leftIndex, rightIndex] =
                    StringComparer.Ordinal.Equals(left[leftIndex], right[rightIndex])
                        ? commonSuffixLengths[leftIndex + 1, rightIndex + 1] + 1
                        : Math.Max(
                            commonSuffixLengths[leftIndex + 1, rightIndex],
                            commonSuffixLengths[leftIndex, rightIndex + 1]);
            }
        }

        List<DiffLine> lines = [];
        int i = 0;
        int j = 0;
        bool hasDifferences = false;
        while (i < left.Count || j < right.Count)
        {
            if (i < left.Count && j < right.Count && StringComparer.Ordinal.Equals(left[i], right[j]))
            {
                lines.Add(new DiffLine(' ', left[i]));
                i++;
                j++;
            }
            else if (j == right.Count ||
                (i < left.Count && commonSuffixLengths[i + 1, j] >= commonSuffixLengths[i, j + 1]))
            {
                lines.Add(new DiffLine('-', left[i]));
                i++;
                hasDifferences = true;
            }
            else
            {
                lines.Add(new DiffLine('+', right[j]));
                j++;
                hasDifferences = true;
            }
        }

        if (!hasDifferences)
        {
            return "";
        }

        StringBuilder builder = new();
        builder.AppendLine($"--- {leftName}");
        builder.AppendLine($"+++ {rightName}");
        builder.AppendLine($"@@ -{DiffRange(left.Count)} +{DiffRange(right.Count)} @@");
        foreach (DiffLine line in lines)
        {
            builder.Append(line.Prefix).AppendLine(line.Text);
        }

        return builder.ToString();
    }

    private static string DiffRange(int count) => count == 0 ? "0,0" : $"1,{count}";

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static int NodeCount(JsonDocument capture) => capture.RootElement.GetProperty("Nodes").GetArrayLength();

    private static int EdgeCount(JsonDocument capture) => capture.RootElement.GetProperty("Edges").GetArrayLength();

    private static string[] RequestedTargets(string capturePath, string relativePath)
    {
        using JsonDocument capture = JsonDocument.Parse(File.ReadAllText(capturePath));
        JsonElement node = capture.RootElement
            .GetProperty("Nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("RelativePath").GetString() == relativePath);

        return node.GetProperty("RequestedTargets")
            .EnumerateArray()
            .Select(target => target.GetString()!)
            .ToArray();
    }

    private static string[] RequestedTargetLists(string capturePath, string relativePath)
    {
        using JsonDocument capture = JsonDocument.Parse(File.ReadAllText(capturePath));
        return capture.RootElement
            .GetProperty("Nodes")
            .EnumerateArray()
            .Where(node => node.GetProperty("RelativePath").GetString() == relativePath)
            .Select(node => TargetList(node.GetProperty("RequestedTargets")
                .EnumerateArray()
                .Select(target => target.GetString()!)
                .ToArray()))
            .OrderBy(static targets => targets, StringComparer.Ordinal)
            .ToArray();
    }

    private static string? GlobalProperty(string capturePath, string relativePath, string propertyName)
    {
        using JsonDocument capture = JsonDocument.Parse(File.ReadAllText(capturePath));
        JsonElement node = capture.RootElement
            .GetProperty("Nodes")
            .EnumerateArray()
            .Single(node => node.GetProperty("RelativePath").GetString() == relativePath);

        return node.GetProperty("GlobalProperties").TryGetProperty(propertyName, out JsonElement property)
            ? property.GetString()
            : null;
    }

    private static HashSet<(string Project, string Target, string TargetFramework)> CapturedTargets(string capturePath)
    {
        using JsonDocument capture = JsonDocument.Parse(File.ReadAllText(capturePath));
        HashSet<(string Project, string Target, string TargetFramework)> result = [];
        foreach (JsonElement node in capture.RootElement.GetProperty("Nodes").EnumerateArray())
        {
            string project = node.GetProperty("RelativePath").GetString()!;
            string targetFramework = node.GetProperty("GlobalProperties").TryGetProperty("TargetFramework", out JsonElement targetFrameworkElement)
                ? targetFrameworkElement.GetString() ?? ""
                : "";
            string[] targets = node.GetProperty("RequestedTargets").EnumerateArray().Select(static target => target.GetString()!).ToArray();
            if (targets.Length == 0)
            {
                targets = node.GetProperty("DefaultTargets").EnumerateArray().Select(static target => target.GetString()!).ToArray();
            }

            foreach (string target in targets)
            {
                result.Add((project, target, targetFramework));
            }
        }

        return result;
    }

    private static string TargetList(params string[] targets) => string.Join(';', targets);

    private static IEnumerable<int> CaptureProjectReferencesCounts(string output)
    {
        foreach (string line in output.Split(Environment.NewLine))
        {
            const string marker = "CaptureProjectReferencesCount ";
            int markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                continue;
            }

            int countIndex = line.IndexOf("Count=", markerIndex, StringComparison.Ordinal);
            if (countIndex >= 0 && int.TryParse(line[(countIndex + "Count=".Length)..].Trim(), out int count))
            {
                yield return count;
            }
        }
    }

    private static void CreateDiamondFixture(string fixtureRoot, int depth)
    {
        Directory.CreateDirectory(fixtureRoot);
        File.WriteAllText(
            Path.Combine(fixtureRoot, "Directory.Build.props"),
            $$"""
            <Project>
              <PropertyGroup>
                <BaseOutputPath>{{fixtureRoot}}/bin/</BaseOutputPath>
                <BaseIntermediateOutputPath>{{fixtureRoot}}/obj/$(MSBuildProjectName)/</BaseIntermediateOutputPath>
              </PropertyGroup>
            </Project>
            """);

        WriteDiamondProject(Path.Combine(fixtureRoot, "leaf", "leaf.csproj"));
        for (int i = 0; i < 2; i++)
        {
            WriteDiamondProject(Path.Combine(fixtureRoot, $"l{depth}_{i}", $"l{depth}_{i}.csproj"), "../leaf/leaf.csproj");
        }

        for (int layer = depth - 1; layer >= 1; layer--)
        {
            for (int i = 0; i < 2; i++)
            {
                WriteDiamondProject(
                    Path.Combine(fixtureRoot, $"l{layer}_{i}", $"l{layer}_{i}.csproj"),
                    $"../l{layer + 1}_0/l{layer + 1}_0.csproj",
                    $"../l{layer + 1}_1/l{layer + 1}_1.csproj");
            }
        }

        WriteDiamondProject(Path.Combine(fixtureRoot, "root", "root.csproj"), "../l1_0/l1_0.csproj", "../l1_1/l1_1.csproj");
    }

    private static void WriteDiamondProject(string path, params string[] references)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string projectReferences = string.Join(
            Environment.NewLine,
            references.Select(static reference => $"""    <ProjectReference Include="{reference}" />"""));
        File.WriteAllText(
            path,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <DisableImplicitFrameworkReferences>true</DisableImplicitFrameworkReferences>
                <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
                <ImplicitUsings>false</ImplicitUsings>
                <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
                <ProduceOnlyReferenceAssembly>false</ProduceOnlyReferenceAssembly>
              </PropertyGroup>
              <ItemGroup>
            {{projectReferences}}
              </ItemGroup>
              <Import Project="$(CaptureProjectReferencesTargets)" Condition="'$(CaptureProjectReferencesTargets)' != '' and Exists('$(CaptureProjectReferencesTargets)')" />
            </Project>
            """);
    }

    private static string StaticGraphTasksAssemblyPath(string repoRoot) =>
        Directory.GetFiles(Path.Combine(repoRoot, "artifacts", "bin", "StaticGraphTasks"), "StaticGraphTasks.dll", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .First();

    private static string FindRepoRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "msbuild-graph-validation.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private static async Task<string> Run(string workingDirectory, string fileName, IReadOnlyList<string> arguments, int expectedExitCode = 0)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Failed to start '{fileName}'.");
        string standardOutput = await process.StandardOutput.ReadToEndAsync();
        string standardError = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == expectedExitCode,
            $"""
            Command failed with exit code {process.ExitCode}, expected {expectedExitCode}: {fileName} {string.Join(' ', arguments)}

            stdout:
            {standardOutput}

            stderr:
            {standardError}
            """);

        return standardOutput + standardError;
    }
}
