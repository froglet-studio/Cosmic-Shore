#!/usr/bin/env bash
# Compile the SHIPPED Serpentine course code (RaceCourseGeometry, HeadlongCircuit, SerpentTimeCourse) and its SHIPPED
# edit-mode tests (SerpentTimeCourseTests, 400 seeds per intensity) against race_course_source_harness's Unity math
# stub and an NUnit shim with Roslyn, and RUN every [Test], no Unity. The redline_course_harness pattern.
#
#   bash Tools/Build/serpent_time_course_harness/run.sh               # every test; exit 0 = all passed
#   bash Tools/Build/serpent_time_course_harness/run.sh --self-test   # ...plus a negative control that must FAIL
#   bash Tools/Build/serpent_time_course_harness/run.sh --parity      # rewrite serpent_time_parity.jsonl (the gates
#                                                                     # Tools/Build/serpent_time_sim.py --self-test reads)
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
A="$ROOT/Assets/_Scripts"
pick_root() {
  for r in "${DOTNET_ROOT:-}" "$HOME/.dotnet" "$(dirname "$(readlink -f "$(command -v dotnet 2>/dev/null || echo /nonexistent/x)")")" \
           /usr/lib/dotnet /usr/share/dotnet; do
    [ -n "$r" ] && ls "$r"/sdk/*/Roslyn/bincore/csc.dll >/dev/null 2>&1 && { echo "$r"; return; }
  done
  echo "no .NET SDK with Roslyn (set DOTNET_ROOT)" >&2; exit 3
}
DOTNET_ROOT="$(pick_root)"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | tail -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
OUT="${TMPDIR:-/tmp}/serpent_time_course_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/SerpentTime/SerpentTimeCourse.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
    "$1" "$A/Tests/Editor/SerpentTimeCourseTests.cs"; do
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

build "$COURSE" serpent
case "${1:-}" in
  --parity)
    { for i in 1 2 3 4; do run serpent --gates "$i" 1 2 3 7 41 $((20365644 + 7919 * i)) $((20470373 + 7919 * i)); done; } > "$HERE/serpent_time_parity.jsonl"
    echo "wrote $HERE/serpent_time_parity.jsonl"; exit 0 ;;
esac

set +e; run serpent > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative control: a mutated copy must make the tests fail.
sed 's/RingRadius = new\[\] { 72f, 60f, 50f, 42f }/RingRadius = new[] { 72f, 60f, 60f, 42f }/' "$COURSE" > "$OUT/SerpentTimeCourse_neg.cs"
grep -q '60f, 60f, 42f' "$OUT/SerpentTimeCourse_neg.cs" || { echo "the I3 mouth mutation did not apply"; exit 1; }
build "$OUT/SerpentTimeCourse_neg.cs" neg
set +e; run neg > "$OUT/neg.txt"; nrc=$?; set -e
if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: an I3 mouth as wide as I2's"; exit 1; fi
echo "negative control fired: an I3 mouth as wide as I2's ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
echo "serpent_time_course_harness self-test: ok"
