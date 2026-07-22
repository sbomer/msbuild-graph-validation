# MSBuild graph validation

Standalone tools for normalizing and comparing MSBuild graph projections captured from binlogs.

The repository uses .NET SDK artifacts output, so builds write under `artifacts/` instead of project-local `bin/` and `obj/` directories.

## Build

```bash
dotnet build msbuild-graph-validation.slnx
```

## Test fixtures

Add MSBuild fixture projects under `tests/StaticGraphValidation.Tests/TestProjects/<case-name>/`. Tests in `GraphValidationFixtureTests` run MSBuild with binlogs or static graph captures, normalize them, and compare graph projections.

```bash
dotnet test msbuild-graph-validation.slnx
```

Comparison tests leave their captures, normalized `nodes.txt`/`edges.txt`, and
generated diffs under `artifacts/tmp/tests/<test-name>/`. The generated
`nodes.diff` and `edges.diff` must match the checked-in files under
`tests/StaticGraphValidation.Tests/ExpectedDiffs/<test-name>/`.

To intentionally update the expected diffs:

```bash
UPDATE_EXPECTED_DIFFS=1 dotnet test msbuild-graph-validation.slnx
```

## Compare binlogs

For simple A/B comparison, convert each binlog to a capture and compare the normalized graphs:

```bash
dotnet run --project src/StaticGraphValidation -- dynamic path/to/left.binlog --repo-root path/to/repo -o artifacts/log/static-graph-validation/left.json
dotnet run --project src/StaticGraphValidation -- dynamic path/to/right.binlog --repo-root path/to/repo -o artifacts/log/static-graph-validation/right.json
dotnet run --project src/StaticGraphValidation -- compare --left artifacts/log/static-graph-validation/left.json --right artifacts/log/static-graph-validation/right.json -o artifacts/log/static-graph-validation/comparison.txt
```

Or compare binlogs directly:

```bash
dotnet run --project src/StaticGraphValidation -- compare-binlogs --left path/to/left.binlog --right path/to/right.binlog --repo-root path/to/repo -o artifacts/log/static-graph-validation/comparison.txt
```

To produce stable text files for `diff -u`, normalize each capture to a directory:

```bash
dotnet run --project src/StaticGraphValidation -- normalize artifacts/log/static-graph-validation/left.json --repo-root path/to/repo --output-dir artifacts/log/static-graph-validation/left
dotnet run --project src/StaticGraphValidation -- normalize artifacts/log/static-graph-validation/right.json --repo-root path/to/repo --output-dir artifacts/log/static-graph-validation/right
diff -u artifacts/log/static-graph-validation/left/nodes.txt artifacts/log/static-graph-validation/right/nodes.txt
diff -u artifacts/log/static-graph-validation/left/edges.txt artifacts/log/static-graph-validation/right/edges.txt
```

## Static graph capture

```bash
dotnet run --project src/StaticGraphValidation -- dynamic path/to/build.binlog --repo-root path/to/repo -o artifacts/log/static-graph-validation/dynamic.json
dotnet run --project src/StaticGraphValidation -- static path/to/Build.proj --repo-root path/to/repo -p Name=Value -o artifacts/log/static-graph-validation/static.json
dotnet run --project src/StaticGraphValidation -- compare --left dynamic.json --right static.json -o comparison.txt
```

For repos that need target-framework selection, import `targets/staticGraphTargetFrameworkSelection.targets`, build `src/StaticGraphTasks`, capture `CaptureStaticGraphSelectedTargetFrameworksRecursive`, then generate a selected-target-frameworks targets file with `GenerateStaticGraphSelectedTargetFrameworks`. By default, the raw/targets files are written under `$(ArtifactsObjDir)` when available, otherwise under the importing project's intermediate output directory.

The wrapper script does that flow and writes all generated files under this repo's `artifacts/` directory:

```bash
scripts/validate-static-graph-replay.sh /path/to/repo Build.proj /p:Configuration=Debug /p:Restore=false /p:Build=true
```
