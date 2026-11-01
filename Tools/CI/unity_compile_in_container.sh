#!/usr/bin/env bash
# Runs INSIDE the GameCI Unity image (unityci/editor:ubuntu-<version>-base-*) for the
# bleeding-edge guard's "Unity compile" job. Activates the Unity licence, runs the edit-mode
# suite (which forces a full compile of runtime, editor and test assemblies), and returns the
# licence on the way out, whatever happened.
#
# Called by .github/workflows/bleeding-edge-guard.yml; not meant to be run by hand. The workflow
# classifies the result (compile break / red tests / green) from the files this writes:
#   $LOG      the full editor log (also streamed to stdout)
#   $RESULTS  the edit-mode results XML (absent = the run never reached the test phase)
#   $CODE     the editor's exit code from the test run
#
# Environment (set by the workflow from the `unity-ci` GitHub Environment's secrets):
#   UNITY_EMAIL, UNITY_PASSWORD   the CI Unity account
#   UNITY_SERIAL                  the seat's serial (Pro/Plus), or the one the workflow read out
#                                 of a Personal Unity_lic.ulf (UNITY_LICENSE); never printed
# Activation mirrors GameCI's own activate.sh / return_license.sh (game-ci/unity-test-runner
# v4.3.1): a throwaway blank project, retries with backoff, -returnlicense on exit.
#
# Never `set -x` here: the password and serial are on the editor's command line.
set -uo pipefail

PROJECT="${GITHUB_WORKSPACE:-/github/workspace}"
LOG="$PROJECT/unity-editmode.log"
RESULTS="$PROJECT/editmode-results.xml"
CODE="$PROJECT/unity-exit-code.txt"
rm -f "$LOG" "$RESULTS" "$CODE"

for v in UNITY_EMAIL UNITY_PASSWORD UNITY_SERIAL; do
  if [ -z "${!v:-}" ]; then
    echo "::error::$v is empty inside the container; the workflow should have chosen the reference compile."
    echo 1 > "$CODE"
    exit 1
  fi
done

# Activation needs a project to open; the real one would start the full import before the
# licence is even known to be good. An empty project costs seconds.
BLANK=/tmp/BlankProject
mkdir -p "$BLANK/Assets" "$BLANK/Packages"
echo '{ "dependencies": {} }' > "$BLANK/Packages/manifest.json"

activated=false
return_license() {
  if [ "$activated" = true ]; then
    echo "Returning the Unity licence."
    unity-editor -batchmode -nographics -quit -logFile /dev/stdout \
      -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -projectPath "$BLANK" > /tmp/return-license.log 2>&1 \
      || echo "::warning::Returning the Unity licence failed; the seat may stay taken until Unity frees it. See the Unity ID dashboard."
    activated=false
  fi
}
trap return_license EXIT
trap 'echo "Interrupted."; exit 130' INT TERM

delay=15
for attempt in 1 2 3 4; do
  echo "Activating the Unity licence (attempt $attempt)."
  # Activation output goes to a file, not the job log: it names the account.
  unity-editor -batchmode -nographics -quit -logFile /tmp/activate.log \
    -serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
    -projectPath "$BLANK" > /dev/null 2>&1
  if [ $? -eq 0 ]; then activated=true; break; fi
  grep -iE 'licen[cs]e|activation|error' /tmp/activate.log | grep -viE 'password|serial' | tail -15 || true
  echo "::warning::Unity licence activation failed (attempt $attempt); retrying in ${delay}s."
  sleep "$delay"; delay=$((delay * 2))
done
if [ "$activated" != true ]; then
  echo "::error::Unity licence activation failed after 4 attempts. Check UNITY_EMAIL / UNITY_PASSWORD / UNITY_SERIAL (or UNITY_LICENSE) in the unity-ci environment, and that the seat is not already in use."
  echo 1 > "$CODE"
  exit 1
fi
echo "Licence activated."

# Edit-mode tests force a full compile of runtime, editor and test assemblies. -runTests must
# NOT be combined with -quit.
unity-editor -batchmode -nographics \
  -projectPath "$PROJECT" \
  -runTests -testPlatform EditMode \
  -testResults "$RESULTS" \
  -logFile - 2>&1 | tee "$LOG"
echo "${PIPESTATUS[0]}" > "$CODE"
echo "Unity exited with code $(cat "$CODE")."
exit 0
