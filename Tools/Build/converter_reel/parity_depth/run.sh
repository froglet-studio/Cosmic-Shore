#!/usr/bin/env bash
# Dumps the shipped depth-prism column maths for parity_depth.cjs (which runs this itself). See Program.cs.
# Needs a .NET 8 SDK (DOTNET_ROOT, default $HOME/.dotnet). harness.csproj must be FORCE-ADDED (`git add -f`): the repo
# gitignores *.csproj for Unity's own.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
OUT="${1:-${TMPDIR:-/tmp}/converter_reel_depth_cs.json}"
dotnet run -c Release --project harness.csproj -- ../data/depth.json ../../../../Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json "$OUT" 2>&1 | grep -vE "warning CS" | tail -20
exit "${PIPESTATUS[0]}"
