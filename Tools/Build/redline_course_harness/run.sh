#!/usr/bin/env bash
# Compile the SHIPPED Redline course code (RaceCourseGeometry, HeadlongCircuit, RedlineCourse, RedlineTimeCrystals,
# RedlineHeat) and its SHIPPED edit-mode tests (RedlineCourseTests, RedlineTimeCrystalsTests, RedlineHeatTests) against
# race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN every [Test] - the 400-seed ladder
# sweep, the flown curve and the heat in a few seconds, no Unity.
#
#   bash Tools/Build/redline_course_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/redline_course_harness/run.sh --self-test     # ...plus four negative controls that must FAIL
#   bash Tools/Build/redline_course_harness/run.sh --sweep         # the corner ladder per intensity (400 seeds)
#   bash Tools/Build/redline_course_harness/run.sh --parity        # rewrite redline_parity.jsonl (gates + Time crystals,
#                                                                  # the file manta_redline_sim.py --self-test reads)
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
OUT="${TMPDIR:-/tmp}/redline_course_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/Redline/RedlineCourse.cs"
CRYSTALS="$A/Controller/Arcade/Redline/RedlineTimeCrystals.cs"
HEAT="$A/Controller/Arcade/Redline/RedlineHeat.cs"

# Roslyn takes native paths: Git Bash's /b/... reads as B:\b/... on Windows.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = crystals source, $3 = output name, $4 = heat source (default: shipped)
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
    "$1" "$2" "${4:-$HEAT}" "$A/Tests/Editor/RedlineCourseTests.cs" "$A/Tests/Editor/RedlineTimeCrystalsTests.cs" \
    "$A/Tests/Editor/RedlineHeatTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:Runner -out:"$(native "$OUT/$3.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$3.runtimeconfig.json"
}

run() { "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"; }

build "$COURSE" "$CRYSTALS" redline
case "${1:-}" in
  --sweep)  run redline --sweep; exit 0 ;;
  --parity)
    { for i in 2 3 4; do run redline --crystals "$i" 1 2 3 7 41; done; } > "$HERE/redline_parity.jsonl"
    echo "wrote $HERE/redline_parity.jsonl"; exit 0 ;;
esac

set +e; run redline > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy must make the tests fail.
negative() {   # $1 = label, $2 = course source, $3 = crystals source, $4 = heat source (default: shipped)
  build "$2" "$3" neg "${4:-$HEAT}"
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
sed 's/CornerFloorRadius = new\[\] { 0.75f \* full, 205f, 175f, 160f }/CornerFloorRadius = new[] { 0.75f * full, 205f, 175f, 90f }/' \
  "$COURSE" > "$OUT/RedlineCourse_neg.cs"
grep -q '175f, 90f' "$OUT/RedlineCourse_neg.cs" || { echo "the I4 floor mutation did not apply"; exit 1; }
negative "an I4 corner floor of 90 u (the Yawstery pivot's ladder)" "$OUT/RedlineCourse_neg.cs" "$CRYSTALS"
sed 's/public const float ApexInset = 70f;/public const float ApexInset = -70f;/' "$CRYSTALS" > "$OUT/RedlineTimeCrystals_neg.cs"
grep -q 'ApexInset = -70f' "$OUT/RedlineTimeCrystals_neg.cs" || { echo "the inset mutation did not apply"; exit 1; }
negative "I4 crystals set OUTSIDE their corners" "$COURSE" "$OUT/RedlineTimeCrystals_neg.cs"
sed 's/StartingTimeLevelByIntensity = { 6, 6, 6, 6 }/StartingTimeLevelByIntensity = { 0, 6, 6, 6 }/' "$COURSE" > "$OUT/RedlineCourse_dark.cs"
grep -q '{ 0, 6, 6, 6 }' "$OUT/RedlineCourse_dark.cs" || { echo "the start-level mutation did not apply"; exit 1; }
negative "I1 starting at Time 0 (Soar dark, the race before 2026-11-11)" "$OUT/RedlineCourse_dark.cs" "$CRYSTALS"
sed 's/public const float CleanMouthFraction = 0.5f;/public const float CleanMouthFraction = 1.5f;/' "$HEAT" > "$OUT/RedlineHeat_neg.cs"
grep -q 'CleanMouthFraction = 1.5f' "$OUT/RedlineHeat_neg.cs" || { echo "the clean-mouth mutation did not apply"; exit 1; }
negative "a clean thread wider than the mouth" "$COURSE" "$CRYSTALS" "$OUT/RedlineHeat_neg.cs"
echo "redline_course_harness self-test: ok"
