#!/usr/bin/env bash
# Compile the SHIPPED Grizzly Time course and powder-keg code (RaceCourseGeometry, HeadlongCircuit, GrizzlyTimeCourse,
# GrizzlyTimeKegs) and its SHIPPED edit-mode tests (GrizzlyTimeCourseTests, GrizzlyTimeKegsTests) against
# race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN every [Test] - the 400-seed
# sweeps in seconds, no Unity. Docs/GrizzlyTime/POWDER_KEGS_SPEC.md section 7.
#
#   bash Tools/Build/grizzly_time_course_harness/run.sh               # every test; exit 0 = all passed
#   bash Tools/Build/grizzly_time_course_harness/run.sh --self-test   # ...plus negative controls that must FAIL
#   bash Tools/Build/grizzly_time_course_harness/run.sh --sweep       # the keg ladder per intensity (400 seeds)
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH. Runs from the repo root (the
# course tests read the shipped prefab and bomb config back).
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
OUT="${TMPDIR:-/tmp}/grizzly_time_course_harness"
mkdir -p "$OUT"

G="$A/Controller/Arcade/GrizzlyTime"
KEGS="$G/GrizzlyTimeKegs.cs"
COURSE="$G/GrizzlyTimeCourse.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = kegs source, $2 = course source, $3 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Data/Enums/PrismKind.cs" "$A/Data/Enums/GameToastSituation.cs" "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" "$2" "$1" \
    "$A/Tests/Editor/GrizzlyTimeCourseTests.cs" "$A/Tests/Editor/GrizzlyTimeKegsTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:Runner -out:"$(native "$OUT/$3.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$3.runtimeconfig.json"
}

run() { (cd "$ROOT" && "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"); }

build "$KEGS" "$COURSE" grizzly
case "${1:-}" in
  --sweep) run grizzly --sweep; exit 0 ;;
esac

set +e; run grizzly > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy must make the tests fail.
negative() {   # $1 = label, $2 = kegs source, $3 = course source
  build "$2" "$3" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain, $4 = file (kegs|course)
  local src="$KEGS"; [ "${4:-kegs}" = course ] && src="$COURSE"
  sed "$2" "$src" > "$OUT/mutant.cs"
  grep -q "$3" "$OUT/mutant.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  if [ "${4:-kegs}" = course ]; then negative "$1" "$KEGS" "$OUT/mutant.cs"; else negative "$1" "$OUT/mutant.cs" "$COURSE"; fi
}
mutate "kegs pulled into the mouth" 's/public const float KegOffset = 140f;/public const float KegOffset = 90f;/' 'KegOffset = 90f'
mutate "chalk inside a keg blast" 's/public const float ChalkFrom = 120f;/public const float ChalkFrom = 40f;/' 'ChalkFrom = 40f'
mutate "chalk on the ring-to-ring line" 's/public const float ChalkHalfGap = 70f;/public const float ChalkHalfGap = -70f;/' 'ChalkHalfGap = -70f'
mutate "kegs on the outside of the corner" 's/if (deflection >= StraightDegrees && inPlane.sqrMagnitude > 1e-6f) return inPlane.normalized;/if (deflection >= StraightDegrees \&\& inPlane.sqrMagnitude > 1e-6f) return -inPlane.normalized;/' 'return -inPlane.normalized;'
mutate "I3 core left plain" 's/{ PrismKind.Plain, PrismKind.Plain, PrismKind.Shielded, PrismKind.Shielded }/{ PrismKind.Plain, PrismKind.Plain, PrismKind.Plain, PrismKind.Shielded }/' 'PrismKind.Plain, PrismKind.Plain, PrismKind.Plain, PrismKind.Shielded'
mutate "I4 mouth back to 46" 's/RingRadius = new\[\] { 96f, 84f, 74f, 66f }/RingRadius = new[] { 96f, 84f, 74f, 46f }/' '74f, 46f' course
echo "grizzly_time_course_harness self-test: ok"
