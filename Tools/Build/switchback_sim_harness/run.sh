#!/usr/bin/env bash
# Switchback Blast Run offline simulator: compiles the SHIPPED course, Blast Run layout, blast planner and
# prism-avoidance code out of Assets/ against the Skim Race harness's Unity shim (+ ShimExtra.cs), and races a
# kinematic Dolphin flown by the AI's executor (SwitchbackBlastAI, GateRaceController, AIPilot and
# ChargeBoostAIPolicySO, ported in Sim.cs). SWITCHBACK.md "Offline: the Blast Run simulator" has the model and its limits.
#
#   bash Tools/Build/switchback_sim_harness/run.sh eval [i=1,2,3,4] [level=1,2,3,4] [nseeds=12 | seeds=...] [flags]
#   bash Tools/Build/switchback_sim_harness/run.sh trace i=3 level=4 seeds=45503 [flags]
#   bash Tools/Build/switchback_sim_harness/run.sh selftest
#   bash Tools/Build/switchback_sim_harness/run.sh tests [name filter]
#        (the SHIPPED SwitchbackBlastRunTests + SwitchbackBlastPlannerTests EditMode tests, headless: NUnitShim.cs, TestRunner.cs)
#   bash Tools/Build/switchback_sim_harness/run.sh races i=1,2,3,4 level=3 seeds=45501,7000 [drive.<field>=v ...]
#        (one line a race, for Tools/Compute/evaluators/switchback_fit.py; drive.<ChargeBoostAIPolicySO field> sets ONE
#        drive for every intensity, maxSeconds=<s> cuts a race)
#
# Flags: planner=0 (the plain gate AI: no blast planner), sense=0, emergency=0, drive=0 (no charge drive),
#        cone=1 (blast with a cone nearer the asset's than the planner's under-promising slot), roll=1 (the gape
#        flown as planned; the game does not fly roll), pickup=<u>, hull=<u>, dangerOnly=0|1 and noDodgeNear=<u> (the shipped SwitchbackController.aiDodgeDangerOnly 1 / aiNoDodgeNearRing 200; 0 0 = the old dodge-everything AI), setup=1 (the old strike's setup leg; shipped off), strikeAtKey / strikeCommit=<cos> / passedByTravel / blastHarmless
#        (step 2's what-ifs and control), driftMult=<x> (the drift's turn multiplier, 1.5), driftLag=0|1 and
#        noseLag=0|1 (the AI nose lag, both on as in the game), noseLead=<gain> noseLeadExtra=<deg> (AIPilot.NoseLeadGain, off at 0), noseFollow=e,m,h,l (the nose follow rate by level for every
#        intensity; 1.5,1.5,1.5,1.5 = the fleet lag for all) and noseFollowI<n>=e,m,h,l (one intensity; shipped table in Sim.cs), carve=<deg> / carveDone=<deg> / carveMax=<s> (step 4's
#        drift-carve what-if), gates=<n>, dt=<s>,
#        hit.slow / hit.secs / hit.dangerSlow / hit.dangerSecs / hit.meter / hit.destroys=0|1 (what a prism does).
# Levels: 1 Easy, 2 Medium, 3 Hard, 4 Legendary (SwitchbackAISkill.ForLevel, SwitchbackAvoidSkill.ForLevel).
#
# Needs a dotnet 8+ SDK (~/.dotnet). No .csproj (the repo gitignores them): everything builds into $TMPDIR.
{
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
OUT="${TMPDIR:-/tmp}/switchback_sim"
A="$ROOT/Assets/_Scripts/Controller/Arcade"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
if [ "${1:-}" = "tests" ]; then
  # The SHIPPED EditMode tests of the layout and the planner (SwitchbackBlastRunTests, SwitchbackBlastPlannerTests),
  # run headless through NUnitShim.cs + TestRunner.cs. `run.sh tests [name filter]`; exit 0 = every case passed.
  printf '"%s"\n' "$ROOT/Tools/Build/skimrace_sim_harness/UnityShim.cs" "$HERE/ShimExtra.cs" "$HERE/NUnitShim.cs" \
    "$HERE/TestRunner.cs" "$A/Switchback/SwitchbackCourse.cs" "$A/Switchback/SwitchbackBlastRun.cs" \
    "$A/Switchback/SwitchbackFeatures.cs" "$A/Switchback/SwitchbackBlastPlanner.cs" "$A/Switchback/BlastRunKeyCrystals.cs" \
    "$A/Switchback/SwitchbackRayMesh.cs" "$A/ScarabRally/ScarabRallyCourse.cs" \
    "$A/Racing/RaceCourseGeometry.cs" "$A/Breakwater/BreakwaterStationBuilder.cs" "$A/Breakwater/BreakwaterCourse.cs" \
    "$ROOT/Assets/_Scripts/Data/Enums/PrismKind.cs" \
    "$ROOT/Assets/_Scripts/Tests/Editor/SwitchbackBlastRunTests.cs" \
    "$ROOT/Assets/_Scripts/Tests/Editor/SwitchbackBlastPlannerTests.cs" > "$OUT/tests.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587,CS0219 \
    -target:exe -main:TestRunner -out:"$OUT/tests.dll" "@$OUT/tests.rsp" >&2
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "${V%%.*}" "$V" > "$OUT/tests.runtimeconfig.json"
  RUN="$OUT/tests_$$"; mkdir -p "$RUN"; cp "$OUT/tests.dll" "$OUT/tests.runtimeconfig.json" "$RUN/"
  trap 'rm -rf "$RUN"' EXIT
  shift
  "$DOTNET" "$RUN/tests.dll" "$@"
  exit
fi
printf '"%s"\n' "$ROOT/Tools/Build/skimrace_sim_harness/UnityShim.cs" "$HERE/ShimExtra.cs" "$HERE/Sim.cs" \
  "$A/Switchback/SwitchbackCourse.cs" "$A/Switchback/SwitchbackBlastRun.cs" "$A/Switchback/SwitchbackFeatures.cs" \
  "$A/Switchback/SwitchbackBlastPlanner.cs" "$A/Switchback/SwitchbackPrismAvoidance.cs" \
  "$A/Racing/RaceCourseGeometry.cs" "$A/Breakwater/BreakwaterStationBuilder.cs" "$A/Breakwater/BreakwaterCourse.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/PrismKind.cs" > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587,CS0219 \
  -target:exe -main:Program -out:"$OUT/sim.dll" "@$OUT/files.rsp" >&2
printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "${V%%.*}" "$V" > "$OUT/sim.runtimeconfig.json"
RUN="$OUT/run_$$"; mkdir -p "$RUN"; cp "$OUT/sim.dll" "$OUT/sim.runtimeconfig.json" "$RUN/"
trap 'rm -rf "$RUN"' EXIT
"$DOTNET" "$RUN/sim.dll" "$@"
exit
}
