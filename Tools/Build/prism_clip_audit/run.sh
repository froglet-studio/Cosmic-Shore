#!/usr/bin/env bash
# Compile every shipped Spawnable shape against the card-art harness's shims and RUN the prism clip
# audit over the spec Tools/Build/audit_prism_clipping.py writes. Run THAT, not this:
#
#   python3 Tools/Build/audit_prism_clipping.py            # the report
#   python3 Tools/Build/audit_prism_clipping.py --check    # fail on any clipping shape
#
# The card-art shims are compiled as PARTIAL copies (written into $TMPDIR, never edited in place) so
# ClipShim.cs can add the few members the shapes' spawn paths name without forking the harness.
# Needs a dotnet 8 SDK (DOTNET_ROOT, a per-user install is fine). No .csproj on purpose.
#
#   bash Tools/Build/prism_clip_audit/run.sh <spec.json>
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
CARD="$ROOT/Tools/Build/card_art_harness"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
OUT="${TMPDIR:-/tmp}/prism_clip_audit"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"

for f in UnityShim PlatformShim; do
  sed -E 's/^(    public )((static |abstract |sealed )*)(class|struct) /\1\2partial \4 /' "$CARD/$f.cs" > "$OUT/$f.cs"
done
# Every generation passes through ClipAudit.Hook, which applies the shipped PrismClearance pass the
# way SpawnableBase.GetTrailData does when the audit asks for the resolved run.
sed -i 's/public SpawnTrailData\[\] GenerateForHarness() => GenerateTrailData();/public SpawnTrailData[] GenerateForHarness() => ClipAudit.Hook(this, GenerateTrailData());/' "$OUT/PlatformShim.cs"
grep -q 'ClipAudit.Hook' "$OUT/PlatformShim.cs" || { echo "FAIL: the card-art GenerateForHarness moved; re-point run.sh's hook" >&2; exit 1; }
# The shapes name UnityEngine.Renderer (ClipShim), which makes the card-art driver's bare
# `Renderer.Render` ambiguous; qualify it in the copy.
# And every leaf lay passes through the prism prefab's per-axis scale window, as it does in the
# game (PrismScaleAnimator.SetTargetScale): ClipAudit.Clamp applies the window the driver read off
# the prefab's prism, or nothing for a shape that admits its authored scale.
sed -e 's/\bRenderer\.Render(/CardArt.Renderer.Render(/' \
    -e 's/new SpawnPoint(m\.Point(p\.Position), m\.R \* p\.Rotation, Vector3\.Scale(m\.S, p\.Scale))/new SpawnPoint(m.Point(p.Position), m.R * p.Rotation, Vector3.Scale(m.S, ClipAudit.Clamp(p.Scale)))/' \
    "$CARD/Program.cs" > "$OUT/Program.cs"
grep -q 'ClipAudit.Clamp' "$OUT/Program.cs" || { echo "FAIL: the card-art Xform moved; re-point run.sh's clamp hook" >&2; exit 1; }
{
  echo "namespace CosmicShore.Utility {"
  sed -n '/public enum CSLogChannel/,/^    }/p' "$ROOT/Assets/_Scripts/Utility/CSDebug.cs"
  echo "}"
  # SpawnableGyroid's block-type enum, lifted verbatim out of the assembler it lives in.
  echo "namespace CosmicShore.Gameplay {"
  sed -n '/public enum GyroidBlockType/,/^    }/p' "$ROOT/Assets/_Scripts/Controller/Assemblers/GyroidAssembler.cs"
  echo "}"
} > "$OUT/Generated.cs"

python3 "$ROOT/Tools/Build/audit_prism_clipping.py" --list-sources | sed "s|^|$ROOT/|" > "$OUT/sources.txt"
{
  printf '%s\n' "$OUT/UnityShim.cs" "$OUT/PlatformShim.cs" "$OUT/Generated.cs" \
    "$CARD/Renderer.cs" "$OUT/Program.cs" "$HERE/ClipShim.cs" "$HERE/Audit.cs"
  cat "$OUT/sources.txt"
} | sed 's/^/"/;s/$/"/' > "$OUT/files.rsp"

"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0660,CS0661,CS0108,CS0162,CS0219,CS0114 \
  -target:exe -main:ClipAudit -out:"$OUT/clipaudit.exe" "@$OUT/files.rsp" >&2
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/clipaudit.runtimeconfig.json"
"$DOTNET" "$OUT/clipaudit.exe" "$@"
