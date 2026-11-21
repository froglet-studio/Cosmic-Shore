#!/usr/bin/env bash
# Compile the SHIPPED Spearline seal code (RaceCourseGeometry, HeadlongCircuit, SpearlineSeals) and its SHIPPED edit-mode
# tests (SpearlineSealsTests) against race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN
# every [Test] - no Unity.
#
#   bash Tools/Build/spearline_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/spearline_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/spearline_harness/run.sh --export I S... # JSON lines: the lap, and passes per policy per seal
#                                                             # (the input of Tools/Build/spearline_race_model.py)
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
OUT="${TMPDIR:-/tmp}/spearline_harness"
mkdir -p "$OUT"

SEALS="$A/Controller/Arcade/Spearline/SpearlineSeals.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = seals source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$A/Data/Enums/PrismKind.cs" "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" "$1" "$A/Tests/Editor/SpearlineSealsTests.cs"; do
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

build "$SEALS" spearline
case "${1:-}" in
  --export) run spearline "$@"; exit 0 ;;
esac

set +e; run spearline > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of SpearlineSeals.cs must make the tests fail.
negative() {   # $1 = label, $2 = seals source
  build "$2" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$SEALS" > "$OUT/SpearlineSeals_neg.cs"
  grep -q "$3" "$OUT/SpearlineSeals_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/SpearlineSeals_neg.cs"
}
mutate "need under the hull's own hole" 's/NeedPrisms = 8,/NeedPrisms = 6,/' 'NeedPrisms = 6,'
mutate "need over a thin stroke" 's/NeedPrisms = 8,/NeedPrisms = 11,/' 'NeedPrisms = 11,'
mutate "no wear (a seal can trap a pilot)" 's/WearPerPass = 2,/WearPerPass = 0,/' 'WearPerPass = 0,'
mutate "armour that does not hold" 's/if (!gone\[i\] \&\& !breaksUnderHull\[i\]) continue;/if (!gone[i] \&\& false) continue;/' '&& false) continue;'
mutate "Headlong's 25% mouth step" 's/MouthByRung = { 96f, 84f, 74f, 66f }/MouthByRung = { 96f, 72f, 62f, 54f }/' '96f, 72f, 62f'
mutate "the hardest corners sealed" 's/int c = turn\[x\].CompareTo(turn\[y\]);/int c = turn[y].CompareTo(turn[x]);/' 'turn\[y\].CompareTo(turn\[x\])'
mutate "armour everywhere at I2 (no plain face to read)" 's/ArmourByRung = { 0f, 120f, 180f, 240f }/ArmourByRung = { 0f, 330f, 180f, 240f }/' '0f, 330f'
mutate "the read ignores armour (cuts it as plain)" 's/                        Vector3 band = s < 0 ? Vector3.zero : strokes\[s\];/                        Vector3 band = strokes.Count > 0 ? strokes[0] : Vector3.zero;/' 'strokes.Count > 0 ? strokes\[0\]'
echo "spearline_harness self-test: ok"
