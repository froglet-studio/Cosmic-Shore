#!/usr/bin/env bash
# port-fmod-from-cs2.sh
# Cherry-picks the 4 FMOD-related commits from Cosmic-Shore2 (branch
# app-shell-polish-v2) onto this repo's current branch (expected:
# bleeding-edge).
#
# Run from the root of Cosmic-Shore3.
#
# Commits (oldest -> newest):
#   605011e64  FMOD engine sounds added to squirell    (adds Assets/Plugins/FMOD/)
#   c890737f8  Element sound system added              (relocates ShipAudioController)
#   f24938021  Added infrastructure for skim sfx
#   b9ea39734  analog drift sounds added

set -euo pipefail

CS2_PATH="${CS2_PATH:-/Users/charlesgreenberg/Documents/GitHub/Cosmic-Shore2}"
CS3_PATH="$(pwd)"
EXPECTED_BRANCH="bleeding-edge"
REMOTE_NAME="cs2-local"
COMMITS=(605011e64 c890737f8 f24938021 b9ea39734)

say() { printf '\n=== %s ===\n' "$*"; }

say "1/6  Sanity checks"
[[ -d .git ]] || { echo "Not a git repo: $CS3_PATH"; exit 1; }
[[ -d "$CS2_PATH/.git" ]] || { echo "CS2 not found at $CS2_PATH (override with CS2_PATH=...)"; exit 1; }
CUR=$(git rev-parse --abbrev-ref HEAD)
[[ "$CUR" == "$EXPECTED_BRANCH" ]] || { echo "On branch '$CUR', expected '$EXPECTED_BRANCH'"; exit 1; }
echo "  CS3 = $CS3_PATH (branch: $CUR)"
echo "  CS2 = $CS2_PATH"

say "2/6  Clean working tree (discarding pre-existing dirty state)"
# Revert tracked-file modifications (Firebase binaries etc.)
git checkout -- .
# Remove sandbox-leftover empty file if present
rm -f testfile_in_repo
# Drop untracked + ignored cruft we know about, plus anything else
# in the working tree that isn't committed.
rm -rf "Assets/Plugins/FMOD" "Assets/Plugins/FMOD.meta" \
       fmod_editor.log \
       "Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs" \
       "Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs.meta"
# Belt-and-suspenders: nuke anything else untracked (won't touch ignored/.gitignored
# directories tracked but reverted above)
git clean -fd
git status --short
echo "  -> working tree clean"

say "3/6  Wire CS2 as a local remote and fetch"
if git remote get-url "$REMOTE_NAME" >/dev/null 2>&1; then
  git remote set-url "$REMOTE_NAME" "$CS2_PATH"
else
  git remote add "$REMOTE_NAME" "$CS2_PATH"
fi
git fetch "$REMOTE_NAME"
# Verify we can resolve every commit
for sha in "${COMMITS[@]}"; do
  if ! git cat-file -e "$sha^{commit}" 2>/dev/null; then
    echo "  Commit $sha not reachable after fetch — aborting"
    exit 1
  fi
  echo "  OK $sha  $(git log -1 --format='%h %s' "$sha")"
done

say "4/6  Cherry-pick (oldest -> newest)"
for sha in "${COMMITS[@]}"; do
  echo
  echo "--- cherry-picking $sha ---"
  if ! git cherry-pick -x "$sha"; then
    cat <<EOF

Cherry-pick of $sha hit a conflict.
Resolve in another shell:
  git status
  # edit conflicted files, then:
  git add -A
  git cherry-pick --continue
  # or to abort everything:
  git cherry-pick --abort

Then re-run this script (it'll skip already-applied commits).
EOF
    exit 2
  fi
done

say "5/6  Verify"
git --no-pager log --oneline -n 6
echo
git status

say "6/6  Optional cleanup"
echo "If you want to drop the local remote when done:"
echo "  git remote remove $REMOTE_NAME"
echo
echo "Done."
