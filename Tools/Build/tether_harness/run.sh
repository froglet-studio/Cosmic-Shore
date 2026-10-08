#!/usr/bin/env bash
# Compile and RUN the SHIPPED Tether physics and its SHIPPED edit-mode tests, headless.
#
#   bash Tools/Build/tether_harness/run.sh
#
# It compiles, from their real paths and never copied:
#   Assets/_Scripts/Controller/Vessel/{TetherMath,AutoTetherRig,LongTetherRope}.cs
#   Assets/_Scripts/Tests/Editor/TetherMathTests.cs
# against Stubs.cs (real Vector3/Mathf maths + the slice of NUnit the tests use) and runs every
# [Test] by reflection. Exit 0 = all green, 1 = a test failed.
#
# What it proves: the four properties TetherMathTests asserts (hooking keeps speed, the reel stops
# at the spin limit, the release boost applies at a half turn, alternating auto-tethers hold
# heading) plus their negative controls, on the exact code that ships. What it does NOT prove:
# anything about TetherVesselTransformer / TetherExecutor, which bind against the real engine and
# are covered by Tools/Build/unity_refcompile, not here.
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}"
[ -x "$DOTNET/dotnet" ] || DOTNET="$HOME/.dotnet"
if [ ! -x "$DOTNET/dotnet" ]; then
  echo "No dotnet at $DOTNET. Install it (no root needed, ~40s):" >&2
  echo "  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir \"\$HOME/.dotnet\"" >&2
  exit 2
fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -langversion:9.0 -nostdlib -noconfig -nologo $REFS -target:exe -main:Runner \
  -out:"$OUT/tether.exe" \
  Tools/Build/tether_harness/Stubs.cs \
  Tools/Build/tether_harness/Runner.cs \
  Assets/_Scripts/Controller/Vessel/TetherMath.cs \
  Assets/_Scripts/Controller/Vessel/AutoTetherRig.cs \
  Assets/_Scripts/Controller/Vessel/LongTetherRope.cs \
  Assets/_Scripts/Tests/Editor/TetherMathTests.cs
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" \
  > "$OUT/tether.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/tether.exe"
