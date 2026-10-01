# evo16 — hardening the 16/16 evo genome (round 3, 1 Oct 2026)

**Bottom line.** The round-1 genome was already robust: **128/128** on `swarm_eval --full` over 8 seeds
(7, 23, 41, 59, 77, 95, 113, 131) × 3 samples, every sample of every transition passing. Its weakness
was never the switches, it was **healing after a vessel strike** (jellyfish −0.62 mean heal over 12
seeds) and **size** (every plan filled the 280 cap). This round keeps 128/128 held-out, makes the
jellyfish heal positive, and gives each plan a different size. It does **not** reach heal ≥ 0.5 on
every plan: the honest mean over 12 fresh seeds is 0.36 / −0.07 / 0.41 / 0.32 (whale / jelly / puffer /
dragonfly) for the published genome and 0.35 / 0.14 / 0.46 / 0.19 for its sibling `g19/`.

## Published genome (`genome.npy`) = gen-19 CMA mean + a selective headcount
| yardstick | result |
|---|---|
| strict `tests_passed` (rollout, seed 7) | **8/8** (summed divergence 189.4) |
| `swarm_eval --full`, seed 7, 3 samples (`eval16.json`) | **16/16**, every rate 1.00 |
| held-out, 8 seeds × 3 samples (`heldout.json`) | **128/128**; sample rates 1.00 except charge→space 0.96, time→space 0.92 |
| per-transition margin, mean / worst over seeds (`evo16_margins.py`) | 0.420 / 0.085 (round 1: 0.404 / 0.161; `g19/`: 0.415 / 0.205) |
| `swarm_probe` (seed 11, `probe.json`), heal m / s / c / t | −0.33 / 0.30 / 0.59 / **1.28** (round 1: 0.46 / −1.17 / 0.94 / 0.60) |
| heal over 12 fresh seeds (`select_hs03t1.json`), mean | 0.36 / −0.07 / 0.41 / 0.32 |
| grown headcount (seed 7) vs plan | whale 280/192, jelly **172**/88, puffer 280/179, dragonfly **148**/76 |

`g19/` (the gen-19 CMA mean without the headcount) is the safer sibling: 128/128 held-out with every
sample passing and the better worst-case margin (0.205), heal 0.35 / 0.14 / 0.46 / 0.19 over 12 seeds,
probe −0.43 / 0.28 / 0.47 / 0.10, but every plan at 280. **The two are within noise of each other on
fitness (1.486 vs 1.522 over the same 12 seeds); pick `g19/` if margin matters more than size.**

The single probe numbers above disagree with the 12-seed means by up to ±0.9 — read the means.

