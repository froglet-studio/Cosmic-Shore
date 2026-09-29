#!/usr/bin/env bash
# Compile the SHIPPED ScarabHullForm against a UnityEngine math stub with Roslyn and RUN it at the
# settings passed as key=value args (render_scarab_card_icons.py reads them off Scarab.prefab).
# Prints the hull's parts as JSON on stdout. Needs a dotnet 8 SDK (per-user install is fine:
# `bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 --install-dir
# $DOTNET_ROOT --no-path`), found through DOTNET_ROOT (default ~/.dotnet).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/scarab_icon_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
printf '%s\n' "$HERE/Stubs.cs" "$HERE/Driver.cs" \
  "$ROOT/Assets/_Scripts/Data/Enums/Element.cs" \
  "$ROOT/Assets/_Scripts/Controller/Vessel/ScarabHullForm.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
  -target:exe -main:Driver -out:"$OUT/hull.exe" "@$OUT/files.rsp" 1>&2
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/hull.runtimeconfig.json"
"$DOTNET" "$OUT/hull.exe" "$@"
