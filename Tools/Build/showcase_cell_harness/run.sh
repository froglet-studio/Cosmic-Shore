#!/usr/bin/env bash
# THE SHOWCASE CELL, ALL TOGETHER (Docs/SWARM_FAUNA.md §25, QA-SWARM-ROUND11-9): every shipped creature core of the
# Swarm cell compiled into ONE harness and run in one world laid out from the authored assets, with three scripted
# pilots. Asserts colliders, combined CPU, the global mass ledger, populations and burns; writes the cell's snapshot.
#
#   bash Tools/Build/showcase_cell_harness/run.sh                 # unit + 3 seeds x 5 min + 1 x 30 min (+ snapshot)
#   bash Tools/Build/showcase_cell_harness/run.sh quick           # unit + 1 seed x 2 min
#   bash Tools/Build/showcase_cell_harness/run.sh all <snap.json> # also write the minute-5 snapshot (render.py draws it)
#
# Findings (an ecology or game-feel outcome: a class goes extinct, the skilled pilot burns more) are reported, not
# failed; SHOWCASE_STRICT=1 fails them too. The laws (colliders, CPU, ledger, caps, the LOD contract, U1-U4) always fail.
# Draw the snapshot: python3 Tools/Build/showcase_cell_harness/render.py <snap.json> <out.png>
#
# Diagnostic knobs (none changes a default run): SHOWCASE_TRACE=1 (per-minute census, plates, bites),
# SHOWCASE_TRACE_EVERY=N (per-population substrate state every N ticks: hunger, phase, distance to flora tissue),
# SHOWCASE_LOD_TRACE=1 (each swarm's LOD state every 10 s), SHOWCASE_LOD=0 (the cell without the ecology LOD),
# SHOWCASE_PILOTS=<n> (park all but the first n pilots), SHOWCASE_SUBSTRATE=<key,...> (only these substrate species),
# SHOWCASE_SEED_AT_FLORA=1 (seed every substrate population at a plant), SHOWCASE_FOOD_MASS=1 (weigh each plant heart's
# food point by its leaf volume, not the game's 1), SHOWCASE_FOOD_PRISMS=1 (one food point per leaf prism, the substrate harness's food), SHOWCASE_FOOD_BAND=1
# (with FOOD_PRISMS: only leaves inside the first population's band).
#
# Needs a dotnet 8 SDK (DOTNET_ROOT). No .csproj on purpose (the repo gitignores *.csproj); builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/showcase_cell_harness"
mkdir -p "$OUT"
FF="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna"
SW="$FF/Swarm"; SUB="$FF/Substrate"; B="$FF/Builders"; TF="$FF/ThreatFlora"

# the pure cores that ship - one line per creature family
CORES=("$SW/ISwarmCore.cs" "$SW/SwarmFieldCore.cs" "$SW/SwarmGridCore.cs" "$SW/SwarmSortCore.cs" "$SW/SwarmEvoFateCore.cs" "$SW/SwarmTickJob.cs" "$SW/SwarmPrismSync.cs")
# round 11f ecology LOD: the pure cores (CellEcologyLod.cs is the Unity host - the harness drives the director as it does)
for f in "$FF"/Ecology/*.cs; do [[ "$(basename "$f")" == CellEcologyLod.cs ]] || CORES+=("$f"); done
CORES+=("$SUB/SubstrateSpecies.cs" "$SUB/SubstrateFields.cs" "$SUB/SubstrateKernel.cs" "$SUB/SubstrateCore.cs" "$SUB/SubstrateTickJob.cs")
CORES+=("$B/BuilderCore.cs" "$B/BuilderColonyCore.cs" "$B/ThiefNestCore.cs" "$B/WearerCore.cs")
HARNESS=("$HERE/CellWorld.cs" "$HERE/Cell.cs" "$HERE/Systems.cs" "$HERE/Program.cs")
DEFINES=""
# round 11c threat flora: its cores, its adapter and the define that registers it (absent = the cell without a grove)
if [ -f "$TF/SnapTrapCore.cs" ]; then CORES+=("$TF/ThreatFloraMath.cs" "$TF/SnapTrapCore.cs" "$TF/PhysarumCore.cs" "$TF/ThreatGroveDefaults.cs"); HARNESS+=("$HERE/GroveSystem.cs"); DEFINES="-define:THREAT_FLORA"; fi

# the layout, read from the author scripts and the assets they wrote (never re-typed)
python3 "$HERE/layout.py" "$OUT/layout.json"
# the plan loader upsamples to PlanDensity (textual gate on the Unity-only file, with its negative control)
python3 "$HERE/check_plan_density.py" --self-test
python3 "$HERE/check_plan_density.py" "$SW/SwarmPlanLibrary.cs"

# Unity compiles the cores against netstandard2.1 + C# 9 - narrower than net8.0. Fail the way Unity would, first.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -out:"$OUT/unityprofile.dll" \
  "${CORES[@]}" || { echo "FAIL: the cores do not compile together against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
"$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -nowarn:CS0649,CS8632 $DEFINES \
  -target:exe -main:Program -out:"$OUT/showcase.exe" "${CORES[@]}" "${HARNESS[@]}"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/showcase.runtimeconfig.json"
exec "$DOTNET" "$OUT/showcase.exe" "$OUT/layout.json" "$ROOT/Assets/_SO_Assets/Swarm Fauna/Plans" "${1:-all}" "${2:-}"
