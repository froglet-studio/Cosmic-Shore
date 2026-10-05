#!/usr/bin/env bash
# THREAT FLORA (round 11c, Docs/THREAT_FLORA.md §6). Three gates, in order, each fatal:
#   1. the two shipped sim cores compile against netstandard2.1 + C# 9 (Unity's API profile);
#   2. the asserted harness: SnapTrapCore + PhysarumCore against the research's numbers (Program.cs);
#   3. a Roslyn TYPE-CHECK of the Unity glue (ThreatGrove / SnapTrapFlora / PhysarumSclerotium /
#      ThreatGroveConfigSO) against hand-copied stubs (GlueStubs.cs says what that does and does not prove).
#
#   bash Tools/Build/threat_flora_harness/run.sh [snap|physarum] [quick]
#
# Needs a dotnet 8 SDK (DOTNET_ROOT, default ~/.dotnet). No .csproj: the repo gitignores them.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/threat_flora_harness"
mkdir -p "$OUT"
TF="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/ThreatFlora"
CORES=("$TF/ThreatFloraMath.cs" "$TF/SnapTrapCore.cs" "$TF/PhysarumCore.cs" "$TF/ThreatGroveDefaults.cs")
GLUE=("$TF/ThreatGroveConfigSO.cs" "$TF/ThreatGrove.cs" "$TF/SnapTrapFlora.cs" "$TF/PhysarumSclerotium.cs")

# 1. Unity's API profile
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -out:"$OUT/cores_unityprofile.dll" \
  "${CORES[@]}" || { echo "FAIL: the threat-flora cores do not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }

# 3 (first, it takes a second): the glue type-check
echo "-r:$NSREF" > "$OUT/glue.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/glue.rsp" -target:library -nowarn:CS0108,CS0114,CS0649,CS0414 \
  -out:"$OUT/glue.dll" "$HERE/GlueStubs.cs" "${CORES[@]}" "${GLUE[@]}" \
  || { echo "FAIL: the threat-flora glue does not type-check against GlueStubs.cs" >&2; exit 1; }
echo "glue type-check OK"

# 2. the asserted harness
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -target:exe -main:Program \
  -out:"$OUT/threatflora.exe" "${CORES[@]}" "$HERE/FloraArena.cs" "$HERE/SnapTrapTests.cs" "$HERE/PhysarumTests.cs" "$HERE/Program.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/threatflora.runtimeconfig.json"
exec "$DOTNET" "$OUT/threatflora.exe" "$@"
