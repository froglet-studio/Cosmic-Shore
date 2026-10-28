#!/usr/bin/env bash
# Froglet Compute on an always-on Ubuntu VM (made for Oracle Cloud's Always Free Ampere A1; any Ubuntu 22.04/24.04
# works): the worker running all the time, and the live Compute Console (the hub) behind HTTPS.
# Docs/Compute/README.md section 15 has the Oracle steps before this one.
#
# On the VM, over SSH, as the default user (ubuntu):
#   export GITHUB_TOKEN=github_pat_...          a fine-grained token for froglet-studio/Cosmic-Shore
#                                               (Contents: read and write; Pull requests: read)
#   curl -fsSL -H "Authorization: Bearer $GITHUB_TOKEN" \
#     https://raw.githubusercontent.com/froglet-studio/Cosmic-Shore/claude/eager-franklin-e6g77h/Tools/Compute/vm/setup-vm.sh | bash
#
# Optional, before running it:
#   export COMPUTE_DOMAIN=compute.example.com   your own domain (its A record pointing at this VM); default:
#                                               <public-ip>.sslip.io, which needs nothing
#   export COMPUTE_BRANCH=bleeding-edge         once Froglet Compute is merged there
# Running it again updates everything and keeps the team key.
set -euo pipefail

BRANCH="${COMPUTE_BRANCH:-claude/eager-franklin-e6g77h}"
SVC=froglet
BASE=/srv/froglet
REPO="$BASE/Cosmic-Shore"
STATE="$BASE/.froglet-compute"
PORT=8080

say() { printf '\n\033[1;36m== %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31mstopped: %s\033[0m\n' "$*" >&2; exit 1; }

[ -n "${GITHUB_TOKEN:-}" ] || die "export GITHUB_TOKEN first (see the top of this script)"
command -v sudo >/dev/null || die "sudo is needed"

say "1/7 packages: git, python3, numpy, caddy"
export DEBIAN_FRONTEND=noninteractive
sudo add-apt-repository -y universe >/dev/null 2>&1 || true
sudo apt-get update -qq
sudo apt-get install -y -qq git python3 python3-numpy caddy curl ca-certificates >/dev/null

say "2/7 firewall: ports 80 and 443 (Oracle's Ubuntu images reject them by default)"
for p in 80 443; do
  sudo iptables -C INPUT -p tcp --dport "$p" -m state --state NEW -j ACCEPT 2>/dev/null \
    || sudo iptables -I INPUT 1 -p tcp --dport "$p" -m state --state NEW -j ACCEPT
done
if command -v netfilter-persistent >/dev/null; then sudo netfilter-persistent save >/dev/null 2>&1 || true; fi

say "3/7 a service user and the repository (only what compute needs; files over 1 MB stay on GitHub)"
id "$SVC" >/dev/null 2>&1 || sudo useradd --system --create-home --home-dir "$BASE" --shell /usr/sbin/nologin "$SVC"
sudo mkdir -p "$STATE" && sudo chown -R "$SVC:$SVC" "$BASE" && sudo chmod 700 "$BASE"
AUTH_URL="https://x-access-token:${GITHUB_TOKEN}@github.com/froglet-studio/Cosmic-Shore.git"
as() { sudo -u "$SVC" -H "$@"; }
if [ ! -d "$REPO/.git" ]; then
  as git clone --quiet --filter=blob:limit=1m --no-checkout --branch "$BRANCH" "$AUTH_URL" "$REPO"
  as git -C "$REPO" sparse-checkout set --no-cone /Tools/Compute/ /Tools/Build/ /Docs/Compute/ /Docs/AI_SYSTEM/ /Docs/TEAM.json
  as git -C "$REPO" checkout --quiet "$BRANCH"
else
  as git -C "$REPO" remote set-url origin "$AUTH_URL"
  as git -C "$REPO" fetch --quiet origin "$BRANCH"
  as git -C "$REPO" checkout --quiet -B "$BRANCH" "origin/$BRANCH"
fi
as git -C "$REPO" config user.name "froglet-compute (vm)"
as git -C "$REPO" config user.email "compute@froglet.invalid"
printf '%s' "$GITHUB_TOKEN" | as tee "$STATE/github-token" >/dev/null && sudo chmod 600 "$STATE/github-token"

