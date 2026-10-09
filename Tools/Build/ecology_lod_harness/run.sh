#!/usr/bin/env bash
# Round 11f (Docs/ECOLOGY_LOD.md): compile the SHIPPED hierarchical-ecology cores (FloraAndFauna/Ecology/*.cs, pure C#)
# plus the swarm cores the swarm's macro state drives, and RUN the asserted gates. Exit code is non-zero on any failure.
#
#   bash Tools/Build/ecology_lod_harness/run.sh                   # every group (~minutes)
#   bash Tools/Build/ecology_lod_harness/run.sh cons,cont         # chosen groups: stomach cons cont consist cycles cost (swarm: swarm_core_harness lod)
#   ECO_SEEDS=6 ECO_CYCLE_HOURS=8 bash Tools/Build/ecology_lod_harness/run.sh consist,cycles
#
# Needs a dotnet 8 SDK. Use a PRIVATE TMPDIR when other workers share the machine. No .csproj (the repo gitignores them).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/ecology_lod_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
FF="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna"
# the pure cores (CellEcologyLod.cs is Unity glue - Tools/Build/swarm_glue_typecheck covers it)
ECO=(); for f in "$FF"/Ecology/*.cs; do [[ "$(basename "$f")" == CellEcologyLod.cs ]] || ECO+=("$f"); done
SW=("$FF/Swarm/ISwarmCore.cs" "$FF/Swarm/SwarmFieldCore.cs" "$FF/Swarm/SwarmGridCore.cs" "$FF/Swarm/SwarmSortCore.cs"
    "$FF/Swarm/SwarmEvoFateCore.cs" "$FF/Swarm/SwarmTickJob.cs" "$FF/Swarm/SwarmPrismSync.cs" "$FF/Swarm/KernelMath.cs")
# Unity compiles against netstandard2.1 + C# 9: fail the way Unity would first
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -warnaserror -nowarn:CS1591 \
  -out:"$OUT/unityprofile.dll" "${ECO[@]}" "${SW[@]}" \
  || { echo "FAIL: the ecology cores do not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -nowarn:CS0649 \
  -target:exe -main:EcologyLodHarness -out:"$OUT/ecolod.exe" \
  "${ECO[@]}" "${SW[@]}" "$HERE/Program.cs" "$HERE/StomachHarness.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/ecolod.runtimeconfig.json"
exec "$DOTNET" "$OUT/ecolod.exe" "$@"
