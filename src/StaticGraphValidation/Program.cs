// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;

if (args.Length == 0)
{
    CommandLine.PrintUsage();
    return 1;
}

string? msbuildPath = Environment.GetEnvironmentVariable("MSBUILD_GRAPH_VALIDATION_MSBUILD_PATH");
if (string.IsNullOrWhiteSpace(msbuildPath))
{
    msbuildPath = ResolveRepoMSBuildPath(Directory.GetCurrentDirectory());
}

if (string.IsNullOrWhiteSpace(msbuildPath))
{
    MSBuildLocator.RegisterDefaults();
}
else
{
    MSBuildLocator.RegisterMSBuildPath(msbuildPath);
}

return Run(args);

static string? FindDotNetRoot(string start)
{
    for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, ".dotnet");
        if (Directory.Exists(Path.Combine(candidate, "sdk")))
        {
            return candidate;
        }
    }

    return null;
}

static string? ResolveRepoMSBuildPath(string start)
{
    string? dotnetRoot = FindDotNetRoot(start);
    if (dotnetRoot is null)
    {
        return null;
    }

    string sdkRoot = Path.Combine(dotnetRoot, "sdk");
    if (!Directory.Exists(sdkRoot))
    {
        return null;
    }

    string? globalJson = FindGlobalJson(start);
    if (globalJson is not null)
    {
        using FileStream stream = File.OpenRead(globalJson);
        using JsonDocument document = JsonDocument.Parse(stream);
        if (document.RootElement.TryGetProperty("sdk", out JsonElement sdk) &&
            sdk.TryGetProperty("version", out JsonElement versionElement))
        {
            string? version = versionElement.GetString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                string candidate = Path.Combine(sdkRoot, version);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
    }

    return Directory.GetDirectories(sdkRoot)
        .OrderBy(static path => SdkVersionSortKey(Path.GetFileName(path)), StringComparer.OrdinalIgnoreCase)
        .LastOrDefault();
}

static string? FindGlobalJson(string start)
{
    for (DirectoryInfo? dir = new(start); dir is not null; dir = dir.Parent)
    {
        string candidate = Path.Combine(dir.FullName, "global.json");
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}

static string SdkVersionSortKey(string version)
{
    string[] parts = version.Split(['.', '-']);
    return string.Join(".", parts.Select(static part => int.TryParse(part, out int value) ? value.ToString("D8") : part));
}

[MethodImpl(MethodImplOptions.NoInlining)]
static int Run(string[] args) => CommandLine.Run(args);

