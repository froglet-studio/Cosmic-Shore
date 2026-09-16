#!/usr/bin/env bash
# Compile the SHIPPED MandelbulbLattice.cs against a UnityEngine stub and RUN it, printing the
# reachable shell (sites + normals) as JSON. Needs a dotnet 8 SDK (per-user install is fine:
# `bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 --install-dir $DOTNET_ROOT
# --no-path`). Used by Tools/Build/verify_mandelbulb_flora_tables.py, which proves the offline
# model against THIS output rather than against a transcription of the C#.
#
#   run.sh <power> <pitch> <iterations> <bailout> <maxSiteRadius>
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/mandelbulb_lattice_harness"
mkdir -p "$OUT"
if [ ! -f "$OUT/lattice.exe" ] || \
   [ "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/MandelbulbLattice.cs" -nt "$OUT/lattice.exe" ] || \
   [ "$HERE/Driver.cs" -nt "$OUT/lattice.exe" ] || [ "$HERE/Stubs.cs" -nt "$OUT/lattice.exe" ]; then
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/Driver.cs" \
    "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/MandelbulbLattice.cs" \
    | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
    -target:exe -main:Driver -out:"$OUT/lattice.exe" "@$OUT/files.rsp"
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
    > "$OUT/lattice.runtimeconfig.json"
fi
"$DOTNET" "$OUT/lattice.exe" "$@"
