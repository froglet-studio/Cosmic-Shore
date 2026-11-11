# Amoebius Genome

Search a space that keeps the procedural compactness of .kkrieger while reproducing Amoebius
(Garrett, 2026-11-10): **.kkrieger generalized is the genome; Amoebius defines the fitness of every phenotype.**

- `agenome/ops.py`: the operator table (werkkzeug3 lineage: textures, SDF shapes, agents and prism trails, cameras,
  post, HUD). It is the single source of truth, and `SPEC.md` is generated from it.
- `agenome/genome.py`: the typed op DAG, its byte form (compactness = zlib size), JSON form, repair, mutation and
  crossover.
- `agenome/glsl.py`, `sim.py`, `render.py`: the compiler to GLSL, the numpy agent sim, and the headless moderngl/llvmpipe
  renderer (0.5 to 1.3 s per 4 s clip at 256x144 on one core).
- `agenome/features.py`: the fitness, i.e. distributional distance to Amoebius clips (colour, luma, spectrum,
  gradients, layout, motion, random-filter texture, blobs), normalised by the target set's own spread.
- `agenome/critic.py`: a small JAX CNN retrained during the search on Amoebius frames against the population's latest
  frames; the fitness is the statistics distance plus its term.
- `agenome/judge.py`: the fixed perceptual judge (v3: the worse of InceptionV3 and MobileNetV2 KID against the reels,
  1.0 = Amoebius' own held-out source). `agenome/labscore.py`: the fitness lab's element-aware scorer beside it.
- `agenome/search.py` + `agenome/selection.py`: the population search over (distance to Amoebius, genome bytes):
  hypervolume survival inside a band near the best and a sep-CMA-ES emitter on the leader's parameter bytes
  (`--tune`), with a MAP-Elites side archive and islands that trade fronts through a shared folder (`--share`,
  `--island`). The diamond (COMPLEXIFY/COMPRESS) and random byte tweaks it replaced stay behind `--survival diamond
  --tuner random`; epsilon-lexicase parents and adaptive operator choice are there too (`--parents`, `--operators`)
  but were a wash. Its distance is `--stat-w` statistics + `--critic-w` critic + `--judge-w` judge + `--lab-w` lab
  (off by default). Why, and the A/B that chose each default: `SEARCH.md`.
- `bench/ab_search.py`: that A/B on a synthetic target (a hidden genome's own renders), no reels needed;
  `tests/test_selection.py` tests the selection pieces.
- `agenome/steward.py` + `LEDGER.md`: the steward loop. The search writes `improvements.jsonl` (every child that beat
  its parent and survived, with the edit that did it and how each feature group moved); `python3 -m agenome.steward
  RUN_DIR ...` condenses a run into an evidence pack; a Claude session reads it every few hours, turns what worked
  into hypotheses with predictions, tests each as an A/B arm and records the verdict in `LEDGER.md`.
  `tests/test_steward.py` tests it.
- `agenome/report.py` + `report_page.html`: the Amoebius Genome Lab page (re-renders and re-scores every genome shown).
- `agenome/cli.py`: render, sheet and score commands.
- `island.sh`: start one island in a fresh Linux container.

Shared data lives in `/mnt/project-files/amoebius-genome/` (project files): CONTRACT.md, targets, seeds and runs.
