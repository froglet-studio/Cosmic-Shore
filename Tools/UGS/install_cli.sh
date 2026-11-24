#!/usr/bin/env bash
# Install the pinned UGS CLI (Linux x64) into a folder and check its hash before it ever runs.
#
#   bash Tools/UGS/install_cli.sh "$RUNNER_TEMP/ugs-cli"     # then put that folder on PATH
#
# Used by .github/workflows/ugs-deploy.yml. It downloads the release binary straight from Unity's GitHub release
# instead of `npm install -g ugs`: the npm package is a downloader that fetches whatever binary the release holds,
# asks the rate-limited GitHub API first, sends install analytics, and checks no hash.
#
# To move to a newer CLI: pick an exact version (`npm view ugs versions`), download its ugs-linux-x64 from
# https://github.com/Unity-Technologies/unity-gaming-services-cli/releases, run `sha256sum` on it, change both values
# below in one commit, and read `ugs deploy --help` of that version (deploy.sh relies on --dry-run, -p and -e).
set -euo pipefail
UGS_CLI_VERSION=2.0.0
UGS_CLI_SHA256=f1c04e4e71105a3ed3ded94c6ed94dbba527316e5b6c2670e3463bcdfe7b8639
DEST="${1:?usage: bash Tools/UGS/install_cli.sh <folder>}"

mkdir -p "$DEST"
url="https://github.com/Unity-Technologies/unity-gaming-services-cli/releases/download/v${UGS_CLI_VERSION}/ugs-linux-x64"
curl -fsSL --retry 4 --retry-delay 2 -o "$DEST/ugs.download" "$url"
echo "${UGS_CLI_SHA256}  $DEST/ugs.download" | sha256sum -c --quiet - || {
  echo "The UGS CLI download does not match the pinned sha256; refusing to run it." >&2
  rm -f "$DEST/ugs.download"
  exit 1
}
mv "$DEST/ugs.download" "$DEST/ugs"
chmod +x "$DEST/ugs"
UGS_CLI_TELEMETRY_DISABLED=1 "$DEST/ugs" --version
