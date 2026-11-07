#!/usr/bin/env bash
# Runs the nose lead's pure-math EditMode tests (NoseLeadRollTests: AIPilot's roll law while the lead steers) headless,
# compiled with the SHIPPED NoseLeadSteering.cs against the Skim Race harness's Unity shim and the Switchback harness's
# NUnit shim and runner. Exit 0 = every case passed. It is not the Unity editor (the shim's math is a port), so the
# editor's own EditMode run still decides; this lets a cloud session see the tests pass or fail.
#
#   bash Tools/Build/nose_lead_harness/run.sh [name filter]
#
# Needs a dotnet 8 SDK: ~/.dotnet, or DOTNET_ROOT (apt's dotnet-sdk-8.0 installs to /usr/lib/dotnet).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(cd "$HERE/../../.." && pwd)"
if [ -z "${DOTNET_ROOT:-}" ]; then
  if [ -d "$HOME/.dotnet" ]; then DOTNET_ROOT="$HOME/.dotnet"; else DOTNET_ROOT=/usr/lib/dotnet; fi
fi
DOTNET="$DOTNET_ROOT/dotnet"
CSC=$(ls "$DOTNET_ROOT"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET_ROOT"/packs/Microsoft.NETCore.App.Ref/*/ref/net* | tail -1)
V=$(ls "$DOTNET_ROOT"/shared/Microsoft.NETCore.App | tail -1)
OUT="$(mktemp -d "${TMPDIR:-/tmp}/nose_lead_harness.XXXXXX")"
trap 'rm -rf "$OUT"' EXIT
ls "$REFDIR"/*.dll | sed 's/^/-r:/' > "$OUT/refs.rsp"
SB="$ROOT/Tools/Build/switchback_sim_harness"
printf '"%s"\n' "$ROOT/Tools/Build/skimrace_sim_harness/UnityShim.cs" "$SB/ShimExtra.cs" "$SB/NUnitShim.cs" "$SB/TestRunner.cs" \
  "$ROOT/Assets/_Scripts/Controller/AI/NoseLeadSteering.cs" \
  "$ROOT/Assets/_Scripts/Tests/Editor/NoseLeadRollTests.cs" > "$OUT/tests.rsp"
"$DOTNET" "$CSC" -nologo -langversion:latest -nostdlib -noconfig -optimize+ "@$OUT/refs.rsp" \
  -nowarn:CS1591,CS0067,CS0649,CS0414,CS1574,CS0169,CS8632,CS0108,CS1587,CS0219 \
  -target:exe -main:TestRunner -out:"$OUT/tests.dll" "@$OUT/tests.rsp" >&2
printf '{"runtimeOptions":{"tfm":"net%s.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "${V%%.*}" "$V" \
  > "$OUT/tests.runtimeconfig.json"
"$DOTNET" "$OUT/tests.dll" "$@"
