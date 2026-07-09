# MSBuild graph validation

Standalone tools for comparing a dynamic MSBuild traversal captured from a binlog with a static graph evaluation.

The repository uses .NET SDK artifacts output, so builds write under `artifacts/` instead of project-local `bin/` and `obj/` directories.

## Build

```bash
dotnet build msbuild-graph-validation.slnx
```

## Test fixtures

Add MSBuild fixture projects under `tests/StaticGraphValidation.Tests/TestProjects/<case-name>/`. Tests in `GraphValidationFixtureTests` run MSBuild with a binlog, convert the dynamic capture, capture the static graph, and compare the graph projections.

```bash
dotnet test msbuild-graph-validation.slnx
```

## Capture and compare

The validation tool supports:

```bash
dotnet run --project src/StaticGraphValidation -- dynamic path/to/build.binlog --repo-root path/to/repo -o artifacts/log/static-graph-validation/dynamic.json
dotnet run --project src/StaticGraphValidation -- static path/to/Build.proj --repo-root path/to/repo -p Name=Value -o artifacts/log/static-graph-validation/static.json
dotnet run --project src/StaticGraphValidation -- compare-replay --dynamic dynamic.json --phase1 phase1.json --static static.json -o comparison.txt
```

For repos that need target-framework replay, import `targets/staticGraphTargetFrameworkSelection.targets`, build `src/StaticGraphTasks`, capture `CaptureStaticGraphSelectedTargetFrameworksRecursive`, then generate a selected-target-frameworks targets file with `GenerateStaticGraphSelectedTargetFrameworks`. By default, the replay raw/targets files are written under `$(ArtifactsObjDir)` when available, otherwise under the importing project's intermediate output directory.

The wrapper script does that flow and writes all generated files under this repo's `artifacts/` directory:

```bash
scripts/validate-static-graph-replay.sh /path/to/repo Build.proj /p:Configuration=Debug /p:Restore=false /p:Build=true
```
