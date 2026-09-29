#!/usr/bin/env bash
#
# SteamPipe upload for Cosmic Shore.
#
# NOTHING HERE RUNS UNTIL THE APP EXISTS ON STEAMWORKS. Set STEAM_APPID / STEAM_DEPOTID once
# checklist item A2 is done and this becomes live. Until then the script exits with a clear
# message rather than half-doing something.
#
# Usage:
#   ./upload.sh --build-dir ../../Builds/Windows64 [--branch internal] [--set-live]
#
# Branch convention (see README.md):
#   default   live build players get      - NEVER set live from here without --set-live default
#   beta      playtest branch (password protected, used for the closed playtest E7)
#   internal  team-only smoke testing
#
# Required environment:
#   STEAM_APPID      numeric app id from Steamworks
#   STEAM_DEPOTID    numeric depot id from Steamworks
#   STEAM_USER       builder account login
#   STEAM_PASSWORD   builder account password (or use a cached steamcmd session)
# Optional:
#   STEAMCMD         path to steamcmd (default: "steamcmd" on PATH)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEMPLATE_DIR="$SCRIPT_DIR/templates"
WORK_DIR="$SCRIPT_DIR/work"

BUILD_DIR=""
BRANCH="internal"
SET_LIVE="false"

# ──────────────────────────────── args ────────────────────────────────
while [[ $# -gt 0 ]]; do
  case "$1" in
    --build-dir) BUILD_DIR="$2"; shift 2 ;;
    --branch)    BRANCH="$2";    shift 2 ;;
    --set-live)  SET_LIVE="true"; shift ;;
    -h|--help)   sed -n '2,25p' "$0"; exit 0 ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
done

die() { echo "ERROR: $*" >&2; exit 1; }

# ──────────────────────────── preconditions ───────────────────────────
: "${STEAM_APPID:?STEAM_APPID is not set. Complete checklist A2 (create the Steamworks app) first.}"
: "${STEAM_DEPOTID:?STEAM_DEPOTID is not set. Find it under Steamworks > your app > Depots.}"
: "${STEAM_USER:?STEAM_USER is not set. Use the builder account, not a personal account.}"

[[ -n "$BUILD_DIR" ]] || die "--build-dir is required."
[[ -d "$BUILD_DIR" ]] || die "Build directory does not exist: $BUILD_DIR"
[[ -f "$BUILD_DIR/CosmicShore.exe" ]] || \
  die "No CosmicShore.exe in $BUILD_DIR. Run the Unity build first (see Docs/BUILD_AND_DELIVERY.md)."

STEAMCMD="${STEAMCMD:-steamcmd}"
command -v "$STEAMCMD" >/dev/null 2>&1 || die "steamcmd not found. Install it or set STEAMCMD=/path/to/steamcmd."

# Guard rail: publishing to the live branch must be deliberate and explicit.
if [[ "$BRANCH" == "default" && "$SET_LIVE" != "true" ]]; then
  echo "NOTE: uploading to 'default' WITHOUT setting it live (no --set-live)."
  echo "      The build will appear in Steamworks and can be published from the web UI."
fi
if [[ "$SET_LIVE" == "true" && "$BRANCH" == "default" ]]; then
  echo "*** This will make the build LIVE for all players on the default branch. ***"
  read -r -p "Type the app id ($STEAM_APPID) to confirm: " confirm
  [[ "$confirm" == "$STEAM_APPID" ]] || die "Confirmation did not match. Aborted."
fi

SETLIVE_VALUE=""
[[ "$SET_LIVE" == "true" ]] && SETLIVE_VALUE="$BRANCH"

# ───────────────────────── description stamping ───────────────────────
# The build manifest is written by CosmicShoreBuildPipeline so the Steam build record says
# exactly which version and commit produced this depot.
DESC="Cosmic Shore"
MANIFEST="$BUILD_DIR/build_manifest.txt"
if [[ -f "$MANIFEST" ]]; then
  VER="$(grep -E '^version=' "$MANIFEST" | cut -d= -f2- || true)"
  COMMIT="$(grep -E '^commit='  "$MANIFEST" | cut -d= -f2- || true)"
  CONFIG="$(grep -E '^configuration=' "$MANIFEST" | cut -d= -f2- || true)"
  DESC="Cosmic Shore ${VER:-?} ${CONFIG:-} ${COMMIT:0:8} -> $BRANCH"
else
  echo "WARNING: no build_manifest.txt in the build folder; Steam build description will be generic."
  DESC="$DESC (unstamped) -> $BRANCH"
fi

# ───────────────────────────── vdf generation ─────────────────────────
CONTENT_ROOT="$(cd "$BUILD_DIR" && pwd)"
BUILD_OUTPUT="$WORK_DIR/output"
mkdir -p "$WORK_DIR" "$BUILD_OUTPUT"

render() {
  sed -e "s|{{APPID}}|$STEAM_APPID|g" \
      -e "s|{{DEPOTID}}|$STEAM_DEPOTID|g" \
      -e "s|{{DESC}}|$DESC|g" \
      -e "s|{{CONTENTROOT}}|$CONTENT_ROOT|g" \
      -e "s|{{BUILDOUTPUT}}|$BUILD_OUTPUT|g" \
      -e "s|{{SETLIVE}}|$SETLIVE_VALUE|g" \
      "$1" > "$2"
}

render "$TEMPLATE_DIR/app_build.vdf"   "$WORK_DIR/app_build.vdf"
render "$TEMPLATE_DIR/depot_build.vdf" "$WORK_DIR/depot_build.vdf"

echo "──────────────────────────────────────────────"
echo " app id      : $STEAM_APPID"
echo " depot id    : $STEAM_DEPOTID"
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

echo "Upload complete. Verify the build in Steamworks > $STEAM_APPID > Builds."
