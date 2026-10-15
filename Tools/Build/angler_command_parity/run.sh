#!/usr/bin/env bash
# The Angler's parity gate: compile the SHIPPED AnglerCommandMath.cs and its SHIPPED edit-mode test
# (Assets/_Scripts/Tests/Editor/AnglerCommandMathTests.cs, whose tables goldens.cjs generates from the Angler Command Lab
# page's own CORE functions) against the shared UnityEngine math stub and NUnit shim (Tools/Build/brittlestar_harness)
# with Roslyn, and RUN every [Test]. Exit 0 = the C# answers every case the page answers.
#
#   bash Tools/Build/angler_command_parity/run.sh               # the shipped pair
#   bash Tools/Build/angler_command_parity/run.sh --self-test   # + planted formula changes that must each FAIL
#   node Tools/Build/angler_command_parity/goldens.cjs --check  # the other half: the test's tables are still the page's
#
# Needs a .NET SDK (8+): DOTNET_ROOT, else /usr/lib/dotnet, else ~/.dotnet.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
if [ -z "${DOTNET_ROOT:-}" ]; then
  if [ -x /usr/lib/dotnet/dotnet ]; then DOTNET_ROOT=/usr/lib/dotnet; else DOTNET_ROOT="$HOME/.dotnet"; fi
fi
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | head -1)
SHARED="$HERE/../brittlestar_harness"
MATH="$ROOT/Assets/_Scripts/Controller/Vessel/AnglerCommandMath.cs"
TEST="$ROOT/Assets/_Scripts/Tests/Editor/AnglerCommandMathTests.cs"
OUT="${TMPDIR:-/tmp}/angler_command_parity"
mkdir -p "$OUT"

build_and_run() {   # $1 = math source, $2 = expected (pass|fail), $3 = label
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$SHARED/Stubs.cs" "$SHARED/NUnitShim.cs" "$HERE/Runner.cs" "$1" "$TEST" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632 \
    -target:exe -main:Runner -out:"$OUT/acp.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  TFM=$(basename "$REFDIR")
  printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/acp.runtimeconfig.json"
  set +e; "$DOTNET" "$OUT/acp.exe" > "$OUT/result.txt"; local rc=$?; set -e
  if [ "$2" = pass ]; then cat "$OUT/result.txt"; return $rc; fi
  if [ $rc -eq 0 ]; then echo "NEGATIVE CONTROL DID NOT FIRE: $3"; return 1; fi
  echo "negative control fired: $3 ($(grep -c '^FAIL' "$OUT/result.txt") failing test(s))"
}

plant() {   # $1 = label, $2 = sed expression applied to a copy of the shipped math
  local copy="$OUT/planted.cs"
  sed "$2" "$MATH" > "$copy"
  if cmp -s "$copy" "$MATH"; then echo "PLANT DID NOT APPLY: $1"; return 1; fi
  build_and_run "$copy" fail "$1"
}

build_and_run "$MATH" pass "shipped"
if [ "${1:-}" = "--self-test" ]; then
  plant "claim penalty 900 -> 800" 's/ClaimPenalty = 900f/ClaimPenalty = 800f/'
  plant "follow gain 1.5 -> 1.4" 's/(gap - 4f) \* 1\.5f/(gap - 4f) * 1.4f/'
  plant "the brush's threshold 0.5 -> 0.45" 's/(depth - 0\.5f) \/ 0\.5f/(depth - 0.45f) \/ 0.5f/'
  plant "the school ring 3.2 -> 3.0" 's/Mathf.Sqrt(i) \* 3\.2f/Mathf.Sqrt(i) * 3.0f/'
  plant "the cone's 1 u floor dropped" 's/if (d > reach || d < 1f) return false;/if (d > reach) return false;/'
  plant "the esca 2.5 u up -> 2 u" 's/u\.\([xyz]\) \* 2\.5f/u.\1 * 2f/g'
  plant "the omni reach made exclusive" 's/points\[i\]) <= r2)/points[i]) < r2)/'
  plant "the omni room ignored" 's/if (into.Count > room) into.RemoveRange/if (false) into.RemoveRange/'
  echo "angler_command_parity --self-test: 8/8 planted changes caught"
fi
