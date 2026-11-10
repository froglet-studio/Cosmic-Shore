#!/usr/bin/env bash
# Run one search island of the Amoebius genome search on this machine (any Linux box with python3 >= 3.10).
#   bash island.sh <island-name> [hours]
# It copies the code from the shared project folder, installs what it needs, and runs the diamond search with the
# learned critic, publishing its Pareto front under runs/<island-name>/ and importing other islands' best every
# 10 generations. Stop it with: pkill -f agenome.search
# Optional: SEEDS="glob glob ..." picks the starting lineages (default: every seed folder but the raw wz3 ones);
# RESUME=none starts without the other islands' fronts; WORKERS=n; TUNE=0.25 (share of parameter-only steps);
# SS=2 renders every clip at 2x and filters it down (closer to the downscaled reels, 2 to 2.7x slower).
set -euo pipefail
NAME=${1:?island name}
HOURS=${2:-6}
SHARE=/mnt/project-files/amoebius-genome
WORK=${WORK:-$HOME/amoebius-island}
mkdir -p "$WORK"
rm -rf "$WORK/agenome" && cp -r "$SHARE/genome/agenome" "$WORK/agenome"
# separate installs in parallel: one serial install crawled at ~40 KB/s in a fresh container (island A, 2026-11-10)
python3 -c "import moderngl, scipy, PIL, jax" 2>/dev/null || {
  python3 -m pip install --quiet scipy 2>/dev/null & python3 -m pip install --quiet jaxlib 2>/dev/null &
  python3 -m pip install --quiet moderngl pillow numpy 2>/dev/null & wait
  python3 -m pip install --quiet "jax[cpu]" 2>/dev/null || true; }
command -v Xvfb >/dev/null || { echo "need Xvfb (apt-get install -y xvfb)"; exit 1; }
F=$SHARE/fitness
mkdir -p "$WORK/reels"
for d in "$F"/reels_256x144/*/; do n=$(basename "$d"); mkdir -p "$WORK/reels/$n"; cp -n "$d/frames.npy" "$WORK/reels/$n/" ; done
WORKERS=${WORKERS:-$(nproc)}
SEEDS=${SEEDS:-"$SHARE/seeds/[!wt]*/*.json $SHARE/seeds/theta/*.json $SHARE/genome/handmade/*.json"}
set -f; read -r -a SEED_ARGS <<< "$SEEDS"; set +f
RESUME_ARGS=()
if [ "${RESUME:-all}" != none ]; then RESUME_ARGS=(--resume $(ls "$SHARE"/runs/*/pareto.json 2>/dev/null | head -8)); fi
GENS=$(( HOURS * 90 ))
cd "$WORK"
nohup python3 -m agenome.search --run "$WORK/run_$NAME" \
  --stats "$F/target_stats_reels_train.npz" --hold "$F/target_stats_reel05_hold.npz" \
  --real "$WORK/reels/reel0[1-4]/frames.npy" --real-hold "$WORK/reels/reel05/frames.npy" \
  --seeds "${SEED_ARGS[@]}" "${RESUME_ARGS[@]}" --tune "${TUNE:-0.25}" --ss "${SS:-1}" \
  --share "$SHARE/runs" --island "$NAME" --workers "$WORKERS" --gens "$GENS" --seed $RANDOM \
  > "$WORK/run_$NAME.out" 2>&1 &
echo "island $NAME running: tail -f $WORK/run_$NAME.out ; results in $SHARE/runs/$NAME/"
