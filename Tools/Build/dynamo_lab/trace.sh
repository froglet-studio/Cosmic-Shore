#!/usr/bin/env bash
# Compile the SHIPPED DynamoCore.cs (from its real path; it needs no engine stubs) with TraceDriver.cs, run it over the
# scenarios file $1 and print the reference trace. Needs a per-user .NET 8 under $HOME/.dotnet (see
# Tools/Build/kite_navigator_lab/trace.sh for the install line; where builds.dotnet.microsoft.com is blocked, the same
# layout can be assembled from the NuGet packages, as recorded in Docs/Studios/HONEYGUIDE_LAB.md).
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ ! -x "$DOTNET/dotnet" ]; then echo "No per-user dotnet at $DOTNET." >&2; exit 2; fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -langversion:9.0 -nullable:disable -nostdlib -noconfig $REFS -target:exe -main:TraceDriver -out:"$OUT/t.exe" \
  Tools/Build/dynamo_lab/TraceDriver.cs \
  Assets/_Scripts/Controller/Vessel/Crew/DynamoCore.cs >&2
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" > "$OUT/t.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/t.exe" "$1"
