#!/usr/bin/env bash
# Compiles and RUNS the shipped crystal-morph geometry suites headlessly:
#   Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs   (the Scarab's hull mapping)
#   Assets/_Scripts/Tests/Editor/CrystalMorphPanelCensusTests.cs   (the Squirrel's panel census)
# against the shipped CrystalMorphMeshBuilder, with UnityStub.cs standing in for UnityEngine
# (plain-array Mesh, value-type maths). Needs a .NET 8 SDK (DOTNET_ROOT) and nuget for NUnit.
# harness.csproj is FORCE-ADDED (`git add -f`): the repo gitignores *.csproj for Unity's own,
# and a tracked file survives a clone regardless. Edit it in place; do not delete and re-create.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
dotnet run -c Release --project harness.csproj -- --noresult "$@" 2>&1 | grep -vE "warning CS" | tail -40
exit "${PIPESTATUS[0]}"
