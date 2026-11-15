#!/usr/bin/env bash
# Sight-line audit across the race modes (Audit.cs). Compiles the shipped course generators against the card-art
# harness's Unity shim and prints, per mode and intensity, how much of the next ring is hidden from the ring you leave.
#   bash Tools/Build/sight_line_audit/run.sh [mode ...]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
CA="$ROOT/Tools/Build/card_art_harness"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/sight_line_audit"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
{
  echo "namespace CosmicShore.Utility {"
  sed -n '/public enum CSLogChannel/,/^    }/p' "$ROOT/Assets/_Scripts/Utility/Foundation/CSDebug.cs"
  echo "}"
} > "$OUT/Generated.cs"
python3 "$ROOT/Tools/Build/render_card_backgrounds.py" --list-sources | sed "s|^|$ROOT/|" > "$OUT/sources.txt"
A="$ROOT/Assets/_Scripts/Controller/Arcade"
for f in $(cat "$HERE/extra_sources.txt" 2>/dev/null); do echo "$ROOT/$f"; done >> "$OUT/sources.txt"
sort -u "$OUT/sources.txt" -o "$OUT/sources.txt"
{
  printf '%s\n' "$CA/UnityShim.cs" "$CA/PlatformShim.cs" "$ROOT/Tools/Build/switchback_sim_harness/ShimExtra.cs" "$ROOT/Tools/Build/switchback_sim_harness/NUnitShim.cs" "$HERE/Audit.cs" "$OUT/Generated.cs"
  cat "$OUT/sources.txt"
} | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661,CS0108,CS0162,CS0219 \
  -target:exe -main:Audit -out:"$OUT/audit.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/audit.runtimeconfig.json"
"$DOTNET" "$OUT/audit.exe" "$@"
