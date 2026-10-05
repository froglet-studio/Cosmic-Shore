#!/usr/bin/env bash
# Compile the SHIPPED controlling-domain resolver (Domains.cs + InitialControllingDomain.cs - pure, no
# Unity) with Program.cs and RUN it (QA-SWARM-ROUND11-13). Exit code is non-zero on any failure.
#
#   bash Tools/Build/cell_control_harness/run.sh
#
# Needs a dotnet 8 SDK. No .csproj on purpose: the repo gitignores *.csproj. Builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/lib/dotnet}"
[ -x "$DOTNET_ROOT/dotnet" ] || DOTNET_ROOT="$HOME/.dotnet"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/cell_control_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
EN="$ROOT/Assets/_Scripts/Data/Enums"
SRC=("$EN/Domains.cs" "$EN/InitialControllingDomain.cs")
# Unity's API profile first (netstandard2.1, C# 9), so a construct Unity rejects fails here too.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library \
  -out:"$OUT/unityprofile.dll" "${SRC[@]}" \
  || { echo "FAIL: the resolver does not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" -target:exe \
  -out:"$OUT/cellcontrol.exe" "${SRC[@]}" "$HERE/Program.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
  > "$OUT/cellcontrol.runtimeconfig.json"
exec "$DOTNET" "$OUT/cellcontrol.exe" \
  "$ROOT/Assets/_Scripts/Controller/Environment/Cell.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs"
