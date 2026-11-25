#!/usr/bin/env bash
# Compile the SHIPPED Riposte AI plan (RiposteDuel), arena rules (RiposteYard) and their SHIPPED edit-mode tests against
# race_course_source_harness's Unity math stub and spearline_harness.s NUnit shim with Roslyn, and RUN every
# [Test] - no Unity.
#
#   bash Tools/Build/riposte_harness/run.sh                 # every test; exit 0 = all passed
#   bash Tools/Build/riposte_harness/run.sh --self-test     # ...plus negative controls that must FAIL
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
A="$ROOT/Assets/_Scripts"
SHIM="$ROOT/Tools/Build/spearline_harness"
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
OUT="${TMPDIR:-/tmp}/riposte_harness"
mkdir -p "$OUT"

DUEL="$A/Controller/Arcade/Riposte/RiposteDuel.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = duel source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/race_course_source_harness/UnityMath.cs" "$SHIM/NUnitShim.cs" "$HERE/Runner.cs" \
    "$1" "$A/Tests/Editor/RiposteDuelTests.cs" "$HERE/YardStub.cs" "${YARD:-$A/Controller/Arcade/Riposte/RiposteYard.cs}"     "$A/Tests/Editor/RiposteYardTests.cs"; do
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

build "$DUEL" riposte
set +e; run riposte > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of RiposteDuel.cs must make the tests fail.
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$DUEL" > "$OUT/RiposteDuel_neg.cs"
  grep -q "$3" "$OUT/RiposteDuel_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$OUT/RiposteDuel_neg.cs" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate_yard() {   # the same, on RiposteYard.cs
  sed "$2" "$A/Controller/Arcade/Riposte/RiposteYard.cs" > "$OUT/RiposteYard_neg.cs"
  grep -q "$3" "$OUT/RiposteYard_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  YARD="$OUT/RiposteYard_neg.cs" build "$DUEL" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate_yard "a rail laid inside its lane" 's/\* (laneRadius + RailStandoff);/* (laneRadius - RailStandoff);/' 'laneRadius - RailStandoff'
mutate_yard "pickets laid end to end (no way off the lane)" 's/index \* (PicketLength + PicketGap);/index * PicketLength;/' 'index \* PicketLength;'
mutate_yard "a gate laid inside the refuel clearing" 's/(gate + 1) \/ (gates + 1);/(gate + 1) \/ gates;/' '(gate + 1) \/ gates;'
mutate_yard "every lane gated at I1" 's/everyNth > 0 \&\& lane % everyNth == 0;/everyNth >= 0;/' 'everyNth >= 0;'
mutate_yard "thorns through the whole scatter" 's/thornBand > 0f \&\& distanceToLaneAxis < laneRadius + thornBand;/thornBand > 0f;/' 'thornBand > 0f;'
mutate_yard "ironwood outside the core" 's/ironwoodCoreRadius > 0f \&\& stickCentre/ironwoodCoreRadius > 0f || stickCentre/' '0f || stickCentre'
mutate "a charge that never runs out (the slow knot)" 's/if (passed || timedOut) return Phase.Extend;/if (false) return Phase.Extend;/' 'if (false) return Phase.Extend;'
mutate "an easy pilot sent cutting" 's/bool wantsFuel = reapBelow > 0f \&\& fuel01 < reapBelow;/bool wantsFuel = fuel01 <= reapBelow;/' 'fuel01 <= reapBelow;'
mutate "a knot the slower hull stays in" 's/return sqr <= knot \* knot \&\& speed <= rivalSpeed;/return false;/' 'return false;'
mutate "a run-out that leaves the arena" 's/return ClampToBall(self + dir \* distance, centre, arenaRadius);/return self + dir * distance;/' 'return self + dir \* distance;'
mutate "a cutting line that misses the heap" 's/return centre + turned \* heapRadius;/return self + turned * heapRadius;/' 'return self + turned'
