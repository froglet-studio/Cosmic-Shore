#!/usr/bin/env bash
# Compile the SHIPPED OrcaCrewCore.cs (from its real path; it needs no engine stubs) with TraceDriver.cs and print the
# reference trace. Args pass through to the driver (horizonSmooth, missileTurn, shieldOpenDeg) for the planted controls.
# Needs a per-user .NET 8 SDK under $HOME/.dotnet:
#   curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet"
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ ! -x "$DOTNET/dotnet" ]; then echo "No per-user dotnet at $DOTNET (see the install line at the top of this file)." >&2; exit 2; fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -langversion:9.0 -nostdlib -noconfig $REFS -target:exe -main:TraceDriver -out:"$OUT/t.exe" \
  Tools/Build/orca_crew_lab/TraceDriver.cs \
  Assets/_Scripts/Controller/Vessel/Crew/OrcaCrewCore.cs >&2
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" > "$OUT/t.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/t.exe" "$@"
