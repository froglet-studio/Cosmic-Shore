#!/usr/bin/env bash
# Compile the SHIPPED swarm sim cores (SwarmFieldCore.cs + SwarmGridCore.cs, pure System.Numerics - no Unity) with
# Program.cs and RUN it: growth, funded laying, selective-kill morphing, vessel reactions, mobbing,
# swimming and the band clamp, each asserted. Exit code is non-zero on any failure.
#
#   bash Tools/Build/swarm_core_harness/run.sh                 # both cores' asserted tests
#   bash Tools/Build/swarm_core_harness/run.sh <plans> grid    # the grid core's only
#   bash Tools/Build/swarm_core_harness/run.sh export <plans> <out.json> 7,23,41   # states for score_grid.py
#
# Needs a dotnet 8 SDK (a per-user install is fine - see .claude/skills/asset-surgery §4). No
# .csproj on purpose: the repo gitignores *.csproj. Everything builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/swarm_core_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -target:exe -main:Program -out:"$OUT/swarmcore.exe" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/ISwarmCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmFieldCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmGridCore.cs" \
  "$HERE/Program.cs" "$HERE/GridHarness.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/swarmcore.runtimeconfig.json"
if [ "${1:-}" = "export" ]; then exec "$DOTNET" "$OUT/swarmcore.exe" "$@"; fi
"$DOTNET" "$OUT/swarmcore.exe" "${1:-$ROOT/Assets/_SO_Assets/Swarm Fauna/Plans}" "${2:-}"
