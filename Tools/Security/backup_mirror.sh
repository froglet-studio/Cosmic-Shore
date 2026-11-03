#!/usr/bin/env bash
# Off-GitHub backup of froglet-studio/cosmic-shore: every branch, tag and Git LFS object.
# Usage: bash Tools/Security/backup_mirror.sh <backup-dir> [<second-remote-url>]
#   Makes/updates a bare mirror in <backup-dir>, fetches all LFS objects, writes a dated tarball,
#   and (optionally) pushes the mirror to a second host (GitLab / Bitbucket / Azure DevOps).
# Run from a trusted machine; schedule it (cron / Task Scheduler) nightly.
set -euo pipefail
DIR="${1:?backup dir required}"; SECOND="${2:-}"
SRC="https://github.com/froglet-studio/cosmic-shore.git"
mkdir -p "$DIR"; cd "$DIR"
if [ -d cosmic-shore.git ]; then git -C cosmic-shore.git remote update --prune
else git clone --mirror "$SRC" cosmic-shore.git; fi
git -C cosmic-shore.git lfs fetch --all origin
STAMP=$(date +%Y-%m-%d)
tar -czf "cosmic-shore-$STAMP.tar.gz" cosmic-shore.git
ls -1t cosmic-shore-*.tar.gz | tail -n +15 | xargs -r rm --   # keep the newest 14
if [ -n "$SECOND" ]; then
  git -C cosmic-shore.git push --mirror "$SECOND"
  git -C cosmic-shore.git lfs push --all "$SECOND"
fi
echo "backup ok: $DIR/cosmic-shore-$STAMP.tar.gz"
