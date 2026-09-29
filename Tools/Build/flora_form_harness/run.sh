#!/usr/bin/env bash
# Compile the SHIPPED FloraElementalForm.cs + FloraReproductionRules.cs against a UnityEngine
# stub and RUN them. Both are pure static files with no Unity type beyond Vector3/Mathf -
# which is exactly why the law lives in its own file rather than inside Flora: a MonoBehaviour
# cannot be compiled out of the editor (Roslyn abandons class-body binding when the base type
# is unresolved), and a rule nobody can run is a rule nobody proved.
#
# Needs a dotnet 8 SDK; a per-user install is fine:
#   bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh) --channel 8.0 \
#        --install-dir $HOME/.dotnet --no-path
#
#   run.sh table
#   run.sh shape 9,3.4,1.5 [x,y,z ...]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
# The real Element enum compiles in rather than being stubbed: it lives in the extracted
# CosmicShore.Data assembly and depends on no Unity type, so the harness runs the game's own
# enum instead of a transcription that could drift from it.
SRCS=("$ROOT/Assets/_Scripts/Utility/DataContainers/FloraElementalForm.cs" \
      "$ROOT/Assets/_Scripts/Utility/DataContainers/FloraReproductionRules.cs" \
      "$ROOT/Assets/_Scripts/Data/Enums/Element.cs")
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/flora_form_harness"
mkdir -p "$OUT"
NEED=0
[ -f "$OUT/form.exe" ] || NEED=1
for f in "${SRCS[@]}" "$HERE/Driver.cs" "$HERE/Stubs.cs"; do
  [ "$f" -nt "$OUT/form.exe" ] && NEED=1
done
if [ "$NEED" = 1 ]; then
  ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
  printf '%s\n' "$HERE/Stubs.cs" "$HERE/Driver.cs" "${SRCS[@]}" \
    | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"
  # csc writes diagnostics to STDOUT and this script's stdout is a DATA channel, so they go
  # to stderr, where they stay visible and still fail the build through set -e.
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
    -nowarn:CS1591,CS1574,CS8632 \
    -target:exe -main:Driver -out:"$OUT/form.exe" "@$OUT/files.rsp" 1>&2
  V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
  printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
    > "$OUT/form.runtimeconfig.json"
fi
"$DOTNET" "$OUT/form.exe" "$@"
