# Amoebius Genome

Search a space that keeps the procedural compactness of .kkrieger while reproducing Amoebius
(Garrett, 2026-11-10): **.kkrieger generalized is the genome; Amoebius defines the fitness of every phenotype.**

- `agenome/ops.py`: the operator table (werkkzeug3 lineage: textures, SDF shapes, agents and prism trails, cameras,
  post, HUD). It is the single source of truth, and `SPEC.md` is generated from it.
- `agenome/genome.py`: the typed op DAG, its byte form (compactness = zlib size), JSON form, repair, mutation and
  crossover.
- `agenome/glsl.py`, `sim.py`, `render.py`: the compiler to GLSL, the numpy agent sim, and the headless moderngl/llvmpipe
  renderer (about 0.5 s per 4 s clip at 256x144).
- `agenome/features.py`: the fitness, i.e. distributional distance to Amoebius clips (colour, luma, spectrum,
  gradients, layout, motion, random-filter texture, blobs), normalised by the target set's own spread.
- `agenome/search.py`: the diamond (COMPLEXIFY/COMPRESS) Pareto search with a MAP-Elites side archive.
- `agenome/cli.py`: render, sheet and score commands.

Shared data lives in `/mnt/project-files/amoebius-genome/` (project files): CONTRACT.md, targets, seeds and runs.
