#!/usr/bin/env bash
# Compile the SHIPPED Squirrel skim-racing brain (Assets/_Scripts/Controller/AI/SkimRacing/*.cs),
# the shipped ShieldShellMath.cs and MinimumThrottleBrake.cs against a UnityEngine /
# Unity.Mathematics shim, and RACE it in a transcription of the Squirrel's flight model on the
# Skim Race ribbons. Every constant is exported from the shipped assets first
# (Tools/Build/squirrel_skim_model.py --export-harness-config), so a retune of the vessel, the
# skim effect or the track moves the result instead of leaving it stale.
#
#   Tools/Build/squirrel_ai_harness/run.sh                     standard report + gates (exit 1 on a failed gate)
#   Tools/Build/squirrel_ai_harness/run.sh --intensity 2 --racers 3 --seeds 20 --profile tier [--fps 30] [--trace]
#
# Needs a dotnet 8 SDK. A per-user install needs no root (~40 s):
#   curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet"
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
if [ ! -x "$DOTNET" ]; then
  echo "No dotnet at $DOTNET - see the header of this script for a no-root install." >&2
  exit 2
fi
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/squirrel_ai_harness"
mkdir -p "$OUT"

python3 "$ROOT/Tools/Build/squirrel_skim_model.py" --export-harness-config "$OUT/config.json"

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
{
  printf '%s\n' "$HERE/UnityShim.cs" "$HERE/World.cs" "$HERE/Race.cs" "$HERE/Program.cs"
  printf '%s\n' "$ROOT/Assets/_Scripts/Utility/ShieldShellMath.cs"
  printf '%s\n' "$ROOT/Assets/_Scripts/Controller/Vessel/MinimumThrottleBrake.cs"
  ls "$ROOT"/Assets/_Scripts/Controller/AI/SkimRacing/*.cs
} | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"

"$DOTNET" "$CSC" -nologo -langversion:10.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0649,CS0414,CS0169,CS8632 \
  -target:exe -main:SquirrelAiHarness.Program -out:"$OUT/harness.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
  > "$OUT/harness.runtimeconfig.json"
"$DOTNET" "$OUT/harness.exe" --config "$OUT/config.json" "$@"