## What was built (all new files)
- `evo16_model.py` — `Evo16Rule` = `evo_model.EvoRule` + new behaviours, each with an on/off gene:
  - **B6 regrowth homeostat**: laying × (1 + gain · deficit vs the swarm's own high-water headcount).
  - **B7 wound sensing**: every tadpole keeps an EMA of its neighbour count; one that suddenly has fewer
    is on a wound and lays more, while (in deficit) the un-wounded lay less — "refill where it was cut".
  - **B8 headcount target** per plan (init = the plan's unit count × e^h): laying gated off past it.
  - **B8b selective headcount** (added after B8 failed, below): only parents whose element is short of
    the plan's share by more than `t_hsel` may lay past the headcount, so a full swarm can still change
    its mix after losing its majority.
  - `stateless = True` (swarm_eval batches the switch branches); state lives on the model and is
    re-initialised whenever the incoming swarm is not the one it produced last step.
- `evo16_fit.py` — one batched fitness that mirrors swarm_eval (4 own + 12 switches, `cull_to`) AND the
  probe (4 strikes) in two batches per seed: ~50 s per seed on one core.
- `evo16_search.py` (CMA-ES, pop 12, 2 fresh seeds/gen, held-out every 4 gens), `evo16_screen.py`
  (variants / `--ablate`), `evo16_select.py` (candidates on 12 shared seeds), `evo16_measure.py`
  (swarm_eval at N seeds, per-transition table), `evo16_margins.py`, `evo16_publish.py`,
  `evo16_pool.py` (see the CPU finding).

## Evolution curve (stage 1: behaviour + new genes, 37 dims, from the round-1 genome)
fitness = mean pass + 0.5·mean margin + mean(min(heal, 0.5)/0.5); max ≈ 2.7. Held-out = CMA mean on 4 seeds.
| gen | best | mean | pass (pop mean) | held-out fit / pass / heal m,s,c,t |
|---|---|---|---|---|
| 0 | 2.09 | 1.11 | 0.984 | |
| 3 | 1.50 | 1.01 | 1.000 | 0.38 / 0.984 / .43 −.86 −.50 −.24 |
| 7 | 1.27 | 0.76 | 1.000 | 0.94 / 1.000 / −.20 −.26 .00 .59 |
| 11 | 1.53 | 1.10 | 0.997 | 1.44 / 1.000 / .34 −.65 .79 .76 |
| 15 | 1.72 | 1.15 | 0.952 | 0.67 / 0.984 / −.50 −1.0 .38 .62 |
| 19 | 1.95 | 1.19 | 0.857 | **1.77** / 1.000 / .78 .24 .64 .11 |
| 23 | 1.60 | 1.23 | 0.970 | 0.70 / 0.984 / −.12 −.02 −.55 .10 |
The curve is flat inside the noise: one strike per plan per seed makes heal swing ±0.5 between seed
pairs, so CMA mostly random-walked. The gen-19 mean was then confirmed on 12 fresh seeds (1.52 vs
round-1 0.90 on the same seeds; `select.json`). Search log: `search_log.jsonl`.

What CMA actually changed (`genome.json`): it **turned B7 wound sensing and B8 headcount OFF**, kept
B6 regrowth, and re-shaped the desired-share table — notably the jellyfish now wants **28% Charge**
(its plan's own share; round 1 had sharpened it away), which is what makes the jellyfish both fit
better (own-plan divergence 22.9 → 12.4 in the probe) and heal positively.

## Ablation (8 seeds, fast fitness, `ablation.json` = published genome, `g19/ablation.json` = sibling)
`g19/` (seeds 6000–6007):
| variant | fitness | pass | heal m,s,c,t |
|---|---|---|---|
| full | 1.461 | 0.992 | .18 .13 .16 .56 |
| no lay homeostat (B1) | 1.054 | **0.528** | .81 −.11 .45 .50 |
| no egg choice (B2) | 1.428 | 0.983 | .45 −.16 .56 .37 |
| no regrowth (B6) | 1.537 | 0.992 | .19 .10 .28 .52 |
| everything off (= G2) | 0.107 | 0.370 | .16 −.49 −.16 .09 |
Add-ons on `g19/` (same seeds, `addon_screen*.json`):
| variant | fitness | pass | heal | note |
|---|---|---|---|---|
| + wound (B7) | 1.186 | 1.000 | −.08 −.19 .47 .14 | helped the round-1 genome's whale (0.31→0.79, `screen1.json`), not this one |
| + headcount at plan size (B8, h=0) | 0.965 | 0.927 | .32 .00 .17 −.49 | **puffer→dragonfly fails 8/8** |
| + B8 at 1.35× plan, selective, any deficit | 1.499 | 0.984 | .47 .23 .23 .21 | but sizes stay 280 |
| **+ B8 at 1.35× plan, selective past deficit 0.1 (published)** | **1.624** | 0.984 | .36 .43 .45 .28 | jelly 172, dragonfly 148 |
Published genome (same seeds, `ablation.json`):
| variant | fitness | pass | heal m,s,c,t |
|---|---|---|---|
| full | 1.624 | 0.984 | .36 .43 .45 .28 |
| no lay homeostat (B1) | 0.756 | **0.784** | .58 −.33 .20 −.40 |
| no egg choice (B2) | 1.168 | 0.992 | .24 −.13 .36 −.01 |
| no regrowth (B6) | 1.717 | 0.992 | .53 .48 .61 .08 |
| no headcount (B8) (= `g19/`) | 1.461 | 0.992 | .18 .13 .16 .56 |
| headcount but not selective (no B8b) | 1.535 | **0.922** | .28 .31 .52 .29 — puffer→dragonfly fails 6/8 |
| everything off (= G2) | 0.107 | 0.370 | .16 −.49 −.16 .09 |
"No regrowth" looks better here but is identical on the 12 selection seeds (1.485 vs 1.486,
`select_hs03t1_noreg.json`) — it is noise, and B6 is kept only because it costs nothing.

**Read:** B1 (the lay homeostat) carries the switches; B2 is minor; B6 regrowth is neutral within
noise (the swarm refills to its cap without it — laying is already saturated after a strike); B7
wound sensing does not survive selection. The heal gain is from the desired-share table, not from a
new actuator.

## Findings worth carrying
1. **A hard headcount freezes switching.** A swarm sitting at its plan size stops laying, and laying is
   the ONLY way its element mix moves (nothing may be culled) — so a 179-tadpole puffer culled toward
   Time never grows the Time it needs (8/8 fails). Any size cap must let the elements the swarm is
   short of keep breeding (B8b). With a majority-amplifying desired-share table, "short" must mean
   short by a margin, or the majority element is always short and the cap never binds.
2. **The heal metric is the bottleneck for search, not the model.** One strike per plan per seed is
   ±0.5 noise; average ≥ 3 strike directions per plan per seed (cheap: +8 rows in the same batch)
   before spending another CMA run on heal.
3. **Swarms cannot shrink**, so size reflects history: a whale that loses its Mass majority to Space
   collapses with the cull to 177 and stays jelly-sized (good), but a puffer → dragonfly stays 280.
4. **CPU**: an unpinned torch process here spreads over all 4 cores even at `set_num_threads(1)` /
   `OMP_NUM_THREADS=1`; four of them run ~18× slower EACH than one alone. `evo16_pool.pinned_pool`
   (sched_setaffinity before importing torch) runs 4 evaluations in the time of 1. Pin BEFORE importing
   torch: one diagnostic pinned after the import ran >50x slow (another did not; not understood). EvoRule is also ~40× faster on swarm_eval when declared
   `stateless` (batched branches) — the round-1 "194 s per seed" was the unbatched path.
5. The container is reclaimed when the session goes idle; long CMA runs must be resumable (they are).

## What a player would see
Four schools that look like their animal from the first minute and keep doing so at any seed. A
**whale** and a **pufferfish** are big (280 heads); a **jellyfish** (~170) and a **dragonfly** (~150)
are visibly smaller, so size now reads as species at a glance. Ram one with a vessel and a third of it
vanishes into lime crystals: the dragonfly closes the wound fastest (probe heal 1.28 — it regrows into a
better dragonfly than before), the pufferfish recovers most of its silhouette, the jellyfish partly, and
the whale refills but lumpier than it was (its heal is the weakest, −0.3 to +0.4 depending on where it is
hit). Eat a school's majority and within ~240 steps it reshapes into the new majority's animal — every
one of the 12 possible switches, at every seed tested. Motion is the G2 rule's (unchanged).

## Recommendation for the next round
1. **Fix the heal yardstick before searching on it**: 3–4 strikes per plan per seed (different
   directions) in the batched fitness; report the mean. Then a CMA run on heal alone with the pass
   rate as a hard gate (reject any candidate < 1.0 on its seeds).
2. **Combine the published genome with field's attractor geometry for healing** — heal is a shape
   problem (where eggs land), and this genome only controls how many and which element. Field's
   designed attractors place units; feeding the attractor as the egg-placement direction (instead of
   "away from the local centroid") is the most promising cross.
3. Make B8b's headcount the default in the C# port (it is production-gating only, respects mass
   conservation, and makes species size readable); keep the 0.1 deficit threshold.
4. Drop B7 wound sensing as a search gene; if wanted, re-try it only with the better heal yardstick.
