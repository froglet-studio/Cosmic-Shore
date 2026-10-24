#!/usr/bin/env bash
# Compile the SHIPPED FleaMath.cs and its SHIPPED edit-mode tests (Assets/_Scripts/Tests/Editor/FleaMathTests.cs) against
# a UnityEngine math stub and an NUnit shim with Roslyn, and RUN every [Test]. Exit 0 = every test passed.
# `--self-test` also plants two defects the tests must catch (negative controls): a perfect window that is not halved,
# and a dull bounce that keeps its speed. Needs a .NET SDK (8+), through DOTNET_ROOT (default ~/.dotnet).
# The goldens the tests hold come from goldens.cjs (the Vessel Studio page's own Flea code run in node). The Shrike
# harness's shape (Tools/Build/shrike_harness).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
OUT="${TMPDIR:-/tmp}/flea_harness"
mkdir -p "$OUT"

build_and_run() {   # $1 = FleaMath source, $2 = expected (pass|fail), $3 = what
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$1" \
    "$ROOT/Assets/_Scripts/Tests/Editor/FleaMathTests.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/flea.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/flea.runtimeconfig.json"
  set +e; "$DOTNET" "$OUT/flea.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$2" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $3"; return 1; fi
  echo "negative control fired: $3 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

MATH="$ROOT/Assets/_Scripts/Controller/Vessel/FleaMath.cs"
build_and_run "$MATH" pass
if [ "${1:-}" = "--self-test" ]; then
  sed 's|time - s.pressT <= p.window / 2f|time - s.pressT <= p.window|' "$MATH" > "$OUT/WindowNotHalved.cs"
  grep -q 'time - s.pressT <= p.window;' "$OUT/WindowNotHalved.cs"
  build_and_run "$OUT/WindowNotHalved.cs" fail "the perfect window not halved"
  sed 's|(perfect ? p.perfectGain : p.dullLoss)|(perfect ? p.perfectGain : 1f)|' "$MATH" > "$OUT/DullKeepsSpeed.cs"
  grep -q '(perfect ? p.perfectGain : 1f)' "$OUT/DullKeepsSpeed.cs"
  build_and_run "$OUT/DullKeepsSpeed.cs" fail "a dull bounce keeps its speed"
fi
