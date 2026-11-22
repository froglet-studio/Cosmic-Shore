#!/usr/bin/env bash
# Froglet Analytics on the Oracle VM (the same machine as Froglet Compute, as its own service and its own user).
# Run on the VM, as a user with sudo:
#
#   export GITHUB_TOKEN=...                      a read-only token for froglet-studio/Cosmic-Shore. Needed the first time;
#                                                saved to <base>/github-token (600) for the nightly self-update, so a
#                                                re-run needs nothing.
#   export ANALYTICS_DOMAIN=analytics.froglet.studio   optional; its A record pointing at this VM.
#                                                Default: analytics.<public-ip-with-dashes>.sslip.io for the first
#                                                instance, <game>-analytics.<public-ip-with-dashes>.sslip.io for another
#   export ANALYTICS_BRANCH=bleeding-edge        optional; the branch the server runs from (a re-run keeps the saved
#                                                one). Once that branch merges, the nightly self-update moves to
#                                                bleeding-edge by itself.
#   export ANALYTICS_ORIGINS="https://froglet-studio.github.io"   optional; dashboard origins allowed to read (CORS)
#   export ANALYTICS_INGEST_ORIGINS="https://tumult.froglet.games"   optional; web pages allowed to SEND events
#                                                (CORS on the ingest routes only). Default: the server's own list.
#   export ANALYTICS_PROFILE_READERS="ian garrett caleb"   optional; each gets a read key (printed once, when made)
#                                                and may open player profiles. Anyone already listed keeps their key.
#   export ANTHROPIC_API_KEY=...                 optional; turns on Claude's overnight read and the Ask box.
#                                                Saved to /etc/<service>.env (root, 600), never to the repo.
#   bash setup-analytics-vm.sh
#
# A second instance, for another game (Tumult, 2026-11-20): the same server code, its own service, user, port, data,
# keys, address and nightly jobs, and the event list and public ingest key from that game's own repository:
#
#   export ANALYTICS_INSTANCE=tumult             the game's short name: service froglet-analytics-tumult, data in
#                                                /var/lib/froglet-analytics-tumult. Unset: the first instance
#                                                (froglet-analytics, Cosmic Shore), exactly as before.
#   export ANALYTICS_PORT=8788                   the local port behind Caddy; one per instance (default 8787)
#   export ANALYTICS_SCHEMA_REPO=froglet-studio/TumultTTG   the public repository holding the game's event list
#   export ANALYTICS_SCHEMA_BRANCH=bleeding-edge optional; the branch read for it (default bleeding-edge)
#   export ANALYTICS_SCHEMA_PATH=docs/analytics/EVENT_SCHEMA.json      the schema's path in that repository
#   export ANALYTICS_KEY_PATH=docs/analytics/ingest_key.public         the game's public ingest key there
#   bash setup-analytics-vm.sh
#
# The nightly self-update refreshes both checkouts: the server's (Cosmic-Shore) and the game's schema.
#
# Safe to run again: it updates the checkout, keeps the data and the keys, and restarts the service.
set -euo pipefail
say() { printf '\n== %s\n' "$*"; }
INSTANCE="${ANALYTICS_INSTANCE:-}"
if [ -n "$INSTANCE" ] && ! [[ "$INSTANCE" =~ ^[a-z][a-z0-9-]{0,30}$ ]]; then
  echo "ANALYTICS_INSTANCE must be a short lower-case name (letters, digits, dashes): got '$INSTANCE'" >&2
  exit 2
fi
SVC=froglet-analytics${INSTANCE:+-$INSTANCE}
BASE=/var/lib/$SVC
REPO="$BASE/repo"
SCHEMA_REPO_DIR="$BASE/schema-repo"
GITHUB_TOKEN="${GITHUB_TOKEN:-$(sudo cat "$BASE/github-token" 2>/dev/null || true)}"
BRANCH="${ANALYTICS_BRANCH:-$(sudo cat "$BASE/branch" 2>/dev/null || echo bleeding-edge)}"
PORT="${ANALYTICS_PORT:-$(sudo cat "$BASE/port" 2>/dev/null || echo 8787)}"
# The game's own schema and key, when they live in another repository (saved, so a re-run needs nothing).
SAVED_SOURCE="$(sudo cat "$BASE/schema-source" 2>/dev/null || true)"
read -r SAVED_REPO SAVED_SCHEMA_BRANCH SAVED_SCHEMA_PATH SAVED_KEY_PATH <<<"${SAVED_SOURCE:-- - - -}" || true
[ "$SAVED_REPO" = "-" ] && SAVED_REPO=""
SCHEMA_REPO="${ANALYTICS_SCHEMA_REPO:-$SAVED_REPO}"
SCHEMA_BRANCH="${ANALYTICS_SCHEMA_BRANCH:-${SAVED_SCHEMA_BRANCH/#-/bleeding-edge}}"
SCHEMA_PATH="${ANALYTICS_SCHEMA_PATH:-${SAVED_SCHEMA_PATH/#-/docs/analytics/EVENT_SCHEMA.json}}"
KEY_PATH="${ANALYTICS_KEY_PATH:-${SAVED_KEY_PATH/#-/docs/analytics/ingest_key.public}}"
if [ -n "$INSTANCE" ] && [ -z "$SCHEMA_REPO" ]; then
  echo "A second instance reads its game's event list from that game's repository: set ANALYTICS_SCHEMA_REPO" >&2
  exit 2
