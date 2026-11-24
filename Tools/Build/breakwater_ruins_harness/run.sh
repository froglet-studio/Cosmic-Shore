#!/usr/bin/env bash
# Compile the SHIPPED Breakwater ruin rule (BreakwaterRuins, with Spearline's ruin generator and Breakwater's course)
# heat rule (BreakwaterHeat) and barrier rule (BreakwaterBarriers) and their SHIPPED edit-mode tests against
# race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN every [Test] - no Unity.
#
#   bash Tools/Build/breakwater_ruins_harness/run.sh              # every test; exit 0 = all passed
#   bash Tools/Build/breakwater_ruins_harness/run.sh --self-test  # ...plus negative controls that must FAIL
#   bash Tools/Build/breakwater_ruins_harness/run.sh --measure    # what the I4 courses of seeds 1-100 carry
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
A="$ROOT/Assets/_Scripts"
pick_root() {
  for r in "${DOTNET_ROOT:-}" "$HOME/.dotnet" "$(dirname "$(command -v dotnet 2>/dev/null || echo /nonexistent/x)")"; do
    [ -n "$r" ] && ls "$r"/sdk/*/Roslyn/bincore/csc.dll >/dev/null 2>&1 && { echo "$r"; return; }
  done
  echo "no .NET SDK with Roslyn (set DOTNET_ROOT)" >&2; exit 3
}
DOTNET_ROOT="$(pick_root)"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | tail -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
OUT="${TMPDIR:-/tmp}/breakwater_ruins_harness"
mkdir -p "$OUT"

RUINS="$A/Controller/Arcade/Breakwater/BreakwaterRuins.cs"
BARRIERS="$A/Controller/Arcade/Breakwater/BreakwaterBarriers.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = ruins source, $2 = output name, $3 = barriers source (default: the shipped one)
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Data/Enums/PrismKind.cs" "$A/Data/Enums/Domains.cs" \
    "$A/Controller/Arcade/Spearline/SpearlineSeals.cs" "$A/Controller/Arcade/Spearline/SpearlineRuins.cs" \
    "$A/Controller/Arcade/Breakwater/BreakwaterCourse.cs" "$A/Controller/Arcade/Breakwater/BreakwaterStationBuilder.cs" \
    "$A/Controller/Arcade/Breakwater/BreakwaterHeat.cs" "${3:-$BARRIERS}" "$1" \
    "$A/Tests/Editor/BreakwaterRuinsTests.cs" "$A/Tests/Editor/BreakwaterBarriersTests.cs" "$A/Tests/Editor/BreakwaterHeatTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:Runner -out:"$(native "$OUT/$2.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$2.runtimeconfig.json"
}

run() { "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"; }

build "$RUINS" breakwater
case "${1:-}" in
  --measure) run breakwater --measure; exit 0 ;;
esac

set +e; run breakwater > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit 1
[ "${1:-}" = "--self-test" ] || exit 0

control() {   # $1 = what it proves, $2 = sed expression, $3 = text that must appear after it
  sed "$2" "$RUINS" > "$OUT/BreakwaterRuins_neg.cs"
  grep -qF -- "$3" "$OUT/BreakwaterRuins_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$OUT/BreakwaterRuins_neg.cs" neg
  set +e; run neg > "$OUT/neg.txt"; nrc=$?; set -e
  [ $nrc -ne 0 ] || { echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; }
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
control "ruins on every leg" "s/=> (leg & 1) == 1;/=> false;/" "OnBareLeg(int leg) => false;"
control "ruins on the way in from the pads" "s/PadKeep + half + sweep) return true;/-1f) return true;/" "pads[k], first) < -1f) return true;"
control "ruins inside a station's reach" "s/- port + StationKeep;/- port - 60f;/" "- port - 60f;"
barrier_control() {   # $1 = what it proves, $2 = sed expression, $3 = text that must appear after it
  sed "$2" "$BARRIERS" > "$OUT/BreakwaterBarriers_neg.cs"
  grep -qF -- "$3" "$OUT/BreakwaterBarriers_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$RUINS" neg "$OUT/BreakwaterBarriers_neg.cs"
  set +e; run neg > "$OUT/neg.txt"; nrc=$?; set -e
  [ $nrc -ne 0 ] || { echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; }
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
barrier_control "mines in the thread" "s/float inner = ThreadRadius + reach + 1f/float inner = 4f/" "float inner = 4f"
barrier_control "barriers below I4" "s/=> intensity >= 4;/=> intensity >= 3;/" "=> intensity >= 3;"
barrier_control "a curtain with the line left open" "s/if (u \* u + v \* v > radius \* radius) continue;/if (u * u + v * v > radius * radius || u * u + v * v < 400f) continue;/" "v * v < 400f) continue;"
barrier_control "a blade in the thread" "s/BladeInner = 20.5f/BladeInner = 8f/" "BladeInner = 8f"
barrier_control "a flak shell in the thread" "s/FlakInner = 28f/FlakInner = 12f/" "FlakInner = 12f"
barrier_control "a battery nothing silences" "s/coreStanding \* 2 <= coreTotal/coreStanding < 0/" "coreStanding < 0"
barrier_control "a shutter that never opens" "s/ShutterTravel = 22f/ShutterTravel = 2f/" "ShutterTravel = 2f"
echo "self-test OK"
