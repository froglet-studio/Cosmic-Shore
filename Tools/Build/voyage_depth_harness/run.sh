#!/usr/bin/env bash
# Compiles and RUNS the shipped voyage rules headlessly (Docs/Voyages/DEPTH.md section 11):
#   Assets/_Scripts/Tests/Editor/VoyageRulesTests.cs, VoyageDepthTests.cs   the edit-mode suites
#   --scorecard [voyages] [galaxies]                                        thousands of voyages, the depth scorecard
# against the shipped Assets/_Scripts/Data/Voyages, with UnityStub.cs standing in for UnityEngine.
# Needs a .NET 8 SDK (DOTNET_ROOT) and nuget for NUnit. harness.csproj is FORCE-ADDED (`git add -f`): the repo
# gitignores *.csproj for Unity's own.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-/usr/lib/dotnet}"
[ -x "$DOTNET_ROOT/dotnet" ] || DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"
dotnet run -c Release --project harness.csproj -- --noresult "$@" 2>&1 | grep -vE "warning CS" | tail -80
exit "${PIPESTATUS[0]}"
