#!/usr/bin/env bash
# Compile the SHIPPED Skein ruins code (SkeinCourse, SkeinRuins, and Spearline's SpearlineRuins it calls) and its
# SHIPPED edit-mode tests (SkeinRuinsTests) against race_course_source_harness's Unity math stub and
# spearline_harness's NUnit shim with Roslyn, and RUN every [Test] - no Unity.
#
#   bash Tools/Build/skein_ruins_harness/run.sh              # every test; exit 0 = all passed
#   bash Tools/Build/skein_ruins_harness/run.sh --self-test  # ...plus negative controls that must FAIL
#   bash Tools/Build/skein_ruins_harness/run.sh --measure    # per-cable counts at I4 (legs, prisms, wheels)
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
OUT="${TMPDIR:-/tmp}/skein_ruins_harness"
mkdir -p "$OUT"

RUINS="$A/Controller/Arcade/Skein/SkeinRuins.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = Skein ruins source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$ROOT/Tools/Build/spearline_harness/NUnitShim.cs" \
    "$HERE/Runner.cs" "$A/Data/Enums/Domains.cs" "$A/Data/Enums/PrismKind.cs" \
    "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" "$A/Controller/Arcade/Spearline/SpearlineSeals.cs" \
    "$A/Controller/Arcade/Spearline/SpearlineRuins.cs" "$A/Controller/Arcade/Skein/SkeinCourse.cs" "$1" \
    "$A/Tests/Editor/SkeinRuinsTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587,CS0660,CS0661 \
    -target:exe -main:Runner -out:"$(native "$OUT/$2.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$2.runtimeconfig.json"
}

run() { "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"; }

build "$RUINS" skein
case "${1:-}" in
  --measure) run skein --measure; exit 0 ;;
esac

set +e; run skein > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of SkeinRuins.cs must make the tests fail.
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$RUINS" > "$OUT/SkeinRuins_neg.cs"
  grep -q "$3" "$OUT/SkeinRuins_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$OUT/SkeinRuins_neg.cs" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate "ruins standing on the cable" 's/ && !ReachesCable(parts\[i\], keepOff, CableKeep));/);/' 'ruins));$'
mutate "launch paths not kept clear" 's/d <= launchLength; d += LaunchStep/d <= 0f; d += LaunchStep/' 'd <= 0f'
mutate "a ruin on every leg" 's/leg % LegStride == 0 && leg \/ LegStride < ruins;/leg >= 0;/' 'leg >= 0;'
mutate "ruins on an easy rung" 's/SpearlineRuins.ForIntensity(intensity)/SpearlineRuins.ForIntensity(4)/' 'ForIntensity(4)'
mutate "a blind wheel sweep" 's/partAlong) < reach && Mathf.Abs(outward - partOut) < reach) return true;/partAlong) < -1f) return true;/' 'partAlong) < -1f'
echo "skein_ruins_harness self-test: ok"
