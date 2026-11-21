#!/usr/bin/env bash
# verify_unity.sh - the headless "/verify-unity" step: compile a Unity project at a given commit with
# the real Unity editor in batchmode, then run the EditMode tests. Prints compile errors as
# file:line and failing tests by name. Exit 0 = compiled and all EditMode tests passed.
#
#   bash Tools/Build/verify_unity.sh [--project PATH] [--commit SHA|REF] [--no-tests]
#                                    [--filter NAME] [--logs DIR] [--allow-dirty] [--timeout SEC]
#
#   --project   A FULL (non-sparse, imported) Unity project/worktree. Default: the repo this script is in.
#   --commit    Check out this commit (detached) first, and restore the previous HEAD afterwards.
#               Refused when the worktree has local changes unless --allow-dirty (then git refuses
#               on its own if a change would be overwritten).
#   --no-tests  Compile only.         --filter  Passed to -testFilter (test/fixture/namespace).
#   --logs      Where logs + results go (default: /tmp/verify_unity.<timestamp>).
#   --timeout   Per Unity run, seconds (default 3600; a first import of a fresh worktree is slow).
#   UNITY=/path/to/Unity overrides the editor (default: the version in ProjectSettings/ProjectVersion.txt
#   under /opt/unity/<ver>/Editor/Unity, else `which Unity`).
#
# Exit codes: 0 ok | 1 compile errors | 2 test failures | 3 setup problem (busy project, no editor,
#             sparse checkout, dirty tree) | 4 Unity crashed/timed out without a verdict.
# It never commits, pushes or edits project files. Only one Unity may open a project at a time, so it
# refuses when another Unity process already has --project open (it does not touch that job).
set -uo pipefail

PROJECT="" COMMIT="" TESTS=1 FILTER="" LOGS="" ALLOW_DIRTY=0 TIMEOUT=3600
while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT="$2"; shift 2;;
    --commit) COMMIT="$2"; shift 2;;
    --no-tests) TESTS=0; shift;;
    --filter) FILTER="$2"; shift 2;;
    --logs) LOGS="$2"; shift 2;;
    --allow-dirty) ALLOW_DIRTY=1; shift;;
    --timeout) TIMEOUT="$2"; shift 2;;
    -h|--help) sed -n '2,22p' "$0"; exit 0;;
    *) echo "verify_unity: unknown argument $1" >&2; exit 3;;
  esac
done
[ -z "$PROJECT" ] && PROJECT="$(cd "$(dirname "$0")/../.." && pwd)"
PROJECT="$(cd "$PROJECT" 2>/dev/null && pwd)" || { echo "verify_unity: no such project dir" >&2; exit 3; }
LOGS="${LOGS:-/tmp/verify_unity.$(date +%Y%m%d-%H%M%S)}"; mkdir -p "$LOGS"

die() { echo "verify_unity: $1" >&2; exit "${2:-3}"; }

[ -f "$PROJECT/ProjectSettings/ProjectVersion.txt" ] || die "$PROJECT has no ProjectSettings/ProjectVersion.txt (sparse checkout? use a full worktree)"
if [ "$(git -C "$PROJECT" config --get core.sparseCheckout 2>/dev/null)" = "true" ]; then
  die "$PROJECT is a sparse checkout - Unity would import a broken project. Use a full worktree."
fi
VER="$(sed -n 's/^m_EditorVersion: *//p' "$PROJECT/ProjectSettings/ProjectVersion.txt")"
UNITY="${UNITY:-/opt/unity/$VER/Editor/Unity}"
[ -x "$UNITY" ] || UNITY="$(command -v Unity || true)"
[ -n "$UNITY" ] && [ -x "$UNITY" ] || die "no Unity editor for $VER (set UNITY=)"

# Another Unity already on this project? Leave it alone.
if pgrep -f "projectPath[= ]$PROJECT( |$)" >/dev/null 2>&1; then
  pgrep -fa "projectPath[= ]$PROJECT( |$)" | grep -v pgrep >&2
  die "another Unity process has $PROJECT open - not disturbing it. Wait, or use another full worktree."
