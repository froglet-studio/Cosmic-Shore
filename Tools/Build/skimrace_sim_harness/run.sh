#!/usr/bin/env bash
# Skim Race AI offline simulator: compiles the SHIPPED pilot core out of Assets/ against a Unity
# shim and runs it through a VesselTransformer-faithful model of the Squirrel on the real track
# geometry (read from MinigameSkimRace.unity). See Sim.cs for the model and Docs/SKIM_RACE_AI.md
# for how its physical constants were calibrated against the editor.
#
#   bash Tools/Build/skimrace_sim_harness/run.sh eval  4 20 [Field=value ...] [ph.Field=value ...]
#   bash Tools/Build/skimrace_sim_harness/run.sh trace 4 3  [...]
#   bash Tools/Build/skimrace_sim_harness/run.sh tune  4 8 30 [...]
#   bash Tools/Build/skimrace_sim_harness/run.sh handicap 2 40 120 ph.HcReaction=0.5 [...]
#        the lobby difficulty's mistake chance that puts an AI seat's median at 120 s (section 10).
#   bash Tools/Build/skimrace_sim_harness/run.sh tuneall 1,2,3,4 4 16 [sigma=s] [final=n] [set=winner] [only=stated] [...]
#        ONE policy tuned on several tracks at once - the general SkimRaceAIConfig that any
#        intensity without its own file falls back to (Docs/SKIM_RACE_AI.md section 6.12).
#   bash Tools/Build/skimrace_sim_harness/run.sh fingerprint
#        each track's map fingerprint as the game computes it (Docs/SKIM_RACE_AI.md section 11).
#   bash Tools/Build/skimrace_sim_harness/run.sh eval 1 20 [...] ph.Seats=2 ph.Team=1 [ph.TeamRule=0]
#        a TEAM race: the seats share one domain and its crystals (section 13), flying the game's team
#        plan; ph.TeamRule=0 flies the rule from before team play (every seat on the nearest crystal).
#   ... ph.PhysicsStep=0.04 tests contacts on the game's 0.04 s fixed step instead of every frame (section 14);
#        with ph.Dt=<frame seconds> it shows how the AI races at another frame rate.
#
# Needs a dotnet 8+ SDK (a per-user install in ~/.dotnet is fine). No .csproj on purpose: the
# repo gitignores *.csproj, so everything builds into $TMPDIR.
#
# The whole body is one { ...; exit; } block on purpose. bash reads a script AS IT RUNS it, so editing
# this file during an hour-long tune shifted its read position and re-ran the last line - a second,
# unwanted tune (2026-10-05). A block is parsed whole before any of it runs, so an edit cannot reach
# a run already in flight - the same reason the assembly runs from a private copy below.
{
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
# SKIMRACE_SHELL_FILE swaps in another SkimRaceShell.cs (A/B of the contact geometry only).
OUT="${TMPDIR:-/tmp}/skimrace_sim${SKIMRACE_BUILD_TAG:+_$SKIMRACE_BUILD_TAG}"
SR="$ROOT/Assets/_Scripts/Controller/AI/SkimRace"
mkdir -p "$OUT"

# Track geometry straight out of the shipped scene: per intensity the waypoint track (and whether
# it is a spline), the laps and the crystal anchors - resolved exactly as the game resolves them, by
# the same reader that computes each intensity's map fingerprint (so a fifth intensity is raced too).
python3 "$ROOT/Tools/Build/skimrace_track_fingerprint.py" --emit-track "$OUT/track.txt"

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '"%s"\n' "$HERE/UnityShim.cs" "$HERE/Sim.cs" \
  "$SR/SkimRaceAIConfigSO.cs" "$SR/SkimRaceCourse.cs" "$SR/SkimRaceObservation.cs" "$SR/SkimRaceDriver.cs" "${SKIMRACE_SHELL_FILE:-$SR/SkimRaceShell.cs}" "$SR/SkimRacePlanner.cs" "$SR/SkimRaceObstacle.cs" "$SR/SkimRaceHandicap.cs" "$SR/SkimRaceTrackFingerprint.cs" "$SR/SkimRaceTargetTracker.cs" "$SR/SkimRaceTeamAssignment.cs" > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
  -target:exe -main:Program -out:"$OUT/sim.dll" "@$OUT/files.rsp" >&2
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
TFM="net${V%%.*}.0"
printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/sim.runtimeconfig.json"
# Run from a private copy so a rebuild cannot swap the assembly under a long tuning run.
RUN="$OUT/run_$$"
mkdir -p "$RUN"
cp "$OUT/sim.dll" "$OUT/sim.runtimeconfig.json" "$OUT/track.txt" "$RUN/"
trap 'rm -rf "$RUN"' EXIT
"$DOTNET" "$RUN/sim.dll" "$RUN/track.txt" "$@"
exit
}
