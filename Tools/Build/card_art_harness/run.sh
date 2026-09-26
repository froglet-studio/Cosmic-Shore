#!/usr/bin/env bash
# Compile the SHIPPED arena and course generators against a UnityEngine + platform shim, run them,
# and render one card background per mode from what they emit. Driven by
# Tools/Build/render_card_backgrounds.py, which writes the render spec this reads - run THAT, not
# this, unless you are debugging the harness itself:
#
#   bash Tools/Build/card_art_harness/run.sh <spec.json> <out-dir>
#
# Needs a dotnet 8 SDK (a per-user install is fine: `bash <(curl -fsSL https://dot.net/v1/dotnet-install.sh)
# --channel 8.0 --install-dir $DOTNET_ROOT --no-path`). No .csproj on purpose: the repo gitignores
# *.csproj (Unity generates its own), so a project file here would be untracked and the harness
# would not survive a clone. Everything builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/card_art_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"

# The log-channel enum, lifted verbatim out of the real logger so generators that name a channel
# compile without the logger's own dependencies.
{
  echo "namespace CosmicShore.Utility {"
  sed -n '/public enum CSLogChannel/,/^    }/p' "$ROOT/Assets/_Scripts/Utility/CSDebug.cs"
  echo "}"
} > "$OUT/Generated.cs"

# Every repo source the harness compiles. The driver hashes this same list into the render
# manifest, so a change to any of them makes the committed cards stale.
python3 "$HERE/../render_card_backgrounds.py" --list-sources | sed "s|^|$ROOT/|" > "$OUT/sources.txt"
{
  printf '%s\n' "$HERE/UnityShim.cs" "$HERE/PlatformShim.cs" "$HERE/Renderer.cs" "$HERE/Program.cs" "$OUT/Generated.cs"
  cat "$OUT/sources.txt"
} | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"

"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661,CS0108,CS0162,CS0219 \
  -target:exe -main:Program -out:"$OUT/cardart.exe" "@$OUT/files.rsp"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/cardart.runtimeconfig.json"
"$DOTNET" "$OUT/cardart.exe" "$@"
