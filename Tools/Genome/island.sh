#!/usr/bin/env bash
# Run one search island of the Amoebius genome search on this machine (any Linux box with python3 >= 3.10).
#   bash island.sh <island-name> [hours]
# It copies the code from the shared project folder, installs what it needs, and runs the Pareto search with the
# learned critic, publishing its Pareto front under runs/<island-name>/ and importing other islands' best every
# 10 generations. Stop it with: pkill -f agenome.search
# Optional: SEEDS="glob glob ..." picks the starting lineages (default: every seed folder but the raw wz3 ones);
# RESUME=none starts without the other islands' fronts (a restart always resumes the island's own population, and
# then reads only seed files written since; ALLSEEDS=1 reads every seed again); WORKERS=n; TUNE=0.25 (share of parameter-only steps);
# SS=2 renders every clip at 2x and filters it down (closer to the downscaled reels, 2 to 2.7x slower);
# JUDGE=1.0 adds the fixed perceptual judge (agenome/judge.py, ImageNet features vs the reels) to the fitness,
# with STATW (weight of the statistics, default 1.0) and CRITICW (the online critic, default 0.5) beside it.
# The judge is v3 since 18:30 (the worse of its two networks; the mean was ridden by pleasing one of them).
# MIGRATE=0 stops importing other islands' best (default every 10 generations; a diversity reservoir uses 0 with
# RESUME=none); LINEAGE=0.25 caps the share of survivors one root seed's descendants may hold (0 = no cap).
# WINDOWS=2 judges two 4-second windows of each phenotype, 8 s apart (HOP=8), and scores their mean: the best genomes
# fit the first 4 seconds only (1.13-1.70 there, 1.21-2.48 later); each window is one more render.
# WINMODE=random draws each evaluation's WINDOWS offsets from 0..SPAN s on a STEP grid (default 20, 2) instead, and
# every REJUDGE generations (default 6) judges the survivors and the front again on windows they have not seen and
# folds them in: two FIXED windows get fitted too (climbed genomes read 0.3-0.6 worse on windows they never saw).
# AUDIT="24,28" (default) judges each generation's best on windows selection never uses, logged as best_audit.
# The search (agenome/search.py, SEARCH.md) defaults to pareto survival and the CMA tuner, the two pieces that won the
# 2026-11-10 A/B; SURVIVAL=diamond TUNER=random runs the search as it was before (set one of them for an A/B island),
# PARENTS=lexicase and OPERATORS=adaptive switch on the two pieces that were a wash; BAND=0.05 is the pareto band
# (fidelity given up for bytes; SPAN is the random-window span).
# LABW=1.0 adds the fitness lab's element-aware scorer (agenome.labscore, 1.0 = Amoebius' held-out source) to the
# fitness beside the judge, on the same windows, scored by LABWORKERS processes (default 2; about 1.5 s a window
# each); its numbers are logged as best_lab and best_audit_lab. Unset or 0, the command is the same as before. A
# lab island's dist is not comparable with the others': give it its own RUNS folder, and IMPORT=<the shared runs
# folder> so it still draws the same immigrants as the islands it is compared with.
# The reels are three sources (reel05 re-cuts reel03, reel04 re-cuts reel02): reels 1, 2, 4 train every measure and
# reels 3, 5 are held out (v2 fitness, 2026-11-10 18:00).
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
CODEDIR=${CODE:-$SHARE/genome/agenome}
rm -rf "$WORK/agenome" "$WORK/ops_ext" && cp -r "$CODEDIR" "$WORK/agenome"
# the extension operators (FFT, SVD, eigenmodes, flames, KIFS, curl flows, prism ribbons ...) live beside agenome and
# install on import; AGENOME_EXT=0 runs the core set alone
[ -d "$(dirname "$CODEDIR")/ops_ext" ] && cp -r "$(dirname "$CODEDIR")/ops_ext" "$WORK/ops_ext"
# separate installs in parallel: one serial install crawled at ~40 KB/s in a fresh container (island A, 2026-11-10)
python3 -c "import moderngl, scipy, PIL, jax" 2>/dev/null || {
  python3 -m pip install --quiet scipy 2>/dev/null & python3 -m pip install --quiet jaxlib 2>/dev/null &
  python3 -m pip install --quiet moderngl pillow numpy 2>/dev/null & wait
  python3 -m pip install --quiet "jax[cpu]" 2>/dev/null || true; }
