#!/usr/bin/env bash
# Compile the SHIPPED substrate (Assets/.../FloraAndFauna/Substrate/*.cs, pure System.Numerics - no Unity) with the
# harness and RUN it: research parameter fidelity, the pack's ring-close-then-strike-together, the locust phase flip,
# the lurker's creep-while-unwatched, the mass ledger, the off-thread tick job and the cost at 10k agents - each
# ASSERTED. Exit code is non-zero on any failure. Docs/SUBSTRATE_FAUNA.md §6.
#
#   bash Tools/Build/substrate_harness/run.sh            # every asserted test
#   bash Tools/Build/substrate_harness/run.sh pack       # one group: fidelity | pack | locust | lurker | ledger | job | index | kernel | bench
#   bash Tools/Build/substrate_harness/run.sh export     # rewrite game_params.json (the species assets' source)
#   bash Tools/Build/substrate_harness/run.sh emotion <jobs.txt>   # the emotion-probe export (Tools/Build/emotion_range, SWARM_FAUNA.md §27)
#
# Needs a dotnet 8 SDK (DOTNET_ROOT, default ~/.dotnet). No .csproj on purpose: the repo gitignores *.csproj.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/substrate_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
SUB="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Substrate"
SW="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm"
# the substrate publishes the swarm's instance contract (SwarmInstance, SwarmJobState) and reuses its index ledger (SwarmEntryLedger)
CORE=("$SUB/SubstrateSpecies.cs" "$SUB/SubstrateFields.cs" "$SUB/SubstrateKernel.cs" "$SUB/SubstrateCore.cs" "$SUB/SubstrateTickJob.cs"
      "$SW/ISwarmCore.cs" "$SW/SwarmFieldCore.cs" "$SW/SwarmTickJob.cs" "$SW/SwarmPrismSync.cs")
# the kernel the game Burst-compiles stays inside what Burst compiles (Burst cannot run here: a textual gate, with a
# negative control - the pre-11c managed step in ReferenceStep.cs must fail it)
# round 11f-2 (group lod): the shared ecology LOD director and its rules (pure; CellEcologyLod.cs is Unity glue)
EF="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Ecology"
LOD=("$SW/SwarmSortCore.cs" "$SW/SwarmGridCore.cs" "$SW/SwarmEvoFateCore.cs")
for f in "$EF"/*.cs; do [[ "$(basename "$f")" == CellEcologyLod.cs ]] || LOD+=("$f"); done
python3 "$HERE/check_burst_substrate.py" "$SUB/SubstrateKernel.cs" "$SUB/SubstrateAgentJob.cs" "$HERE/ReferenceStep.cs" || exit 1
# Unity compiles these against netstandard2.1 + C# 9 - narrower than net8.0. Fail the way Unity would, first.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -out:"$OUT/unityprofile.dll" \
  "${CORE[@]}" || { echo "FAIL: the substrate core does not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -nowarn:CS0649 \
  -target:exe -main:SubstrateHarness -out:"$OUT/substrate.exe" "${CORE[@]}" "$HERE/ReferenceStep.cs" "$HERE/Program.cs" \
  "$HERE/LodHarness.cs" "${LOD[@]}" "$HERE/EmotionSubstrate.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/substrate.runtimeconfig.json"
exec "$DOTNET" "$OUT/substrate.exe" "$HERE/research_params.json" "${1:-all}" "${@:2}"
