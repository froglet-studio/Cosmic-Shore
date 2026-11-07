#!/usr/bin/env bash
# Compile the SHIPPED ThresherChainSolver.cs (from its real path) with this folder's Driver.cs and Stubs.cs, and print
# the plant probe's JSON (see Driver.cs). thresher_time_sim.py --probe writes it to plant_probe.json beside this file.
# Needs a .NET 8 SDK: $DOTNET_ROOT, ~/.dotnet or /usr/lib/dotnet (apt-get install -y dotnet-sdk-8.0).
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-}"
for d in "$DOTNET" "$HOME/.dotnet" /usr/lib/dotnet /usr/share/dotnet; do
  if [ -n "$d" ] && [ -x "$d/dotnet" ]; then DOTNET="$d"; break; fi
done
if [ ! -x "$DOTNET/dotnet" ]; then echo "No .NET 8 SDK found (apt-get install -y dotnet-sdk-8.0)." >&2; exit 2; fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig $REFS -target:exe -main:Driver \
  -define:UNITY_EDITOR -nowarn:CS0414 -out:"$OUT/x.exe" \
  Tools/Build/thresher_time_sim/Stubs.cs \
  Tools/Build/thresher_time_sim/Driver.cs \
  Assets/_Scripts/Controller/Vessel/ThresherChainSolver.cs >&2
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" \
  > "$OUT/x.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/x.exe" "$@"
