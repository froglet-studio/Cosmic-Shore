#!/usr/bin/env bash
# Compile the SHIPPED Perch Run course (RaceCourseGeometry, PerchRunCourse) and its SHIPPED edit-mode tests
# (PerchRunCourseTests) against race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN
# every [Test] - the 400-seed ladder in seconds, no Unity. Run from anywhere; the tests read GobyPistolConfig.asset from
# the repo root.
#
#   bash Tools/Build/perch_run_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/perch_run_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/perch_run_harness/run.sh --sweep         # the ladder and three pilots' race clocks per intensity
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
OUT="${TMPDIR:-/tmp}/perch_run_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/PerchRun/PerchRunCourse.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$HERE/Stubs.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$1" "$A/Tests/Editor/PerchRunCourseTests.cs"; do
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

build "$COURSE" perchrun
case "${1:-}" in
  --sweep)  run perchrun --sweep; exit 0 ;;
  --trace)  run perchrun "$@"; exit 0 ;;
  --export) run perchrun "$@"; exit 0 ;;
esac

set +e; run perchrun > "$OUT/result.txt"; rc=$?; set -e
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
  sed "$2" "$COURSE" > "$OUT/PerchRunCourse_neg.cs"
  grep -q "$3" "$OUT/PerchRunCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/PerchRunCourse_neg.cs"
}
mutate "the dart reaches further than the asset" 's/public const float DartRange = 600f, DartMinRange = 140f;/public const float DartRange = 700f, DartMinRange = 140f;/' 'DartRange = 700f'
mutate "the walk lets legs pass through other rings" 's/float clear = s.RingRadius + RingClearance;/float clear = -1f;/' 'float clear = -1f;'
mutate "the hairpin's leg out is short" 's/HairpinOutMin = 510f, HairpinOutMax = 540f/HairpinOutMin = 300f, HairpinOutMax = 340f/' 'HairpinOutMin = 300f'
mutate "the blocker stands off its leg" 's/coral.Add(new Coral(Vector3.Lerp(rings\[k - 1\].Position, rings\[k\].Position, BlockerAt)/coral.Add(new Coral(Vector3.Lerp(rings[k - 1].Position, rings[k].Position, BlockerAt) + Vector3.up * 80f/' 'Vector3.up \* 80f'
mutate "the AI ignores the coral" 's/if (FirstCoral(from, dir, len + HullRadius + 2f, coral, out _)) continue;/if (false) continue;/' 'if (false) continue;'
mutate "no hairpins" 's/HairpinSlot = new\[\] { -1, 1, 3, 4 }\[i\]/HairpinSlot = new[] { -1, -1, 3, 4 }[i]/' 'HairpinSlot = new\[\] { -1, -1, 3, 4 }'
mutate "the double is too long for one dart" 's/DoubleLegMin = 200f, DoubleLegMax = 240f/DoubleLegMin = 360f, DoubleLegMax = 400f/' 'DoubleLegMin = 360f'
echo "perch_run_harness self-test: ok"
