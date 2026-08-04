#!/usr/bin/env bash
#
# Local / CI wrapper around the Unity batchmode Windows build.
# The Unity-side logic lives in Assets/_Scripts/Editor/Build/CosmicShoreBuildPipeline.cs,
# which is the source of truth for what a shipping build is.
#
# Usage:
#   ./build_windows.sh [--dev] [--output Builds/Windows64] [--version 0.2.0]
#
# Requires UNITY_PATH to point at the Unity 6000.3.17f1 executable, e.g.
#   export UNITY_PATH="/opt/unity/6000.3.17f1/Editor/Unity"
#   export UNITY_PATH="C:/Program Files/Unity/Hub/Editor/6000.3.17f1/Editor/Unity.exe"

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
METHOD="CosmicShore.Editor.CosmicShoreBuildPipeline.BuildWindowsRelease"
OUTPUT="Builds/Windows64"
VERSION=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --dev)     METHOD="CosmicShore.Editor.CosmicShoreBuildPipeline.BuildWindowsDevelopment"; shift ;;
    --output)  OUTPUT="$2";  shift 2 ;;
    --version) VERSION="$2"; shift 2 ;;
    -h|--help) sed -n '2,16p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

: "${UNITY_PATH:?UNITY_PATH is not set. Point it at the Unity 6000.3.17f1 executable.}"
[[ -x "$UNITY_PATH" ]] || { echo "ERROR: UNITY_PATH is not executable: $UNITY_PATH" >&2; exit 1; }

# Stamp the commit so build_manifest.txt (and therefore the Steam build description) is traceable.
COMMIT="$(git -C "$PROJECT_ROOT" rev-parse HEAD 2>/dev/null || echo unknown)"

ARGS=(
  -quit -batchmode -nographics
  -projectPath "$PROJECT_ROOT"
  -executeMethod "$METHOD"
  -buildOutput "$OUTPUT"
  -buildCommit "$COMMIT"
  -logFile -
)
[[ -n "$VERSION" ]] && ARGS+=(-buildVersion "$VERSION")

echo "Building Windows x64 -> $OUTPUT (commit ${COMMIT:0:8})"
"$UNITY_PATH" "${ARGS[@]}"

echo "Build finished. Artefacts in $PROJECT_ROOT/$OUTPUT"
