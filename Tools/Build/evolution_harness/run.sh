#!/usr/bin/env bash
# The evolution harness (Docs/EVOLUTION.md §6): compile the SHIPPED genome core
# (Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution/*.cs, pure C#) plus the stomach it provisions
# (FloraAndFauna/Ecology/FaunaStomach.cs) first against netstandard2.1 / C# 9 - Unity's profile, so a core that
# would not compile in the Editor fails HERE - then against the installed .NET, together with the arena model and the
# gates, and RUN them. Exit code is non-zero on any failed gate or any negative control that did not bite.
#
#   bash Tools/Build/evolution_harness/run.sh                 # every group: core arena evidence golden (~1-2 min)
#   bash Tools/Build/evolution_harness/run.sh core,arena      # the fast gates only (seconds)
#   EVO_SEEDS=3 EVO_HOURS=2 bash Tools/Build/evolution_harness/run.sh evidence
#
# Writes Tools/Evolution/results/{results,golden,shipped}.json (the lab bakes them in: Tools/Evolution/build_lab.py).
# Needs a .NET SDK (8 or later; DOTNET_ROOT or /opt/dotnet or ~/.dotnet). The netstandard2.1 reference assembly is
# taken from the SDK's NETStandard.Library.Ref pack when it ships one, else fetched once from nuget.org into the
# cache below (the .NET 10 SDK ships none). No .csproj (the repo gitignores them).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
if [ -z "${DOTNET_ROOT:-}" ]; then
  for cand in /opt/dotnet "$HOME/.dotnet" /usr/share/dotnet /usr/lib/dotnet; do
    if [ -x "$cand/dotnet" ]; then DOTNET_ROOT="$cand"; break; fi
  done
fi
export DOTNET_ROOT="${DOTNET_ROOT:?no dotnet SDK found - set DOTNET_ROOT}"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | sort -V | tail -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | sort -V | tail -1)
TFM=$(basename "$REFDIR")
OUT="${TMPDIR:-/tmp}/evolution_harness"
mkdir -p "$OUT"

NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll 2>/dev/null | head -1 || true)
if [ -z "$NSREF" ]; then
  NSREF="$OUT/nsref/ref/netstandard2.1/netstandard.dll"
  if [ ! -f "$NSREF" ]; then
    echo "fetching netstandard.library.ref 2.1.0 (the SDK ships no NETStandard.Library.Ref pack)..."
    mkdir -p "$OUT/nsref"
    curl -sSL -m 300 -o "$OUT/nsref/ns.nupkg" https://api.nuget.org/v3-flatcontainer/netstandard.library.ref/2.1.0/netstandard.library.ref.2.1.0.nupkg
    python3 -I -c "import zipfile,sys; zipfile.ZipFile(sys.argv[1]).extractall(sys.argv[2])" "$OUT/nsref/ns.nupkg" "$OUT/nsref"
  fi
fi

ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
EVO="$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Evolution"
CORE=("$EVO"/LifeformGenome.cs "$EVO"/EvolutionSettings.cs "$EVO"/GenomeExpression.cs "$EVO"/GenomeMutation.cs "$EVO"/EvolutionLedger.cs
      "$ROOT/Assets/_Scripts/Controller/Environment/FloraAndFauna/Ecology/FaunaStomach.cs")

# Gate 0: Unity's profile (netstandard2.1, C# 9, warnings are errors). A core that fails here fails in the Editor.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library -warnaserror -nowarn:CS1591 \
  -out:"$OUT/unityprofile.dll" "${CORE[@]}" "$HERE/UnityAttributeStubs.cs" \
  || { echo "FAIL: the evolution core does not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
echo "[ok] the evolution core compiles against netstandard2.1 / C# 9 with warnings as errors"

"$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" -nowarn:CS0649,CS8632 \
  -target:exe -main:EvolutionHarness.Program -out:"$OUT/evo.exe" \
  "${CORE[@]}" "$HERE/UnityAttributeStubs.cs" "$HERE/EvolutionArena.cs" "$HERE/Program.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | sort -V | tail -1)
printf '{"runtimeOptions":{"tfm":"%s","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$TFM" "$V" > "$OUT/evo.runtimeconfig.json"
exec "$DOTNET" "$OUT/evo.exe" --root "$ROOT" "$@"
