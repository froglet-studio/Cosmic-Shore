#!/usr/bin/env bash
# Runs INSIDE the GameCI Unity image (unityci/editor:ubuntu-<version>-base-*) for the look-sheet job
# (.github/workflows/look-sheet.yml): activates the Unity licence, plays the real game headless under Xvfb
# and captures what the player sees (Assets/_Scripts/Editor/Look/LookSheetLauncher.cs), then returns the
# licence on the way out, whatever happened. Docs/LOOK_SHEET.md.
#
# The activation is the bleeding-edge guard's (Tools/CI/unity_compile_in_container.sh on branch
# claude/ci-hosted-unity-compile, GameCI's activate.sh / return_license.sh): when that script lands, the
# activation here should be one shared helper the two call, not two copies.
#
# Environment (set by the workflow from the `unity-ci` GitHub Environment's secrets):
#   UNITY_EMAIL, UNITY_PASSWORD   the CI Unity account
#   UNITY_SERIAL                  the seat's serial (Pro/Plus), or the one read out of a Personal
#                                 Unity_lic.ulf; never printed. EMPTY = login mode.
#   LOOK_KIND                     menu | mode
#   LOOK_WHAT                     screens (HOME,HANGAR,PORT) or a mode (switchback | thresher-time)
#   LOOK_TIMES, LOOK_INTENSITY    for a mode: the game seconds after the turn starts, the intensity
#   LOOK_OUT                      the output folder, relative to the workspace (default Logs/LookSheet)
#
# Never `set -x` here: the password and serial are on the editor's command line.
set -uo pipefail

PROJECT="${GITHUB_WORKSPACE:-/github/workspace}"
OUT="$PROJECT/${LOOK_OUT:-Logs/LookSheet}"
LOG="$PROJECT/unity-look.log"
CODE="$PROJECT/unity-look-exit-code.txt"
mkdir -p "$OUT"
rm -f "$LOG" "$CODE"

for v in UNITY_EMAIL UNITY_PASSWORD; do
  if [ -z "${!v:-}" ]; then
    echo "::error::$v is empty inside the container; the workflow should have skipped the look sheet."
    echo 1 > "$CODE"
    exit 1
  fi
done

BLANK=/tmp/BlankProject
mkdir -p "$BLANK/Assets" "$BLANK/Packages"
echo '{ "dependencies": {} }' > "$BLANK/Packages/manifest.json"

if [ -n "${UNITY_SERIAL:-}" ]; then
  how=serial
  ACTIVATE=(-serial "$UNITY_SERIAL" -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD")
else
  how=login
  ACTIVATE=(-username "$UNITY_EMAIL" -password "$UNITY_PASSWORD")
fi

activated=false
return_license() {
  if [ "$activated" = true ]; then
    echo "Returning the Unity licence."
    unity-editor -batchmode -nographics -quit -logFile /dev/stdout \
      -returnlicense -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -projectPath "$BLANK" > /tmp/return-license.log 2>&1 \
      || { [ "$how" = login ] && echo "Returning the licence did not succeed (login mode; a Personal licence has no seat to free)." \
           || echo "::warning::Returning the Unity licence failed; the seat may stay taken until Unity frees it."; }
    activated=false
  fi
}
trap return_license EXIT
trap 'echo "Interrupted."; exit 130' INT TERM

delay=15
for attempt in 1 2 3 4; do
  echo "Activating the Unity licence ($how, attempt $attempt)."
  unity-editor -batchmode -nographics -quit -logFile /tmp/activate.log \
    "${ACTIVATE[@]}" -projectPath "$BLANK" > /dev/null 2>&1
  if [ $? -eq 0 ]; then activated=true; break; fi
  grep -iE 'licen[cs]e|activation|error' /tmp/activate.log | grep -viE 'password|serial' | tail -15 || true
  echo "::warning::Unity licence activation failed (attempt $attempt); retrying in ${delay}s."
  sleep "$delay"; delay=$((delay * 2))
done
if [ "$activated" != true ]; then
  echo "::error::Unity licence activation ($how) failed after 4 attempts. Check UNITY_EMAIL / UNITY_PASSWORD (and UNITY_SERIAL) in the unity-ci environment; the account needs two-factor sign-in off and an active licence."
  echo 1 > "$CODE"
  exit 1
fi
echo "Licence activated ($how)."

# The arguments the launcher reads. Never -nographics (nothing would render) and never -quit (Unity
# would exit before Play starts): the launcher quits itself with -lookQuit.
ARGS=(-batchmode -projectPath "$PROJECT" -executeMethod CosmicShore.Editor.LookSheetLauncher.Run
      -lookOut "$OUT" -lookSize 1920x1080 -lookPhone 1600x900 -lookQuit -logFile -)
case "${LOOK_KIND:-menu}" in
  menu) ARGS+=(-lookScreens "${LOOK_WHAT:-HOME,HANGAR,PORT}") ;;
  mode) ARGS+=(-lookMode "${LOOK_WHAT:-switchback}" -lookTimes "${LOOK_TIMES:-5,15,30}" -lookIntensity "${LOOK_INTENSITY:-1}") ;;
  *) echo "::error::LOOK_KIND must be menu or mode (got '${LOOK_KIND}')"; echo 3 > "$CODE"; exit 3 ;;
esac

# A GPU-less runner renders through Mesa under a virtual display; GameCI's image ships xvfb-run.
if command -v xvfb-run > /dev/null 2>&1; then
  xvfb-run -a -s "-screen 0 1920x1080x24" unity-editor "${ARGS[@]}" 2>&1 | tee "$LOG"
  echo "${PIPESTATUS[0]}" > "$CODE"
else
  echo "::warning::xvfb-run is not in this image; running without a virtual display (a GPU-less run will capture blank frames and fail the gate)."
  unity-editor "${ARGS[@]}" 2>&1 | tee "$LOG"
  echo "${PIPESTATUS[0]}" > "$CODE"
fi
echo "Unity exited with code $(cat "$CODE")."
exit 0
