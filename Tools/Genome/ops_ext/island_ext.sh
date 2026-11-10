#!/usr/bin/env bash
# ops_ext variant of genome/island.sh (copied 2026-11-10): runs agenome.search with the extension ops.
#   EXT=off runs the control arm (same code, no ext ops); EXTW=0.5 weights ext ops in mutation; EXTFAM=spectral,linalg.
#   Fronts go to ops_ext/runs/<name>/ (RUNS) so islands on core code never import unknown ops.
# Run one search island of the Amoebius genome search on this machine (any Linux box with python3 >= 3.10).
#   bash island.sh <island-name> [hours]
# It copies the code from the shared project folder, installs what it needs, and runs the diamond search with the
# learned critic, publishing its Pareto front under runs/<island-name>/ and importing other islands' best every
# 10 generations. Stop it with: pkill -f agenome.search
# Optional: SEEDS="glob glob ..." picks the starting lineages (default: every seed folder but the raw wz3 ones);
# RESUME=none starts without the other islands' fronts; WORKERS=n; TUNE=0.25 (share of parameter-only steps);
# SS=2 renders every clip at 2x and filters it down (closer to the downscaled reels, 2 to 2.7x slower);
# JUDGE=1.0 adds the fixed perceptual judge (agenome/judge.py, ImageNet features vs the reels) to the fitness,
# with STATW (weight of the statistics, default 1.0) and CRITICW (the online critic, default 0.5) beside it.
# It runs in the FOREGROUND (a cloud container kills detached processes once it idles after a turn): start it as a
# tracked background task (Bash run_in_background, 2 h timeout) and start it again when that ends - it resumes from
# its own population. BG=1 detaches it with nohup instead (only on a machine that stays up).
# Testing unmerged code: CODE=<your checkout>/Tools/Genome/agenome runs it, and RUNS=<your folder>/runs keeps its
# front out of runs/ (islands on the merged code cannot decode ops they do not have yet).
set -euo pipefail
NAME=${1:?island name}
HOURS=${2:-6}
SHARE=/mnt/project-files/amoebius-genome
WORK=${WORK:-$HOME/amoebius-island}
mkdir -p "$WORK"
rm -rf "$WORK/agenome" "$WORK/ops_ext" && cp -r "${CODE:-$SHARE/genome/agenome}" "$WORK/agenome"
cp -r "$SHARE/ops_ext" "$WORK/ops_ext" && rm -rf "$WORK/ops_ext/renders" "$WORK/ops_ext/runs"
export PYTHONPATH="$WORK"
# separate installs in parallel: one serial install crawled at ~40 KB/s in a fresh container (island A, 2026-11-10)
python3 -c "import moderngl, scipy, PIL, jax" 2>/dev/null || {
  python3 -m pip install --quiet scipy 2>/dev/null & python3 -m pip install --quiet jaxlib 2>/dev/null &
  python3 -m pip install --quiet moderngl pillow numpy 2>/dev/null & wait
  python3 -m pip install --quiet "jax[cpu]" 2>/dev/null || true; }
if [ "${JUDGE:-0}" != 0 ]; then
  KERAS_BACKEND=jax python3 -c "import keras, h5py" 2>/dev/null || python3 -m pip install --quiet keras h5py 2>/dev/null
  mkdir -p "$HOME/.keras/models" && cp -n "$SHARE"/fitness/judge/*.h5 "$HOME/.keras/models/" 2>/dev/null || true
fi
export KERAS_BACKEND=jax
command -v Xvfb >/dev/null || { echo "need Xvfb (apt-get install -y xvfb)"; exit 1; }
F=$SHARE/fitness
mkdir -p "$WORK/reels"
for d in "$F"/reels_256x144/*/; do n=$(basename "$d"); mkdir -p "$WORK/reels/$n"; cp -n "$d/frames.npy" "$WORK/reels/$n/" ; done
WORKERS=${WORKERS:-$(nproc)}
SEEDS=${SEEDS:-"$SHARE/seeds/[!wt]*/*.json $SHARE/seeds/theta/*.json $SHARE/genome/handmade/*.json"}
[ "${EXT:-on}" != off ] && SEEDS="$SEEDS ${EXTSEEDS:-$SHARE/ops_ext/seeds/*.json}"
set -f; read -r -a SEED_ARGS <<< "$SEEDS"; set +f
RESUME_ARGS=()
OWN=()
for f in "$WORK/run_$NAME/population.json" "$WORK/run_$NAME/pareto.json"; do [ -f "$f" ] && OWN+=("$f"); done
if [ "${RESUME:-all}" != none ]; then RESUME_ARGS=(--resume ${OWN[@]+"${OWN[@]}"} $(ls "$SHARE"/runs/*/pareto.json 2>/dev/null | head -16 || true))
elif [ ${#OWN[@]} -gt 0 ]; then RESUME_ARGS=(--resume "${OWN[@]}"); fi
GENS=$(( HOURS * 90 ))
cd "$WORK"
EXTARGS=(--ext-weight "${EXTW:-0.5}")
[ "${EXT:-on}" = off ] && EXTARGS=(--ext-off) && export AGENOME_EXT=0
[ -n "${EXTFAM:-}" ] && EXTARGS+=(--ext-families "$EXTFAM")
CMD=(python3 -m ops_ext.run_search "${EXTARGS[@]}" --run "$WORK/run_$NAME" \
  --stats "$F/target_stats_reels_train.npz" --hold "$F/target_stats_reel05_hold.npz" \
  --real "$WORK/reels/reel0[1-4]/frames.npy" --real-hold "$WORK/reels/reel05/frames.npy" \
  --seeds "${SEED_ARGS[@]}" "${RESUME_ARGS[@]}" --tune "${TUNE:-0.25}" --ss "${SS:-1}" \
  --judge-w "${JUDGE:-0}" --stat-w "${STATW:-1.0}" --critic-w "${CRITICW:-0.5}" \
  --share "${RUNS:-$SHARE/ops_ext/runs}" --island "$NAME" --workers "$WORKERS" --gens "$GENS" --seed $RANDOM)
echo "island $NAME: log $WORK/run_$NAME.out ; results in ${RUNS:-$SHARE/ops_ext/runs}/$NAME/ ; resuming from ${#OWN[@]} own file(s)"
if [ "${BG:-0}" = 1 ]; then nohup "${CMD[@]}" >> "$WORK/run_$NAME.out" 2>&1 & exit 0; fi
exec "${CMD[@]}" >> "$WORK/run_$NAME.out" 2>&1
