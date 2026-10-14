#!/usr/bin/env bash
# Run the party layer's Block 3 scenarios: five instances of the game, five processes, one party.
#
#   bash Tools/Build/prisma_party_scenarios/run.sh            # build, launch 5, run, tear down
#   KEEP=1 bash Tools/Build/prisma_party_scenarios/run.sh     # leave the worktree + logs behind
#
# WHY PROCESSES, NOT NGO'S IN-PROCESS HARNESS. Block 3 asked for NGO's NetcodeIntegrationTest
# (several NetworkManagers in one process). Measured 2026-10-08, it cannot test this layer here:
# its helpers compile into Unity.Netcode.Runtime.Tests (autoReferenced false, referencing an
# assembly name absent from the package), the project's tests live in Assembly-CSharp-Editor which
# an asmdef cannot reference back into, and - decisively - the code under test reads
# NetworkManager.Singleton at 161 sites in 54 runtime files, so two NetworkManagers in one process
# share one Singleton and every gameplay path talks to whichever registered last. A process per
# player gives each its own Singleton and runs the shipped code unmodified.
#
# WHAT IT RUNS ON. Prisma (Port/): the game's Assets/_Scripts compiled live against a .NET
# re-implementation of the engine, each pilot online the way START ONLINE is (UGS sign-in, UGS Relay,
# UGS Lobby - Port/docs/MULTIPLAYER.md §6.10) with all three pointed at local stand-ins: the sign-in at
# ugs_auth_standin.py, Relay and Lobby at standins/ (the engine's FrogletRelayServer and the engine tests'
# UgsLobbyStandIn). Nothing here talks to the live UGS project. See README.md for what a pass here does
# and does not prove - it is evidence, not a substitute for the owner's MPPM run (Block 2).
#
# WHAT IT TOUCHES. Nothing committed: a `git worktree` of HEAD with THIS checkout's
# Assets/_Scripts copied over it (uncommitted edits are tested), the engine's API gaps filled in
# that worktree only (../prisma_edit_mode_tests/gapfill.py - Port/CLAUDE.md), and removed after.
#
# NEEDS the .NET 10 SDK, as ../prisma_edit_mode_tests/run.sh does. ~10-15 min wall, most of it the
# first-run boot of five instances.
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"
DOTNET10_ROOT="${DOTNET10_ROOT:-$HOME/.dotnet10}"
if [ -x "$DOTNET10_ROOT/dotnet" ]; then export DOTNET_ROOT="$DOTNET10_ROOT" PATH="$DOTNET10_ROOT:$PATH"; fi
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "prisma_party_scenarios: no .NET 10 SDK found (see ../prisma_edit_mode_tests/run.sh)." >&2; exit 2
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

WORK="${PRISMA_PARTY_WORK:-${TMPDIR:-/tmp}/prisma_party_scenarios}"
mkdir -p "$WORK" && WORK="$(cd "$WORK" && pwd)"
WT="$WORK/worktree"; PLAYER="$WORK/player"; STANDINS="$WORK/standins"; LOGS="$WORK/logs"; HOMES="$WORK/home"
BASE_PORT="${PRISMA_PARTY_BASE_PORT:-47900}"
LABELS=(A B C D E)
PIDS=()

cleanup() {
  for p in "${PIDS[@]:-}"; do [ -n "$p" ] && kill -9 "$p" 2>/dev/null || true; done
  if [ "${KEEP:-0}" != "1" ]; then
    git -C "$REPO" worktree remove --force "$WT" >/dev/null 2>&1 || true
    git -C "$REPO" worktree prune
  fi
}
trap cleanup EXIT

git -C "$REPO" worktree remove --force "$WT" >/dev/null 2>&1 || rm -rf "$WT"
git -C "$REPO" worktree prune
git -C "$REPO" worktree add --detach --quiet "$WT" HEAD
rm -rf "$WT/Assets/_Scripts" && cp -a "$REPO/Assets/_Scripts" "$WT/Assets/_Scripts"
echo "prisma_party_scenarios: engine gap-fill (worktree only; a no-op once the port has the members)"
python3 "$HERE/../prisma_edit_mode_tests/gapfill.py" "$WT"

echo "prisma_party_scenarios: building the player against this checkout's Assets/_Scripts"
rm -rf "$PLAYER" "$STANDINS" "$LOGS" "$HOMES"; mkdir -p "$LOGS" "$HOMES"
if ! dotnet build "$WT/Port/src/CosmicShore.Player" -c Debug -v q -nologo -o "$PLAYER" > "$WORK/build.log" 2>&1; then
  grep -oE '[^ /]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: [^[]*' "$WORK/build.log" | sort -u | head -40
  echo "prisma_party_scenarios: BUILD FAILED - full log: $WORK/build.log" >&2
  exit 1
fi

