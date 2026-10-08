#!/usr/bin/env bash
# Compile the SHIPPED BlackHoleConfigSO.cs + BlackHoleToolModel.cs (the Black Hole tool's logic,
# Docs/BLACK_HOLE.md §6.1) with a few UnityEngine stubs and RUN the assertions in Program.cs:
# the tool reaches every config field, every number is bounded, the asset, the SO and the tool
# agree key for key, the size helpers, the clamping, with two negative controls.
#
#   bash Tools/Build/black_hole_tool_harness/run.sh
#
# Needs a dotnet 8 SDK (DOTNET_ROOT, default ~/.dotnet). No .csproj on purpose: the repo gitignores *.csproj.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/black_hole_tool_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
SO="$ROOT/Assets/_Scripts/ScriptableObjects/BlackHoleConfigSO.cs"
MODEL="$ROOT/Assets/_Scripts/Controller/Environment/BlackHole/BlackHoleToolModel.cs"
ASSET="$ROOT/Assets/Resources/BlackHoleConfig.asset"
# The model is compiled in the Editor and development players only; this is a development compile.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -define:DEVELOPMENT_BUILD \
  -nowarn:CS0414 -target:exe -main:BlackHoleToolHarness -out:"$OUT/bhtool.exe" \
  "$HERE/Stubs.cs" "$SO" "$MODEL" "$HERE/Program.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/bhtool.runtimeconfig.json"
exec "$DOTNET" "$OUT/bhtool.exe" "$ASSET" "$SO"
