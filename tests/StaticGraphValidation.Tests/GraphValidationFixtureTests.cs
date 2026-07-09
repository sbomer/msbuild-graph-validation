using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace StaticGraphValidation.Tests;

public sealed class GraphValidationFixtureTests
{
    [Fact]
    public async Task SingleProjectWithNoProjectReferencesMatchesStaticGraph()
    {
        string repoRoot = FindRepoRoot();
        string fixtureRoot = Path.Combine(repoRoot, "tests", "StaticGraphValidation.Tests", "TestProjects", "single-project-no-references");
        string projectPath = Path.Combine(fixtureRoot, "build.proj");
        string outputRoot = Path.Combine(repoRoot, "artifacts", "tmp", "tests", nameof(SingleProjectWithNoProjectReferencesMatchesStaticGraph));

        if (Directory.Exists(outputRoot))
        {
            Directory.Delete(outputRoot, recursive: true);
        }

        Directory.CreateDirectory(outputRoot);

        string dynamicBinlog = Path.Combine(outputRoot, "dynamic.binlog");
        string dynamicJson = Path.Combine(outputRoot, "dynamic.json");
        string phase1Json = Path.Combine(outputRoot, "phase1.json");
        string staticJson = Path.Combine(outputRoot, "static.json");
        string comparisonText = Path.Combine(outputRoot, "comparison.txt");

        await Run(repoRoot, "dotnet", ["msbuild", projectPath, "/t:Build", $"/bl:{dynamicBinlog}", "/nologo", "/v:minimal"]);
        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "dynamic", dynamicBinlog, "--repo-root", fixtureRoot, "-o", dynamicJson]);
        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "static", projectPath, "--repo-root", fixtureRoot, "-t", "Build", "-o", staticJson]);

        await File.WriteAllTextAsync(phase1Json, """
            {
              "Schema": "static-graph-validation.v1",
              "Kind": "dynamic",
              "Nodes": [],
              "Edges": [],
              "Diagnostics": []
            }
            """);

        await Run(repoRoot, "dotnet", ["run", "--no-build", "--project", Path.Combine(repoRoot, "src", "StaticGraphValidation"), "--", "compare-replay", "--dynamic", dynamicJson, "--phase1", phase1Json, "--static", staticJson, "-o", comparisonText]);

        using JsonDocument dynamicCapture = JsonDocument.Parse(await File.ReadAllTextAsync(dynamicJson));
        using JsonDocument staticCapture = JsonDocument.Parse(await File.ReadAllTextAsync(staticJson));

        Assert.Equal(1, dynamicCapture.RootElement.GetProperty("Nodes").GetArrayLength());
        Assert.Equal(0, dynamicCapture.RootElement.GetProperty("Edges").GetArrayLength());
        Assert.Equal(1, staticCapture.RootElement.GetProperty("Nodes").GetArrayLength());
        Assert.Equal(0, staticCapture.RootElement.GetProperty("Edges").GetArrayLength());
    }

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

    private static async Task Run(string workingDirectory, string fileName, string[] arguments)
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
            process.ExitCode == 0,
            $"""
            Command failed with exit code {process.ExitCode}: {fileName} {string.Join(' ', arguments)}

            stdout:
            {standardOutput}

            stderr:
            {standardError}
            """);
    }
}
