#!/usr/bin/env bash
# Compile the SHIPPED builder / thief sim cores (Assets/.../FloraAndFauna/Builders/*Core.cs - pure System.Numerics, no
# Unity) against netstandard2.1 (Unity's API profile) and then with Arena.cs + Program.cs, and RUN the asserted
# tests (Docs/BUILDERS_AND_THIEVES.md §7). Exit code is non-zero on any failure.
#
#   bash Tools/Build/builders_harness/run.sh             # everything
#   bash Tools/Build/builders_harness/run.sh fortress    # the fortress block only
#   bash Tools/Build/builders_harness/run.sh thieves     # the thieves block only
#   bash Tools/Build/builders_harness/run.sh wearers     # the wearers block only
#
# Needs a dotnet 8 SDK (DOTNET_ROOT). No .csproj on purpose (the repo gitignores *.csproj); builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/builders_harness"
mkdir -p "$OUT"
B="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Builders"
CORES=("$B/BuilderCore.cs" "$B/BuilderColonyCore.cs" "$B/ThiefNestCore.cs" "$B/WearerCore.cs")
# Unity compiles these against netstandard2.1 + C# 9 - narrower than net8.0; fail the way Unity would
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -out:"$OUT/unityprofile.dll" \
  "${CORES[@]}" || { echo "FAIL: the builder cores do not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -target:exe -main:Program -out:"$OUT/builders.exe" "${CORES[@]}" "$HERE/Arena.cs" "$HERE/WearArena.cs" "$HERE/Program.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$V" > "$OUT/builders.runtimeconfig.json"
exec "$DOTNET" "$OUT/builders.exe" "$@"
