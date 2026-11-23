#!/usr/bin/env bash
# Compile the SHIPPED Waystation course (RaceCourseGeometry, WaystationCourse, WaystationTower, WaystationHopPlan) and
# its SHIPPED edit-mode tests (WaystationTowerTests, WaystationHopPlanTests) against this harness's Unity math stub and an
# NUnit shim with Roslyn, and RUN every [Test] - no Unity (Lantern Towers, 2026-11-11; the perch_run_harness pattern).
#
#   bash Tools/Build/waystation_tower_harness/run_tests.sh               # every test; exit 0 = none failed
#   bash Tools/Build/waystation_tower_harness/run_tests.sh --self-test   # ...plus negative controls that must FAIL
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH. Builds in a scratch folder,
# never in the repo.
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
OUT="${TMPDIR:-/tmp}/waystation_tower_tests"
mkdir -p "$OUT"

TOWER="$A/Controller/Arcade/Waystation/WaystationTower.cs"
PLAN="$A/Controller/Arcade/Waystation/WaystationHopPlan.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = tower source, $2 = plan source, $3 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$HERE/Unity.cs" "$HERE/Stubs.cs" "$HERE/NUnitShim.cs" "$HERE/TestRunner.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Arcade/Waystation/WaystationCourse.cs" \
    "$1" "$2" "$A/Tests/Editor/WaystationTowerTests.cs" "$A/Tests/Editor/WaystationHopPlanTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:TestRunner -out:"$(native "$OUT/$3.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$3.runtimeconfig.json"
}

run() { "$DOTNET" "$(native "$OUT/$1.dll")"; }

build "$TOWER" "$PLAN" tests
set +e; run tests > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of the shipped source must make the tests fail.
negative() {   # $1 = label, $2 = tower source, $3 = plan source
  build "$2" "$3" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate_tower() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$TOWER" > "$OUT/WaystationTower_neg.cs"
  grep -q "$3" "$OUT/WaystationTower_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/WaystationTower_neg.cs" "$PLAN"
}
mutate_plan() {
  sed "$2" "$PLAN" > "$OUT/WaystationHopPlan_neg.cs"
  grep -q "$3" "$OUT/WaystationHopPlan_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$TOWER" "$OUT/WaystationHopPlan_neg.cs"
}
mutate_tower "every hop loose (no tight hops, no shelf to read)" \
  's/float shelf = IsTightHop(dGate, s) ? high - ShelfCut(dGate, s) : high;/float shelf = high;/' 'float shelf = high;'
mutate_tower "a tight hop's runway too short for its own landing" \
  's/step = shelf + RunwayAfter(shelf, roll);/step = shelf + RunwayAfter(shelf, roll) - 30f;/' 'RunwayAfter(shelf, roll) - 30f'
mutate_tower "the shelf keeps the reaction (the lanterns end at the laid depth)" \
  's/? DeepestLanding(course, i, s, 0f, s.WindowLow, s.WindowHigh + FarEdgeReach)/? DeepestLanding(course, i, s, ReactSeconds, s.WindowLow, s.WindowHigh)/' \
  'DeepestLanding(course, i, s, ReactSeconds, s.WindowLow, s.WindowHigh)$'
mutate_tower "a dust-stolen tile is laid over" \
  's/!exists || destroyed || (!ours \&\& displacement > 1f);/!exists || destroyed || !ours;/' '|| !ours;'
mutate_tower "the crown gem at every rung" 's/if (s.CrownGem <= 0f) return;/if (s.CrownGem < 0f) return;/' 'CrownGem < 0f'
mutate_plan "every seat lands alike" 's/(WaystationTower.Hash01(seat + 1, gate, 4243) \* 2f - 1f) \* s.AiErrorU/0f * s.AiErrorU/' '0f \* s.AiErrorU'
mutate_plan "the autopilot aims at the window's far edge" \
  's/return Mathf.Clamp(aim, s.WindowLow + AiSafety, WaystationTower.FarEdge(course, i, s) - AiSafety);/return s.WindowHigh;/' 'return s.WindowHigh;'
mutate_plan "a clean landing breaks the streak" \
  's/if (grade == HopGrade.Clean) return streak;/if (grade == HopGrade.Clean) return new Streak(0, 0);/' 'Clean) return new Streak(0, 0)'
echo "waystation_tower_harness tests self-test: ok"