fi
if ! [[ "$PORT" =~ ^[0-9]{4,5}$ ]]; then echo "ANALYTICS_PORT must be a port number: got '$PORT'" >&2; exit 2; fi
APT="sudo DEBIAN_FRONTEND=noninteractive apt-get"

say "1/8 packages: git, python3, python3-venv, caddy"
$APT update -qq >/dev/null
$APT install -y -qq git python3 python3-venv caddy curl ca-certificates sqlite3 >/dev/null

say "2/8 a service user ($SVC) and a sparse checkout (the server and the event schema only)"
id "$SVC" >/dev/null 2>&1 || sudo useradd --system --create-home --home-dir "$BASE" --shell /usr/sbin/nologin "$SVC"
sudo chown "$SVC:$SVC" "$BASE" && sudo chmod 700 "$BASE"
as() { sudo -u "$SVC" -H "$@"; }
AUTH_URL="https://github.com/froglet-studio/Cosmic-Shore.git"
[ -n "$GITHUB_TOKEN" ] && AUTH_URL="https://x-access-token:${GITHUB_TOKEN}@github.com/froglet-studio/Cosmic-Shore.git"
# $BASE is 700 and owned by the service user, so every test of a path inside it runs as that user.
if ! as test -d "$REPO/.git"; then
  as git clone --quiet --single-branch --filter=blob:limit=1m --no-checkout --branch "$BRANCH" "$AUTH_URL" "$REPO"
  as git -C "$REPO" sparse-checkout set --no-cone /Tools/Analytics/server/ /Tools/Analytics/console/ /Docs/Analytics/EVENT_SCHEMA.json
  as git -C "$REPO" checkout --quiet "$BRANCH"
else
  as git -C "$REPO" remote set-url origin "$AUTH_URL"
  as git -C "$REPO" sparse-checkout set --no-cone /Tools/Analytics/server/ /Tools/Analytics/console/ /Docs/Analytics/EVENT_SCHEMA.json
  as git -C "$REPO" fetch --quiet origin "+refs/heads/$BRANCH:refs/remotes/origin/$BRANCH"
  as git -C "$REPO" checkout --quiet -B "$BRANCH" "origin/$BRANCH"
fi
as git -C "$REPO" remote set-url origin "https://github.com/froglet-studio/Cosmic-Shore.git"
echo "$BRANCH" | sudo -u "$SVC" tee "$BASE/branch" >/dev/null
echo "$PORT" | sudo -u "$SVC" tee "$BASE/port" >/dev/null
if [ -n "$SCHEMA_REPO" ]; then
  # The game's repository is public: no token. Only the schema and the key are checked out.
  SCHEMA_URL="https://github.com/$SCHEMA_REPO.git"
  if ! as test -d "$SCHEMA_REPO_DIR/.git"; then
    as git clone --quiet --single-branch --filter=blob:limit=1m --no-checkout --branch "$SCHEMA_BRANCH" "$SCHEMA_URL" "$SCHEMA_REPO_DIR"
  else
    as git -C "$SCHEMA_REPO_DIR" remote set-url origin "$SCHEMA_URL"
    as git -C "$SCHEMA_REPO_DIR" fetch --quiet origin "+refs/heads/$SCHEMA_BRANCH:refs/remotes/origin/$SCHEMA_BRANCH"
  fi
  as git -C "$SCHEMA_REPO_DIR" sparse-checkout set --no-cone "/$SCHEMA_PATH" "/$KEY_PATH"
  as git -C "$SCHEMA_REPO_DIR" checkout --quiet -B "$SCHEMA_BRANCH" "origin/$SCHEMA_BRANCH" 2>/dev/null \
    || as git -C "$SCHEMA_REPO_DIR" checkout --quiet "$SCHEMA_BRANCH"
  as test -s "$SCHEMA_REPO_DIR/$SCHEMA_PATH" || { echo "no $SCHEMA_PATH on $SCHEMA_REPO@$SCHEMA_BRANCH" >&2; exit 3; }
  as test -s "$SCHEMA_REPO_DIR/$KEY_PATH" || { echo "no $KEY_PATH on $SCHEMA_REPO@$SCHEMA_BRANCH" >&2; exit 3; }
  echo "$SCHEMA_REPO $SCHEMA_BRANCH $SCHEMA_PATH $KEY_PATH" | sudo -u "$SVC" tee "$BASE/schema-source" >/dev/null
  SCHEMA_FILE="$SCHEMA_REPO_DIR/$SCHEMA_PATH"
  KEY_FILE="$SCHEMA_REPO_DIR/$KEY_PATH"
