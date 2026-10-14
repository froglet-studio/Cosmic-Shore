#!/usr/bin/env bash
# Runs the Elemental Converter's round-5 pieces on the shipped combine data. See Program.cs.
# Needs a .NET 8 SDK (DOTNET_ROOT). harness.csproj is FORCE-ADDED (`git add -f`): the repo gitignores *.csproj.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
dotnet run -c Release --project harness.csproj -- ../../../Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json "$@" 2>&1 | grep -vE "warning CS" | tail -40
exit "${PIPESTATUS[0]}"
