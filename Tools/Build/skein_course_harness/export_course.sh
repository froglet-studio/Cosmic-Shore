#!/usr/bin/env bash
# Regenerate skein_courses.json (Skein's rails and rings for Tools/Build/skein_urchin_sim.py) from the SHIPPED
# SkeinCourse.cs, compiled against race_course_source_harness's UnityEngine stub. The seed and strand count of each
# intensity are READ off Assets/_Prefabs/Spawnables/SpawnableSkein<i>.prefab (SpawnableSkein.CableSeed: cableSeed, else
# seed), so a retuned arena or generator makes --check fail. Needs a dotnet 8 SDK ($DOTNET_ROOT, default ~/.dotnet;
# `apt-get install dotnet-sdk-8.0` puts it at /usr/lib/dotnet).
#   bash Tools/Build/skein_course_harness/export_course.sh            # rewrite skein_courses.json
#   bash Tools/Build/skein_course_harness/export_course.sh --check    # exit 1 if skein_courses.json is stale
# Hold-out cables (not flown in the game; used to check a trained AI is not fitted to four cables) are written to
# skein_holdout.json from the same prefabs' seeds + 1..2 (SKEIN_HOLDOUT=0 skips them).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
if [ -z "${DOTNET_ROOT:-}" ]; then
  if [ -x "$HOME/.dotnet/dotnet" ]; then DOTNET_ROOT="$HOME/.dotnet"; else DOTNET_ROOT=/usr/lib/dotnet; fi
fi
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/skein_course_export"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/ExportCourse.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/Domains.cs" \
  "$ROOT/Assets/_Scripts/Controller/Arcade/Skein/SkeinCourse.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:ExportCourse -out:"$OUT/export.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/export.runtimeconfig.json"

ARGS=(); HOLD=()
for i in 1 2 3 4; do
  P="$ROOT/Assets/_Prefabs/Spawnables/SpawnableSkein$i.prefab"
  cable=$(sed -n 's/^  cableSeed: \(-\{0,1\}[0-9]*\)$/\1/p' "$P"); base=$(sed -n 's/^  seed: \(-\{0,1\}[0-9]*\)$/\1/p' "$P")
  n=$(sed -n 's/^  strandCount: \([0-9]*\)$/\1/p' "$P")
  [ -n "$cable" ] && [ -n "$base" ] && [ -n "$n" ] || { echo "could not read seed / strandCount off $P" >&2; exit 3; }
  seed=$([ "$cable" != "0" ] && echo "$cable" || echo "$base")
  ARGS+=("$i:$seed:$n"); HOLD+=("$i:$((seed + 1)):$n" "$i:$((seed + 2)):$n")
done
"$DOTNET" "$OUT/export.exe" "${ARGS[@]}" > "$OUT/skein_courses.json"
[ "${SKEIN_HOLDOUT:-1}" = "0" ] || "$DOTNET" "$OUT/export.exe" "${HOLD[@]}" > "$OUT/skein_holdout.json"
if [ "${1:-}" = "--check" ]; then
  rc=0
  for f in skein_courses.json skein_holdout.json; do
    [ -f "$OUT/$f" ] || continue
    cmp -s "$OUT/$f" "$HERE/$f" && echo "$f: up to date" || { echo "$f: STALE (run export_course.sh)"; rc=1; }
  done
  exit $rc
else
  cp "$OUT/skein_courses.json" "$HERE/"; echo "wrote $HERE/skein_courses.json ($(wc -c < "$HERE/skein_courses.json") bytes)"
  [ -f "$OUT/skein_holdout.json" ] && cp "$OUT/skein_holdout.json" "$HERE/" && echo "wrote $HERE/skein_holdout.json ($(wc -c < "$HERE/skein_holdout.json") bytes)"
fi
