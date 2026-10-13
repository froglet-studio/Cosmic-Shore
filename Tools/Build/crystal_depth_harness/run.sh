#!/usr/bin/env bash
# Compiles and RUNS the shipped depth-prism maths headlessly:
#   Assets/_Scripts/Tests/Editor/DepthRatchetTests.cs  (the ratchet's two promises, with and without a random pose
#                                                       per crystal, and the pose's parity with the lab's JS twin)
# against the shipped DepthRatchet + CrystalPoseJitter, with UnityStub.cs standing in for UnityEngine.
# Needs a .NET 8 SDK (DOTNET_ROOT) and nuget for NUnit. harness.csproj is FORCE-ADDED (`git add -f`): the repo
# gitignores *.csproj for Unity's own.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$PATH"
dotnet run -c Release --project harness.csproj -- --noresult "$@" 2>&1 | grep -vE "warning CS" | tail -40
exit "${PIPESTATUS[0]}"