else
  SCHEMA_FILE="$REPO/Docs/Analytics/EVENT_SCHEMA.json"
  KEY_FILE=""
fi
if [ -n "$GITHUB_TOKEN" ]; then
  printf '%s' "$GITHUB_TOKEN" | sudo -u "$SVC" tee "$BASE/github-token" >/dev/null
  sudo chmod 600 "$BASE/github-token"
fi

say "3/8 the Claude SDK in its own environment (only the overnight read and the Ask box use it)"
as test -x "$BASE/venv/bin/python" || as python3 -m venv "$BASE/venv"
as "$BASE/venv/bin/pip" install --quiet --upgrade anthropic

say "4/8 the config and the keys (kept if they exist)"
IP="$(curl -fsS https://api.ipify.org || curl -fsS https://ifconfig.me)"
DOMAIN="${ANALYTICS_DOMAIN:-${INSTANCE:+$INSTANCE-}analytics.${IP//./-}.sslip.io}"
export FROGLET_ANALYTICS_CONFIG="$BASE/config.json"
as env FROGLET_ANALYTICS_CONFIG="$FROGLET_ANALYTICS_CONFIG" "$BASE/venv/bin/python" "$REPO/Tools/Analytics/server/analytics_server.py" init
as env FROGLET_ANALYTICS_CONFIG="$FROGLET_ANALYTICS_CONFIG" ORIGINS="${ANALYTICS_ORIGINS:-}" \
   INGEST_ORIGINS="${ANALYTICS_INGEST_ORIGINS:-}" READERS="${ANALYTICS_PROFILE_READERS:-}" \
   FA_BASE="$BASE" FA_PORT="$PORT" FA_SCHEMA="$SCHEMA_FILE" FA_KEY="$KEY_FILE" "$BASE/venv/bin/python" - <<'PY'
import json, os, secrets
path = os.environ["FROGLET_ANALYTICS_CONFIG"]
config = json.load(open(path))
config["data_dir"] = os.path.join(os.environ["FA_BASE"], "data")
config["schema_path"] = os.environ["FA_SCHEMA"]
config["port"] = int(os.environ["FA_PORT"])
config["public_key_file"] = os.environ["FA_KEY"]       # empty: Cosmic Shore's, beside the server
origins = [o for o in os.environ.get("ORIGINS", "").split() if o]
if origins:
    config["allowed_origins"] = origins
ingest_origins = [o for o in os.environ.get("INGEST_ORIGINS", "").split() if o]
if ingest_origins:
    config["ingest_origins"] = ingest_origins
made = []
for name in os.environ.get("READERS", "").lower().split():
    if name not in config["read_keys"]:
        config["read_keys"][name] = "fa_read_" + secrets.token_urlsafe(24)
        made.append(name)
    if name not in config.setdefault("profile_readers", []):
        config["profile_readers"].append(name)
json.dump(config, open(path, "w"), indent=2)
os.chmod(path, 0o600)
for name in made:
    print(f"read key for {name} (send it to them privately; shown once): {config['read_keys'][name]}")
print("profile readers:", ", ".join(config.get("profile_readers", [])) or "none")
PY
if [ -n "${ANTHROPIC_API_KEY:-}" ]; then
  printf 'ANTHROPIC_API_KEY=%s\n' "$ANTHROPIC_API_KEY" | sudo tee "/etc/$SVC.env" >/dev/null
fi
sudo touch "/etc/$SVC.env" && sudo chmod 600 "/etc/$SVC.env"

say "5/8 the service"
if [ "$(sudo ss -Hltn "sport = :$PORT" 2>/dev/null | wc -l)" -gt 0 ] && ! sudo systemctl is-active --quiet "$SVC"; then
  echo "port $PORT is taken by another service; choose another with ANALYTICS_PORT" >&2
  exit 2
