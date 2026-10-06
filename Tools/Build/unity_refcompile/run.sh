#!/usr/bin/env bash
# Compile the project's runtime C# (Assembly-CSharp + every asmdef/package it references) against
# REAL Unity 6 reference assemblies and package sources, the way a Unity player build would.
#   bash Tools/Build/unity_refcompile/run.sh                 # player config (shipped IL2CPP release)
#   bash Tools/Build/unity_refcompile/run.sh --config player-dev
# Needs network (api.nuget.org + github.com + packages.unity.com, read-only) on the FIRST run only
# (without packages.unity.com five UGS packages are bucketed by name instead); everything fetched
# is cached under ${UNITY_REFCOMPILE_CACHE:-$TMPDIR/unity_refcompile_cache} and never committed.
# Offline with no cache it exits 2 with a clear message. See README.md for what it does and does
# not prove. Exit 0 = no errors in project code; 1 = errors (listed, changed-tonight ones tagged).
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-/usr/lib/dotnet}"
[ -x "$DOTNET_ROOT/dotnet" ] || DOTNET_ROOT="$HOME/.dotnet"
export TMPDIR="${TMPDIR:-/tmp}"
python3 "$HERE/fetch.py"
python3 "$HERE/build.py" "$@"
