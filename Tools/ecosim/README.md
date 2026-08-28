# ecosim — headless ecosystem + performance loop

A dependency-free (Python 3, stdlib only) stand-in for "run the menu and watch
FPS + populations" that the agent can run with **no Unity and no C# toolchain**.
It reads the **real** Cosmic Shore config assets, models the cell food-web's heavy
steady state, and estimates frame cost from a physically-motivated model
calibrated to a real measurement — so ecology/perf config can be evaluated before
anyone opens the editor.

```
python3 Tools/ecosim/ecosim.py
```

## What it tells you

- The **as-authored** heavy steady state (prisms pinned at Frenzy, fauna at caps)
  and its predicted FPS — should reproduce the calibration anchor.
- **Per-lever sensitivity**: how FPS moves as you cut the prism ceiling (Frenzy),
  fauna caps, or fauna query radius (cubic on the dominant `OverlapSphere` cost).
- **Named candidate configs** with predicted FPS, so you can pick the lightest
  change that clears 60.

## The performance model (why the menu was 5 fps)

CPU-bound, so one wall-second = 1000 ms of CPU work per second. Two kinds:

- **per-frame** (scales with fps): `BASE + prisms·C_PRISM + fauna·C_FAUNA`
- **fixed-rate** fauna `Physics.OverlapSphere` queries (fire at `1/behaviorPeriod`
  regardless of fps), each touching the prism colliders in its radius:
  `overlap_ms/s = Σ_species (count/period)·prisms·(radius/cellR)³·CLUSTERING·C_OVERLAP`

`fps = (1000 − overlap_ms/s) / frame_fixed_ms`. At the menu's old steady state
(5400 prisms, ~90 fauna, 70 m query radii) the overlap term alone ate ~71 % of the
budget — that is the 5 fps.

## Closing the loop (calibration)

The cost constants are pinned to ONE real data point (`calibration.csv`, first
row = anchor) using documented priors (`OVERLAP_SHARE`, `CLUSTERING`, `BASE_MS`,
`C_FAUNA` in `ecosim.py`). It is a **lever-ranker / budget-setter, not an oracle** —
treat a predicted FPS as "comfortably clears 60 with margin", not a guarantee.

To tighten it, capture real samples in Unity with **`EcosystemPerfProbe`**
(`Assets/_Scripts/Controller/Environment/EcosystemPerfProbe.cs`): add it to a
Menu_Main GameObject (or set the `ECOSIM_PROBE` scripting define to auto-spawn it),
play the menu, and copy its Console lines —

```
[ECOSIM] prisms=1024 fauna=33 fps=72.4
```

— into `calibration.csv` (most-trusted steady-state sample first). Re-running
ecosim then recalibrates against the real numbers. Over a few captures the model's
`C_PRISM` / `C_OVERLAP` split converges on reality.

### The autonomous iteration loop

```
edit Blob config assets  ->  python3 ecosim.py  ->  read predicted fps + levers
                         ->  human plays menu, pastes EcosystemPerfProbe lines
                                                      into calibration.csv
                         ->  ecosim recalibrates    ->  repeat until 60+fps & alive
```

## Model assumptions to refine (top of `ecosim.py`)

- `cell_radius_m` (600) — the Blob membrane radius; affects collider density.
- `CLUSTERING` (18) — fauna query dense regions, not mean density.
- `OVERLAP_SHARE` (0.72) — share of budget the overlap term eats at the anchor.
- `GRAZE_PER_HERBIVORE_S`, flora/trail rates — only affect the (secondary)
  oscillation dynamics, not the heavy-steady-state perf estimate.

All are one-line edits; the probe samples are the real ground truth.
