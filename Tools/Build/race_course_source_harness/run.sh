#!/usr/bin/env bash
# Compile the SHIPPED RaceCourseSource + every per-mode source + every course generator they call
# against a UnityEngine stub, and RUN Driver: it asserts the extraction out of GateRaceController
# was a pure move (bit-identical courses, same laps / lead-in / targets / shell) and exercises the
# arena-built sources' failure path. Needs a dotnet 8 SDK (per-user install is fine:
# `bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 --install-dir
# $DOTNET_ROOT --no-path`). Exit 0 = pass.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
A="$ROOT/Assets/_Scripts"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/race_course_source_harness"
mkdir -p "$OUT"

# Generated.cs: two pieces EXTRACTED from shipped files rather than retyped, so the harness
# cannot drift from them - the overrides' Default* constants, and GateRaceController's two
# static fold methods (the controller itself is a NetworkBehaviour and cannot compile here).
python3 - "$A" "$OUT/Generated.cs" <<'PY'
import re, sys
a, out = sys.argv[1], sys.argv[2]
so = open(f"{a}/ScriptableObjects/EndConditionOverridesSO.cs").read()
consts = re.findall(r"^\s*public const int Default\w+ = \d+;", so, re.M)
assert consts, "no Default* constants found in EndConditionOverridesSO.cs"
grc = open(f"{a}/Controller/Arcade/Racing/GateRaceController.cs").read()
start = grc.index("        public static int RaceLengthFor(")
end = grc.index("        /// <summary>", grc.index("        public static int RingIndexFor("))
body = grc[start:end]
open(out, "w").write(
    "using UnityEngine;\n"
    "namespace CosmicShore.ScriptableObjects { public partial class EndConditionOverridesSO {\n"
    + "\n".join(consts) + "\n} }\n"
    "namespace CosmicShore.Gameplay { public abstract class GateRaceController {\n"
    + body + "} }\n")
PY

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/UnityMath.cs" "$HERE/Platform.cs" "$HERE/Driver.cs" "$OUT/Generated.cs" \
  "$A/Data/Enums/Domains.cs" \
  "$A/Data/Enums/GameModes.cs" \
  "$A/Data/Enums/PrismKind.cs" \
  "$A/Controller/Arcade/Racing/RaceCourseGeometry.cs" \
  "$A/Controller/Arcade/Racing/RaceCourseSource.cs" \
  "$A/Controller/Arcade/Switchback/SwitchbackCourse.cs" \
  "$A/Controller/Arcade/Switchback/SwitchbackCourseSource.cs" \
  "$A/Controller/Arcade/Headlong/HeadlongCircuit.cs" \
  "$A/Controller/Arcade/Headlong/HeadlongCourseSource.cs" \
  "$A/Controller/Arcade/Redline/RedlineCourse.cs" \
  "$A/Controller/Arcade/Redline/RedlineCourseSource.cs" \
  "$A/Controller/Arcade/Breakwater/BreakwaterCourse.cs" \
  "$A/Controller/Arcade/Breakwater/BreakwaterStationBuilder.cs" \
  "$A/Controller/Arcade/Breakwater/BreakwaterCourseSource.cs" \
  "$A/Controller/Arcade/Waystation/WaystationCourse.cs" \
  "$A/Controller/Arcade/Waystation/WaystationCourseSource.cs" \
  "$A/Controller/Arcade/Skein/SkeinCourse.cs" \
  "$A/Controller/Arcade/Skein/SkeinCourseSource.cs" \
  "$A/Controller/Arcade/Regatta/RegattaCourse.cs" \
  "$A/Controller/Arcade/Regatta/RegattaCourseSource.cs" \
  | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:Driver -out:"$OUT/course.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/course.runtimeconfig.json"
"$DOTNET" "$OUT/course.exe"
