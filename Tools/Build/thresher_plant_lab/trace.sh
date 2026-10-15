#!/usr/bin/env bash
# Compile the SHIPPED ThresherChainSolver.cs (from its real path) with the chain harness's Unity stubs and
# TraceDriver.cs, run it over scenarios.json, and write the reference trace to $1 (default: stdout).
# Needs the per-user .NET 8 SDK that Tools/Build/thresher_chain_harness/run.sh documents.
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ ! -x "$DOTNET/dotnet" ]; then
  echo "No per-user dotnet at $DOTNET - see Tools/Build/thresher_chain_harness/run.sh for the install line." >&2
  exit 2
fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -langversion:9.0 -nostdlib -noconfig $REFS -target:exe -main:TraceDriver \
  -nowarn:CS0414 -out:"$OUT/t.exe" \
  Tools/Build/thresher_chain_harness/Stubs.cs \
  Tools/Build/thresher_plant_lab/TraceDriver.cs \
  Assets/_Scripts/Controller/Vessel/ThresherChainSolver.cs
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" \
  > "$OUT/t.runtimeconfig.json"
if [ -n "${1:-}" ]; then "$DOTNET/dotnet" "$OUT/t.exe" Tools/Build/thresher_plant_lab/scenarios.json > "$1"
else "$DOTNET/dotnet" "$OUT/t.exe" Tools/Build/thresher_plant_lab/scenarios.json; fi
