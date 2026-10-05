#!/usr/bin/env bash
# Roslyn TYPE-CHECK of the substrate's Unity glue (Docs/SUBSTRATE_FAUNA.md §6) against hand-copied stubs of every API
# it touches (Stubs.cs says what that does and does not prove). Library target: nothing runs. The sibling of
# Tools/Build/swarm_glue_typecheck - same profile (netstandard2.1, C# 9), its own stubs.
#   bash Tools/Build/substrate_glue_typecheck/run.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/substrate_glue_typecheck"; mkdir -p "$OUT"
echo "-r:$NSREF" > "$OUT/refs.rsp"
S="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Substrate"
W="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm"
# every .cs in Substrate/ - a new glue file is type-checked without editing this script
mapfile -t SUB < <(ls "$S"/*.cs)
"$DOTNET_ROOT/dotnet" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" -target:library \
  -warnaserror+ -nowarn:CS0108,CS0114,CS1574,CS0649 -out:"$OUT/glue.dll" "$HERE/Stubs.cs" "${SUB[@]}" \
  "$W/ISwarmCore.cs" "$W/SwarmFieldCore.cs" "$W/SwarmTickJob.cs" "$W/SwarmMemberQuery.cs"
echo "type-check OK (${#SUB[@]} substrate files)"
