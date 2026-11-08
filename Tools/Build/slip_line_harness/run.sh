#!/usr/bin/env bash
# Compile the SHIPPED Slip Line course (RaceCourseGeometry, SlipLineCourse) and its SHIPPED edit-mode tests
# (SlipLineCourseTests) against race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN
# every [Test] - the 400-seed ladder in seconds, no Unity. Run from anywhere; the tests read HoneyguideConfig.asset and Honeyguide.prefab from
# the repo root.
#
#   bash Tools/Build/slip_line_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/slip_line_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/slip_line_harness/run.sh --sweep         # the ladder and three pilots' race clocks with the real Ratel AI per intensity
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
OUT="${TMPDIR:-/tmp}/slip_line_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/SlipLine/SlipLineCourse.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$HERE/Stubs.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$1" "$A/Tests/Editor/SlipLineCourseTests.cs" "$A/Controller/Vessel/Crew/DynamoCore.cs"; do
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

build "$COURSE" slipline
case "${1:-}" in
  --sweep)  run slipline --sweep; exit 0 ;;
  --trace|--why)  run slipline "$@"; exit 0 ;;
  --export) run slipline "$@"; exit 0 ;;
esac

set +e; run slipline > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy must make the tests fail.
negative() {   # $1 = label, $2 = course source
  build "$2" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$COURSE" > "$OUT/SlipLineCourse_neg.cs"
  grep -q "$3" "$OUT/SlipLineCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/SlipLineCourse_neg.cs"
}
mutate "the slip reaches further than the asset" 's/SlipReach = 400f, ZipSpeed/SlipReach = 500f, ZipSpeed/' 'SlipReach = 500f'
mutate "rails run down the ring's centre" 's/public const float RailOffset = 0.5f;/public const float RailOffset = 0f;/' 'RailOffset = 0f'
mutate "rails end on their ring" 's/public const float RailPast = 40f;/public const float RailPast = 0f;/' 'RailPast = 0f'
mutate "long rails no longer than short ones" 's/LongMin = 260f, LongMax = 370f/LongMin = 130f, LongMax = 170f/' 'LongMin = 130f'
mutate "the colours hash on the cell's position" 's/Mix(System.BitConverter.SingleToInt32Bits(axis.x));/Mix(System.BitConverter.SingleToInt32Bits(rings[k].Position.x));/' 'rings\[k\].Position.x));'
mutate "a chain's second rail starts off the landing" 's/Vector3 from = first.End + first.Direction \* ExitOffset;/Vector3 from = first.End + first.Direction * 30f;/' 'Direction \* 30f;'
echo "slip_line_harness self-test: ok"
