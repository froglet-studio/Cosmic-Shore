#!/usr/bin/env bash
# Compiles and RUNS the shipped crystal-morph geometry suites headlessly:
#   Assets/_Scripts/Tests/Editor/CrystalMorphMeshBuilderTests.cs   (the Scarab's hull mapping)
#   Assets/_Scripts/Tests/Editor/CrystalMorphPanelCensusTests.cs   (the Squirrel's panel census)
#   Assets/_Scripts/Tests/Editor/DolphinCrystalMorphGeometryTests.cs (the Dolphin's crystal onto its
#                                                                   blast cone, cast around the crystal)
#   Assets/_Scripts/Tests/Editor/CrystalHullFusionGeometryTests.cs (an elemental crystal's faces
#                                                                   landing on a vessel hull)
#   Assets/_Scripts/Tests/Editor/ShrikeCrystalMorphGeometryTests.cs (the Shrike: the crystal folding into
#                                                                   its nest flora's bud, CrystalMorphRecast)
#   Assets/_Scripts/Tests/Editor/SphereBlastCrystalMorphGeometryTests.cs (Butterfly / Rhino / Manta:
#                                                                   the crystal onto its spherical blast)
#   Assets/_Scripts/Tests/Editor/FaunaBirthCrystalMorphGeometryTests.cs (Angler / Jacks: the crystal
#                                                                   folding into the newborn creature's heart)
#   Assets/_Scripts/Tests/Editor/HoneyguideCrystalMorphGeometryTests.cs (Honeyguide: the crystal breaking
#                                                                   into the hive comb's eight heart cells)
#   Assets/_Scripts/Tests/Editor/VesselAbsorbCrystalMorphGeometryTests.cs (Brittle Star / Sparrow / Stoat / Orca /
#                                                                   Wrasse: the crystal folding into the hull)
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
