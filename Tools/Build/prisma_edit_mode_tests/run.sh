#!/usr/bin/env bash
# Run the game's EDIT-MODE tests with no Unity Editor, on Prisma's engine (Port/).
#
#   bash Tools/Build/prisma_edit_mode_tests/run.sh                 # the suites in suites.txt
#   bash Tools/Build/prisma_edit_mode_tests/run.sh path/to/XTests.cs [more.cs ...]
#   bash Tools/Build/prisma_edit_mode_tests/run.sh --filter 'FullyQualifiedName~Party'
#   KEEP=1 bash Tools/Build/prisma_edit_mode_tests/run.sh          # keep the temp worktree + project
#
# WHY. unity_refcompile compiles against Unity's REFERENCE assemblies, whose method bodies are
# `throw null`: a test that builds a Vector3, calls Mathf, ScriptableObject.CreateInstance or
# Resources.Load cannot execute there. Prisma re-implements that runtime API in .NET, compiles the
# real Assets/_Scripts against it (Port/src/CosmicShore.Live), and reads the real .asset / .prefab
# YAML (Port/src/CosmicShore.Content). This runner compiles the shipped test files against that
# build, with the port's own source rewrite, and runs them under NUnit - so ScriptableObjects,
# SOAP lists, GameObjects and shipped assets behave as they do in the Editor.
#
# WHAT IT TOUCHES. Nothing committed. It makes a `git worktree` of HEAD, copies THIS checkout's
# Assets/_Scripts over it (so uncommitted edits are tested), fills the engine's API gaps in that
# worktree only (gapfill.py - Port/CLAUDE.md: the engine changes only in a port session), builds
# a test project from PortTests.csproj.in in the same temp folder, and removes the worktree.
#
# WHAT IT CANNOT TELL YOU (each seen 2026-10-08, none of them a game defect):
#   - Unity-Editor-only semantics: a test that LogAssert.Expects "Destroy may not be called from
#     edit mode" fails, because the engine lets Destroy run.
#   - Main-thread identity: MainThreadDispatcher.IsOnMainThread is false on NUnit's worker thread,
#     so code that guards on it (SceneTransitionManager.SetFadeImmediate) bails out.
#   - Anything that needs a second process, a socket, UGS or Relay: use MPPM for those.
# The shim's LogAssert follows Unity's rule: an Error/Assert/Exception log a test did not Expect
# FAILS that test, and an Expect'ed log that never arrives fails it too.
#
# NEEDS the .NET 10 SDK (Prisma's TargetFramework). If `dotnet` 10 is not on PATH:
#   curl -sSL -o /tmp/dotnet-install.sh https://dot.net/v1/dotnet-install.sh
#   bash /tmp/dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet10" --no-path
# and run with DOTNET10_ROOT=$HOME/.dotnet10 (the default).
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(git -C "$HERE" rev-parse --show-toplevel)"
DOTNET10_ROOT="${DOTNET10_ROOT:-$HOME/.dotnet10}"
if [ -x "$DOTNET10_ROOT/dotnet" ]; then export DOTNET_ROOT="$DOTNET10_ROOT" PATH="$DOTNET10_ROOT:$PATH"; fi
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "prisma_edit_mode_tests: no .NET 10 SDK found (see the header of this script)." >&2; exit 2
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

FILTER=""
FILES=()
while [ $# -gt 0 ]; do
  case "$1" in
    --filter) FILTER="$2"; shift 2 ;;
    *) FILES+=("$1"); shift ;;
  esac
done
if [ ${#FILES[@]} -eq 0 ]; then
  while IFS= read -r line; do
    [[ -z "$line" || "$line" == \#* ]] && continue
    FILES+=("$line")
  done < "$HERE/suites.txt"
fi

WORK="${PRISMA_TESTS_WORK:-${TMPDIR:-/tmp}/prisma_edit_mode_tests}"
WT="$WORK/worktree"
PROJ="$WORK/proj"
mkdir -p "$WORK"
cleanup() {
  if [ "${KEEP:-0}" != "1" ]; then
    git -C "$REPO" worktree remove --force "$WT" >/dev/null 2>&1 || true
  fi
}
trap cleanup EXIT

git -C "$REPO" worktree remove --force "$WT" >/dev/null 2>&1 || rm -rf "$WT"
git -C "$REPO" worktree prune
git -C "$REPO" worktree add --detach --quiet "$WT" HEAD
# Test what is in THIS checkout, committed or not.
rm -rf "$WT/Assets/_Scripts" && cp -a "$REPO/Assets/_Scripts" "$WT/Assets/_Scripts"
echo "prisma_edit_mode_tests: engine gap-fill (worktree only)"
python3 "$HERE/gapfill.py" "$WT"

rm -rf "$PROJ" && mkdir -p "$PROJ/src/mp" "$PROJ/shim"
cp "$HERE"/shim/*.cs "$PROJ/shim/"
for f in "${FILES[@]}"; do cp "$REPO/$f" "$PROJ/src/mp/"; done
LIVE_REL="$(python3 -c "import os,sys; print(os.path.relpath(sys.argv[1], sys.argv[2]))" "$PROJ/obj/live-tests" "$WT/Port/src/CosmicShore.Live")"
sed -e "s#@WT@#$WT#g" -e "s#@PROJ@#$PROJ#g" -e "s#@LIVE_REL@#$LIVE_REL#g" "$HERE/PortTests.csproj.in" > "$PROJ/PortTests.csproj"

echo "prisma_edit_mode_tests: building ${#FILES[@]} test file(s) against Prisma's live build of Assets/_Scripts"
if ! dotnet build "$PROJ" -c Debug -v q -nologo > "$WORK/build.log" 2>&1; then
  grep -oE '[^ /]+\.cs\([0-9]+,[0-9]+\): error CS[0-9]+: [^[]*' "$WORK/build.log" | sort -u | head -40
  echo "prisma_edit_mode_tests: BUILD FAILED - full log: $WORK/build.log" >&2
  exit 1
fi

TRX="$WORK/results.trx"
rm -f "$TRX"
set +e
PORTTESTS_PROJECT_ROOT="$WT" dotnet test "$PROJ" --no-build -c Debug ${FILTER:+--filter "$FILTER"} \
  --logger "trx;LogFileName=$TRX" --blame-hang-timeout 180s > "$WORK/test.log" 2>&1
RC=$?
set -e
grep -E '^(Failed!|Passed!)' "$WORK/test.log" || tail -20 "$WORK/test.log"
[ -f "$TRX" ] && python3 "$HERE/summarize.py" "$TRX" --table
exit $RC
