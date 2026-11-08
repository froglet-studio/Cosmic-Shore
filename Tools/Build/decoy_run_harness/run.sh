#!/usr/bin/env bash
# Compile the SHIPPED Decoy Run course (RaceCourseGeometry, DecoyRunCourse, JacksRacePlan) and its SHIPPED edit-mode tests
# (DecoyRunCourseTests) against race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN
# every [Test] - the 400-seed ladder in seconds, no Unity. Run from anywhere; the tests read JacksConfig.asset from
# the repo root.
#
#   bash Tools/Build/decoy_run_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/decoy_run_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/decoy_run_harness/run.sh --sweep         # the ladder and three pilots' race clocks per intensity
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
OUT="${TMPDIR:-/tmp}/decoy_run_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/DecoyRun/DecoyRunCourse.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$HERE/Stubs.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Vessel/JacksRacePlan.cs" \
    "$1" "$A/Tests/Editor/DecoyRunCourseTests.cs"; do
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

build "$COURSE" decoyrun
case "${1:-}" in
  --sweep)  run decoyrun --sweep; exit 0 ;;
  --export) run decoyrun "$@"; exit 0 ;;
esac

set +e; run decoyrun > "$OUT/result.txt"; rc=$?; set -e
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
  sed "$2" "$COURSE" > "$OUT/DecoyRunCourse_neg.cs"
  grep -q "$3" "$OUT/DecoyRunCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/DecoyRunCourse_neg.cs"
}
mutate "the jacks run faster than the asset" 's/public const float JackSpeed = 208f;/public const float JackSpeed = 260f;/' 'JackSpeed = 260f'
mutate "the walk lets legs pass through other rings" 's/float clear = s.RingRadius + RingClearance;/float clear = -1f;/' 'float clear = -1f;'
mutate "the next flat rolls about the wrong line" 's/n = RaceCourseGeometry.RotateAbout(normal, a\[k - 1\], tilt/n = RaceCourseGeometry.RotateAbout(normal, Vector3.forward, tilt/' 'RotateAbout(normal, Vector3.forward, tilt'
mutate "the decoy lands short of its ring" 's/public const float Overshoot = 45f;/public const float Overshoot = -90f;/' 'Overshoot = -90f'
mutate "the chalk rails sit on the legs" 's/float lane = rings\[i\].Radius + ChalkLane;/float lane = 0f;/' 'float lane = 0f;'
mutate "the chalk ignores the flight lines" 's/if (ClearOfFlight(rings, start, at)) dots.Add(at);/dots.Add(at);/' '                        dots.Add(at);'
mutate "a skewer's first ring faces where it was entered from" 's/if (skewer) a\[k - 1\] = d;/if (false) a[k - 1] = d;/' 'if (false) a'
mutate "I4 starts without the second decoy" 's/StartTimeLevel = new\[\] { 0, 0, 0, 5 }/StartTimeLevel = new[] { 0, 0, 0, 0 }/' 'StartTimeLevel = new\[\] { 0, 0, 0, 0 }'
echo "decoy_run_harness self-test: ok"
