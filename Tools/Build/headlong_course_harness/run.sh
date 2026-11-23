#!/usr/bin/env bash
# Compile the SHIPPED Headlong stampede code (RaceCourseGeometry, HeadlongCircuit, RedlineTimeCrystals.Apex, HeadlongHedges)
# and its SHIPPED edit-mode tests (HeadlongHedgesTests) against race_course_source_harness's Unity math stub and an NUnit
# shim with Roslyn, and RUN every [Test] - the 400-seed ladder in a few seconds, no Unity.
#
#   bash Tools/Build/headlong_course_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/headlong_course_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/headlong_course_harness/run.sh --sweep         # the stampede ladder per intensity (400 seeds)
#   bash Tools/Build/headlong_course_harness/run.sh --export I S... # JSON lines: gates, hedges, crystals (the model's input)
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
OUT="${TMPDIR:-/tmp}/headlong_course_harness"
mkdir -p "$OUT"

HEDGES="$A/Controller/Arcade/Headlong/HeadlongHedges.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = hedges source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Data/Enums/PrismKind.cs" "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" "$A/Controller/Arcade/Redline/RedlineTimeCrystals.cs" \
    "$1" "$A/Tests/Editor/HeadlongHedgesTests.cs"; do
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

build "$HEDGES" headlong
case "${1:-}" in
  --sweep)  run headlong --sweep; exit 0 ;;
  --export) run headlong "$@"; exit 0 ;;
esac

set +e; run headlong > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy must make the tests fail.
negative() {   # $1 = label, $2 = hedges source
  build "$2" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$HEDGES" > "$OUT/HeadlongHedges_neg.cs"
  grep -q "$3" "$OUT/HeadlongHedges_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/HeadlongHedges_neg.cs"
}
mutate "hedges in the rings' approach" 's/public const float RingClearance = 150f;/public const float RingClearance = 20f;/' 'RingClearance = 20f'
mutate "the thorn window on the line" 's/public const float WindowOffset = 110f;/public const float WindowOffset = 0f;/' 'WindowOffset = 0f'
mutate "I4 without Time 5" 's/StartingTimeLevelByIntensity = { 0, 0, 0, 5 }/StartingTimeLevelByIntensity = { 0, 0, 0, 0 }/' '{ 0, 0, 0, 0 }'
mutate "slabs far apart (a hedge you slip through)" 's/public const float Pitch = 15f;/public const float Pitch = 4f;/' 'Pitch = 4f'
echo "headlong_course_harness self-test: ok"
