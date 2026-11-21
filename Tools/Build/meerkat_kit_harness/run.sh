#!/usr/bin/env bash
# Compile the SHIPPED MeerkatSolitonMath (the Meerkat's kit: Garrett's design, approved by Yash 2026-10-28) and its SHIPPED
# edit-mode tests (Assets/_Scripts/Tests/Editor/MeerkatSolitonMathTests.cs), with SolitonTunnelShapeMath and its
# SolitonTunnelShapeMathTests ([TestCase] rows, NUnitTestCase.cs), against a UnityEngine
# math stub (Stubs.cs, the brittlestar harness's plus a matrix) and NUnit shim with Roslyn, and RUN every [Test]. Exit 0 = every test passed. `--self-test` also plants three
# wrong ports (a Time L5 Boost Out that boosts nothing, a pool that retires the NEWEST, a portal on the corners instead of
# the edges) and proves the tests catch each (negative controls).
# Needs a .NET SDK (8+), found through DOTNET_ROOT (default ~/.dotnet).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
OUT="${TMPDIR:-/tmp}/meerkat_kit_harness"
mkdir -p "$OUT"
MATH="$ROOT/Assets/_Scripts/Controller/Vessel/R_VesselActions/MeerkatSolitonMath.cs"
TESTS="$ROOT/Assets/_Scripts/Tests/Editor/MeerkatSolitonMathTests.cs"
# ...and the wormhole's own arithmetic and tests (round 23's surface area and swallow goldens live there)
WMATH="$ROOT/Assets/_Scripts/Controller/Environment/SolitonTunnel/SolitonTunnelShapeMath.cs"
WTESTS="$ROOT/Assets/_Scripts/Tests/Editor/SolitonTunnelShapeMathTests.cs"
RMATH="$ROOT/Assets/_Scripts/Controller/Environment/SolitonTunnel/SolitonTunnelMath.cs"   # the slip and slide (SolitonTunnelShapeMathTests reads it)
RTESTS="$ROOT/Assets/_Scripts/Tests/Editor/SolitonTunnelMathTests.cs"   # the network layout goldens and the slide's rules

build_and_run() {   # $1 = math source, $2 = expected (pass|fail), $3 = what
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/NUnitShim.cs" "$HERE/NUnitTestCase.cs" \
    "$HERE/Runner.cs" "$1" "$TESTS" "$WMATH" "$WTESTS" "$RMATH" "$RTESTS" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/mk.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/mk.runtimeconfig.json"
  set +e; "$DOTNET" "$OUT/mk.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$2" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $3"; return 1; fi
  echo "negative control fired: $3 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

build_and_run "$MATH" pass
if [ "${1:-}" = "--self-test" ]; then
  sed 's/rideExitMultiplier \* Mathf.Max(1f, boostOut)/rideExitMultiplier/' "$MATH" > "$OUT/NoBoostOut.cs"
  grep -q 'boostOutUnlocked ? rideExitMultiplier :' "$OUT/NoBoostOut.cs"
  build_and_run "$OUT/NoBoostOut.cs" fail "Boost Out that boosts nothing"
  sed 's/retired.Add(placed\[0\]);/retired.Add(placed[placed.Count - 1]);/; s/placed.RemoveAt(0);/placed.RemoveAt(placed.Count - 1);/' "$MATH" > "$OUT/PoolNewest.cs"
  grep -q 'placed.RemoveAt(placed.Count - 1);' "$OUT/PoolNewest.cs"
  build_and_run "$OUT/PoolNewest.cs" fail "the pool retires the newest"
  sed 's/float a = (k + 0.5f) \/ PortalCount/float a = (k + 0f) \/ PortalCount/' "$MATH" > "$OUT/PortalCorners.cs"
  grep -q '(k + 0f) / PortalCount' "$OUT/PortalCorners.cs"
  build_and_run "$OUT/PortalCorners.cs" fail "the portal on the corners"
fi
