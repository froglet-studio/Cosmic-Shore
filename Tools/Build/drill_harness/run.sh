#!/usr/bin/env bash
# Compile the SHIPPED drill data layer (tokens, conditions, composer, hull facts, progress store,
# the three SOs) plus the real ability map, glyph set and binding map against a UnityEngine stub,
# and RUN Driver. Needs a dotnet 8 SDK (see ../race_course_source_harness/run.sh). Exit 0 = pass.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
A="$ROOT/Assets/_Scripts"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/drill_harness"
mkdir -p "$OUT"

# The HintBinding enum, extracted from the shipped MonoBehaviour rather than retyped.
python3 - "$A/UI/Elements/InputDeviceIconSetSwitcher.cs" "$OUT/HintBinding.cs" <<'PY'
import re, sys
src = open(sys.argv[1]).read()
m = re.search(r"public enum HintBinding\s*\{.*?\}", src, re.S)
assert m, "HintBinding enum not found"
open(sys.argv[2], "w").write("namespace CosmicShore.UI { public partial class InputDeviceIconSetSwitcher {\n" + m.group(0) + "\n} }\n")
PY

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/Unity.cs" "$HERE/Gameplay.cs" "$HERE/Driver.cs" "$HERE/RunnerDriver.cs" "$OUT/HintBinding.cs" \
  "$A/Data/Enums/Element.cs" "$A/Data/Enums/InputEvents.cs" "$A/Data/Enums/VesselClassType.cs" \
  "$A/Data/Enums/GameModes.cs" "$A/Data/Enums/ScoringMetric.cs" \
  "$A/ScriptableObjects/ElementalAbilityMapSO.cs" "$A/ScriptableObjects/ControlGlyphSetSO.cs" \
  "$A/ScriptableObjects/GameOfTheWeekSO.cs" \
  "$A/UI/Elements/InputHintBindingMap.cs" \
  "$A/System/CloudData/Models/DrillProgressCloudData.cs" \
  "$A"/Controller/Arcade/Preview/Drill/*.cs > "$OUT/sources.rsp"

"$DOTNET" "$CSC" -nologo -nullable:disable -langversion:latest -nowarn:CS0649,CS0169,CS0414 \
  -out:"$OUT/drill.dll" @"$OUT/refs.rsp" @"$OUT/sources.rsp"
cat > "$OUT/drill.runtimeconfig.json" <<JSON
{ "runtimeOptions": { "tfm": "net8.0", "framework": { "name": "Microsoft.NETCore.App", "version": "8.0.0" } } }
JSON
"$DOTNET" "$OUT/drill.dll"
