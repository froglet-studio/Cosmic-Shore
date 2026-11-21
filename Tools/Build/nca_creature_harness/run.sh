#!/usr/bin/env bash
# The NCA creature core (Docs/NCA_CREATURES.md) against the lab's own runtime, and its behaviour gates.
#
#   bash Tools/Build/nca_creature_harness/run.sh            # compile (Unity's API profile + net8), parity, gates, measure --assert
#   bash Tools/Build/nca_creature_harness/run.sh measure    # print grown_voxels / grow_steps for author_nca_creatures.py
#   bash Tools/Build/nca_creature_harness/run.sh bench      # ms per step of a grown creature
#
# Parity needs node and the research branch (origin/cece/gifted-curie-x2cpd0): the lab's built nca_creature.js is run on
# the research weights.json (lab_reference.js), with both its backends, and NcaVoxelCore must reproduce the state: exactly
# against the WebAssembly SIMD kernel it mirrors, to 1e-5 against the f64 JS path. Without the research ref the
# parity step is SKIPPED (said so, loudly) and the gates still run. Needs a dotnet 8 SDK (DOTNET_ROOT; see
# .claude/skills/asset-surgery §4). No .csproj on purpose: the repo gitignores *.csproj. Builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
[[ -x "$DOTNET_ROOT/dotnet" ]] || DOTNET_ROOT=/usr/lib/dotnet
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/nca_creature_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
CORE="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/NcaCreature/NcaVoxelCore.cs"
WEIGHTS_DIR="$ROOT/Assets/_SO_Assets/NCA Creatures"
RESEARCH_REF="origin/cece/gifted-curie-x2cpd0"

# Unity compiles the core against its netstandard2.1 profile - narrower than net8 - so fail the way Unity would first.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -out:"$OUT/unityprofile.dll" \
  "$CORE" || { echo "FAIL: NcaVoxelCore.cs does not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -target:exe -main:Program -out:"$OUT/ncacore.exe" "$CORE" "$HERE/Program.cs"
cat > "$OUT/ncacore.runtimeconfig.json" <<'JSON'
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }
JSON
run() { "$DOTNET" "$OUT/ncacore.exe" "$@"; }

MODE="${1:-all}"
fail=0
for W in "$WEIGHTS_DIR"/*NcaWeights.json; do
  SPECIES=$(basename "$W" NcaWeights.json)
  echo "=== $SPECIES"
  case "$MODE" in
    measure) run measure "$W" || fail=1; continue ;;
    bench) run bench "$W" || fail=1; continue ;;
  esac
  SRC=$(python3 -c "import json,sys; print(json.load(open(sys.argv[1]))['source'])" "$W")
  if command -v node >/dev/null && git -C "$ROOT" show "$SRC" > "$OUT/$SPECIES.lab_weights.json" 2>/dev/null \
     && git -C "$ROOT" show "$RESEARCH_REF:Tools/Ecology/flight/creatures/nca_creature.js" > "$OUT/nca_creature.js" 2>/dev/null; then
    node "$HERE/lab_reference.js" "$OUT/nca_creature.js" "$OUT/$SPECIES.lab_weights.json" "$OUT/$SPECIES.wasm.json" wasm
    node "$HERE/lab_reference.js" "$OUT/nca_creature.js" "$OUT/$SPECIES.lab_weights.json" "$OUT/$SPECIES.js.json" js
    run parity "$W" "$OUT/$SPECIES.wasm.json" "$OUT/$SPECIES.js.json" || fail=1
  else
    echo "  [SKIP] parity: node or $RESEARCH_REF not reachable here (git fetch origin cece/gifted-curie-x2cpd0) - gates only"
  fi
  run gates "$W" || fail=1
  run measure "$W" --assert || fail=1
done
[[ $fail == 0 ]] && echo "nca_creature_harness: OK" || { echo "nca_creature_harness: FAILED" >&2; exit 1; }
