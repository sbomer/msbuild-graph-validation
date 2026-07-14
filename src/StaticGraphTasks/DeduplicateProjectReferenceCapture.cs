// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MSBuild.GraphValidation.Tasks;

public sealed class DeduplicateProjectReferenceCapture : Microsoft.Build.Utilities.Task
{
    [Required]
    public ITaskItem[] Items { get; set; } = [];

    [Output]
    public ITaskItem[] UniqueItems { get; private set; } = [];

    public override bool Execute()
    {
        HashSet<CaptureKey> keys = [];
        List<ITaskItem> uniqueItems = new(Items.Length);

        foreach (ITaskItem item in Items)
        {
            if (keys.Add(new CaptureKey(item)))
            {
                uniqueItems.Add(new TaskItem(item));
            }
        }

        UniqueItems = uniqueItems.ToArray();
        return true;
    }

    private readonly record struct CaptureKey(
        string ParentProject,
        string ParentTargetFramework,
        string ReferencedProject,
        string SetTargetFramework)
    {
        public CaptureKey(ITaskItem item)
            : this(
                item.GetMetadata("CaptureParentProject"),
                item.GetMetadata("CaptureParentTargetFramework"),
                item.GetMetadata("CaptureReferencedProject"),
                item.GetMetadata("CaptureSetTargetFramework"))
        {
        }
    }
}
