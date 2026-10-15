#!/usr/bin/env bash
# Regenerate course.js (the Regatta circuit the page races on) from the SHIPPED course code.
# Same compile as Tools/Build/regatta_course_harness/run.sh (its Stubs.cs, the three pure course files),
# with ExportCourse.cs as the entry point. Needs a dotnet 8 SDK ($DOTNET_ROOT, default ~/.dotnet).
#   bash Docs/Studios/RegattaAI/export_course.sh            # rewrite course.js
#   bash Docs/Studios/RegattaAI/export_course.sh --check    # exit 1 if course.js is stale
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
H="$ROOT/Tools/Build/regatta_course_harness"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/regatta_ai_export"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$H/Stubs.cs" "$HERE/ExportCourse.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/Domains.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Regatta/RegattaCourse.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:ExportCourse -out:"$OUT/export.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/export.runtimeconfig.json"
"$DOTNET" "$OUT/export.exe" > "$OUT/course.js"
if [ "${1:-}" = "--check" ]; then
  cmp -s "$OUT/course.js" "$HERE/course.js" && echo "course.js: up to date" || { echo "course.js: STALE (run export_course.sh)"; exit 1; }
else
  cp "$OUT/course.js" "$HERE/course.js"; echo "wrote $HERE/course.js ($(wc -c < "$HERE/course.js") bytes)"
fi