fi

PREV=""
restore() { [ -n "$PREV" ] && git -C "$PROJECT" checkout -q --detach "$PREV" 2>/dev/null && echo "verify_unity: restored $PROJECT to ${PREV:0:9}"; }
if [ -n "$COMMIT" ]; then
  if [ $ALLOW_DIRTY -eq 0 ] && [ -n "$(git -C "$PROJECT" status --porcelain --untracked-files=no)" ]; then
    die "$PROJECT has local changes (git status). Commit/stash them, or pass --allow-dirty."
  fi
  PREV="$(git -C "$PROJECT" rev-parse HEAD)"
  git -C "$PROJECT" fetch -q origin 2>/dev/null || true
  git -C "$PROJECT" checkout -q --detach "$COMMIT" || die "checkout $COMMIT failed"
  trap restore EXIT
fi
SHA="$(git -C "$PROJECT" rev-parse --short=9 HEAD 2>/dev/null || echo '?')"
echo "verify_unity: project=$PROJECT commit=$SHA unity=$VER logs=$LOGS"

# file:line errors from a Unity log, deduplicated, paths relative to the project.
errors_of() { grep -E ': error [A-Z]+[0-9]+:' "$1" | sed -E "s#^$PROJECT/##; s#^([^(]+)\(([0-9]+),[0-9]+\): error #\1:\2: error #" | sort -u; }

# 1. compile (import + script compile, then quit)
CLOG="$LOGS/compile.log"
timeout "$TIMEOUT" "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$CLOG" >/dev/null 2>&1; crc=$?
ERRS="$(errors_of "$CLOG")"
if [ -n "$ERRS" ] || grep -q "Scripts have compiler errors" "$CLOG"; then
  echo "COMPILE: FAILED ($(echo "$ERRS" | grep -c . ) errors) - $CLOG"; echo "$ERRS"; exit 1
fi
if [ $crc -ne 0 ] && ! grep -q "Exiting batchmode successfully" "$CLOG"; then
  echo "COMPILE: NO VERDICT (Unity exit $crc, see $CLOG)"; tail -20 "$CLOG"; exit 4
fi
echo "COMPILE: OK ($(grep -o 'Exiting batchmode successfully now!' "$CLOG" | head -1))"
[ $TESTS -eq 0 ] && exit 0

# 2. EditMode tests
TLOG="$LOGS/editmode.log" TXML="$LOGS/editmode.xml"
args=(-batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode -testResults "$TXML" -logFile "$TLOG")
[ -n "$FILTER" ] && args+=(-testFilter "$FILTER")
timeout "$TIMEOUT" "$UNITY" "${args[@]}" >/dev/null 2>&1; trc=$?
[ -f "$TXML" ] || { echo "TESTS: NO RESULTS (Unity exit $trc, see $TLOG)"; errors_of "$TLOG"; tail -20 "$TLOG"; exit 4; }
python3 - "$TXML" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
a = r.attrib
print(f"TESTS: total={a.get('total')} passed={a.get('passed')} failed={a.get('failed')} skipped={a.get('skipped')} inconclusive={a.get('inconclusive')} duration={a.get('duration')}s")
for tc in r.iter('test-case'):
    if tc.get('result') == 'Failed':
        msg = (tc.findtext('failure/message') or '').strip().splitlines()
        print(f"  FAILED {tc.get('fullname')}: {msg[0] if msg else ''}")
PY
failed="$(sed -n 's/.*<test-run[^>]* failed="\([0-9]*\)".*/\1/p' "$TXML" | head -1)"
[ "${failed:-1}" = "0" ] && { echo "VERIFY: OK ($SHA)"; exit 0; }
echo "VERIFY: TEST FAILURES ($SHA) - $TXML"; exit 2
