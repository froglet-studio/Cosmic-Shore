#!/usr/bin/env bash
# Upload the game's Cloud Code scripts to one UGS environment (Tools/UGS/README.md, "Deploying the scripts").
#
#   bash Tools/UGS/deploy.sh check                    # the gate only: every script's tests and the price check
#   bash Tools/UGS/deploy.sh development --dry-run    # the gate, then what the CLI WOULD change there (uploads nothing)
#   bash Tools/UGS/deploy.sh development              # always first
#   bash Tools/UGS/deploy.sh production
#
# The usual way is the button (.github/workflows/ugs-deploy.yml), which runs this same file in the commit it deploys,
# development before production, production behind an approval, and records each deploy on the launch-status branch.
# Keep this interface (check | <env> [--dry-run]): the workflow calls it in whatever commit it deploys, so a rollback to
# an older commit runs that commit's copy.
#
# Safe to run before any build turns a switch on: a deployed script does nothing until a game calls it, and every
# switch (ServerEconomyGate, ServerScoreGate, MatchReportGate, FairPlayTrustGate, ServerEntitlementGate) ships off.
# It does NOT apply the access policies: project-policy.json stops players posting to leaderboards directly, which
# breaks every build that still posts directly, so it is applied only once a build with ServerScoreGate on is out.
# Needs the UGS CLI signed in to the project (ugs login, or UGS_CLI_SERVICE_KEY_ID / UGS_CLI_SERVICE_SECRET_KEY),
# except for `check`. Never add `set -x` here: the CLI reads the key from the environment.
set -euo pipefail
ENV="${1:-}"
DRY="${2:-}"
usage() { echo "usage: bash Tools/UGS/deploy.sh check | development|production [--dry-run]" >&2; exit 2; }
case "$ENV" in check|development|production) ;; *) usage;; esac
case "$DRY" in ""|--dry-run) ;; *) usage;; esac
[ "$ENV" = check ] && [ -n "$DRY" ] && usage
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"
PROJECT=3030fd69-28ab-433f-b4bd-22b9b93c5118
# The one list of what is deployed. Tools/UGS/ugs_manifest.py reads it from here (the plan's diff and the records'
# hashes), so a script added here is planned, deployed and recorded without another edit.
SCRIPTS=(Tools/UGS/CloudCode/economy.js Tools/UGS/CloudCode/weekly-score.js Tools/UGS/CloudCode/match-report.js
         Tools/UGS/CloudCode/trust.js Tools/UGS/Commerce/entitlements.js)

echo "== tests"
for s in "${SCRIPTS[@]}"; do
  t="${s%.js}.test.cjs"
  [ -f "$t" ] || { echo "No test for $s (expected $t): every deployed script has one." >&2; exit 1; }
  node "$t" | tail -1
done
echo "== price check"
python3 Tools/Build/author_economy_catalog.py --check

if [ "$ENV" = check ]; then
  echo "Gate passed: the ${#SCRIPTS[@]} scripts' tests and the price check. Nothing was uploaded."
  exit 0
fi

command -v ugs >/dev/null || { echo "The UGS CLI is not installed: bash Tools/UGS/install_cli.sh (or npm install -g ugs@2.0.0)" >&2; exit 2; }

if [ -n "$DRY" ]; then
  echo "== dry run against $ENV (uploads nothing)"
  ugs deploy "${SCRIPTS[@]}" --project-id "$PROJECT" --environment-name "$ENV" --dry-run
  exit 0
fi

echo "== deploy to $ENV"
ugs deploy "${SCRIPTS[@]}" --project-id "$PROJECT" --environment-name "$ENV"
echo "Deployed. Not applied (on purpose): Tools/UGS/AccessControl/*.json; see Tools/UGS/README.md for when."