if [ "${JUDGE:-0}" != 0 ] || [ "${LABW:-0}" != 0 ]; then
  KERAS_BACKEND=jax python3 -c "import keras, h5py" 2>/dev/null || python3 -m pip install --quiet keras h5py 2>/dev/null
  mkdir -p "$HOME/.keras/models" && cp -n "$SHARE"/fitness/judge/*.h5 "$HOME/.keras/models/" 2>/dev/null || true
fi
LAB_ARGS=()
if [ "${LABW:-0}" != 0 ]; then
  # the lab scorer's own needs (fitness_lab/scorer/MERGE.md); it uses the judge's InceptionV3 weights copied above
  python3 -c "import cv2, skimage, pywt, sklearn, cloudpickle" 2>/dev/null || python3 -m pip install --quiet \
    opencv-python-headless scikit-image PyWavelets scikit-learn cloudpickle 2>/dev/null
  LAB_ARGS=(--lab-w "$LABW" --lab-workers "${LABWORKERS:-2}")
fi
# IMPORT=<folder> draws immigrants from that folder's islands while RUNS keeps this island's own front elsewhere
IMPORT_ARGS=()
[ -n "${IMPORT:-}" ] && IMPORT_ARGS=(--import-from "$IMPORT")
export KERAS_BACKEND=jax
command -v Xvfb >/dev/null || { echo "need Xvfb (apt-get install -y xvfb)"; exit 1; }
F=$SHARE/fitness
mkdir -p "$WORK/reels"
for d in "$F"/reels_256x144/*/; do n=$(basename "$d"); mkdir -p "$WORK/reels/$n"; cp -n "$d/frames.npy" "$WORK/reels/$n/" ; done
WORKERS=${WORKERS:-$(nproc)}
SEEDS=${SEEDS:-"$SHARE/seeds/[!wt]*/*.json $SHARE/seeds/theta/*.json $SHARE/seeds/designed/*/*.json $SHARE/hier/seeds/*.json $SHARE/genome/handmade/*.json $SHARE/genome/ops_ext/seeds/*.json"}
set -f; read -r -a SEED_ARGS <<< "$SEEDS"; set +f
RESUME_ARGS=()
OWN=()
for f in "$WORK/run_$NAME/population.json" "$WORK/run_$NAME/pareto.json"; do [ -f "$f" ] && OWN+=("$f"); done
if [ "${RESUME:-all}" != none ]; then RESUME_ARGS=(--resume ${OWN[@]+"${OWN[@]}"} $(ls "$SHARE"/runs/*/pareto.json 2>/dev/null | head -16 || true))
elif [ ${#OWN[@]} -gt 0 ]; then RESUME_ARGS=(--resume "${OWN[@]}"); fi
# a resuming island evaluated every seed file at its first start; it re-reads only the ones written since its last
# population save (about 150 genomes to score at a restart instead of 600). ALLSEEDS=1 reads them all again.
POP="$WORK/run_$NAME/population.json"
if [ -f "$POP" ] && [ "${ALLSEEDS:-0}" = 0 ]; then
  NEWSEEDS=()
  for pat in "${SEED_ARGS[@]}"; do for f in $pat; do [ -f "$f" ] && [ "$f" -nt "$POP" ] && NEWSEEDS+=("$f"); done; done
  echo "island $NAME: ${#NEWSEEDS[@]} seed file(s) newer than its population (ALLSEEDS=1 reads every seed)"
  SEED_ARGS=(${NEWSEEDS[@]+"${NEWSEEDS[@]}"})
fi
GENS=$(( HOURS * 90 ))
cd "$WORK"
CMD=(python3 -m agenome.search --run "$WORK/run_$NAME" \
  --stats "$F/target_stats_train_v2.npz" --hold "$F/target_stats_hold_v2.npz" \
  --real "$WORK/reels/reel0[124]/frames.npy" --real-hold "$WORK/reels/reel0[35]/frames.npy" \
  --seeds ${SEED_ARGS[@]+"${SEED_ARGS[@]}"} ${RESUME_ARGS[@]+"${RESUME_ARGS[@]}"} --tune "${TUNE:-0.25}" --ss "${SS:-1}" \
  --judge-w "${JUDGE:-0}" --stat-w "${STATW:-1.0}" --critic-w "${CRITICW:-0.5}" \
  --migrate-every "${MIGRATE:-10}" --lineage-cap "${LINEAGE:-0.25}" \
  --judge-windows "${WINDOWS:-1}" --judge-hop "${HOP:-8}" --window-mode "${WINMODE:-fixed}" \
  --window-span "${SPAN:-20}" --window-step "${STEP:-2}" --audit-offsets "${AUDIT:-24,28}" \
  --rejudge-every "$( [ "${WINMODE:-fixed}" = random ] && echo "${REJUDGE:-6}" || echo 0 )" \
  --survival "${SURVIVAL:-pareto}" --parents "${PARENTS:-tournament}" --operators "${OPERATORS:-fixed}" \
  --tuner "${TUNER:-cma}" --span "${BAND:-0.05}" \
  ${LAB_ARGS[@]+"${LAB_ARGS[@]}"} ${IMPORT_ARGS[@]+"${IMPORT_ARGS[@]}"} \
  --share "${RUNS:-$SHARE/runs}" --island "$NAME" --workers "$WORKERS" --gens "$GENS" --seed $RANDOM)
echo "island $NAME: log $WORK/run_$NAME.out ; results in ${RUNS:-$SHARE/runs}/$NAME/ ; resuming from ${#OWN[@]} own file(s)"
if [ "${BG:-0}" = 1 ]; then nohup "${CMD[@]}" >> "$WORK/run_$NAME.out" 2>&1 & exit 0; fi
exec "${CMD[@]}" >> "$WORK/run_$NAME.out" 2>&1
