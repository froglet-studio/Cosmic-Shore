#!/usr/bin/env bash
# Charge light parity: builds the C# reference (parity_charge/, the SHIPPED ChargeArcs.cs on the shipped combine data)
# and holds the JS port (src/chargeArcs.js) to it with parity_charge.cjs, negative controls included. Also checks that
# data/charge_arcs.json is current. Needs a .NET 8 SDK (DOTNET_ROOT, default ~/.dotnet), node and python3.
# parity_charge/harness.csproj must be FORCE-ADDED (`git add -f`): the repo gitignores *.csproj.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
OUT="${1:-$(mktemp -d)}"
python3 extract_charge_arcs.py --check
dotnet run -c Release --project parity_charge/harness.csproj -- ../../.. "$OUT" 2>&1 | grep -vE "warning CS" | tail -5
node parity_charge.cjs "$OUT"
