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
# re-implementation of the engine, with Netcode's model over TCP and UGS Lobby + Relay stood in by
# a shared session directory (COSMIC_SHORE_NET_DIR). See README.md for what a pass here does and
# does not prove - it is evidence, not a substitute for the owner's MPPM run (Block 2).
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
WT="$WORK/worktree"; PLAYER="$WORK/player"; LOGS="$WORK/logs"; NET="$WORK/net"; HOMES="$WORK/home"
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
rm -rf "$PLAYER" "$LOGS" "$NET" "$HOMES"; mkdir -p "$LOGS" "$NET" "$HOMES"
if ! dotnet build "$WT/Port/src/CosmicShore.Player" -c Debug -v q -nologo -o "$PLAYER" > "$WORK/build.log" 2>&1; then
  grep -oE '[^ /]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: [^[]*' "$WORK/build.log" | sort -u | head -40
  echo "prisma_party_scenarios: BUILD FAILED - full log: $WORK/build.log" >&2
  exit 1
fi

# PRISMA_RELAY=1: every pilot hosts and joins through Froglet's relay server (Unity Relay's protocol,
# Port/docs/MULTIPLAYER.md §6.7) instead of connecting directly - the path a game over the internet takes.
# PRISMA_RELAY=ugs: the same relay, reached the way UGS Relay is (COSMIC_SHORE_RELAY=ugs, §6.8): each pilot
# signs in to a local stand-in of UGS Player Authentication (ugs_auth_standin.py) and allocates with its
# token. Nothing here talks to the live UGS project.
if [ "${PRISMA_RELAY:-0}" != "0" ]; then
  nohup dotnet "$PLAYER/CosmicShore.dll" --relay-server 0 0 > "$LOGS/relay.log" 2>&1 < /dev/null &
  PIDS+=($!)
  RELAY_URL=""
  for _ in $(seq 1 100); do
    RELAY_URL="$(grep -o 'COSMIC_SHORE_RELAY=http[^)]*' "$LOGS/relay.log" 2>/dev/null | head -1 | cut -d= -f2 || true)"
    [ -n "$RELAY_URL" ] && break; sleep 0.2
  done
  if [ -z "$RELAY_URL" ]; then echo "prisma_party_scenarios: the relay server did not start:" >&2; cat "$LOGS/relay.log" >&2; exit 1; fi
  if [ "$PRISMA_RELAY" = "ugs" ]; then
    UGS_PROJECT="00000000-0000-0000-0000-00000000f106"
    python3 -I "$HERE/ugs_auth_standin.py" 0 "$UGS_PROJECT" > "$LOGS/ugs-auth.log" 2>&1 < /dev/null &
    PIDS+=($!)
    AUTH_URL=""
    for _ in $(seq 1 100); do
      AUTH_URL="$(grep -o 'COSMIC_SHORE_UGS_AUTH_URL=[^)]*' "$LOGS/ugs-auth.log" 2>/dev/null | head -1 | cut -d= -f2 || true)"
      [ -n "$AUTH_URL" ] && break; sleep 0.2
    done
    if [ -z "$AUTH_URL" ]; then echo "prisma_party_scenarios: the UGS sign-in stand-in did not start:" >&2; cat "$LOGS/ugs-auth.log" >&2; exit 1; fi
    export COSMIC_SHORE_RELAY=ugs COSMIC_SHORE_UGS_PROJECT="$UGS_PROJECT" COSMIC_SHORE_UGS_AUTH_URL="$AUTH_URL" COSMIC_SHORE_UGS_RELAY_URL="$RELAY_URL"
    echo "prisma_party_scenarios: every pilot signs in to the UGS stand-in at $AUTH_URL and goes through the relay at $RELAY_URL"
  else
    export COSMIC_SHORE_RELAY="$RELAY_URL"
    echo "prisma_party_scenarios: every pilot goes through Froglet's relay at $RELAY_URL (log: $LOGS/relay.log)"
  fi
fi

# One profile per pilot, each with its own HOME so no instance inherits another run's saved
# username, consent or party; one shared session directory standing in for UGS Lobby + Relay.
cd "$WORK"   # nothing an instance writes relative to its cwd may land in the repo
SPEC="$WORK/instances.json"
echo "{" > "$SPEC"
for k in "${!LABELS[@]}"; do
  L="${LABELS[$k]}"; PORT=$((BASE_PORT + k + 1))
  # The engine falls back to ~/.local/share when .NET reports no LocalApplicationData
  # (LocalDataPath.cs); creating the folder keeps each pilot's saves under its own HOME either way.
  mkdir -p "$HOMES/$L/.local/share"
  HOME="$HOMES/$L" XDG_DATA_HOME="$HOMES/$L/.local/share" COSMIC_SHORE_PROJECT="$WT" COSMIC_SHORE_PROFILE="party$L" COSMIC_SHORE_NET_DIR="$NET" \
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
exit $RC
