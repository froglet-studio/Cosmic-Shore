#!/usr/bin/env bash
# Compile the SHIPPED pure Goby & Pistol code (PistolPlane, PistolBodyMath, PistolPaths, PistolAI, GobyDen) and its
# SHIPPED pure tests (Assets/_Scripts/Tests/Editor/GobyPistolTrailHoleTests.cs, GobyPistolEnergyTests.cs) against a
# UnityEngine math stub and an NUnit shim with Roslyn, and RUN every [Test]. Exit 0 = every test passed. `--self-test`
# additionally plants four defects - a grounded Pistol that ignores holes (phantom ground under it), a fallen one that
# lands again, a flight that pays no more for big jumps, and a stretch that counts a shared block twice - and proves the
# tests catch each (negative controls). Needs a .NET SDK (8+), found through DOTNET_ROOT (default ~/.dotnet):
#   bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 --install-dir $HOME/.dotnet --no-path
# The Shrike harness's shape (Tools/Build/shrike_harness).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
OUT="${TMPDIR:-/tmp}/goby_pistol_harness"
mkdir -p "$OUT"
DIR="$ROOT/Assets/_Scripts/Controller/Vessel/Crew/GobyPistol"

build_and_run() {   # $1 = body math source, $2 = expected (pass|fail), $3 = what, $4 = plane source, $5 = den source
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/NUnitShim.cs" "$HERE/Runner.cs" "$1" \
    "${4:-$DIR/PistolPlane.cs}" "${5:-$DIR/GobyDen.cs}" "$DIR/PistolPaths.cs" "$ROOT/Assets/_Scripts/Controller/AI/Goby/PistolAI.cs" \
    "$ROOT/Assets/_Scripts/Data/Enums/PrismKind.cs" \
    "$ROOT/Assets/_Scripts/Tests/Editor/GobyPistolTrailHoleTests.cs" "$ROOT/Assets/_Scripts/Tests/Editor/GobyPistolEnergyTests.cs" \
    | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -define:UNITY_EDITOR "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/goby.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/goby.runtimeconfig.json"
  set +e; "$DOTNET" "$OUT/goby.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$2" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $3"; return 1; fi
  echo "negative control fired: $3 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

BODY="$DIR/PistolBodyMath.cs"
build_and_run "$BODY" pass
if [ "${1:-}" = "--self-test" ]; then
  # 1. Phantom ground: a grounded Pistol rides on over a hole as if its blocks still stood.
  sed 's/if (PistolPlane.SolidAt(f, p.x)) { p.y = PistolPlane.Ground(f, p.x); return; }/{ p.y = PistolPlane.Ground(f, p.x); return; }/' "$BODY" > "$OUT/BodyPhantom.cs"
  grep -q '{ p.y = PistolPlane.Ground(f, p.x); return; }' "$OUT/BodyPhantom.cs"
  build_and_run "$OUT/BodyPhantom.cs" fail "grounded over a hole"
  # 2. A fallen Pistol lands again on the ground past the hole (under it).
  sed 's/if (p.falling || !PistolPlane.SolidAt(f, p.x))/if (!PistolPlane.SolidAt(f, p.x))/' "$BODY" > "$OUT/BodyRelands.cs"
  grep -q 'if (!PistolPlane.SolidAt(f, p.x))$' "$OUT/BodyRelands.cs"
  build_and_run "$OUT/BodyRelands.cs" fail "a fallen Pistol lands again"
  # 3. Energy that pays no more for a big jump than for as many hops.
  sed 's/(1f + Mathf.Max(0f, flightBonus) \* Mathf.Max(0, nth))/1f/' "$DIR/GobyDen.cs" > "$OUT/DenFlat.cs"
  grep -q 'Mathf.Max(0f, perBlock) \* 1f;' "$OUT/DenFlat.cs"
  build_and_run "$BODY" fail "big jumps pay no more" "$DIR/PistolPlane.cs" "$OUT/DenFlat.cs"
  # 4. A stretch that counts the block it starts on (two stretches meeting on a block count it twice).
  sed 's/return up ? b > lo \&\& b <= hi : b >= lo \&\& b < hi;/return b >= lo \&\& b <= hi;/' "$DIR/PistolPlane.cs" > "$OUT/PlaneTwice.cs"
  grep -q 'return b >= lo && b <= hi;' "$OUT/PlaneTwice.cs"
  build_and_run "$BODY" fail "a shared block counted twice" "$OUT/PlaneTwice.cs"
fi
