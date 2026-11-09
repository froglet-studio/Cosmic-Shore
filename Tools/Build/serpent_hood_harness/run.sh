#!/usr/bin/env bash
# Compile the SHIPPED Hood geometry (SerpentineGate, SerpentHoodLayout) and its SHIPPED edit-mode tests
# (SerpentHoodLayoutTests) against race_course_source_harness's Unity math stub with Roslyn, and RUN every [Test], no
# Unity. The serpent_time_course_harness pattern.
#
#   bash Tools/Build/serpent_hood_harness/run.sh               # every test; exit 0 = all passed
#   bash Tools/Build/serpent_hood_harness/run.sh --self-test   # ...plus a negative control that must FAIL
#   bash Tools/Build/serpent_hood_harness/run.sh --parts       # every prism as JSON lines (serpent_hood_budget.py --parity)
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
OUT="${TMPDIR:-/tmp}/serpent_hood_harness"
mkdir -p "$OUT"

LAYOUT="$A/Controller/Arcade/SerpentHood/SerpentHoodLayout.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = layout source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/Vector2Shim.cs" "$HERE/NUnitShim.cs" \
    "$HERE/Runner.cs" "$A/Data/Enums/PrismKind.cs" "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$A/Controller/Arcade/SerpentTime/SerpentTimeCluster.cs" "$A/Controller/Arcade/SerpentHood/SerpentineGate.cs" \
    "$1" "$A/Tests/Editor/SerpentHoodLayoutTests.cs"; do
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

build "$LAYOUT" hood
case "${1:-}" in
  --parts) run hood --parts; exit 0 ;;
esac

set +e; run hood > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative control: I4's perches pulled in to I1's radius would crowd each other (and drop shots past the ladder).
sed 's/_ => new Rung(20, 1120f, 6, 660f/_ => new Rung(20, 600f, 6, 560f/' "$LAYOUT" > "$OUT/SerpentHoodLayout_neg.cs"
grep -q 'new Rung(20, 600f' "$OUT/SerpentHoodLayout_neg.cs" || { echo "the I4 radius mutation did not apply"; exit 1; }
build "$OUT/SerpentHoodLayout_neg.cs" neg
set +e; run neg > "$OUT/neg.txt"; nrc=$?; set -e
if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: I4's perches at 600 u"; exit 1; fi
echo "negative control fired: I4's perches at 600 u ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
echo "serpent_hood_harness self-test: ok"
