#!/usr/bin/env bash
# Render the SHIPPED kite juice (KiteNavigatorJuice.cs, compiled from its real path with the card-art harness's Unity shim)
# as a contact sheet: one row per effect, one column per moment, the kite view from above. Exit 1 when a vertex is not
# finite or an effect draws nothing while it should. Needs the per-user .NET 8 SDK (see trace.sh).
#   bash Tools/Build/kite_navigator_lab/juice_sheet.sh [out.png] [pilot]   (pilot: the chase camera's view, in perspective)
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ ! -x "$DOTNET/dotnet" ]; then echo "No per-user dotnet at $DOTNET (see trace.sh)." >&2; exit 2; fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig $REFS -nowarn:CS8632,CS0649 -target:exe -main:JuiceSheet \
  -out:"$OUT/j.exe" \
  Tools/Build/kite_navigator_lab/JuiceSheet.cs \
  Tools/Build/card_art_harness/UnityShim.cs \
  Assets/_Scripts/Data/Enums/Element.cs \
  Assets/_Scripts/UI/View/ScopePetalGeometry.cs \
  Assets/_Scripts/UI/View/ElementPetalGeometry.cs \
  Assets/_Scripts/Controller/Vessel/Crew/KiteNavigatorCore.cs \
  Assets/_Scripts/Controller/Vessel/Crew/KiteBurst.cs \
  Assets/_Scripts/Controller/Vessel/Crew/KiteNavigatorJuice.cs >&2
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" > "$OUT/j.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/j.exe" "$PWD" "${1:-$OUT/juice_sheet.png}" ${2:-}
