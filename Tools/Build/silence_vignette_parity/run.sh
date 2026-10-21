#!/usr/bin/env bash
# Parity gate for the silence vignette's SHAPE (Docs/ElementalAbilitySystem/SILENCE.md §5):
# the game's SilenceVignetteMask.cs and the Silence Vignette Lab's JavaScript twin
# (Docs/Studios/SilenceVignetteLab.html) must be the same function, and the lab's SHIPPED block must
# equal Assets/Resources/VesselSilenceConfig.asset. The C# is compiled with a float32 Mathf stub and
# printed on a grid; parity.cjs evaluates the twin on the same grid and compares (1e-4), then runs
# planted controls that must fail.
#
# Needs a dotnet 8 SDK (as Tools/Build/elemental_transfer_harness) and node.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/silence_vignette_parity"
mkdir -p "$OUT"

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -target:exe -main:Driver -out:"$OUT/mask.exe" \
  "$HERE/Stubs.cs" "$HERE/Driver.cs" "$ROOT/Assets/_Scripts/Controller/FX/SilenceVignetteMask.cs" 1>&2

V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
  > "$OUT/mask.runtimeconfig.json"

"$DOTNET" "$OUT/mask.exe" "$HERE/cases.txt" > "$OUT/cs.txt"
node "$HERE/parity.cjs" "$ROOT/Docs/Studios/SilenceVignetteLab.html" \
  "$ROOT/Assets/Resources/VesselSilenceConfig.asset" "$HERE/cases.txt" "$OUT/cs.txt"
