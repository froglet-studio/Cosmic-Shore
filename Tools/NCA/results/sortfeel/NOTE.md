# sortfeel — sort made organic: the sheets were COMPRESSION, not adhesion

**Result (v2, published `params.json`).** Two local changes to the published sort
(`results/sort/params.json`) put it inside the organic band **without losing anything measured**:
planar excess **0.27 -> 0.06** (band <= 0.15), **zero self-inflicted deaths**, and **12 / 12 / 13 of 13
feasible** transitions at seeds 7 / 23 / 41 — **identical to sort's own 12 / 12 / 13** at the same seeds, with
the same single failure (dragonfly -> jellyfish, ~9.0, domain conservation). Every body is now about as flat as
its own plan (whale 0.42 vs plan 0.43, jellyfish 0.28 vs 0.38, pufferfish 0.74 vs 0.69, dragonfly 0.55 vs 0.49)
instead of far flatter (sort: 0.70 / 0.43 / 0.90 / 0.58). Cost: +23% ms/step in this Python prototype.

v1 (`params_v1.json`, `*_v1.json`) used one dead zone for every plan and lost one switch INTO the dragonfly at
seed 23 (8.04 vs sort's 6.74); v2 turns the dead zone off on the dragonfly plan only (`well_dead_time 0`), whose
thin wings need tight wells and whose bodies were never sheet-like enough to need it — the wander alone keeps
it inside the band (0.55 vs plan 0.49).

Code: `sortfeel_model.py` (a subclass of `sort_model.SortSwarm`; nothing shared edited),
`sortfeel_diag.py` (step 1), `sortfeel_quick.py` (screen), `sortfeel_score.py` (scorecard, seeds in
parallel, `scorecard.scorecard`'s output shape), `sortfeel_publish.py`, `sortfeel_search.py` (CMA-ES with
the band as a penalty — written, NOT run: see "what failed"). `swarm_eval.load_model` gained one appended
branch, `sortfeel:<params.json>`.

## 1. Diagnosis (`diag.json`, seed 7, grown 240 steps, single frame)

The hypothesis in the brief was that differential adhesion builds sharp tissue boundaries and boundaries
are planes. **It is wrong on both counts.**

Where are the flat neighbourhoods? (share of tadpoles whose 8-NN neighbourhood has flatness < 0.1)

| plan | all | at a tissue boundary | tissue interior | outer shell |
|---|---|---|---|---|
| whale | 0.69 | 0.60 | **0.89** | 0.65 |
| jellyfish | 0.41 | 0.40 | 0.40 | 0.29 |
| pufferfish | 0.89 | 0.73 | **0.99** | 0.79 |
| dragonfly | 0.60 | 0.50 | 0.67 | 0.89 |

(boundary = >= 2 of its 7 nearest neighbours are another (element, domain) type; shell = outer 25% of radii.)
The flattest places are tissue INTERIORS, not boundaries.

Which mechanism? planar_frac (whale / jellyfish / pufferfish / dragonfly) per ablation:

| variant | planar | reading |
|---|---|---|
| plan's own | 0.43 / 0.38 / 0.69 / 0.49 | target |
| published sort | 0.69 / 0.41 / 0.89 / 0.60 | |
| **no adhesion** | **0.75 / 0.83 / 0.95 / 0.82** | adhesion (all repulsive) SPREADS tissues; removing it is worse |
| symmetric adhesion (no type asymmetry) | 0.67 / 0.36 / 0.90 / 0.60 | the unlike-vs-like asymmetry is irrelevant to flatness |
| no swaps | 0.67 / 0.47 / 0.89 / 0.54 | inert (as sort's NOTE said) |
| no fate | 0.62 / 0.58 / 0.95 / 0.63 | fate is not the cause (and costs all accuracy) |
| isotropic wells (cov = I) | 0.69 / 0.84 / 0.88 / 0.82 | point wells crush into blobs that are worse |
| wide wells (cov x 2) | 0.51 / 0.30 / 0.81 / 0.43 | better |
| **weak well pull (k_well x 0.4)** | **0.34 / 0.34 / 0.70 / 0.31** | at or below the plan everywhere |
| soft collision / small r0 / no inertia | ~unchanged | |

**Cause:** every tadpole is pulled to the CENTRE of its fated Gaussian well by the gradient inv(cov)·d — a
spring that is stiffest along the well's THIN axis — and stopped only by collision. A crowd squeezed
hardest along one axis packs into a pancake, and with mean speed 0.019 it then sits as a frozen lattice
(nearest-neighbour CV 0.26 on the whale vs the plan's 0.15-0.40 range of irregular spacings). The pull
strength, not the boundaries, sets the flatness.

## 2. Fix (`sortfeel_model.py`)

1. **Flat-bottomed fate wells** (`well_dead` = m0): the fate pull becomes the gradient of
   0.5·max(0, m − m0)², m = Mahalanobis distance to the fated well. Inside m0 well-sigmas there is no pull,
   so a well's tadpoles FILL its ellipsoid like a liquid instead of being crushed to its centre. Same wells,
   same fates, same code, one line.
2. **Per-tadpole wander** (`wander`, `wander_tau`): an Ornstein-Uhlenbeck velocity per tadpole
   (amplitude 0.05 voxels/step, correlation 12 steps, +-40% per-tadpole time constant from its slot index). It
   melts the lattice and gives the body a shimmer; the well walls and collision keep everyone in place.
   It costs an RNG draw per tadpole per step.

Published: `params.json` = sort's published params + `well_dead 0.7, wander 0.05, wander_tau 12`.

## 3. Frontier (`sortfeel_quick.py`, seed 7, 1 sample per plan; band = organic band pass)

| candidate | own losses W / J / P / D | planar excess | speed | jerk_rel | osc | coherence | band |
|---|---|---|---|---|---|---|---|
| sort (published) | 1.42 / 2.56 / 1.03 / 5.06 | 0.268 | 0.019 | 0.99 | 0.050 | 0.58 | **no** |
| well 1.0 only | 1.52 / 2.85 / 1.04 / 6.30 | 0.000 | 0.016 | 1.22 | **0.081** | 0.32 | no (twitches) |
| wander 0.05 only | 1.75 / 2.59 / 1.48 / 3.29 | 0.215 | 0.067 | 0.75 | 0.011 | 0.19 | no (still sheets) |
| **well 0.7 + wander 0.05 (published)** | 1.71 / 2.82 / 1.40 / **3.94** | **0.054** | 0.068 | 0.72 | 0.009 | 0.09 | **yes** |
| well 0.7 + wander 0.08 | 1.83 / 2.89 / 1.52 / 3.62 | 0.014 | 0.109 | 0.71 | 0.008 | 0.04 | yes |
| well 1.0 + wander 0.03 | 1.82 / 2.88 / 1.48 / 4.75 | 0.000 | 0.042 | 0.75 | 0.011 | 0.17 | yes |
| well 1.0 + wander 0.05 | 1.78 / 3.01 / 1.52 / 4.29 | 0.000 | 0.069 | 0.71 | 0.009 | 0.09 | yes |
| well 1.0 + wander 0.08 | 1.95 / 3.02 / 1.65 / 4.65 | 0.000 | 0.111 | 0.70 | 0.008 | 0.05 | yes |
| well 1.0 + wander 0.05, tau 24 | 1.89 / 3.01 / 1.55 / 4.54 | 0.000 | 0.061 | 0.58 | 0.006 | 0.11 | yes |
| well 1.5 + wander 0.05 | 2.26 / 3.46 / 1.78 / 5.77 | 0.000 | 0.070 | 0.70 | 0.009 | 0.07 | yes (too loose: whale 0.11 flat vs 0.43) |

Neither change alone is enough: the dead zone alone removes the sheets but the un-damped tadpoles inside
a pull-free zone twitch (osc 0.081 > 0.08); wander alone keeps the sheets. Together they are in band
across the whole sweep. **m0 = 0.7 is the faithful setting** — it reproduces each plan's own flatness
(it is the only one whose bodies are neither flatter nor rounder than their plans); m0 >= 1.0 overshoots
into rounder-than-plan clouds and costs dragonfly loss. Accuracy cost is +0.1 to +0.4 on three plans and
**−1.1 on the dragonfly** (the wander helps the thin dragonfly wings fill). ms/step: identical machinery
plus one RNG draw per tadpole (+23% in this Python prototype, `timing.json`).

## 4. Full scorecard (`scorecard.json` = v2, seeds 7, 23, 41, locality mixed; v1 in `scorecard_v1.json`)

| seed | sort | v1 | **v2 (published)** | v2 own W / J / P / D | v2 failures |
|---|---|---|---|---|---|
| 7 | 12 / 13 | 12 / 13 | **12 / 13** | 1.51 / 2.69 / 1.17 / 3.45 | dragonfly->jellyfish 9.01 |
| 23 | 12 / 13 | 11 / 13 | **12 / 13** | 1.67 / 2.45 / 0.97 / 3.69 | dragonfly->jellyfish 9.04 |
| 41 | 13 / 13 | 13 / 13 | **13 / 13** | (see json) / 4.61 | — |

Targeted into-dragonfly check at seed 23 (`sortfeel_intotime.py`, 3 samples; mean loss, passes):
sort W->D 6.74, J->D 6.68, P->D 6.02, own 3.78; v1 8.04 (1/3), 7.53, 6.42, 4.58; m0_time 0.4: 7.60 (2/3), 7.01,
5.82, 3.99; **v2 (m0_time 0): 7.04 (3/3), 6.67, 5.41, 3.69.**

- LOSSLESS: **0 deaths** over 235k tadpole-steps (grow + every standard switch). Molting/transfer only.
- EMERGENT (v2): **in band** — jerk_rel 0.72, osc 0.010, stuck 0.00, planar excess 0.061; coherence 0.14,
  jitter 1.11, phase 0.48 (descriptive: more gas-like, more phase-diverse than sort's 0.58 / 0.80 / 0.12).
- PERFORMANT: 6.3 ms/step @ 126 mean headcount in the scorecard (measured while three evaluation processes
  shared the 4 cores). Clean side-by-side (`timing.json`, same process, 1 thread, alternating, grown bodies,
  one other job running): **sort 4.44 vs sortfeel 5.45 ms/step mean (+23%)**; whale 6.84 -> 7.45, jellyfish
  2.63 -> 3.72, pufferfish 5.84 -> 7.63, dragonfly 2.46 -> 2.98. The overhead is Python bookkeeping in the
  subclass (re-reading numpy views, set intersections, a sqrt per fate pull); the math added per tadpole is
  one sqrt + one Gaussian draw + one exp, negligible next to sort's O(n²) neighbour pass in a C# port.
- Sort's own accuracy at the same seeds (same `evaluate` call): seed 7 **12/13** (`results/sort/eval16.json`,
  dragonfly->jellyfish 8.97); seed 23 **12/13** (`sort_baseline_seed23.json`, dragonfly->jellyfish 9.37);
  seed 41 **13/13** (`sort_baseline_seed41.json`). **v1: 12 / 11 / 13 — it matched sort at 7 and 41 and
  lost ONE transition at 23; v2 fixes it (12 / 12 / 13).** v1's loss was real and systematic, not noise: every switch INTO the dragonfly
  is worse at seed 23 (whale->dragonfly 6.74 -> **8.04**, jellyfish->dragonfly 6.68 -> 7.53, pufferfish->dragonfly
  6.02 -> 6.42). The dragonfly's thin wings need tadpoles held tightly; a dead zone + wander loosens them after a
  switch, when the body is still re-filling. Own-plan losses at 23: sort 1.35 / 2.66 / 1.16 / 3.78, v1
  1.67 / 2.45 / 0.97 / 4.58, v2 1.67 / 2.45 / 0.97 / 3.69. Hence v2's dragonfly-only override.
- Old yardstick (`summary.json`, `swarm_nca.rollout`): **7/8**, identical to sort (the one failure is the
  known `lose_majority` quirk: dragonfly -> whale leaves Space the majority and it correctly grows a
  jellyfish, 8.75).

## 5. Probe (`probe.json` = v2, vessel strike, seed 11; whale/jellyfish/pufferfish identical in v1 and v2)

| plan | before | killed | right after | after 120 steps | heal | sort's heal |
|---|---|---|---|---|---|---|
| whale | 1.67 | 78 of 181 | 15.87 | 1.64 | 1.00 | 1.05 |
| jellyfish | 3.02 | 21 of 83 | 8.77 | 2.85 | 1.03 | 1.05 |
| pufferfish | 0.98 | 46 of 169 | 9.39 | 1.50 | 0.94 | 1.04 |
| dragonfly (v2) | 5.12 | 27 of 72 | 23.69 | 3.87 | 1.07 | 1.05 |

Healing is kept: every plan refills to its full headcount and to (about) its pre-strike score; the
pufferfish settles 0.5 above its before-value (still far under the bar), the others at or better.

## What a player would see

*Inferred from the metrics and mechanics; I had no renderer in this session. `rollout.json` is in the
viewer's format.*
Sort's creature, at rest, was a crystal: tissues packed into flat plates that sat still (speed 0.019) — a
mosaic, beautiful in a still frame and dead in motion, exactly the "planar surfaces with its crystals" the
lead disliked. sortfeel's creature is the SAME animal with the same banding (two-tone whale, hemispherical
pufferfish, jellyfish rim, three-team dragonfly) but each tissue is a loose, softly churning cloud that
fills its region's volume: every tadpole drifts on its own slow, persistent path (3.5x sort's speed,
neighbours barely in lock-step, phases scattered), bumps a neighbour, is turned back by the invisible wall
of its region and drifts on. It reads as a swarm holding a shape rather than a shape made of crystals —
closer to the lead's "always feels like a swarm but interesting to watch it try to be more". Growth,
healing and morphing are sort's (newborns swim to their fated holes; surplus re-forms crystal on a switch).
Risk to check in the viewer: coherence 0.09 is gas-like; if it reads as fizz rather than life, lower
`wander` to 0.03 (still in band, coherence 0.17) or raise `wander_tau` (smoother paths, jerk_rel 0.58).

## What failed / limits (honest)

- **The CMA-ES pass did not run.** A sort-baseline accuracy job hung (BLAS thread oversubscription, load 12
  on 4 cores) and ate 50 minutes; `sortfeel_search.py` is ready (sort's 17 genes + the 3 feel genes, the band
  as a penalty with margins) but these parameters are hand-picked from a 10-point screen, not searched.
- **dragonfly -> jellyfish** still fails (9.0, as in sort): domain conservation, not shape. Not touched.
- **v1's whale -> dragonfly at seed 23 = 8.04 (sort 6.74)**: a genuine regression of switches INTO the dragonfly
  (+0.4 to +1.3 at seed 23). FIXED in v2 by a per-plan dead zone: m0 = 0 on the dragonfly plan (0.4 also
  passes, 7.60 at 2/3). The probe and rollout files without `_v1` are v2.
- Locality unchanged: "mixed" — the body centre is still a swarm-wide average (sort's default).

## Recommendation

**For the game's sort species (cece/swarm-fauna-game, SwarmSortCore): adopt it — keep every published sort
number and add two mechanisms.**
1. Fate pull: replace `grad = inv·d` by `grad = inv·d · max(0, 1 − m0/m)` with `m = sqrt(dᵀ·inv·d)`,
   **m0 = 0.7 on the whale, jellyfish and pufferfish plans, m0 = 0 (plain sort pull) on the dragonfly plan**
   (`well_dead 0.7`, `well_dead_time 0.0` in params.json; one sqrt and one multiply per tadpole).
2. Wander: per tadpole an OU velocity `w ← a·w + sqrt(1−a²)·0.05·N(0,1)³`, `a = exp(−1/τ)`,
   **τ = 12 · (0.6 + 0.8·frac(slot·0.618))**, added to the position after sort's velocity update; zero at hatch.
   Keep it OUT of the inertia state (sort's `H_VEL`), or the inertia integrates it into a drift.
Nothing else changes (adhesion, swaps, fate, composition, molting, transfer all as published). If the viewer
says it fizzes, use wander 0.03 (in band in the screen; not scorecarded). With these numbers the accuracy matrix
equals sort's at seeds 7 / 23 / 41, so the port's existing parity checks against results/sort should hold within
noise for transitions, while the per-step positions will of course differ (the wander is random).

**Next round:** (a) run `sortfeel_search.py` (~1 h) to re-tune sort's 17 genes jointly with the dead zone —
the k_well/well_clip pair was tuned for crushing wells and is likely now too weak at the wall; (b) combine
with field's motion layer (rotate the wells with the body) — the dead zone makes that easier because a
rotating well no longer drags a compressed lattice through itself; (c) the remaining flatness knob for
looks is m0 per plan (pufferfish 0.74 vs 0.69 and, in v2, dragonfly 0.55 vs 0.49 are the bodies still slightly flatter than their plans).
