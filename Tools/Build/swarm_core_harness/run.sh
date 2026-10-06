#!/usr/bin/env bash
# Compile the SHIPPED swarm sim cores (SwarmFieldCore.cs + SwarmGridCore.cs + SwarmSortCore.cs, pure System.Numerics - no Unity) with
# Program.cs and RUN it: growth, funded laying, selective-kill morphing, vessel reactions, mobbing,
# swimming and the band clamp, each asserted. Exit code is non-zero on any failure.
#
#   bash Tools/Build/swarm_core_harness/run.sh                 # all three cores' asserted tests
#   bash Tools/Build/swarm_core_harness/run.sh <plans> grid    # the grid core's only
#   bash Tools/Build/swarm_core_harness/run.sh <plans> sort    # the sort core's only
#   bash Tools/Build/swarm_core_harness/run.sh <plans> evo     # the evofate core's only
#   SWARM_DENSITY=5 bash Tools/Build/swarm_core_harness/run.sh <plans> tickjob   # round 7: the off-thread tick (R7a-g); round 11a: R11b-e
#   SWARM_DENSITY=5 bash Tools/Build/swarm_core_harness/run.sh <plans> lineage [out.json]   # round 9: regional lineages (R9a-d)
#   SWARM_DENSITY=5 bash Tools/Build/swarm_core_harness/run.sh <plans> emotion <jobs.txt>   # §27: emotion-probe export (Tools/Build/emotion_range)
#   SWARM_DENSITY=5 bash Tools/Build/swarm_core_harness/run.sh <plans> lod      # round 11f: the swarm as an IMacroPopulation
#   bash Tools/Build/swarm_core_harness/run.sh <plans> tandava <tandava plans>   # Tandava: scripted forms + the stage director (T1-T9)
#   bash Tools/Build/swarm_core_harness/run.sh evofate <plans> <fixture.json>   # exactness vs Python (evofate_fixture.py)
#   bash Tools/Build/swarm_core_harness/run.sh export <plans> <out.json> 7,23,41   # states for score_grid.py / score_sort.py
#   bash Tools/Build/swarm_core_harness/run.sh smoothsort <plans> <out.json> 7 researchSortFeelF8 0:1,...  # swarm_smooth events (score_sortfeel.py)
#   bash Tools/Build/swarm_core_harness/run.sh benchsort <plans> researchSort,researchSortFeelF8 1,4,16,64   # ms per swarm-step
#   bash Tools/Build/swarm_core_harness/run.sh yardstick <plans> <out.json> 7,23,41 3 game16,game8   # the 16-transition yardstick (score_combo.py)
#
# Needs a dotnet 8 SDK (a per-user install is fine - see .claude/skills/asset-surgery §4). No
# .csproj on purpose: the repo gitignores *.csproj. Everything builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/swarm_core_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
# Unity compiles these files against its netstandard2.1 API profile, which is NARROWER than net8.0 (e.g. no
# Vector<T>(ReadOnlySpan<T>) constructor - that shipped once as CS1503 in SwarmEvoFateCore and the net8 build below
# could not see it). So compile the five cores against netstandard2.1 first and fail the way Unity would.
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
SW="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm"
TANDAVA="$ROOT/Assets/_Scripts/Controller/Arcade/Tandava/TandavaDirectorCore.cs"   # pure C#: the stage director
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -out:"$OUT/unityprofile.dll" \
  "$SW/ISwarmCore.cs" "$SW/SwarmFieldCore.cs" "$SW/SwarmGridCore.cs" "$SW/SwarmSortCore.cs" "$SW/SwarmEvoFateCore.cs" \
  "$SW/SwarmTickJob.cs" "$SW/SwarmPrismSync.cs" "$TANDAVA" || { echo "FAIL: the sim cores do not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
# Round 11a-2 (§19.4): the body pose the game Burst-compiles (SwarmPoseJob) must stay Burst-compilable - textual gate,
# with the round-11a System.Numerics pose as its negative control.
python3 "$HERE/check_burst_pose.py" "$SW/SwarmPrismSync.cs" "$HERE/TickJobHarness.cs" || exit 1
# Round 11a (Docs/SWARM_FAUNA.md §19.1): the virtual-entry queries a member is found through vs the SHIPPED prism
# predicates, both extracted verbatim from PrismSpatialIndex.cs (R11a). Its own small executable; runs first because
# it takes about a second.
python3 "$HERE/extract_burst_predicates.py" "$ROOT/Assets/_Scripts/Controller/Managers/PrismSpatialIndex.cs" "$OUT/ShippedPrismQuery.g.cs"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -nowarn:CS0649 \
  -target:exe -main:QueryHarness -out:"$OUT/swarmquery.exe" \
  "$HERE/BurstShim.cs" "$OUT/ShippedPrismQuery.g.cs" "$HERE/QueryHarness.cs"
# round 11f: the pure ecology cores (CellEcologyLod.cs is Unity glue - swarm_glue_typecheck covers it)
ECO=(); for f in "$ROOT"/Assets/_Scripts/Controller/Environment/FloraAndFauna/Ecology/*.cs; do
  [[ "$(basename "$f")" == CellEcologyLod.cs ]] || ECO+=("$f"); done
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -target:exe -main:Program -out:"$OUT/swarmcore.exe" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/ISwarmCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmFieldCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmGridCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmSortCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmEvoFateCore.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmTickJob.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Swarm/SwarmPrismSync.cs" \
  "${ECO[@]}" "$TANDAVA" \
  "$HERE/Program.cs" "$HERE/TickJobHarness.cs" "$HERE/GridHarness.cs" "$HERE/SortHarness.cs" "$HERE/SortFeelHarness.cs" "$HERE/EvoHarness.cs" "$HERE/LineageHarness.cs" \
  "$HERE/SwarmLodHarness.cs" "$HERE/Round11dHarness.cs" "$HERE/EmotionExport.cs" "$HERE/TandavaHarness.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/swarmcore.runtimeconfig.json"
cp "$OUT/swarmcore.runtimeconfig.json" "$OUT/swarmquery.runtimeconfig.json"
if [ "${1:-}" = "query" ]; then exec "$DOTNET" "$OUT/swarmquery.exe"; fi
if [ "${2:-}" = "emotion" ]; then exec "$DOTNET" "$OUT/swarmcore.exe" "$@"; fi   # round 11d-2: <plans> emotion <jobs.txt>
case "${1:-}" in export|yardstick|evofate|smoothsort|benchsort|jolt|switchdiag) ;; *) "$DOTNET" "$OUT/swarmquery.exe" || exit 1;; esac
case "${1:-}" in export|yardstick|evofate|smoothsort|benchsort|jolt|switchdiag) exec "$DOTNET" "$OUT/swarmcore.exe" "$@";; esac
"$DOTNET" "$OUT/swarmcore.exe" "${1:-$ROOT/Assets/_SO_Assets/Swarm Fauna/Plans}" "${2:-}" ${3:+"$3"}
