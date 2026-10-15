#!/usr/bin/env bash
# Compile the SHIPPED pure Shrike code (ShrikeHullForm, ShrikeRules) and its SHIPPED edit-mode tests
# (Assets/_Scripts/Tests/Editor/ShrikeTests.cs) against a UnityEngine math stub and an NUnit shim with Roslyn, and RUN
# every [Test]. Exit 0 = every test passed. `--self-test` additionally plants an inside-out frustum and a nest that
# fires its last health block and proves the tests catch both (negative controls). `--obj DIR` writes the shipped hull
# as OBJ instead (for the card). Needs a .NET SDK (8+), found through DOTNET_ROOT (default ~/.dotnet).
# The Brittle Star harness's shape (Tools/Build/brittlestar_harness).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
OUT="${TMPDIR:-/tmp}/shrike_harness"
mkdir -p "$OUT"

build_and_run() {   # $1 = hull form source, $2 = rules source, $3 = expected (pass|fail), $4 = what
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$1" "$2" \
    "$ROOT/Assets/_Scripts/Tests/Editor/ShrikeTests.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/shrike.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/shrike.runtimeconfig.json"
  if [ -n "${OBJ_DIR:-}" ]; then "$DOTNET" "$OUT/shrike.exe" --obj "$OBJ_DIR"; return; fi
  set +e; "$DOTNET" "$OUT/shrike.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$3" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $4"; return 1; fi
  echo "negative control fired: $4 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

FORM="$ROOT/Assets/_Scripts/Controller/Vessel/ShrikeHullForm.cs"
RULES="$ROOT/Assets/_Scripts/Controller/Vessel/ShrikeRules.cs"
if [ "${1:-}" = "--obj" ]; then OBJ_DIR="$2" build_and_run "$FORM" "$RULES" pass; exit 0; fi
build_and_run "$FORM" "$RULES" pass
if [ "${1:-}" = "--self-test" ]; then
  # 1. The frustum's sides wound inside out.
  sed 's/Quad(m, p00, p01, p11, p10, accent)/Quad(m, p00, p10, p11, p01, accent)/' "$FORM" > "$OUT/FormFlipped.cs"
  grep -q 'Quad(m, p00, p10, p11, p01, accent)' "$OUT/FormFlipped.cs"
  build_and_run "$OUT/FormFlipped.cs" "$RULES" fail "frustum sides inside out"
  # 2. A nest that fires its last health block.
  sed 's/health > 1 \&\& firedForLock/health > 0 \&\& firedForLock/' "$RULES" > "$OUT/RulesLastBlock.cs"
  grep -q 'health > 0 && firedForLock' "$OUT/RulesLastBlock.cs"
  build_and_run "$FORM" "$OUT/RulesLastBlock.cs" fail "fires its last health block"
fi
