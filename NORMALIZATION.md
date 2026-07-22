# Normalization methodology

The comparison tool normalizes each binlog into a graph of project target results,
then compares the normalized left and right graphs.

## Input model

Each binlog is first converted to a `GraphCapture` JSON by walking Structured
Logger project nodes. A captured node contains:

- project path
- requested targets from the binlog project invocation
- default targets resolved by re-evaluating the project with captured global properties
- global properties from the project invocation
- project-to-project edges reconstructed from the binlog tree

## Node identity

Normalized node identity is:

```text
ProjectPath + Target + GlobalProperties
```

### ProjectPath

The absolute project path is normalized to use `/` separators. Text output uses
a path relative to `--repo-root` when that option is supplied.

### Target

The tool computes effective targets first:

```text
effective targets = RequestedTargets if non-empty, otherwise DefaultTargets
```

Then each effective target becomes a separate normalized node. For example:

```text
RequestedTargets = Pack;GetPackageFiles
```

normalizes to two target-result nodes:

```text
Target = Pack
Target = GetPackageFiles
```

This intentionally compares target results rather than requested-target batches.
MSBuild graph builds may schedule `A;B` together while dynamic builds request
`A` and `B` separately. Downstream MSBuild task result lookup consumes target
results, so splitting batched requests makes the comparison answer: "are the
same target results available for the same project instance?"

If `RequestedTargets` is empty and `DefaultTargets` cannot be resolved, the
target remains empty. The tool does not guess that an empty target list means
`Build`; it emits a warning diagnostic when default-target resolution fails.

### GlobalProperties

Global properties are part of normalized node identity. This is important because
MSBuild project identity includes global properties.

The current ignored global properties are:

```text
IsGraphBuild
ProjectReferenceReplayTargets
UseProjectReferenceReplay
```

`IsGraphBuild` is ignored because it is an expected difference between normal
and graph builds and does not represent a user-requested project instance
difference. The replay properties are ignored because they configure the
validation mechanism rather than the evaluated project instance being compared.

The target `GetTargetFrameworksWithPlatformForSingleTargetFramework` is also
currently excluded because replay-pruned target frameworks can produce expected
differences in this query target.

No other property whitelist is applied. Properties such as `Configuration`,
`TargetFramework`, `RuntimeIdentifier`, `Platform`, `PackageTargetRuntime`, and
custom properties still participate in identity unless explicitly normalized in
the future.

## Edge identity

Normalized edge identity is:

```text
NormalizedFromNode + NormalizedToNode
```

Because multi-target nodes are split into per-target nodes, edges involving a
multi-target project invocation become the cross-product of normalized source
and target result nodes.

## Evaluated project instance counts

The comparison report separately counts evaluated project instances using:

```text
ProjectPath + GlobalProperties
```

This count does not include targets, so it remains useful when target-result
nodes are filtered or split during normalization.

## Text output

`normalize <capture.json> --output-dir <dir>` writes:

```text
nodes.txt
edges.txt
```

`nodes.txt` is grouped for human-readable full-context diffs:

```text
Project: /path/to/project.proj
  Target: Build
    Globals: Configuration=Debug;...
  Target: Pack
    Globals: Configuration=Release;...
```

`edges.txt` is one canonical sorted line per edge:

```text
From=Project=...|Target=...|GlobalProperties=...|To=Project=...|Target=...|GlobalProperties=...
```

For investigation, generate full-context diffs:

```bash
diff -U 1000000 left-normalized/nodes.txt right-normalized/nodes.txt > nodes.diff
diff -U 1000000 left-normalized/edges.txt right-normalized/edges.txt > edges.diff
```

## Interpreting common differences

### Empty targets vs explicit `Build`

Normal binlogs may record an entry project with empty requested targets, while
graph builds may materialize the default target as `Build`. If default target
resolution succeeds and the default is `Build`, both normalize to `Target=Build`.

### Execution-time `ProjectReference` metadata mutation

If a target mutates `ProjectReference.AdditionalProperties`, dynamic MSBuild task
invocations can pass additional global properties to child projects. Static graph
construction happens from evaluation-time project references and will not see
execution-time metadata mutations. Those differences remain visible because the
resulting child global properties are part of normalized identity.

### Batched target lists

Static graph may schedule combined requested target lists such as:

```text
Pack;GetPackageIdentity;GetPackageFiles;DetermineRuntimeDependencies
```

while dynamic builds invoke the same targets separately. The normalization splits
these into individual target result nodes, so target-result availability can
match even when requested-target batching differs.
