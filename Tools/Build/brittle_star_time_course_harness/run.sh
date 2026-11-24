#!/usr/bin/env bash
# Compile the SHIPPED Brittle Star Time course code (RaceCourseGeometry, HeadlongCircuit, BrittleStarTimeCourse, and the
# Shift Line's BrittleStarTimeLine / BrittleStarTimeCoach / BrittleStarTimeHeat) and its SHIPPED edit-mode tests
# (BrittleStarTimeCourseTests, BrittleStarTimeLineTests) against race_course_source_harness's Unity math stub and an
# NUnit shim with Roslyn, and RUN every [Test] - no Unity. FlightSim.cs is a model of the hull flying the course (its
# speed and turn laws), used by --race / --tune / --check-race to measure what reading the course and the coach are worth.
#
#   bash Tools/Build/brittle_star_time_course_harness/run.sh               # every test; exit 0 = all passed
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --self-test   # ...plus the measured claims and negative controls
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --sweep       # each rung's movement numbers
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --gates I S   # one lap's rings as JSON (intensity, seed)
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --race [N]    # pilots and AIs flown over N seeds a rung
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --tune [N]    # search the coach's pace tables
#   bash Tools/Build/brittle_star_time_course_harness/run.sh --check-race [N]  # the measured claims; exit 0 = they hold
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
LINE="$A/Controller/Arcade/BrittleStarTime/BrittleStarTimeLine.cs"
COACH="$A/Controller/Arcade/BrittleStarTime/BrittleStarTimeCoach.cs"
HEAT="$A/Controller/Arcade/BrittleStarTime/BrittleStarTimeHeat.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

# build <output name> [<shipped file> <its replacement>]: the shipped sources, one optionally swapped for a mutated copy.
build() {
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$HERE/FlightSim.cs" "$HERE/RaceSweep.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
    "$COURSE" "$LINE" "$COACH" "$HEAT" \
    "$A/Tests/Editor/BrittleStarTimeCourseTests.cs" "$A/Tests/Editor/BrittleStarTimeLineTests.cs"; do
    [ "$f" = "${2:-}" ] && f="$3"
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:Runner -out:"$(native "$OUT/$1.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$1.runtimeconfig.json"
}

run() { (cd "$ROOT" && "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"); }

build bst
case "${1:-}" in
  --sweep) run bst --sweep; exit 0 ;;
  --gates) run bst --gates "$2" "$3"; exit 0 ;;
  --race) run bst --race "${2:-24}"; exit 0 ;;
  --tune) run bst --tune "${2:-16}"; exit 0 ;;
  --check-race) set +e; run bst --check-race "${2:-16}"; exit $? ;;
esac

set +e; run bst > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# The measured claims (BRITTLESTARTIME.md section 0) on the shipped code: the let-go gates are worth >= 3% at I3 and I4,
# every coached AI finishes every rung, Easy is slower than Legendary, and the coach beats today's AI.
set +e; run bst --check-race 16 > "$OUT/race.txt"; rrc=$?; set -e
cat "$OUT/race.txt"
[ $rrc -eq 0 ] || { echo "the measured claims do not hold"; exit 1; }

# Negative controls: each mutated copy of a shipped file must make the tests (or, with $5 = race, the measured claims) fail.
negative() {   # $1 = label, $2 = shipped file, $3 = sed expression, $4 = grep proof the mutation applied, $5 = "race"
  local base; base=$(basename "$2" .cs)
  sed "$3" "$2" > "$OUT/${base}_neg.cs"
  grep -q "$4" "$OUT/${base}_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build neg "$2" "$OUT/${base}_neg.cs"
  set +e
  if [ "${5:-}" = "race" ]; then run neg --check-race 16 > "$OUT/neg.txt"; else run neg > "$OUT/neg.txt"; fi
  local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing line(s))"
}
negative "a slalom so gentle Speed Mode holds it (amplitude 50)" "$COURSE" \
  's/public const float SlalomAmplitude = 90f;/public const float SlalomAmplitude = 50f;/' 'SlalomAmplitude = 50f'
negative "hairpins wide enough to take flat out (width 300)" "$COURSE" \
  's/public const float HairpinWidth = 90f;/public const float HairpinWidth = 300f;/' 'HairpinWidth = 300f'
negative "a corkscrew coil so wide Speed Mode holds it unrolled (radius 160)" "$COURSE" \
  's/public const float CoilRadius = 100f;/public const float CoilRadius = 160f;/' 'CoilRadius = 160f'
negative "a corkscrew sampled a half turn apart (aliases)" "$COURSE" \
  's/public const float CoilStepDegrees = 90f;/public const float CoilStepDegrees = 180f;/' 'CoilStepDegrees = 180f'
negative "every rung flies two laps (the length re-cut undone)" "$COURSE" \
  's/LapsByIntensity = { 2, 3, 3, 4 };/LapsByIntensity = { 2, 2, 2, 2 };/' 'LapsByIntensity = { 2, 2, 2, 2 };'
negative "let-go gates only for corners under 40% of full Speed (the hairpins lose theirs)" "$LINE" \
  's/public const float LetGoBelowShare = 0.9f;/public const float LetGoBelowShare = 0.4f;/' 'LetGoBelowShare = 0.4f'
negative "let-go columns standing in the line (outset -40)" "$LINE" \
  's/public const float ColumnOutset = 12f;/public const float ColumnOutset = -40f;/' 'ColumnOutset = -40f'
negative "a coach that never lets go at the gate" "$COACH" \
  's/return !(approaching \&\& (g.Position - pos).magnitude <= letGo);/return true;/' 'return true;'
negative "a flat-out grade that ignores the heart (heart share 1)" "$HEAT" \
  's/public const float HeartShare = 0.6f;/public const float HeartShare = 1.0f;/' 'HeartShare = 1.0f'
negative "MEASURED: let-go gates that stand nowhere are worth nothing (the 3% bar fires)" "$LINE" \
  's/public const float LetGoBelowShare = 0.9f;/public const float LetGoBelowShare = 0.4f;/' 'LetGoBelowShare = 0.4f' race
echo "brittle_star_time_course_harness self-test: ok"
