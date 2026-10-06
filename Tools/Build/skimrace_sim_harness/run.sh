#!/usr/bin/env bash
# Skim Race AI offline simulator: compiles the SHIPPED pilot core out of Assets/ against a Unity
# shim and runs it through a VesselTransformer-faithful model of the Squirrel on the real track
# geometry (read from MinigameSkimRace.unity). See Sim.cs for the model and Docs/SKIM_RACE_AI.md
# for how its physical constants were calibrated against the editor.
#
#   bash Tools/Build/skimrace_sim_harness/run.sh eval  4 20 [Field=value ...] [ph.Field=value ...]
#   bash Tools/Build/skimrace_sim_harness/run.sh trace 4 3  [...]
#   bash Tools/Build/skimrace_sim_harness/run.sh tune  4 8 30 [...]
#
# Needs a dotnet 8+ SDK (a per-user install in ~/.dotnet is fine). No .csproj on purpose: the
# repo gitignores *.csproj, so everything builds into $TMPDIR.
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
# it is a spline), the per-intensity laps, and the crystal anchors.
python3 - "$ROOT" "$OUT/track.txt" <<'PY'
import re, sys
root, out = sys.argv[1], sys.argv[2]
t = open(f"{root}/Assets/_Scenes/Multiplayer Scenes/MinigameSkimRace.unity").read()
docs = re.split(r"\n--- ", t)
def block_with(key):
    for d in docs:
        if key in d: return d
    raise SystemExit("missing " + key)
track = block_with("prismSpacing:")
cm = block_with("listOfCrystalPositions:")
mon = block_with("lapsPerIntensity:")
def sets(blk, start, stop):
    body = blk.split(start)[1].split(stop)[0]
    return [re.findall(r"x: ([-\d.e]+), y: ([-\d.e]+), z: ([-\d.e]+)", s) for s in body.split("- positions:")[1:]]
wps = sets(track, "waypoints:", "useSplinePerIntensity")
anchors = sets(cm, "listOfCrystalPositions:", "anchorJitterRadius")
spl = re.search(r"useSplinePerIntensity: ([0-9a-f]+)", track).group(1)
spline = [int(spl[i*8:i*8+2], 16) for i in range(len(spl)//8)]
laps_hex = re.search(r"lapsPerIntensity: ([0-9a-f]+)", mon).group(1)
laps = [int.from_bytes(bytes.fromhex(laps_hex[i*8:i*8+8]), "little") for i in range(len(laps_hex)//8)]
with open(out, "w") as fh:
    for i in range(4):
        fmt = lambda pts: ";".join(",".join(p) for p in pts)
        fh.write(f"{i+1}|{spline[i]}|{laps[i]}|{fmt(wps[i])}|{fmt(anchors[i])}\n")
PY

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '"%s"\n' "$HERE/UnityShim.cs" "$HERE/Sim.cs" \
  "$SR/SkimRaceAIConfigSO.cs" "$SR/SkimRaceCourse.cs" "$SR/SkimRaceObservation.cs" "$SR/SkimRaceDriver.cs" "${SKIMRACE_SHELL_FILE:-$SR/SkimRaceShell.cs}" "$SR/SkimRacePlanner.cs" "$SR/SkimRaceObstacle.cs" \
  "$ROOT/Assets/_Scripts/Utility/MathfNoAlloc.cs" > "$OUT/files.rsp"
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
