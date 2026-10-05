#!/usr/bin/env bash
# Roslyn TYPE-CHECK of the swarm's Unity glue against hand-copied stubs of every API it touches
# (Stubs.cs says what that does and does not prove). Library target: nothing runs.
#   bash Tools/Build/swarm_glue_typecheck/run.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
# Unity's API profile (netstandard2.1) - narrower than net8.0, and what the editor compiles against
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/swarm_glue_typecheck"; mkdir -p "$OUT"
echo "-r:$NSREF" > "$OUT/refs.rsp"
S="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm"
"$DOTNET_ROOT/dotnet" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" -target:library \
  -nowarn:CS0108,CS0114 -out:"$OUT/glue.dll" "$HERE/Stubs.cs" \
  "$S/ISwarmCore.cs" "$S/SwarmFieldCore.cs" "$S/SwarmGridCore.cs" "$S/SwarmSortCore.cs" "$S/SwarmEvoFateCore.cs" "$S/SwarmFaunaConfigSO.cs" "$S/SwarmPlanLibrary.cs" "$S/SwarmFauna.cs" "$S/SwarmTadpoleFauna.cs" \
  "$S/SwarmTickJob.cs" "$S/SwarmPrismSync.cs" "$S/SwarmMemberRenderer.cs" "$S/../VirtualFauna.cs"
echo "type-check OK"
