#!/usr/bin/env bash
# Roslyn TYPE-CHECK of the swarm's Unity glue against hand-copied stubs of every API it touches
# (Stubs.cs says what that does and does not prove). Library target: nothing runs.
#   bash Tools/Build/swarm_glue_typecheck/run.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/swarm_glue_typecheck"; mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
S="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm"
"$DOTNET_ROOT/dotnet" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" -target:library \
  -nowarn:CS0108,CS0114 -out:"$OUT/glue.dll" "$HERE/Stubs.cs" \
  "$S/SwarmFieldCore.cs" "$S/SwarmFaunaConfigSO.cs" "$S/SwarmPlanLibrary.cs" "$S/SwarmFauna.cs" "$S/SwarmTadpoleFauna.cs"
echo "type-check OK"
