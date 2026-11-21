#!/usr/bin/env bash
# Compile the PURE course files (RaceCourseGeometry, HeadlongCircuit, SlingshotCourse) against the
# regatta harness's UnityEngine math stub and RUN them: 400 seeds per intensity in the 480..1080
# race shell - eight gates, gate 0 on the pole, every gate in the shell, no corner under the floor
# or under twice the Stoat's cruise pivot - and print the measured ladder SLINGSHOT.md quotes.
# Exit 0 = all checks passed. Needs a dotnet 8 SDK, found through DOTNET_ROOT (default ~/.dotnet).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/slingshot_course_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/../regatta_course_harness/Stubs.cs" "$HERE/Driver.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Slingshot/SlingshotCourse.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:Driver -out:"$OUT/course.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/course.runtimeconfig.json"
"$DOTNET" "$OUT/course.exe"
