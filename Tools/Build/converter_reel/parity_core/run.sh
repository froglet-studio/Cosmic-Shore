#!/usr/bin/env bash
# Holds Tools/Build/converter_reel/src/combineCore.js to the SHIPPED converter core (OmniCombineCore & co).
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
OUT="${TMPDIR:-/tmp}/converter_reel_core.json"
dotnet run -c Release --project harness.csproj -- ../../../../Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json "$OUT" 2>&1 | grep -vE "warning CS" | tail -5
node ../parity_core.cjs "$OUT" "$@"
