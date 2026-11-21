#!/usr/bin/env bash
# Compile and RUN the SHIPPED MinimumThrottleBrake.cs against this harness.
#
# What it proves, and what it does not. It proves the reverse-capable brake added for the Urchin
# is BIT-IDENTICAL to the pre-reverse one for every hull that cannot command reverse (T1), that
# the flag it is gated on genuinely changes the answer wherever it can (T1b), that a reversing
# vessel released to centre lands on an EXACT zero in the same time a forward one does (T2), that
# it can never push a reversing vessel past the stop into forward motion (T3), and that a reverse
# COMMAND is never clamped to a stop (T4). It does not prove anything about the transformer that
# calls it — that file is type-checked, not run.
#
# T1b is the one to read before changing a constant: the brake's contract is that the exponential
# owns the fast fall and the constant rate owns only the tail, so the flag can ONLY matter below
# `rate / LERP_AMOUNT`. A control picked above that crossover comes back green while proving
# nothing — which is exactly how this test first passed 1 of 3.
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
  -out:"$OUT/x.exe" \
  Tools/Build/urchin_reverse_harness/Stubs.cs \
  Tools/Build/urchin_reverse_harness/Driver.cs \
  Assets/_Scripts/Controller/Vessel/MinimumThrottleBrake.cs
printf '{"runtimeOptions":{"tfm":"net8.0","framework":{"name":"Microsoft.NETCore.App","version":"%s"}}}' "$VER" \
  > "$OUT/x.runtimeconfig.json"
"$DOTNET/dotnet" "$OUT/x.exe"