# The UGS stand-ins. The sign-in (ugs_auth_standin.py) hands out id tokens shaped "id-PLAYER-N", which
# the Lobby stand-in reads as the caller; the relay takes any bearer (no secret), as our harness relay
# always did. Each pilot signs in as its own profile's UGS player, so five pilots are five players.
echo "prisma_party_scenarios: building the UGS Relay + Lobby stand-ins"
cp -a "$HERE/standins" "$WORK/standins-src"
if ! dotnet build "$WORK/standins-src" -c Debug -v q -nologo -o "$STANDINS" -p:PortRoot="$WT/Port" > "$WORK/standins-build.log" 2>&1; then
  grep -E 'error' "$WORK/standins-build.log" | sort -u | head -20
  echo "prisma_party_scenarios: STAND-IN BUILD FAILED - full log: $WORK/standins-build.log" >&2
  exit 1
fi
rm -rf "$WORK/standins-src"

wait_for() {  # wait_for LOG VARIABLE: the value a stand-in prints as VARIABLE=VALUE once it listens
  local v=""
  for _ in $(seq 1 100); do
    v="$(grep -o "$2=[^) ]*" "$1" 2>/dev/null | head -1 | cut -d= -f2- || true)"
    [ -n "$v" ] && break; sleep 0.2
  done
  echo "$v"
}

UGS_PROJECT="00000000-0000-0000-0000-00000000f106"
python3 -I "$HERE/ugs_auth_standin.py" 0 "$UGS_PROJECT" > "$LOGS/ugs-auth.log" 2>&1 < /dev/null &
PIDS+=($!)
nohup dotnet "$STANDINS/PartyStandIns.dll" > "$LOGS/standins.log" 2>&1 < /dev/null &
PIDS+=($!)
AUTH_URL="$(wait_for "$LOGS/ugs-auth.log" COSMIC_SHORE_UGS_AUTH_URL)"
RELAY_URL="$(wait_for "$LOGS/standins.log" COSMIC_SHORE_UGS_RELAY_URL)"
LOBBY_URL="$(wait_for "$LOGS/standins.log" COSMIC_SHORE_UGS_LOBBY_URL)"
if [ -z "$AUTH_URL" ] || [ -z "$RELAY_URL" ] || [ -z "$LOBBY_URL" ]; then
  echo "prisma_party_scenarios: a UGS stand-in did not start:" >&2; cat "$LOGS/ugs-auth.log" "$LOGS/standins.log" >&2; exit 1
fi
export COSMIC_SHORE_UGS_PROJECT="$UGS_PROJECT" COSMIC_SHORE_UGS_AUTH_URL="$AUTH_URL" \
  COSMIC_SHORE_UGS_RELAY_URL="$RELAY_URL" COSMIC_SHORE_UGS_LOBBY_URL="$LOBBY_URL"
echo "prisma_party_scenarios: UGS stand-ins up - sign-in $AUTH_URL, Relay $RELAY_URL, Lobby $LOBBY_URL"

# One profile per pilot, each with its own HOME so no instance inherits another run's saved
# username, consent, party or UGS session token.
cd "$WORK"   # nothing an instance writes relative to its cwd may land in the repo
SPEC="$WORK/instances.json"
echo "{" > "$SPEC"
for k in "${!LABELS[@]}"; do
  L="${LABELS[$k]}"; PORT=$((BASE_PORT + k + 1))
  # The engine falls back to ~/.local/share when .NET reports no LocalApplicationData
  # (LocalDataPath.cs); creating the folder keeps each pilot's saves under its own HOME either way.
  mkdir -p "$HOMES/$L/.local/share"
  HOME="$HOMES/$L" XDG_DATA_HOME="$HOMES/$L/.local/share" COSMIC_SHORE_PROJECT="$WT" COSMIC_SHORE_PROFILE="party$L" \
    COSMIC_SHORE_AUDIO=off nohup dotnet "$PLAYER/CosmicShore.dll" --headless --realtime --verbose --control-port "$PORT" \
    > "$LOGS/$L.log" 2>&1 < /dev/null &
  PIDS+=($!)
  SEP=","; [ "$k" -eq $((${#LABELS[@]} - 1)) ] && SEP=""
  printf '  "%s": {"port": %d, "log": "%s", "pid": %d}%s\n' "$L" "$PORT" "$LOGS/$L.log" "$!" "$SEP" >> "$SPEC"
done
echo "}" >> "$SPEC"
echo "prisma_party_scenarios: ${#LABELS[@]} instances up (ports $((BASE_PORT + 1))-$((BASE_PORT + ${#LABELS[@]}))), logs in $LOGS"

set +e
python3 "$HERE/scenarios.py" --instances "$SPEC" --out "$WORK/results.json" --repo "$REPO"
RC=$?
set -e
echo "prisma_party_scenarios: $(grep -c 'sign-in: player' "$LOGS/ugs-auth.log" || true) UGS sign-ins (log: $LOGS/ugs-auth.log)"
exit $RC
