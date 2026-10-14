#!/usr/bin/env bash
#
# SteamPipe upload for Cosmic Shore - one script, two Steamworks apps.
#
# NOTHING HERE RUNS UNTIL THE TARGET APP EXISTS ON STEAMWORKS. The script refuses until the ids
# for the chosen target are exported, and it never derives one app's ids from the other's. Until
# then it exits with a clear message rather than half-doing something.
#
# Usage:
#   ./upload.sh --build-dir ../../Builds/Windows64 [--target base|playtest] [--branch internal] [--set-live]
#
# Targets (see README.md and Docs/STEAM_PLAYTEST_RUNBOOK.md):
#   base       The store app - the future paid build. Ids from checklist item A2.   (default target)
#              env STEAM_APPID / STEAM_DEPOTID
#   playtest   The Playtest child app - the live invite channel. Ids from checklist item A4.
#              env STEAM_PLAYTEST_APPID / STEAM_PLAYTEST_DEPOTID
#
# Branch convention (per app - the SAME branch name has a DIFFERENT audience on each app):
#   default    base app:      everyone who owns the game  - the paid release, nothing before it
#              playtest app:  every tester granted access - the live invite channel
#   internal   team-only smoke testing on either app (password protected)
#   beta       base app only: the Revision-1 closed-playtest branch; no audience under the Playtest model
#
# --set-live default makes you type the TARGET's app id back. Valve documents that the default
# branch cannot be set live from a build script at all (App Admin > Builds does it), so the normal
# path is to upload WITHOUT --set-live and publish from the web UI.
#
# Required environment: the id pair for the chosen target (above), plus
#   STEAM_USER       builder account login
#   STEAM_PASSWORD   builder account password (or use a cached steamcmd session)
# Optional:
#   STEAMCMD         path to steamcmd (default: "steamcmd" on PATH)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE_DIR="$SCRIPT_DIR/templates"
WORK_DIR="$SCRIPT_DIR/work"

BUILD_DIR=""
TARGET="base"
BRANCH="internal"
SET_LIVE="false"

# ──────────────────────────────── args ────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --build-dir) BUILD_DIR="$2"; shift 2 ;;
    --target)    TARGET="$2";    shift 2 ;;
    --branch)    BRANCH="$2";    shift 2 ;;
    --set-live)  SET_LIVE="true"; shift ;;
    -h|--help)   sed -n '2,/^set -euo pipefail$/p' "$0" | sed '$d'; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

die() { echo "ERROR: $*" >&2; exit 1; }

# ──────────────────────────── target → ids ────────────────────────────
# Each target reads its OWN pair of environment variables. The refusal names the checklist item
# that produces the id, because the id does not exist until that item is done.
case "$TARGET" in
  base)
    APPID_VAR="STEAM_APPID"
    DEPOTID_VAR="STEAM_DEPOTID"
    APP_HINT="Complete checklist A2 (create the Steamworks app) first."
    DEPOT_HINT="Find it under Steamworks > the base app > Depots (created by A2)."
    AUDIENCE="everyone who owns the game (the BASE app)"
    ;;
  playtest)
    APPID_VAR="STEAM_PLAYTEST_APPID"
    DEPOTID_VAR="STEAM_PLAYTEST_DEPOTID"
    APP_HINT="Complete checklist A4 (create the Playtest child app - Docs/STEAM_PLAYTEST_RUNBOOK.md) first."
    DEPOT_HINT="Find it under Steamworks > the Playtest child app > Depots (created by A4)."
    AUDIENCE="every tester who has been granted Playtest access (the PLAYTEST app)"
    ;;
  *) die "Unknown --target '$TARGET'. Use 'base' (STEAM_APPID) or 'playtest' (STEAM_PLAYTEST_APPID)." ;;
esac

APPID="${!APPID_VAR:-}"
DEPOTID="${!DEPOTID_VAR:-}"
[[ -n "$APPID" ]]   || die "$APPID_VAR is not set. $APP_HINT"
[[ -n "$DEPOTID" ]] || die "$DEPOTID_VAR is not set. $DEPOT_HINT"
[[ "$APPID"   =~ ^[0-9]+$ ]] || die "$APPID_VAR must be a numeric Steam app id (got '$APPID')."
[[ "$DEPOTID" =~ ^[0-9]+$ ]] || die "$DEPOTID_VAR must be a numeric Steam depot id (got '$DEPOTID')."

# The child app has its own ids. If both pairs are exported and one id is shared, somebody pasted
# the base app's id into the Playtest slot (or the reverse) - that is exactly the mistake that
# publishes a build to the wrong audience, so stop here.
if [[ -n "${STEAM_APPID:-}" && -n "${STEAM_PLAYTEST_APPID:-}" && "$STEAM_APPID" == "$STEAM_PLAYTEST_APPID" ]]; then
  die "STEAM_APPID and STEAM_PLAYTEST_APPID are the same id. The Playtest child app (A4) is a separate app from the base app (A2)."
