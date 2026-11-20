#!/usr/bin/env bash
# Froglet Analytics nightly self-update (Shombith, 2026-11-06). Installed by setup-analytics-vm.sh as
# /usr/local/bin/froglet-analytics-update and run by the froglet-analytics-update.timer every night at 04:20 UTC (before
# the 05:00 nightly read), as root. Safe to run by hand at any time:  sudo froglet-analytics-update
#
#  1. Fetches the branch the server follows (/var/lib/froglet-analytics/branch, default bleeding-edge).
#  2. Once that branch has been merged into bleeding-edge (or deleted after its merge), it follows bleeding-edge from
#     then on, by itself.
#  3. If there is newer code, it checks it out and runs the server's own tests on it. Pass: it restarts the service.
#     Fail: it goes back to the code that was running and leaves the service untouched; it tries again the next night.
#  4. It never touches the data, the keys or the config.
#  5. An instance for another game (setup-analytics-vm.sh with ANALYTICS_INSTANCE, e.g. Tumult) also reads that game's
#     event list and public key from the game's repository (<base>/schema-source). When either changed, it checks the
#     schema is readable and restarts the service; an unreadable one is not checked out.
#
# The instance is the first argument (its service name); none means the first instance, froglet-analytics.
set -uo pipefail
SVC="${1:-froglet-analytics}"
case "$SVC" in froglet-analytics | froglet-analytics-[a-z0-9-]*) ;; *) echo "unknown instance '$SVC'" >&2; exit 2 ;; esac
BASE="/var/lib/$SVC"
REPO="$BASE/repo"
SERVER="$REPO/Tools/Analytics/server"
log() { echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] $*"; }
as() { sudo -u "$SVC" -H "$@"; }

BRANCH="$(cat "$BASE/branch" 2>/dev/null || echo bleeding-edge)"
FROM_BRANCH="$BRANCH"
TOKEN="$(cat "$BASE/github-token" 2>/dev/null || true)"
URL="https://github.com/froglet-studio/Cosmic-Shore.git"
[ -n "$TOKEN" ] && URL="https://x-access-token:${TOKEN}@github.com/froglet-studio/Cosmic-Shore.git"
fetch() { as git -C "$REPO" fetch --quiet "$URL" "+refs/heads/$1:refs/remotes/origin/$1" 2>/dev/null; }

# The game's own event list (a second instance only): updated before the server code, so a restart below takes both.
SCHEMA_CHANGED=0
if [ -s "$BASE/schema-source" ] && [ -d "$BASE/schema-repo/.git" ]; then
  read -r S_REPO S_BRANCH S_PATH S_KEY < "$BASE/schema-source"
  S_DIR="$BASE/schema-repo"
  if as git -C "$S_DIR" fetch --quiet "https://github.com/$S_REPO.git" "+refs/heads/$S_BRANCH:refs/remotes/origin/$S_BRANCH" 2>/dev/null; then
    S_OLD="$(as git -C "$S_DIR" rev-parse HEAD)"
    S_NEW="$(as git -C "$S_DIR" rev-parse "origin/$S_BRANCH")"
    if [ "$S_OLD" != "$S_NEW" ]; then
      if as git -C "$S_DIR" show "$S_NEW:$S_PATH" | as python3 -c 'import json,sys; e=json.load(sys.stdin)["events"]; assert e and isinstance(e, dict)' 2>/dev/null \
         && [ -n "$(as git -C "$S_DIR" show "$S_NEW:$S_KEY" 2>/dev/null)" ]; then
        as git -C "$S_DIR" checkout --quiet -B "$S_BRANCH" "$S_NEW"
        SCHEMA_CHANGED=1
        log "schema from $S_REPO@$S_BRANCH ${S_OLD:0:9} -> ${S_NEW:0:9}"
      else
        log "schema on $S_REPO@$S_BRANCH ${S_NEW:0:9} is unreadable (or the key is missing); kept ${S_OLD:0:9}"
      fi
    fi
  else
    log "could not reach $S_REPO; the schema stays at $(as git -C "$S_DIR" rev-parse --short HEAD)"
  fi
fi

if ! fetch bleeding-edge; then
  log "could not reach GitHub; staying on $(as git -C "$REPO" rev-parse --short HEAD)"
  [ "$SCHEMA_CHANGED" = 1 ] && systemctl restart "$SVC" && log "service restarted for the new schema"
  exit 0
fi
if [ "$BRANCH" != "bleeding-edge" ]; then
  if ! fetch "$BRANCH" || as git -C "$REPO" merge-base --is-ancestor "origin/$BRANCH" origin/bleeding-edge; then
    log "$BRANCH is merged into bleeding-edge (or gone): following bleeding-edge from now on"
    BRANCH="bleeding-edge"
  fi
fi

OLD="$(as git -C "$REPO" rev-parse HEAD)"
NEW="$(as git -C "$REPO" rev-parse "origin/$BRANCH")"
if [ "$OLD" = "$NEW" ]; then
  [ "$BRANCH" != "$FROM_BRANCH" ] && echo "$BRANCH" > "$BASE/branch"
  log "up to date on $BRANCH at ${NEW:0:9}"
  [ "$SCHEMA_CHANGED" = 1 ] && systemctl restart "$SVC" && log "service restarted for the new schema"
  exit 0
fi

as git -C "$REPO" sparse-checkout set --no-cone /Tools/Analytics/server/ /Tools/Analytics/console/ /Docs/Analytics/EVENT_SCHEMA.json
as git -C "$REPO" checkout --quiet -B "$BRANCH" "$NEW"
if as env FROGLET_ANALYTICS_CONFIG="$BASE/config.json" "$BASE/venv/bin/python" "$SERVER/analytics_server.py" --self-test \
     > "$BASE/last-update-tests.log" 2>&1; then
  echo "$BRANCH" > "$BASE/branch"
  chown "$SVC:$SVC" "$BASE/branch"
  install -m 755 "$SERVER/self-update.sh" /usr/local/bin/froglet-analytics-update
  systemctl restart "$SVC"
  log "updated $BRANCH ${OLD:0:9} -> ${NEW:0:9}; tests passed; service restarted"
else
  as git -C "$REPO" checkout --quiet -B "$FROM_BRANCH" "$OLD"
  [ "$SCHEMA_CHANGED" = 1 ] && systemctl restart "$SVC" && log "service restarted for the new schema"
  log "tests FAILED on ${NEW:0:9} ($BRANCH); kept ${OLD:0:9}. Details: $BASE/last-update-tests.log"
  exit 1
fi
