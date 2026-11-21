#!/usr/bin/env bash
# Records the showcase session (menu -> Bloomrush card -> load -> flight) from a built player and
# assembles it into an MP4 at true game speed. Usage: tools/record_session.sh PLAYER_DIR OUT_DIR
# PLAYER_DIR is a `dotnet build src/CosmicShore.Player -c Release -o DIR` output (Linux needs
# libglfw.so.3 beside it). Takes ~25 min on a CPU-only box; flight frames dominate.
set -euo pipefail
PLAYER=$(realpath "$1"); OUT=$(realpath -m "$2"); ROOT=$(git rev-parse --show-toplevel)
REC="$OUT/rec"; rm -rf "$REC"; mkdir -p "$REC"
COSMIC_SHORE_AUDIO=off COSMIC_SHORE_PROJECT="$ROOT" xvfb-run -a -s "-screen 0 1600x900x24" \
  dotnet "$PLAYER/CosmicShore.dll" --render-from 999999 \
  --record "$REC/menu:420-1000:2" --record "$REC/load:1010-2150:10" --record "$REC/fly:2160-2760:2" \
  --do "700:click 615,778" --do "840:click 633,610" --do "1000:click 1423,830" --do "1900:click 800,690" \
  --do "2160:hold W 3000" --do "2330:hold D 50" --do "2480:hold A 60" > "$OUT/record.log" 2>&1
python3 "$(dirname "$0")/assemble_recording.py" "$REC" "$OUT/session.mp4" | tee "$OUT/chapters.json"