fi
sudo tee "/etc/systemd/system/$SVC.service" >/dev/null <<UNIT
[Unit]
Description=Froglet Analytics $SVC (ingest, the console's read API, the overnight read)
After=network-online.target
Wants=network-online.target

[Service]
User=$SVC
WorkingDirectory=$REPO
Environment=FROGLET_ANALYTICS_CONFIG=$BASE/config.json
EnvironmentFile=-/etc/$SVC.env
ExecStart=$BASE/venv/bin/python $REPO/Tools/Analytics/server/analytics_server.py serve
Restart=always
RestartSec=10
NoNewPrivileges=true
ProtectSystem=strict
ReadWritePaths=$BASE
PrivateTmp=true

[Install]
WantedBy=multi-user.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable --now "$SVC.service" >/dev/null
sudo systemctl restart "$SVC.service"

say "6/8 HTTPS: Caddy at https://$DOMAIN (its own file, beside the Compute console)"
sudo mkdir -p /etc/caddy/conf.d
sudo tee "/etc/caddy/conf.d/$SVC.caddy" >/dev/null <<CADDY
$DOMAIN {
	encode gzip
	request_body {
		max_size 4MB
	}
	reverse_proxy 127.0.0.1:$PORT
}
CADDY
grep -q 'import /etc/caddy/conf.d/\*.caddy' /etc/caddy/Caddyfile 2>/dev/null \
  || printf '\nimport /etc/caddy/conf.d/*.caddy\n' | sudo tee -a /etc/caddy/Caddyfile >/dev/null
sudo systemctl enable --now caddy >/dev/null
sudo systemctl reload caddy || sudo systemctl restart caddy

say "7/8 the nightly self-update (04:20 UTC; follows bleeding-edge once this branch merges)"
# One copy of the script for every instance; each instance's unit passes its own name.
sudo install -m 755 "$REPO/Tools/Analytics/server/self-update.sh" /usr/local/bin/froglet-analytics-update
sudo tee "/etc/systemd/system/$SVC-update.service" >/dev/null <<UNIT
[Unit]
Description=Froglet Analytics $SVC nightly self-update
After=network-online.target
Wants=network-online.target

[Service]
Type=oneshot
ExecStart=/usr/local/bin/froglet-analytics-update $SVC
UNIT
sudo tee "/etc/systemd/system/$SVC-update.timer" >/dev/null <<UNIT
[Unit]
Description=Froglet Analytics $SVC nightly self-update

[Timer]
OnCalendar=*-*-* 04:20:00 UTC
RandomizedDelaySec=10m
Persistent=true

[Install]
WantedBy=timers.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable --now "$SVC-update.timer" >/dev/null

say "8/8 the nightly backup (04:40 UTC, after the self-update and before Oracle's 05:00 boot-volume backup)"
# A consistent, gzipped snapshot of events.db and aggregates.db in $BASE/data/backups/ (14 kept). Off the VM, the
# Oracle boot-volume backup policy (daily 05:00 UTC, 5 kept, set in the Oracle console) copies the disk with that
# night's snapshot on it. A bucket upload runs only if FA_BACKUP_* are in /etc/$SVC.env (none today).
sudo tee "/etc/systemd/system/$SVC-backup.service" >/dev/null <<UNIT
[Unit]
Description=Froglet Analytics $SVC nightly backup (a gzipped snapshot of both databases)

[Service]
Type=oneshot
User=$SVC
WorkingDirectory=$REPO
Environment=FROGLET_ANALYTICS_CONFIG=$BASE/config.json
EnvironmentFile=-/etc/$SVC.env
ExecStart=$BASE/venv/bin/python $REPO/Tools/Analytics/server/analytics_server.py backup
NoNewPrivileges=true
ProtectSystem=strict
ReadWritePaths=$BASE
PrivateTmp=true
UNIT
sudo tee "/etc/systemd/system/$SVC-backup.timer" >/dev/null <<UNIT
[Unit]
Description=Froglet Analytics $SVC nightly backup, 04:40 UTC

[Timer]
OnCalendar=*-*-* 04:40:00 UTC
Persistent=true

[Install]
WantedBy=timers.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable --now "$SVC-backup.timer" >/dev/null

sleep 3
curl -fsS "http://127.0.0.1:$PORT/healthz" >/dev/null && echo "server: up on 127.0.0.1:$PORT" || echo "server: NOT answering; journalctl -u $SVC"
echo "address: https://$DOMAIN   (the console page, the dashboard API, and the game's analytics host)"
echo "schema:  $SCHEMA_FILE"
echo "branch:  $BRANCH   (nightly self-update: systemctl list-timers $SVC-update; log: journalctl -u $SVC-update)"
echo "backups: $BASE/data/backups (04:40 UTC: systemctl list-timers $SVC-backup; now: sudo systemctl start $SVC-backup)"
echo "keys:    sudo cat $BASE/config.json   (a reader's key: sudo -u $SVC env FROGLET_ANALYTICS_CONFIG=$BASE/config.json $BASE/venv/bin/python $REPO/Tools/Analytics/server/analytics_server.py key read ian)"
