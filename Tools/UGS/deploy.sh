#!/usr/bin/env bash
# Upload the game's Cloud Code scripts to one UGS environment (Tools/UGS/README.md, "Deploying the scripts").
#
#   bash Tools/UGS/deploy.sh development     # always first
#   bash Tools/UGS/deploy.sh production
#
# Safe to run before any build turns a switch on: a deployed script does nothing until a game calls it, and every
# switch (ServerEconomyGate, ServerScoreGate, MatchReportGate, FairPlayTrustGate, ServerEntitlementGate) ships off.
# It does NOT apply the access policies: project-policy.json stops players posting to leaderboards directly, which
# breaks every build that still posts directly, so it is applied only once a build with ServerScoreGate on is out.
# Needs the UGS CLI signed in to the project (ugs login, or UGS_CLI_SERVICE_KEY_ID / UGS_CLI_SERVICE_SECRET_KEY).
set -euo pipefail
ENV="${1:-}"
case "$ENV" in development|production) ;; *) echo "usage: bash Tools/UGS/deploy.sh development|production" >&2; exit 2;; esac
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
PROJECT=3030fd69-28ab-433f-b4bd-22b9b93c5118
SCRIPTS=(Tools/UGS/CloudCode/economy.js Tools/UGS/CloudCode/weekly-score.js Tools/UGS/CloudCode/match-report.js
         Tools/UGS/CloudCode/trust.js Tools/UGS/Commerce/entitlements.js)

command -v ugs >/dev/null || { echo "The UGS CLI is not installed: npm install -g ugs" >&2; exit 2; }

echo "== tests"
for s in "${SCRIPTS[@]}"; do node "${s%.js}.test.cjs" | tail -1; done
python3 Tools/Build/author_economy_catalog.py --check

echo "== deploy to $ENV"
ugs deploy "${SCRIPTS[@]}" --project-id "$PROJECT" --environment-name "$ENV"
echo "Deployed. Not applied (on purpose): Tools/UGS/AccessControl/*.json; see Tools/UGS/README.md for when."
