#!/usr/bin/env bash
# Compile the SHIPPED Thornline course (RaceCourseGeometry, ThornlineCourse) and its SHIPPED edit-mode tests
# (ThornlineCourseTests) against race_course_source_harness's Unity math stub and an NUnit shim with Roslyn, and RUN
# every [Test] - the 400-seed ladder in seconds, no Unity. Run from anywhere; the tests read ShrikeConfig.asset from
# the repo root.
#
#   bash Tools/Build/thornline_course_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/thornline_course_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#   bash Tools/Build/thornline_course_harness/run.sh --sweep         # the ladder and the race clock per intensity
#   bash Tools/Build/thornline_course_harness/run.sh --ai [--pack] [--seeds N] [--check]
#                                                                    # AI Shrikes race whole courses (RaceReplica.cs)
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
OUT="${TMPDIR:-/tmp}/thornline_course_harness"
mkdir -p "$OUT"

COURSE="$A/Controller/Arcade/Thornline/ThornlineCourse.cs"
cd "$ROOT"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = course source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$HERE/ShimExtra.cs" "$HERE/NUnitShim.cs" \
    "$HERE/Runner.cs" "$HERE/RaceReplica.cs" \
    "$A/Data/Enums/PrismKind.cs" "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
    "$A/Controller/AI/NoseLeadSteering.cs" "${PLAN:-$A/Controller/Arcade/Thornline/ThornlineAIPlan.cs}" \
    "$1" "$A/Tests/Editor/ThornlineCourseTests.cs" "$A/Tests/Editor/ThornlineAIPlanTests.cs"; do
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

build "$COURSE" thornline
case "${1:-}" in
  --sweep)  run thornline --sweep; exit 0 ;;
  --ai)     shift; set +e; run thornline --ai "$@"; exit $? ;;
  --export) run thornline "$@"; exit 0 ;;
esac

set +e; run thornline > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy must make the tests fail.
negative() {   # $1 = label, $2 = course source
  build "$2" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$COURSE" > "$OUT/ThornlineCourse_neg.cs"
  grep -q "$3" "$OUT/ThornlineCourse_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  negative "$1" "$OUT/ThornlineCourse_neg.cs"
}
mutate "accidental skips allowed (gentle bends, no margin)" 's/public const float AccidentMargin = 15f;/public const float AccidentMargin = -40f;/; s/MinBend = new\[\] { 35f, 30f, 30f, 35f }/MinBend = new[] { 3f, 3f, 3f, 3f }/' 'MinBend = new\[\] { 3f, 3f, 3f, 3f }'
mutate "legs pass through strangers" 's/public const float ThornClearance = 20f;/public const float ThornClearance = -30f;/' 'ThornClearance = -30f'
mutate "the sling range drifts from the asset" 's/public const float SlingRange = 1600f;/public const float SlingRange = 1700f;/' 'SlingRange = 1700f'
mutate "crystals on the centre line" 's/CrystalRimOffset = 8f;/CrystalRimOffset = 0f;/' 'CrystalRimOffset = 0f'
mutate "rings off the middle of the leg" 's/(thorns\[k - 1\] + thorns\[k\]) \* 0.5f,/Vector3.Lerp(thorns[k - 1], thorns[k], 0.4f),/' 'Lerp(thorns\[k - 1\], thorns\[k\], 0.4f)'
# 2026-11-17 upgrade (Docs/Thornline/UPGRADE_SPEC.md): each claim's own control.
mutate "the old rim mark, outside the 12 u the sling catches" 's/CrystalRimOffset = 8f;/CrystalRimOffset = 17f;/' 'CrystalRimOffset = 17f'
mutate "the anchor reach drifts from dockQueryReach" 's/public const float AnchorReach = 12f;/public const float AnchorReach = 14f;/' 'AnchorReach = 14f'
mutate "lanes on the lance line" 's/public const float LaneOffset = 6.5f;/public const float LaneOffset = 2f;/' 'LaneOffset = 2f'
mutate "lanes off the anchor disc" 's/public const float LaneOffset = 6.5f;/public const float LaneOffset = 13f;/' 'LaneOffset = 13f'
mutate "no seat-cone guard in the walk" '/DistanceToSeatCone(next) < \|DistanceToSeatCone(ring) < /d; s/ \&\& SeatsClear(s, t, out _);/;/' 'Valid(s, t, out _);$'
mutate "the old regrow sphere (size + 30 u)" 's/float h = size \* 0.5f, r = HullRadius + 1f;/float h = size * 0.5f + 30f, r = HullRadius + 1f;/' 'h = size \* 0.5f + 30f'

# The AI race replica (run.sh --ai): the shipped rows hold every claim, and each control breaks one.
build "$COURSE" thornline
ai_claims() {   # $1 = label, $2 = must pass (0) or fail (1), the rest = --ai arguments
  local label="$1" want="$2"; shift 2
  set +e; run thornline --ai --seeds 30 --check "$@" > "$OUT/ai.txt"; local arc=$?; set -e
  if [ "$want" = 0 ] && [ $arc -ne 0 ]; then cat "$OUT/ai.txt"; echo "AI CLAIMS FAIL: $label"; exit 1; fi
  if [ "$want" = 1 ] && [ $arc -eq 0 ]; then echo "AI NEGATIVE CONTROL DID NOT FIRE: $label"; exit 1; fi
  if [ "$want" = 0 ]; then echo "ai claims hold: $label"; else echo "ai negative control fired: $label ($(grep -c '^CLAIM FAILS' "$OUT/ai.txt") claim(s))"; fi
}
ai_claims "the shipped rows, solo" 0
ai_claims "the shipped rows, a pack of four" 0 --pack
ai_claims "no nose lead (the shipped AIPilot law)" 1 --lead 0,0,0,0
ai_claims "the fleet's gains (1 at every difficulty)" 1 --lead 1,1,1,1
ai_claims "centre aim in a pack (no lanes)" 1 --pack --centre
# The replica's kit numbers are the shipped ones (ThornlineCourseTests pins the rest).
grep -q "  PitchScaler: 60$" "$ROOT/Assets/_Prefabs/Spacevessels/Shrike.prefab" && grep -q "  YawScaler: 40$" "$ROOT/Assets/_Prefabs/Spacevessels/Shrike.prefab"   && grep -q "  RollScaler: 15$" "$ROOT/Assets/_Prefabs/Spacevessels/Shrike.prefab" && grep -q "  restrictedTurnMultiplier: 3$" "$ROOT/Assets/_Prefabs/Spacevessels/Shrike.prefab"   && grep -q "  undockPush: 8$" "$ROOT/Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset"   && grep -q "float _maxDistance = 50f;" "$ROOT/Assets/_Scripts/Controller/AI/AIPilot.cs" && grep -q "aggressiveness = 100f;" "$ROOT/Assets/_Scripts/Controller/AI/AIPilot.cs"   && grep -q "gain \* VesselTransformer.RotationFollowRate / noseFollowRate" "$ROOT/Assets/_Scripts/Controller/AI/AIPilot.cs"   || { echo "the replica's kit numbers no longer match Shrike.prefab / ShrikeConfig.asset / AIPilot.cs"; exit 1; }
echo "replica kit numbers match the prefab, the config and AIPilot"
echo "thornline_course_harness self-test: ok"
