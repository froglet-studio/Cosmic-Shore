#!/usr/bin/env bash
# Compile the SHIPPED Palisade arena layout (PalisadeArena.cs) and its SHIPPED edit-mode tests (PalisadeArenaTests)
# against race_course_source_harness's Unity math stub and spearline_harness's NUnit shim with Roslyn, and RUN every
# [Test] - no Unity.
#
#   bash Tools/Build/palisade_harness/run.sh               # every test; exit 0 = all passed
#   bash Tools/Build/palisade_harness/run.sh --self-test   # ...plus negative controls that must FAIL
#   bash Tools/Build/palisade_harness/run.sh --census      # what stands on each rung, counted from the shipped C#
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
A="$ROOT/Assets/_Scripts"
pick_root() {
  for r in "${DOTNET_ROOT:-}" "$HOME/.dotnet" "$(dirname "$(command -v dotnet 2>/dev/null || echo /nonexistent/x)")"; do
    [ -n "$r" ] && ls "$r"/sdk/*/Roslyn/bincore/csc.dll >/dev/null 2>&1 && { echo "$r"; return; }
  done
  echo "no .NET SDK with Roslyn (set DOTNET_ROOT)" >&2; exit 3
}
DOTNET_ROOT="$(pick_root)"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | tail -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
OUT="${TMPDIR:-/tmp}/palisade_harness"
mkdir -p "$OUT"

ARENA="$A/Controller/Arcade/Palisade/PalisadeArena.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = arena source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$ROOT/Tools/Build/spearline_harness/NUnitShim.cs" \
    "$HERE/Runner.cs" "$A/Data/Enums/PrismKind.cs" "$1" "$A/Tests/Editor/PalisadeArenaTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587 \
    -target:exe -main:Runner -out:"$(native "$OUT/$2.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$2.runtimeconfig.json"
}

run() { "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"; }

build "$ARENA" palisade
case "${1:-}" in
  --census) run palisade --census; exit 0 ;;
esac

set +e; run palisade > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of PalisadeArena.cs must make the tests fail.
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$ARENA" > "$OUT/PalisadeArena_neg.cs"
  grep -q "$3" "$OUT/PalisadeArena_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$OUT/PalisadeArena_neg.cs" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate "a lane inside the spawn ring" 's/LaneRadius = 640f/LaneRadius = 560f/' 'LaneRadius = 560f'
mutate "posts that stand in the run" 's/(i \* PostSpacing), across, lane.Axis,/(i * PostSpacing), lane.Side, lane.Axis,/' 'PostSpacing), lane.Side, lane.Axis,'
mutate "a run out of the blade" 's/RunOffset = 14f/RunOffset = 30f/' 'RunOffset = 30f'
mutate "a frame off the lane" 's/last + axis \* FrameGap, axis, radial)/last + axis * FrameGap + radial * 40f, axis, radial)/' 'FrameGap + radial \* 40f'
mutate "a mouth no wall fits" 's/FrameRadius = 30f/FrameRadius = 20f/' 'FrameRadius = 20f'
mutate "a shielded post at I1" 's/PostsPerLane = 14, ShieldEvery = 0,/PostsPerLane = 14, ShieldEvery = 4,/' 'PostsPerLane = 14, ShieldEvery = 4,'
mutate "I2 as much fuel as I1" 's/PostsPerLane = 10, ShieldEvery = 5,/PostsPerLane = 14, ShieldEvery = 0,/' 'Lanes = 3, PostsPerLane = 14, ShieldEvery = 0, FrameClosure = 0.75f'
mutate "I3 frames as closed as I2" 's/FrameClosure = 0.5f, /FrameClosure = 0.75f,/' 'ShieldEvery = 3, FrameClosure = 0.75f'
mutate "a broken frame open to the outside" 's/float firstAngle = -(standing - 1) \* step \* 0.5f;/float firstAngle = 180f - (standing - 1) * step * 0.5f;/' 'firstAngle = 180f'
mutate "danger tips at I2" 's/FrameClosure = 0.75f, DangerTips = false,/FrameClosure = 0.75f, DangerTips = true,/' 'FrameClosure = 0.75f, DangerTips = true,'
mutate "danger rubble at I3" 's/Rubble = 10, RubbleDangerEvery = 0/Rubble = 10, RubbleDangerEvery = 2/' 'Rubble = 10, RubbleDangerEvery = 2'
mutate "rubble in the lanes" 's/if (DistanceToRun(position, lanes\[k\]) < RubbleLaneClearance) return false;/if (false) return false;/;s/Vector3 position = direction \* radius;/Vector3 position = lanes[j % lanes.Count].FirstPost + direction * (70f + j);/' 'FirstPost + direction \* (70f + j);'
mutate "thin posts" 's/PostScale = new(8f, 8f, 34f)/PostScale = new(5f, 5f, 34f)/' 'PostScale = new(5f, 5f, 34f)'
mutate "parallel lanes" 's/Quaternion.AngleAxis(18f + 47f \* k, radial) \* across/Vector3.up/' 'Vector3 axis = (Vector3.up).normalized;'
echo "palisade_harness self-test: ok"
