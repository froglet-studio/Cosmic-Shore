# Direction A — the substrate

ONE simulation (agents + stigmergy fields + quorum + bodies) that every species is a parameter set of.
Program: `../PROGRAM.md`. Results and recommendation: `../DISCOVERIES.md` § Substrate.

| file | what |
|---|---|
| `core.py` | the substrate: `Regime` (one end of the phase axis), `SpeciesParams`, `BodyPlan`, `Substrate` (numpy reference path) |
| `fields.py` | stigmergy grids per cell (food = a read of live prism volume; alarm, threat, per-species trail), separable blur + decay |
| `kernels_nb.py` | numba kernels: cell-moment neighbours, context choice, the **fused Burst-shaped step**, eat query, exact-pair neighbours (ablation) |
| `species.py` | grazer, locust, pack, leviathan, lurker, stampede — parameter sets only |
| `metrics.py` | encounter metrics (heading-to-pilot, pursuit, near fraction, ring sd) beside the shared scorecard |
| `laws.py` | per-step audit: mass conserved, every death has an active cause, no pop-in/out, no teleport |
| `quorum_demo.py` | the locust claim: one param set, cute sparse+fed vs frightening dense+hungry, and the emergent band |
| `assembly_demo.py` | a school condensing into a manta made of its members and dissolving again |
| `run_species.py` | the shared threat/feel scorecard for every species |
| `frac_study.py`, `blend_study.py`, `nbr_study.py` | fractional update / attention LOD, intent low-pass, moments vs exact neighbours |
| `bench.py` | cost per agent-step, numpy / numba / fused, N = 1k..100k, k = 1/4/8 → `bench.json` |
| `make_viewer.py` | the playback viewer (`viewer_sample.html` is the committed small sample; `--full` writes `out/`) |
| `DESIGN_BURST.md` | the Unity port: Burst job chain, GPU compute alternative, main-thread cost |
| `run_all.sh` | re-run everything (two lanes, then the bench alone) |

Run from `Tools/Ecology`: `python3 -m substrate.<module>`. Needs `numpy` (+ `numba` for the fast backends).

A species is two `Regime`s plus a quorum rule:

```python
locust = SpeciesParams(solitary=Regime(speed=26, w_curious=1.0, crowd=0.5, ...),
                       gregarious=Regime(speed=95, w_hunt=1.8, crowd=2.0, ...),
                       q_w_dens=1.0, q_up=0.55, q_down=0.30)       # flip on density x hunger, hysteresis
```

Every weight the steering reads is `lerp(solitary, gregarious, phase)`, and `phase` relaxes toward a
hysteretic sigmoid of the quorum signal `(q_w_dens*density + q_w_prox*pilot_proximity + q_w_alarm*alarm) *
hunger^q_hunger`, plus contagion from neighbours. No branch selects a behaviour.
