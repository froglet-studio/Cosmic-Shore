#!/usr/bin/env bash
# Compile the three PURE course files (RaceCourseGeometry, HeadlongCircuit, RegattaCourse) against
# a UnityEngine math stub and RUN them: prints Tools/Build/regatta_course_measurements.json to
# stdout and the per-intensity summary + the 60-seed sweep verdict to stderr. Needs a dotnet 8
# SDK (per-user install is fine: `bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh)
# --channel 8.0 --install-dir $DOTNET_ROOT --no-path`). Re-run after ANY edit to those files and
# commit the JSON: author_regatta_assets.py --check fails when the JSON's source hash is stale.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/regatta_course_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/Stubs.cs" "$HERE/Driver.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/Domains.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:Driver -out:"$OUT/course.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/course.runtimeconfig.json"
"$DOTNET" "$OUT/course.exe"
