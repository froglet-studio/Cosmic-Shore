# Direction E — hierarchical ecology

Two levels, one world. Flora is a voxel field both levels share (never LOD'd). Fauna live as **macro cohorts**
per region far from pilots and as **agents** near them. See `Tools/Ecology/DISCOVERIES.md` § "Hierarchical
ecology" for findings and the recommended game architecture.

| file | what |
|---|---|
| `params.py` | every number (biology shared by both levels, fitted macro rates, LOD radii, planted-bug switch) |
| `world.py` | regions × voxels, flora + skeleton + soil, the closed mass budget |
| `macro.py` | cohort (Escalator Boxcar Train) population dynamics: integer counts, additive moments N / ΣE / ΣE² |
| `macro_bins.py` | the FIRST macro level (stomach bins) — kept as the recorded negative result |
| `micro.py` | agents (SoA): grazing, chase / flee, migration, births, starvation |
| `sim.py` | `HierSim`: LOD manager (expand / absorb / arrive), impostors, ledger, summaries |
| `calibrate.py` | fits the macro rates FROM micro: occupancy, Holling a/h, hops, plus probes |
| `sweep.py`, `longrun.py` | macro-only parameter sweeps and long runs (plots in `results/`) |
| `viewer.py` | records a run around pilots → `results/hierarchy_viewer.html` |
| `cost/kernels.c`, `cost/measure.py` | compiled cost reference + game-scale cost run |
| `tests/` | the three GATES, each with planted-bug negative controls |

```
cd Tools/Ecology
python -m hierarchy.tests.test_conservation    # mass + headcount across expand/absorb/arrive
python -m hierarchy.tests.test_consistency     # macro-for-T-then-expand vs micro-for-T
python -m hierarchy.tests.test_continuity      # nothing pops in or out where a pilot can see
python -m hierarchy.longrun --T 28800 --snap 60 --tag x
python -m hierarchy.cost.measure
python -m hierarchy.viewer
```
Needs numpy, scipy, matplotlib; a C compiler for the cost kernels.
