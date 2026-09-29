#!/usr/bin/env bash
# Compile the SHIPPED MandelbulbSurface.cs against a UnityEngine stub and RUN it. This is
# the only way anything about this species is proven: an out-of-editor SYNTAX check over a
# MonoBehaviour proves almost nothing (Roslyn abandons class-body binding when the base
# type is unresolved), but the whole growth rule is a pure file with no Unity types beyond
# Vector3/Mathf, so it compiles against a stub and executes.
#
# Needs a dotnet 8 SDK; a per-user install is fine:
#   bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 \
#        --install-dir $HOME/.dotnet --no-path
#
#   run.sh selftest
#   run.sh probe <power> <cx> <cy> <cz> <w> <h> <iterations> <bailout> <sigma>
#   run.sh bake  <power> <cx> <cy> <cz> <delta> <w> <h> <degree> <iterations> <bailout> <sigma>
#   run.sh grow  <inputFile>
#   run.sh shipped <element> <w0> <w1> <w2> <gridW> <seed> <budget> <rules...>
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
FLORA="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna"
# The shipped table and the real Element enum compile in too, so the harness proves the
# SHIPPED basis growing the SHIPPED plant rather than a transcription of either. Element
# lives in the extracted CosmicShore.Data assembly and depends on no Unity type, so it can
# be taken verbatim instead of stubbed.
SRCS=("$FLORA/MandelbulbSurface.cs" "$FLORA/MandelbulbSurfaceTables.cs" \
      "$ROOT/Assets/_Scripts/Data/Enums/Element.cs")
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/mandelbulb_surface_harness"
mkdir -p "$OUT"
NEED=0
[ -f "$OUT/surface.exe" ] || NEED=1
for f in "${SRCS[@]}" "$HERE/Driver.cs" "$HERE/Stubs.cs" "$HERE/Bulb.cs"; do
  [ "$f" -nt "$OUT/surface.exe" ] && NEED=1
done
if [ "$NEED" = 1 ]; then
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/Bulb.cs" "$HERE/Driver.cs" "${SRCS[@]}" \
    | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  # csc writes diagnostics to STDOUT and this script's stdout is a DATA channel, so they go
  # to stderr, where they stay visible and still fail the build through set -e.
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661 \
    -target:exe -main:Driver -out:"$OUT/surface.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
    > "$OUT/surface.runtimeconfig.json"
fi
"$DOTNET" "$OUT/surface.exe" "$@"
