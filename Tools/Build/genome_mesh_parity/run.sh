#!/usr/bin/env bash
# Build and run the genome-mesh parity harness (no Unity, no SDK: the csc.dll root dotnet_from_nuget.py lays out).
#   bash Tools/Build/genome_mesh_parity/run.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
CSC="$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | sort | tail -1)"
REF="$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | sort | tail -1)"
RT="$(ls -d "$DOTNET_ROOT"/shared/Microsoft.NETCore.App/* | sort | tail -1)"
OUT="${TMPDIR:-/tmp}/genome_mesh_parity"
mkdir -p "$OUT"
refs=(); for r in "$REF"/*.dll; do refs+=("-r:$r"); done
"$DOTNET_ROOT/dotnet" "$CSC" -nologo -optimize+ -nullable:disable -out:"$OUT/parity.dll" "${refs[@]}" \
  "$HERE/Parity.cs" "$REPO/Assets/_Scripts/Utility/SpindleWireRingGeometry.cs"
cat > "$OUT/parity.runtimeconfig.json" <<JSON
{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"$(basename "$RT")"}}}
JSON
exec "$DOTNET_ROOT/dotnet" "$OUT/parity.dll" "$REPO"
