#!/usr/bin/env bash
# The Regatta course FEATURES offline (RegattaCourseFeatures: sections, gaps, detours, alternate rings, line crystals).
# Compiles the pure course files with the harness's UnityEngine stub, a minimal NUnit stub and the EditMode test file
# Assets/_Scripts/Tests/Editor/RegattaCourseFeaturesTests.cs, then:
#   1. RUNS every [Test] in that file (no Unity editor needed), and
#   2. writes features.json: the fixture Tools/Compute/evaluators/regatta_features.py --self-test holds the Python port to.
#   bash Tools/Build/regatta_course_harness/features.sh            # run the tests, rewrite features.json
#   bash Tools/Build/regatta_course_harness/features.sh --check    # run the tests, exit 1 if features.json is stale
# Needs a dotnet 8 SDK ($DOTNET_ROOT, default ~/.dotnet; /usr/lib/dotnet from apt also works).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
[ -x "$DOTNET_ROOT/dotnet" ] || DOTNET_ROOT=/usr/lib/dotnet
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/regatta_features_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/Stubs.cs" "$HERE/NUnitStub.cs" "$HERE/FeaturesHarness.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/Domains.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/PrismKind.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourseFeatures.cs" \
  "$ROOT/Assets/_Scripts/Tests/Editor/RegattaCourseFeaturesTests.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -define:UNITY_EDITOR "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:FeaturesHarness -out:"$OUT/features.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/features.runtimeconfig.json"
"$DOTNET" "$OUT/features.exe" > "$OUT/features.json"
if [ "${1:-}" = "--check" ]; then
  cmp -s "$OUT/features.json" "$HERE/features.json" && echo "features.json: up to date" || { echo "features.json: STALE (run features.sh)"; exit 1; }
else
  cp "$OUT/features.json" "$HERE/features.json"; echo "wrote $HERE/features.json ($(wc -c < "$HERE/features.json") bytes)"
fi