say "4/7 the address and the team key"
IP="$(curl -fsS https://api.ipify.org || curl -fsS https://ifconfig.me)"
DOMAIN="${COMPUTE_DOMAIN:-${IP//./-}.sslip.io}"
KEY="$(as python3 - "$STATE/hub.json" <<'PY'
import json, os, secrets, sys
p = sys.argv[1]
try:
    cfg = json.load(open(p))
except Exception:
    cfg = {}
cfg.setdefault("key", secrets.token_urlsafe(18))
json.dump(cfg, open(p, "w"), indent=1)
os.chmod(p, 0o600)
print(cfg["key"])
PY
)"
as python3 - "$STATE/config.json" "$REPO" "$KEY" "$PORT" "$BRANCH" <<'PY'
import json, sys
p, repo, key, port, branch = sys.argv[1:6]
json.dump({"repo": repo, "hub": f"http://127.0.0.1:{port}", "hub_key": key, "ref": branch,
           "queue": ["bleeding-edge", "ai", "compute-queue", branch]}, open(p, "w"), indent=1)
PY

say "5/7 services: the hub (the live console) and the worker (all the time)"
QUEUE="bleeding-edge ai compute-queue $BRANCH"
ENVS="Environment=FROGLET_COMPUTE_HOME=$STATE
Environment=FROGLET_COMPUTE_USER=vm
Environment=FROGLET_COMPUTE_HOST=oracle"
sudo tee /etc/systemd/system/froglet-hub.service >/dev/null <<UNIT
[Unit]
Description=Froglet Compute hub (the live console)
After=network-online.target
Wants=network-online.target

[Service]
User=$SVC
WorkingDirectory=$REPO
$ENVS
ExecStartPre=-/usr/bin/git -C $REPO pull --ff-only --quiet
ExecStart=/usr/bin/python3 $REPO/Tools/Compute/compute.py hub --port $PORT --url https://$DOMAIN --queue $QUEUE --github-token-file $STATE/github-token --quiet
Restart=always
RestartSec=10

[Install]
WantedBy=multi-user.target
UNIT
sudo tee /etc/systemd/system/froglet-compute.service >/dev/null <<UNIT
[Unit]
Description=Froglet Compute worker (a dedicated machine: works all the time)
After=network-online.target froglet-hub.service
Wants=network-online.target

[Service]
User=$SVC
WorkingDirectory=$REPO
$ENVS
ExecStart=/usr/bin/python3 $REPO/Tools/Compute/compute.py run --always --quiet --repo $REPO --home origin/$BRANCH --progress-minutes 2 --queue $QUEUE
Nice=5
Restart=always
RestartSec=30

[Install]
WantedBy=multi-user.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable --now froglet-hub.service froglet-compute.service >/dev/null
sudo systemctl restart froglet-hub.service froglet-compute.service

say "6/7 HTTPS: Caddy in front of the hub at https://$DOMAIN"
sudo tee /etc/caddy/Caddyfile >/dev/null <<CADDY
$DOMAIN {
	encode gzip
	reverse_proxy 127.0.0.1:$PORT
}
CADDY
sudo systemctl enable --now caddy >/dev/null
sudo systemctl reload caddy || sudo systemctl restart caddy

say "7/7 checking"
for i in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:$PORT/api/status" | grep -q '"built": [0-9]'; then break; fi
  sleep 5
done
curl -fsS "http://127.0.0.1:$PORT/api/status" || echo "(the first build is still running: give it a minute)"
echo
systemctl --no-pager --lines=0 status froglet-hub froglet-compute caddy | grep -E "●|Active:" || true

cat <<DONE

Froglet Compute is running on this VM.

  The live console:   https://$DOMAIN
  The team key:       $KEY
      (the console asks for it once on each phone or browser; share it only with the team)

  Put a team computer on the live console, from its Cosmic-Shore folder:
    git fetch origin $BRANCH && git show origin/$BRANCH:Tools/Compute/setup.py | python3 - --hub https://$DOMAIN --hub-key $KEY
  (Windows PowerShell: ; instead of &&, and python instead of python3)

  Logs:     journalctl -u froglet-hub -u froglet-compute -f
  Update:   run this script again (it keeps the key)
DONE
