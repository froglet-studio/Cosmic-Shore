#!/usr/bin/env bash
# Run one search island of the Amoebius genome search on this machine (any Linux box with python3 >= 3.10).
#   bash island.sh <island-name> [hours]
# It copies the code from the shared project folder, installs what it needs, and runs the diamond search with the
# learned critic, publishing its Pareto front under runs/<island-name>/ and importing other islands' best every
# 10 generations. Stop it with: pkill -f agenome.search
set -euo pipefail
NAME=${1:?island name}
HOURS=${2:-6}
SHARE=/mnt/project-files/amoebius-genome
WORK=${WORK:-$HOME/amoebius-island}
mkdir -p "$WORK"
rm -rf "$WORK/agenome" && cp -r "$SHARE/genome/agenome" "$WORK/agenome"
python3 -m pip install --quiet moderngl numpy scipy pillow "jax[cpu]" 2>/dev/null || true
command -v Xvfb >/dev/null || { echo "need Xvfb (apt-get install -y xvfb)"; exit 1; }
F=$SHARE/fitness
mkdir -p "$WORK/reels"
for d in "$F"/reels_256x144/*/; do n=$(basename "$d"); mkdir -p "$WORK/reels/$n"; cp -n "$d/frames.npy" "$WORK/reels/$n/" ; done
WORKERS=${WORKERS:-$(nproc)}
GENS=$(( HOURS * 90 ))
cd "$WORK"
nohup python3 -m agenome.search --run "$WORK/run_$NAME" \
  --stats "$F/target_stats_reels_train.npz" --hold "$F/target_stats_reel05_hold.npz" \
  --real "$WORK/reels/reel0[1-4]/frames.npy" --real-hold "$WORK/reels/reel05/frames.npy" \
  --seeds "$SHARE/seeds/[!wt]*/*.json" "$SHARE/seeds/theta/*.json" "$SHARE/genome/handmade/*.json" \
  --resume $(ls "$SHARE"/runs/*/pareto.json 2>/dev/null | head -8) \
  --share "$SHARE/runs" --island "$NAME" --workers "$WORKERS" --gens "$GENS" --seed $RANDOM \
  > "$WORK/run_$NAME.out" 2>&1 &
echo "island $NAME running: tail -f $WORK/run_$NAME.out ; results in $SHARE/runs/$NAME/"
