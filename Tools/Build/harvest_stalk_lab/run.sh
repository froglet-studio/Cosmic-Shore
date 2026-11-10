#!/usr/bin/env bash
# The Harvest stalk lab (Docs/MassAndSpaceGames/THRESHER_SPACE.md section 10): the SHIPPED ThresherChainSolver and
# dials flown round one Harvest sheaf. Run from the repo root:
#   bash Tools/Build/harvest_stalk_lab/run.sh                 # stalks in every direction (as built)
#   bash Tools/Build/harvest_stalk_lab/run.sh wheel 0          # stalks in one plane, lap in that plane
#   bash Tools/Build/harvest_stalk_lab/run.sh wheel 20         # ... the lap 20 degrees off the wheel
#   bash Tools/Build/harvest_stalk_lab/run.sh wheel 0 axis     # ... then strike down the axle
# Needs a dotnet 8 SDK (DOTNET_ROOT, default /usr/lib/dotnet then ~/.dotnet).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$( [ -x /usr/lib/dotnet/dotnet ] && echo /usr/lib/dotnet || echo "$HOME/.dotnet")}"
out="${TMPDIR:-/tmp}/harvest_stalk_lab"
"$DOTNET_ROOT/dotnet" build "$here/StalkLab.csproj" -c Release -v q -o "$out" --nologo >/dev/null
cd "$(git -C "$here" rev-parse --show-toplevel)"
"$DOTNET_ROOT/dotnet" "$out/StalkLab.dll" "$@"
