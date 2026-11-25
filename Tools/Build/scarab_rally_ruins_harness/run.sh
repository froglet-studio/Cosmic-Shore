#!/usr/bin/env bash
# Compile the SHIPPED Scarab Rally ruins code (ScarabRallyRuins, Spearline's SpearlineRuins it calls, Switchback's course
# and Blast Run layout it is cut round) and its SHIPPED edit-mode tests (ScarabRallyRuinsTests) against the Skim Race
# harness's Unity shim and the Switchback harness's NUnit shim and test runner with Roslyn, and RUN every [Test] - no Unity.
#
#   bash Tools/Build/scarab_rally_ruins_harness/run.sh              # every test; exit 0 = all passed
#   bash Tools/Build/scarab_rally_ruins_harness/run.sh --self-test  # ...plus negative controls that must FAIL
#
# Run from the repo root (one test reads SwitchbackController.cs by its repo path).
# Needs a .NET SDK (8+): DOTNET_ROOT, else ~/.dotnet, else the folder of `dotnet` on PATH.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
cd "$ROOT"
A="$ROOT/Assets/_Scripts/Controller/Arcade"
SW="$ROOT/Tools/Build/switchback_sim_harness"
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
OUT="${TMPDIR:-/tmp}/scarab_rally_ruins_harness"
mkdir -p "$OUT"

RUINS="$A/ScarabRally/ScarabRallyRuins.cs"

native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

build() {   # $1 = Scarab Rally ruins source, $2 = output name
  : > "$OUT/refs.rsp"
  for d in "$REFDIR"/*.dll; do printf -- '-r:"%s"\n' "$(native "$d")" >> "$OUT/refs.rsp"; done
  : > "$OUT/files.rsp"
  for f in "$ROOT/Tools/Build/skimrace_sim_harness/UnityShim.cs" "$SW/ShimExtra.cs" "$SW/NUnitShim.cs" "$SW/TestRunner.cs" \
    "$ROOT/Assets/_Scripts/Data/Enums/PrismKind.cs" "$A/Racing/RaceCourseGeometry.cs" \
    "$A/Switchback/SwitchbackCourse.cs" "$A/Switchback/SwitchbackBlastRun.cs" "$A/Switchback/SwitchbackFeatures.cs" \
    "$A/Breakwater/BreakwaterStationBuilder.cs" "$A/Breakwater/BreakwaterCourse.cs" \
    "$A/Spearline/SpearlineSeals.cs" "$A/Spearline/SpearlineRuins.cs" \
    "$A/ScarabRally/ScarabRallyCourse.cs" "$1" "$ROOT/Assets/_Scripts/Tests/Editor/ScarabRallyRuinsTests.cs"; do
    printf '"%s"\n' "$(native "$f")" >> "$OUT/files.rsp"
  done
  "$DOTNET" "$(native "$CSC")" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$(native "$OUT/refs.rsp")" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587,CS0219 \
    -target:exe -main:TestRunner -out:"$(native "$OUT/$2.dll")" "@$(native "$OUT/files.rsp")" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
  printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' \
    "${V%%.*}" "$V" > "$OUT/$2.runtimeconfig.json"
}

run() { "$DOTNET" "$(native "$OUT/$1.dll")" "${@:2}"; }

build "$RUINS" ruins
set +e; run ruins > "$OUT/result.txt"; rc=$?; set -e
cat "$OUT/result.txt"
[ $rc -eq 0 ] || exit $rc
[ "${1:-}" = "--self-test" ] || exit 0

# Negative controls: each mutated copy of ScarabRallyRuins.cs must make the tests fail.
mutate() {   # $1 = label, $2 = sed expression, $3 = probe the mutated copy must contain
  sed "$2" "$RUINS" > "$OUT/ScarabRallyRuins_neg.cs"
  grep -q "$3" "$OUT/ScarabRallyRuins_neg.cs" || { echo "the mutation did not apply: $1"; exit 1; }
  build "$OUT/ScarabRallyRuins_neg.cs" neg
  set +e; run neg > "$OUT/neg.txt"; local nrc=$?; set -e
  if [ $nrc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $1"; exit 1; fi
  echo "negative control fired: $1 ($(grep -c '^FAIL' "$OUT/neg.txt") failing test(s))"
}
mutate "the field is not cut out" 's/laid\[i\] = laid\[i\] && !ReachesField(parts\[i\], field, near);/laid[i] = laid[i] \&\& true;/' 'laid\[i\] && true;'
mutate "the sight line is not kept" 's/ && !ReachesSightLine(course, intensity, parts\[i\])//' 'laid.Add(WheelOf(wheels, i) < 0);'
mutate "no budget: every leg keeps its ruin" 's/for (int k = 0; k < budget; k++)/for (int k = 0; k < 3 * budget; k++)/' 'k < 3 \* budget'
mutate "scraps are laid" 's/if (left >= MinRuinPrisms) return true;/if (left > 0) return true;/' 'if (left > 0) return true;'
mutate "the budget is spent on the first legs" 's|k \* (eligible.Count - 1) / (budget - 1)|k|' 'int place = budget == 1 ? 0 : k;'
mutate "the wrong nucleus margin" 's/FieldNucleusMargin = 30f/FieldNucleusMargin = 45f/' 'FieldNucleusMargin = 45f'
echo "self-test OK"
