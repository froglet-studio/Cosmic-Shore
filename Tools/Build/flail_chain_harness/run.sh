#!/usr/bin/env bash
# Compile and RUN the SHIPPED FlailChainSolver.cs and the SHIPPED FlailChainSolverTests.cs (both from
# their real paths) against Unity-shaped stubs with real vector maths, then print the feel table.
# Exit 1 on any failing test. See README.md.
set -euo pipefail
cd "$(dirname "$0")/../../.."
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ ! -x "$DOTNET/dotnet" ]; then
  echo "No per-user dotnet at $DOTNET. Install it (no root needed, ~40s):" >&2
  echo "  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir \"\$HOME/.dotnet\"" >&2
  exit 2
fi
CSC=$(ls "$DOTNET"/sdk/*/Roslyn/bincore/csc.dll | head -1)
REFDIR=$(ls -d "$DOTNET"/packs/Microsoft.NETCore.App.Ref/*/ref/net8.0 | head -1)
REFS=$(ls "$REFDIR"/*.dll | sed 's/^/-r:/' | tr '\n' ' ')
VER=$(ls "$DOTNET"/shared/Microsoft.NETCore.App | head -1)
OUT=$(mktemp -d)
"$DOTNET/dotnet" "$CSC" -langversion:9.0 -nostdlib -noconfig $REFS -target:exe -main:Driver \
  -define:UNITY_EDITOR -nowarn:CS0414 -out:"$OUT/x.exe" \
  Tools/Build/flail_chain_harness/Stubs.cs \
  Tools/Build/flail_chain_harness/Driver.cs \
  Assets/_Scripts/Controller/Vessel/FlailChainSolver.cs \
  Assets/_Scripts/Tests/Editor/FlailChainSolverTests.cs
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" \
  > "$OUT/x.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/x.exe"
