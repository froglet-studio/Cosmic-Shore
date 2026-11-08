#!/usr/bin/env bash
# Compile the SHIPPED Breach course (RaceCourseGeometry, BreachCourse) and its SHIPPED edit-mode tests
# (BreachCourseTests) against race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN
# every [Test] - the 400-seed ladder in seconds, no Unity. The tests read Orca.prefab, OrcaCrewConfig.asset and the
# danger slow from the repo root.
#
#   bash Tools/Build/breach_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/breach_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/breach_harness/run.sh --sweep         # the ladder and three helms' race clocks per intensity
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
OUT="${TMPDIR:-/tmp}/breach_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/Breach/BreachCourse.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$HERE/Stubs.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$1" "$A/Tests/Editor/BreachCourseTests.cs"; do
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

build "$COURSE" breach
case "${1:-}" in
  --sweep)  run breach --sweep; exit 0 ;;
  --trace|--why)  run breach "$@"; exit 0 ;;
  --export) run breach "$@"; exit 0 ;;
esac

set +e; run breach > "$OUT/result.txt"; rc=$?; set -e
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
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain (with $4 = -v: must NOT contain)
  sed "$2" "$COURSE" > "$OUT/BreachCourse_neg.cs"
  if [ "${4:-}" = "-v" ]; then
    ! grep -qF "$3" "$OUT/BreachCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  else
    grep -qF "$3" "$OUT/BreachCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  fi
  negative "$1" "$OUT/BreachCourse_neg.cs"
}
mutate "the tank refills faster than the asset" 's/BoostTank = 3f, BoostRefill = 0.25f;/BoostTank = 3f, BoostRefill = 0.5f;/' 'BoostRefill = 0.5f'
mutate "the walls stand so far ahead a burn swings back in" 's/WallAhead = new\[\] { 0f, 0f, 110f, 90f }/WallAhead = new[] { 0f, 0f, 110f, 300f }/' '110f, 300f'
mutate "the walls are rounded on the outside" 's/? -outward.normalized : e1;/? outward.normalized : e1;/' '? outward.normalized : e1;'
mutate "the walls are no wider than the ring" 's/=> g.Radius + s.WallMargin;/=> g.Radius - 2f;/' '=> g.Radius - 2f;'
mutate "the burn ignores the walls" 's/if (IsWalled(next, s) \&\& (PastWall(hull, rings, next, s) || d < WallStop)) return false;//' 'd < WallStop)) return false;' -v
mutate "a burn starts on an empty tank" 's/public const float MinBurnStart = 1f;/public const float MinBurnStart = 0f;/' 'MinBurnStart = 0f'
echo "breach_harness self-test: ok"
