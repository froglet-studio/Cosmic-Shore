#!/usr/bin/env bash
# Compile the SHIPPED controlling-domain resolver (Domains.cs + InitialControllingDomain.cs - pure, no
# First runs three NEGATIVE CONTROLS (mutants that ignore the authored rule) and fails unless each
# mutant makes the harness fail.
# Unity) with Program.cs and RUN it (QA-SWARM-ROUND11-13). Exit code is non-zero on any failure.
#
#   bash Tools/Build/cell_control_harness/run.sh
#
# Needs a dotnet 8 SDK. No .csproj on purpose: the repo gitignores *.csproj. Builds into $TMPDIR.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
DOTNET_ROOT="${DOTNET_ROOT:-/usr/lib/dotnet}"
[ -x "$DOTNET_ROOT/dotnet" ] || DOTNET_ROOT="$HOME/.dotnet"
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
NSREF=$(ls "$DOTNET_ROOT"/packs/NETStandard.Library.Ref/*/ref/netstandard2.1/netstandard.dll | head -1)
OUT="${TMPDIR:-/tmp}/cell_control_harness"
mkdir -p "$OUT"
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
EN="$ROOT/Assets/_Scripts/Data/Enums"
SRC=("$EN/Domains.cs" "$EN/InitialControllingDomain.cs")
# Unity's API profile first (netstandard2.1, C# 9), so a construct Unity rejects fails here too.
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "-r:$NSREF" -target:library \
  -out:"$OUT/unityprofile.dll" "${SRC[@]}" \
  || { echo "FAIL: the resolver does not compile against netstandard2.1 (Unity's API profile)" >&2; exit 1; }
"$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" -target:exe \
  -out:"$OUT/cellcontrol.exe" "${SRC[@]}" "$HERE/Program.cs"
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | head -1)
RTC='{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}'
printf "$RTC" "$V" > "$OUT/cellcontrol.runtimeconfig.json"

# NEGATIVE CONTROLS: the same harness against resolvers that IGNORE the authored rule must FAIL,
# or a green run proves nothing. Each mutant is a one-line sed of the shipped file; the sed is
# itself checked to have changed the source, so a refactor that renames the line breaks loudly.
mutant() { # name, sed expression
  local name="$1" expr="$2" src="$OUT/mutant_$1.cs"
  sed "$expr" "$EN/InitialControllingDomain.cs" > "$src"
  if cmp -s "$src" "$EN/InitialControllingDomain.cs"; then
    echo "FAIL: negative control '$name' did not mutate the resolver (line moved?)" >&2; exit 1
  fi
  "$DOTNET" "$CSC" -nologo -langversion:9.0 -nostdlib -noconfig "@$OUT/refs.rsp" -target:exe \
    -out:"$OUT/mutant_$name.exe" "$EN/Domains.cs" "$src" "$HERE/Program.cs"
  printf "$RTC" "$V" > "$OUT/mutant_$name.runtimeconfig.json"
  if "$DOTNET" "$OUT/mutant_$name.exe" "$ROOT/Assets/_Scripts/Controller/Environment/Cell.cs" \
       "$ROOT/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs" > "$OUT/mutant_$name.log" 2>&1; then
    echo "FAIL: negative control '$name' PASSED - the harness cannot see the authored rule being ignored" >&2; exit 1
  fi
  echo "negative control '$name': harness fails as it must ($(grep -c '  FAIL ' "$OUT/mutant_$name.log") failing checks)"
}
# 1. the resolver skips the authored start entirely (the pre-fix behaviour).
mutant ignore_start 's/^\( *\)if (IsPlayable(startingController)) return startingController;/\1\/\/ ignored/'
# 2. the authored start sits BELOW gameData's volume leader (the pilot, once they lay trail).
mutant start_below_volume 's/^\( *\)if (IsPlayable(startingController)) return startingController;/\1if (IsPlayable(volumeLeader) \&\& volumeLeaderVolume > 0f) return volumeLeader; if (IsPlayable(startingController)) return startingController;/'
# 3. OpposingLocalPilot resolves to the pilot's own colour (a friendly cell).
mutant opposing_is_pilot 's/InitialControllingDomain.OpposingLocalPilot => Opposing(localPilot),/InitialControllingDomain.OpposingLocalPilot => localPilot,/'
echo
exec "$DOTNET" "$OUT/cellcontrol.exe" \
  "$ROOT/Assets/_Scripts/Controller/Environment/Cell.cs" \
  "$ROOT/Assets/_Scripts/Controller/Environment/CellNetworkSync.cs"
