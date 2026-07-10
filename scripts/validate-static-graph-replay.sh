#!/usr/bin/env bash
set -euo pipefail

tool_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <repo-root> <entry-project> [msbuild properties/arguments]" >&2
  exit 1
fi

repo_root="$(cd "$1" && pwd)"
entry_project="$2"
shift 2

if [[ "$entry_project" != /* ]]; then
  entry_project="$repo_root/$entry_project"
fi

configuration="${CONFIGURATION:-Debug}"
log_dir="$tool_root/artifacts/log/static-graph-validation"
obj_dir="$tool_root/artifacts/obj/static-graph-validation"
raw_file="$obj_dir/selected-target-frameworks.raw.txt"
targets_file="$obj_dir/selected-target-frameworks.targets"
tasks_dll="$tool_root/artifacts/bin/StaticGraphTasks/${configuration,,}/StaticGraphTasks.dll"
custom_after="$tool_root/targets/staticGraphTargetFrameworkSelection.targets"

mkdir -p "$log_dir" "$obj_dir"

static_properties=()
static_targets=()
for arg in "$@"; do
  case "$arg" in
    /p:*|-p:*)
      static_properties+=("-p" "${arg#*:}")
      ;;
    /t:*|-t:*|/target:*|-target:*)
      static_targets+=("-t" "${arg#*:}")
      ;;
  esac
done

echo "Building validation tools..."
dotnet build "$tool_root/msbuild-graph-validation.slnx" -c "$configuration" -v:minimal

echo "Capturing dynamic build graph..."
dotnet msbuild "$entry_project" \
  "/bl:$log_dir/dynamic.binlog" \
  "$@"

echo "Capturing target framework selection..."
dotnet msbuild "$entry_project" \
  /t:CaptureStaticGraphSelectedTargetFrameworksRecursive \
  "/bl:$log_dir/selection.binlog" \
  "$@" \
  "/p:CustomAfterMicrosoftCommonTargets=$custom_after" \
  /p:CaptureStaticGraphSelectedTargetFrameworks=true \
  "/p:StaticGraphSelectedTargetFrameworksRawFile=$raw_file" \
  "/p:StaticGraphTasksAssemblyPath=$tasks_dll"

echo "Generating selected target framework targets..."
dotnet msbuild "$entry_project" \
  /t:GenerateStaticGraphSelectedTargetFrameworks \
  "$@" \
  "/p:CustomAfterMicrosoftCommonTargets=$custom_after" \
  "/p:StaticGraphSelectedTargetFrameworksRawFile=$raw_file" \
  "/p:StaticGraphSelectedTargetFrameworksTargets=$targets_file" \
  "/p:StaticGraphTasksAssemblyPath=$tasks_dll"

echo "Converting dynamic captures..."
dotnet run --no-build --project "$tool_root/src/StaticGraphValidation" -- dynamic "$log_dir/dynamic.binlog" --repo-root "$repo_root" -o "$log_dir/dynamic.json"

echo "Capturing static graph..."
dotnet run --no-build --project "$tool_root/src/StaticGraphValidation" -- static "$entry_project" \
  --repo-root "$repo_root" \
  "${static_targets[@]}" \
  "${static_properties[@]}" \
  -p "CustomAfterMicrosoftCommonTargets=$custom_after" \
  -p StaticGraphUseSelectedTargetFrameworks=true \
  -p "StaticGraphSelectedTargetFrameworksTargets=$targets_file" \
  -o "$log_dir/static.json"

echo "Comparing normalized graph projections..."
dotnet run --no-build --project "$tool_root/src/StaticGraphValidation" -- compare \
  --left "$log_dir/dynamic.json" \
  --right "$log_dir/static.json" \
  -o "$log_dir/comparison.txt"

echo "Wrote validation artifacts to $log_dir"
