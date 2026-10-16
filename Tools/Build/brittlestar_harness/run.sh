#!/usr/bin/env bash
# Compile the SHIPPED pure Brittle Star code (BrittleStarHullForm, BrittleStarRules, BrittleStarCord) and its SHIPPED
# edit-mode tests (Assets/_Scripts/Tests/Editor/BrittleStarTests.cs) against a UnityEngine math stub and an NUnit shim
# with Roslyn, and RUN every [Test]. Exit 0 = every test passed. `--self-test` additionally plants an inside-out face, a
# third spare ring and a cord that stretches past its length, and proves the tests catch each (negative controls).
# `--obj DIR` writes the shipped hull as OBJ instead (for the card).
# Needs a .NET SDK (8+), found through DOTNET_ROOT (default ~/.dotnet).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
OUT="${TMPDIR:-/tmp}/brittlestar_harness"
mkdir -p "$OUT"

build_and_run() {   # $1 = hull form source, $2 = rules source, $3 = expected (pass|fail)
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" \
    "$ROOT/Assets/_Scripts/Data/Enums/Element.cs" "$1" "$2" "${CORD_SRC:-$CORD}" \
    "$ROOT/Assets/_Scripts/Tests/Editor/BrittleStarTests.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/bs.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/bs.runtimeconfig.json"
  if [ -n "${OBJ_DIR:-}" ]; then "$DOTNET" "$OUT/bs.exe" --obj "$OBJ_DIR"; return; fi
  set +e; "$DOTNET" "$OUT/bs.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$3" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $4"; return 1; fi
  echo "negative control fired: $4 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

FORM="$ROOT/Assets/_Scripts/Controller/Vessel/BrittleStarHullForm.cs"
RULES="$ROOT/Assets/_Scripts/Controller/Vessel/BrittleStarRules.cs"
CORD="$ROOT/Assets/_Scripts/Controller/Vessel/BrittleStarCord.cs"
if [ "${1:-}" = "--obj" ]; then OBJ_DIR="$2" build_and_run "$FORM" "$RULES" pass; exit 0; fi
build_and_run "$FORM" "$RULES" pass
if [ "${1:-}" = "--self-test" ]; then
  # 1. Every face flipped inside out (the orientation test in Tri inverted).
  sed 's/if (Vector3.Dot(n, (a + b + c) \/ 3f - inside) < 0f)/if (Vector3.Dot(n, (a + b + c) \/ 3f - inside) > 0f)/' "$FORM" > "$OUT/FormFlipped.cs"
  grep -q '> 0f)' "$OUT/FormFlipped.cs"
  build_and_run "$OUT/FormFlipped.cs" "$RULES" fail "faces inside out"
  # 2. A crystal past a full stock still adds a spare ring.
  sed 's/if (Spares >= maxSpares) return false;/if (Spares > maxSpares) return false;/' "$RULES" > "$OUT/RulesThreeSpares.cs"
  grep -q 'Spares > maxSpares' "$OUT/RulesThreeSpares.cs"
  build_and_run "$FORM" "$OUT/RulesThreeSpares.cs" fail "three spares held"
  # 3. The cord's links stop being renormalised: a whipping cord stretches past its length.
  sed 's/u = u.sqrMagnitude > 1e-8f ? u.normalized : _direction\[i\];/u = u.sqrMagnitude > 1e-8f ? u : _direction[i];/' "$CORD" > "$OUT/CordStretchy.cs"
  grep -q '? u : _direction' "$OUT/CordStretchy.cs"
  CORD_SRC="$OUT/CordStretchy.cs" build_and_run "$FORM" "$RULES" fail "cord stretches"
fi
