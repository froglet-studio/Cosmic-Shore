#!/usr/bin/env bash
# NESTED GYROID (Docs/ECOSYSTEM.md §58). Compiles the SHIPPED NestedGyroidLattice.cs + the measured
# NestedGyroidTemplate.cs (the gyroid flora's tiling, Tools/Build/measure_nested_gyroid_template.py) and RUNS them:
#   1. the core compiles against netstandard2.1 + C# 9 (Unity's API profile);
#   2. the acceptance gates on the default config (Driver.cs says what each asserts, and each has a
#      negative control that must FAIL when the thing it guards is broken);
#   3. a model of the Urchin's layered ride kernel driven across the built stack.
#
#   bash Tools/Build/nested_gyroid_harness/run.sh            # gates (exit 1 on any failure)
#   bash Tools/Build/nested_gyroid_harness/run.sh sweep      # also sweeps N / tMax / cellsPerSide
#
# Needs a dotnet 8 SDK (DOTNET_ROOT, default ~/.dotnet). No .csproj: the repo gitignores them.
#   curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir "$HOME/.dotnet"
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
[ -d "$ROOT/Assets" ] || { echo "ROOT resolved to $ROOT, which has no Assets/" >&2; exit 2; }
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
[ -x "$DOTNET" ] || { echo "No dotnet at $DOTNET (see the header for the one-line install)" >&2; exit 2; }
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll 2>/dev/null | head -1 || true)
OUT="${TMPDIR:-/tmp}/nested_gyroid_harness"
mkdir -p "$OUT"
FAF="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna"
CORES=("$FAF/NestedGyroidLattice.cs" "$FAF/NestedGyroidTemplate.cs")

# 1. Unity's API profile (skipped, loudly, when the netstandard reference pack is absent)
if [ -n "$NSREF" ]; then
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library \
    -out:"$OUT/core_unityprofile.dll" "${CORES[@]}" \
    || { echo "FAIL: the nested-gyroid core does not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
  echo "core compiles against netstandard2.1 / C# 9: OK"
else
  echo "WARN: no NETStandard.Library.Ref pack - the Unity-profile compile was NOT checked" >&2
fi

# 1b. a Roslyn TYPE-CHECK of the Unity glue against hand-copied stubs (GlueStubs.cs says what that proves)
G="$ROOT/Assets/_Scripts"
GLUE=("$G/Controller/Environment/FloraAndFauna/NestedGyroidFlora.cs" "$G/Controller/Environment/FloraAndFauna/NestedGyroidConfigSO.cs"
      "$G/Controller/Vessel/ILayeredPrismscape.cs" "$G/Controller/Vessel/PrismscapeTopology.cs" "$G/Controller/Vessel/BlockscapeFollower.cs"
      "$G/Data/Enums/PrismscapeDimension.cs")
if [ -n "$NSREF" ]; then
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -warnaserror- -nowarn:CS0108,CS0114,CS0649,CS0414 \
    -out:"$OUT/glue.dll" "$HERE/GlueStubs.cs" "${CORES[@]}" "${GLUE[@]}" \
    || { echo "FAIL: the nested-gyroid glue does not type-check against GlueStubs.cs" >&2; exit 1; }
  echo "glue type-check OK"
fi

# 2 + 3. the gates
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -target:exe -main:Driver \
  -out:"$OUT/nestedgyroid.exe" "${CORES[@]}" "$HERE/Driver.cs" "$HERE/RideModel.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" \
  > "$OUT/nestedgyroid.runtimeconfig.json"
NG_ROOT="$ROOT" exec "$DOTNET" "$OUT/nestedgyroid.exe" "$@"