fi
if [[ -n "${STEAM_DEPOTID:-}" && -n "${STEAM_PLAYTEST_DEPOTID:-}" && "$STEAM_DEPOTID" == "$STEAM_PLAYTEST_DEPOTID" ]]; then
  die "STEAM_DEPOTID and STEAM_PLAYTEST_DEPOTID are the same id. Each app has its own depot."
fi

# ──────────────────────────── preconditions ───────────────────────────
: "${STEAM_USER:?STEAM_USER is not set. Use the builder account, not a personal account.}"

[[ -n "$BUILD_DIR" ]] || die "--build-dir is required."
[[ -d "$BUILD_DIR" ]] || die "Build directory does not exist: $BUILD_DIR"
[[ -f "$BUILD_DIR/CosmicShore.exe" ]] || \
  die "No CosmicShore.exe in $BUILD_DIR. Run the Unity build first (see Docs/BUILD_AND_DELIVERY.md)."

STEAMCMD="${STEAMCMD:-steamcmd}"
command -v "$STEAMCMD" >/dev/null 2>&1 || die "steamcmd not found. Install it or set STEAMCMD=/path/to/steamcmd."

# Guard rail: publishing to the live branch must be deliberate and explicit, on EITHER app.
# On the Playtest app, "default" is the invite channel - every granted tester gets it.
if [[ "$BRANCH" == "default" && "$SET_LIVE" != "true" ]]; then
  echo "NOTE: uploading to 'default' on the $TARGET app WITHOUT setting it live (no --set-live)."
  echo "      The build will appear in Steamworks and can be published from the web UI."
fi
if [[ "$SET_LIVE" == "true" && "$BRANCH" == "default" ]]; then
  echo "*** This will make the build LIVE on the default branch for $AUDIENCE. ***"
  confirm=""
  read -r -p "Type the $TARGET app id ($APPID_VAR) to confirm: " confirm || true
  [[ "$confirm" == "$APPID" ]] || die "Confirmation did not match $APPID_VAR. Aborted."
fi

SETLIVE_VALUE=""
[[ "$SET_LIVE" == "true" ]] && SETLIVE_VALUE="$BRANCH"

# ───────────────────────── description stamping ───────────────────────
# The build manifest is written by CosmicShoreBuildPipeline so the Steam build record says
# exactly which version and commit produced this depot. The target is stamped too, so the two
# apps' build lists cannot be confused with each other.
DESC="Cosmic Shore"
MANIFEST="$BUILD_DIR/build_manifest.txt"
if [[ -f "$MANIFEST" ]]; then
  VER="$(grep -E '^version=' "$MANIFEST" | cut -d= -f2- || true)"
  COMMIT="$(grep -E '^commit='  "$MANIFEST" | cut -d= -f2- || true)"
  CONFIG="$(grep -E '^configuration=' "$MANIFEST" | cut -d= -f2- || true)"
  DESC="Cosmic Shore ${VER:-?} ${CONFIG:-} ${COMMIT:0:8} [$TARGET] -> $BRANCH"
else
  echo "WARNING: no build_manifest.txt in the build folder; Steam build description will be generic."
  DESC="$DESC (unstamped) [$TARGET] -> $BRANCH"
fi

# ───────────────────────────── vdf generation ─────────────────────────
CONTENT_ROOT="$(cd "$BUILD_DIR" && pwd)"
BUILD_OUTPUT="$WORK_DIR/output"
mkdir -p "$WORK_DIR" "$BUILD_OUTPUT"

render() {
  sed -e "s|{{APPID}}|$APPID|g" \
      -e "s|{{DEPOTID}}|$DEPOTID|g" \
      -e "s|{{DESC}}|$DESC|g" \
      -e "s|{{CONTENTROOT}}|$CONTENT_ROOT|g" \
      -e "s|{{BUILDOUTPUT}}|$BUILD_OUTPUT|g" \
      -e "s|{{SETLIVE}}|$SETLIVE_VALUE|g" \
      "$1" > "$2"
}

render "$TEMPLATE_DIR/app_build.vdf"   "$WORK_DIR/app_build.vdf"
render "$TEMPLATE_DIR/depot_build.vdf" "$WORK_DIR/depot_build.vdf"

echo "──────────────────────────────────────────────"
echo " target      : $TARGET"
echo " app id      : $APPID ($APPID_VAR)"
echo " depot id    : $DEPOTID ($DEPOTID_VAR)"
echo " content     : $CONTENT_ROOT"
echo " branch      : $BRANCH"
echo " set live    : $SET_LIVE"
echo " description : $DESC"
echo "──────────────────────────────────────────────"

# ─────────────────────────────── upload ───────────────────────────────
if [[ -n "${STEAM_PASSWORD:-}" ]]; then
  "$STEAMCMD" +login "$STEAM_USER" "$STEAM_PASSWORD" \
              +run_app_build "$WORK_DIR/app_build.vdf" +quit
else
  # Relies on a cached steamcmd session; run `steamcmd +login <user>` once interactively
  # (including Steam Guard) on the build machine to establish it.
  "$STEAMCMD" +login "$STEAM_USER" \
              +run_app_build "$WORK_DIR/app_build.vdf" +quit
fi

echo "Upload complete. Verify the build in Steamworks > $APPID ($TARGET app) > Builds."
