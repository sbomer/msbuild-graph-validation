// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.GraphValidation.Tasks;

public sealed class DeduplicateItems : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] Items { get; set; } = [];

    public string MetadataNames { get; set; } = "";

    [Output]
    public ITaskItem[] DeduplicatedItems { get; private set; } = [];

    public override bool Execute()
    {
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        List<ITaskItem> deduplicated = [];

        foreach (ITaskItem item in Items)
        {
            string key = string.Join(
                "|",
                MetadataNames.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(name => item.GetMetadata(name)));
            if (seen.Add(key))
            {
                deduplicated.Add(new TaskItem(item));
            }
        }

        DeduplicatedItems = deduplicated.ToArray();
        return true;
    }
}
