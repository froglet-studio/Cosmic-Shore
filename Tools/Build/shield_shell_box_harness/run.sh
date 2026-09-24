#!/usr/bin/env bash
# Compiles and RUNS the SHIPPED ShieldShellMath.cs against brute force.
# The box predicates must agree with PhysX's own shape semantics exactly — for an
# unshielded prism the box IS the surface, so any disagreement is a behaviour
# change, not an improvement. 40,000 randomised poses, zero tolerance.
set -euo pipefail
cd "$(dirname "$0")"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$PATH:$DOTNET_ROOT"
dotnet run -c Release --project harness.csproj 2>&1 | grep -vE "warning CS8981" | tail -8
