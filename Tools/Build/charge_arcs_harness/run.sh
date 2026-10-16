#!/usr/bin/env bash
# Runs the SHIPPED Charge light core (ChargeArcs.cs) on the real Charge crystal and omni meshes. See Program.cs.
# Needs a .NET 8 SDK (DOTNET_ROOT) and python3. harness.csproj is FORCE-ADDED (`git add -f`): the repo gitignores *.csproj.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
python3 dump.py
dotnet run -c Release --project harness.csproj "$@" 2>&1 | grep -vE "warning CS" | tail -40
exit "${PIPESTATUS[0]}"
