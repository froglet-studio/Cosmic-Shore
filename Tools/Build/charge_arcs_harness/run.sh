#!/usr/bin/env bash
# Runs the SHIPPED Charge light core (ChargeArcs.cs) on the real Charge crystal and omni meshes. See Program.cs.
#   run.sh               the checks (ALL PASS)
#   run.sh --bench       the CPU cost per crystal per frame, with the stub's DOUBLE-precision Mathf.Exp/Sqrt as Unity's
#                        Mathf is; Unity's own number comes from the CrystalChargeArcs.LateUpdate profiler marker
#   run.sh --dump FILE   every frame's light and mesh as raw floats: a speed-up must leave it byte-identical (cmp)
# On a .NET newer than 8 only: DOTNET_ROLL_FORWARD=Major runs the net8.0 build.
# Needs a .NET 8 SDK (DOTNET_ROOT) and python3. harness.csproj is FORCE-ADDED (`git add -f`): the repo gitignores *.csproj.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
python3 dump.py
dotnet run -c Release --project harness.csproj "$@" 2>&1 | grep -vE "warning CS" | tail -40
exit "${PIPESTATUS[0]}"
