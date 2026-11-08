#!/usr/bin/env bash
# Compile the SHIPPED Brittle Star Time course code (RaceCourseGeometry, HeadlongCircuit, BrittleStarTimeCourse) and its
# SHIPPED edit-mode tests (BrittleStarTimeCourseTests) against race_course_source_harness's Unity math stub and an NUnit
# shim with Roslyn, and RUN every [Test] - the four movement rungs over 400 seeds in a few seconds, no Unity.
#
#   bash Tools/Build/brittle_star_time_course_harness/run.sh               # every test; exit 0 = all passed
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --self-test   # ...plus negative controls that must FAIL
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --sweep       # each rung's movement numbers
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --gates I S   # one lap's rings as JSON (intensity, seed)
#
# The tests read the shipped assets by repo-relative path, so the runner runs from the repo root.
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
OUT="${TMPDIR:-/tmp}/brittle_star_time_course_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/BrittleStarTime/BrittleStarTimeCourse.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
    "$1" "$A/Tests/Editor/BrittleStarTimeCourseTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:Runner -out:"$(native "$OUT/$2.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$2.runtimeconfig.json"
}

run() { (cd "$ROOT" && "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"); }

build "$COURSE" bst
case "${1:-}" in
  --sweep) run bst --sweep; exit 0 ;;
  --gates) run bst --gates "$2" "$3"; exit 0 ;;
esac

set +e; run bst > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of the course must make the tests fail.
negative() {   # $1 = label, $2 = sed expression, $3 = grep proof the mutation applied
  sed "$2" "$COURSE" > "$OUT/BrittleStarTimeCourse_neg.cs"
  grep -q "$3" "$OUT/BrittleStarTimeCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$OUT/BrittleStarTimeCourse_neg.cs" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
negative "a slalom so gentle Speed Mode holds it (amplitude 50)" \
  's/public const float SlalomAmplitude = 90f;/public const float SlalomAmplitude = 50f;/' 'SlalomAmplitude = 50f'
negative "hairpins wide enough to take flat out (width 300)" \
  's/public const float HairpinWidth = 90f;/public const float HairpinWidth = 300f;/' 'HairpinWidth = 300f'
negative "a corkscrew coil so wide Speed Mode holds it unrolled (radius 160)" \
  's/public const float CoilRadius = 100f;/public const float CoilRadius = 160f;/' 'CoilRadius = 160f'
negative "a corkscrew sampled a half turn apart (aliases)" \
  's/public const float CoilStepDegrees = 90f;/public const float CoilStepDegrees = 180f;/' 'CoilStepDegrees = 180f'
echo "brittle_star_time_course_harness self-test: ok"
