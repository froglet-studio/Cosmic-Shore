#!/usr/bin/env bash
# The gates /ship runs on the merged tree before the PR (SKILL.md 1.5): the static checks
# bleeding-edge-guard.yml runs, every asset generator's --check, and - only when a .NET SDK is
# already present - the headless reference compile (player + editor). Prints one line per check and
# a final verdict; exit 1 on any failure. A READER script: it writes nothing into the repo.
#
# The gate LIST lives in Tools/Build/ship_gates.py (since 2026-11-06), so the Unity Source Control
# window can run the same gates on Windows, where there may be no bash. Add a gate THERE, never here.
# (Was .claude/skills/to-bleeding-edge/gates.sh until 2026-10-26, when /to-bleeding-edge folded into /ship.)
set -uo pipefail
cd "$(git rev-parse --show-toplevel)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
exec python3 Tools/Build/ship_gates.py "$@"
