#!/usr/bin/env bash
# Compile the FOUR SHIPPED Cleave arena generators against a UnityEngine + platform shim and RUN
# them, writing Tools/Build/cleave_arena_measurements.json.
#
# Three of the four arenas cull prisms with value noise, so an analytic model would have to
# re-implement that noise to be exact and would silently become an ESTIMATE the day either
# drifted. Running the real code has no drift surface - and the same run measures the JITTERED
# volume exactly rather than through an E[k^3] expectation.
#
# Needs a dotnet 8 SDK (per-user install is fine: `bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh)
# --channel 8.0 --install-dir $DOTNET_ROOT --no-path`). No .csproj on purpose: the repo gitignores
# *.csproj (Unity generates its own), so a project file here would be untracked and the harness
# would not survive a clone. Everything builds into $TMPDIR.
#
# Re-run after ANY edit to the arena generators, SliceArenaGeometry, or the shims, and COMMIT the
# JSON: cleave_budget.py re-hashes every one of those sources and refuses a stale measurement.
#
#   bash Tools/Build/cleave_arena_harness/run.sh            # measure and write the JSON
#   bash Tools/Build/cleave_arena_harness/run.sh --check    # fail if the committed JSON is stale
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/cleave_arena_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
ARENAS="$ROOT/Assets/_Scripts/Controller/Environment/MiniGameObjects"
printf '%s\n' "$HERE/UnityShim.cs" "$HERE/PlatformShim.cs" "$HERE/Program.cs" \
  "$ARENAS/SliceArenaGeometry.cs" \
  "$ARENAS/SpawnablePanes.cs" \
  "$ARENAS/SpawnableSwell.cs" \
  "$ARENAS/SpawnableRibcage.cs" \
  "$ARENAS/SpawnableTwistbands.cs" | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661,CS0108 \
  -target:exe -main:Program -out:"$OUT/arenas.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/arenas.runtimeconfig.json"
"$DOTNET" "$OUT/arenas.exe" "$@"
