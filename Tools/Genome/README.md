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
- `agenome/search.py`: the diamond (COMPLEXIFY/COMPRESS) Pareto search with a MAP-Elites side archive, a
  parameter-tuning share (`--tune`) and islands that trade fronts through a shared folder (`--share`, `--island`).
  Its distance is `--stat-w` statistics + `--critic-w` critic + `--judge-w` judge + `--lab-w` lab (off by default).
- `agenome/report.py` + `report_page.html`: the Amoebius Genome Lab page (re-renders and re-scores every genome shown).
- `agenome/cli.py`: render, sheet and score commands.
- `island.sh`: start one island in a fresh Linux container.

Shared data lives in `/mnt/project-files/amoebius-genome/` (project files): CONTRACT.md, targets, seeds and runs.
