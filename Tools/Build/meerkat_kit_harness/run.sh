#!/usr/bin/env bash
# Compile the SHIPPED MeerkatSolitonMath (the Meerkat's kit: Garrett's design, approved by Yash 2026-10-28) and its SHIPPED
# edit-mode tests (Assets/_Scripts/Tests/Editor/MeerkatSolitonMathTests.cs) against the brittlestar harness's UnityEngine
# math stub and NUnit shim with Roslyn, and RUN every [Test]. Exit 0 = every test passed. `--self-test` also plants three
# wrong ports (the area spread over the mouth instead of the throat, a pool that retires the NEWEST, a portal on the
# corners instead of the edges) and proves the tests catch each (negative controls).
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

build_and_run() {   # $1 = math source, $2 = expected (pass|fail), $3 = what
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/../brittlestar_harness/Stubs.cs" "$HERE/../brittlestar_harness/NUnitShim.cs" "$HERE/Runner.cs" \
    "$1" "$TESTS" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
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
  sed 's/AreaFactor(k));$/AreaFactor(k)) \/ k;/' "$MATH" > "$OUT/AreaOnMouth.cs"
  grep -q 'AreaFactor(k)) / k;' "$OUT/AreaOnMouth.cs"
  build_and_run "$OUT/AreaOnMouth.cs" fail "the area spread over the mouth"
  sed 's/retired.Add(placed\[0\]);/retired.Add(placed[placed.Count - 1]);/; s/placed.RemoveAt(0);/placed.RemoveAt(placed.Count - 1);/' "$MATH" > "$OUT/PoolNewest.cs"
  grep -q 'placed.RemoveAt(placed.Count - 1);' "$OUT/PoolNewest.cs"
  build_and_run "$OUT/PoolNewest.cs" fail "the pool retires the newest"
  sed 's/float a = (k + 0.5f) \/ PortalCount/float a = (k + 0f) \/ PortalCount/' "$MATH" > "$OUT/PortalCorners.cs"
  grep -q '(k + 0f) / PortalCount' "$OUT/PortalCorners.cs"
  build_and_run "$OUT/PortalCorners.cs" fail "the portal on the corners"
fi
