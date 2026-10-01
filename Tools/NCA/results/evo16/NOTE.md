# evo16 — hardening the 16/16 evo genome (round 3, 1 Oct 2026)

**Bottom line.** The round-1 genome was already robust: **128/128** on `swarm_eval --full` over 8 seeds
(7, 23, 41, 59, 77, 95, 113, 131) × 3 samples, every sample of every transition passing
(`heldout_round1_genome.json`). Its weakness was never the switches; it was **healing after a vessel
strike** (the jellyfish got WORSE after regrowing) and **size** (every plan filled the 280 cap). This
round keeps 128/128 held-out, roughly doubles mean healing, fixes the jellyfish's shape, and (as a
variant) gives each plan its own size. **It does not reach heal ≥ 0.5 on every plan.**

Heal, measured the low-noise way (`evo16_heal.py`: 8 seeds × 4 strike directions = 32 strikes per plan,
`heal4x8.json`, `headcount/heal4x8_variants.json`), mean heal / mean recovered divergence (lower = closer
to its plan):
| genome | whale | jellyfish | pufferfish | dragonfly |
|---|---|---|---|---|
| round 1 (`results/evo`) | 0.19 / 19.2 | **−0.65** / 25.7 | 0.05 / 16.4 | 0.36 / 26.7 |
| **evo16 primary (this folder)** | **0.29** / 19.1 | −0.22 / 19.8 | **0.42** / 15.1 | **0.39** / 28.2 |
| evo16 `headcount/` (h 0.3) | 0.13 / 19.3 | 0.01 / 20.1 | 0.33 / 14.5 | 0.21 / 29.4 |
| headcount h 0.6 (spec below) | 0.28 / 19.3 | −0.07 / **18.6** | 0.42 / 14.9 | 0.33 / 28.4 |
The jellyfish's heal ratio stays low because a strike hurts a well-formed jellyfish a lot (14.8 → 19.7
divergence); in absolute terms it now recovers to 19.8, where round 1 ended at 25.7 — worse than its cut.

## Primary genome (`genome.npy`, the gen-19 CMA mean)
| yardstick | result |
|---|---|
| strict `tests_passed` (rollout, seed 7, `summary.json`) | **8/8** (summed divergence 182.6) |
| `swarm_eval --full`, seed 7, 3 samples (`eval16.json`) | **16/16**, every rate 1.00 |
| held-out, 8 seeds × 3 samples (`heldout.json`) | **128/128**, every sample rate 1.00 |
| per-transition margin, mean / worst over seeds (`evo16_margins.py`) | 0.415 / 0.205 (round 1: 0.404 / 0.161) |
| `swarm_probe` (seed 11, one strike per plan, `probe.json`) | −0.43 / 0.28 / 0.47 / 0.10 (round 1: 0.46 / −1.17 / 0.94 / 0.60); ±0.5 noise, read the 32-strike table |
| grown headcount | 280 for every plan |
| own-plan divergence, jellyfish | 13.4 at seed 7 (round 1: 20.0); 14.8 mean over 8 seeds (round 1: 21.9) |

## `headcount/` variant (primary + B8 headcount at 1.35× plan size, B8b selective past deficit 0.1)
| yardstick | result |
|---|---|
| strict `tests_passed` / eval16 seed 7 | 8/8 / 16/16 |
| held-out 8 seeds × 3 | **128/128**; sample rates 1.00 except charge→space 0.96, time→space 0.92 |
| margin mean / worst | 0.420 / 0.085 |
| probe (seed 11) | −0.33 / 0.30 / 0.59 / 1.28 |
| grown headcount (seed 7) | whale 280, puffer 280, **jellyfish 172, dragonfly 148** (plans 192 / 179 / 88 / 76) |
| at h 0.6 (`headcount/heldout_h06.json`) | 128/128, worst margin 0.096, jellyfish 225, dragonfly 201 |
Rebuild: `evo16_screen.apply(evo16_model.from_evo(np.load("results/evo16/genome.npy")),
"sw_head=0.5,h_head=0.3,k_head=1.5,sw_hsel=0.5,t_hsel=0.1")` (h_head=0.6 for the larger one).
Size and heal trade off along h: the closer to plan size, the weaker the healing.

## What was built (all new files)
- `evo16_model.py` — `Evo16Rule` = `evo_model.EvoRule` + new behaviours, each with an on/off gene:
  - **B6 regrowth homeostat**: laying × (1 + gain · deficit vs the swarm's own high-water headcount).
  - **B7 wound sensing**: each tadpole keeps an EMA of its neighbour count; one that suddenly has fewer
    is on a wound and lays more, while (in deficit) the un-wounded lay less — "refill where it was cut".
  - **B8 headcount target** per plan (the plan's unit count × e^h): laying gated off past it.
  - **B8b selective headcount** (added after B8 failed): parents whose element is short of the plan's
    share by more than `t_hsel` may lay past the headcount, so a full swarm can still change its mix.
  - B5 output gains (round 1) stay available; never switched on.
  - `stateless = True`; state lives on the model, re-initialised whenever the incoming swarm is not the
    one it produced last step (swarm_eval's batched branches see a fresh model, as a new swarm would).
- `evo16_fit.py` — one batched fitness mirroring swarm_eval (4 own + 12 switches via `cull_to`) AND the
  probe (4 strikes); ~50 s per seed on one core.
- `evo16_search.py` (CMA-ES, pop 12, 2 fresh seeds/gen, held-out every 4 gens), `evo16_screen.py`
  (variants / `--ablate`), `evo16_select.py` (candidates on 12 shared seeds), `evo16_heal.py`
  (K-strike heal), `evo16_measure.py` (swarm_eval at N seeds), `evo16_margins.py`, `evo16_publish.py`,
  `evo16_pool.py` (see the CPU finding).

## Evolution curve (37 dims: behaviour + new genes, from the round-1 genome; `search_log.jsonl`)
fitness = mean pass + 0.5·mean margin + mean(min(heal, 0.5)/0.5) (max ≈ 2.7). Held-out = CMA mean, 4 seeds.
| gen | best | pop mean | pass (pop) | held-out fit / pass / heal m,s,c,t |
|---|---|---|---|---|
| 0 | 2.09 | 1.11 | 0.984 | |
| 3 | 1.50 | 1.01 | 1.000 | 0.38 / 0.984 / .43 −.86 −.50 −.24 |
| 7 | 1.27 | 0.76 | 1.000 | 0.94 / 1.000 / −.20 −.26 .00 .59 |
| 11 | 1.53 | 1.10 | 0.997 | 1.44 / 1.000 / .34 −.65 .79 .76 |
| 15 | 1.72 | 1.15 | 0.952 | 0.67 / 0.984 / −.50 −1.0 .38 .62 |
| 19 | 1.95 | 1.19 | 0.857 | **1.77** / 1.000 / .78 .24 .64 .11 |
| 23 | 1.60 | 1.23 | 0.970 | 0.70 / 0.984 / −.12 −.02 −.55 .10 |
24 generations (~2 h; the container was reclaimed twice mid-run and the search resumed from its pickle).
The curve is flat inside the noise: one strike per plan per seed swings heal by ±0.5 between seed
pairs, so CMA mostly random-walked. The gen-19 mean was chosen on 12 fresh shared seeds (fitness 1.52
vs round-1 0.90 on the same seeds; `select.json`) and confirmed on the 32-strike yardstick above.

What CMA changed (`genome.json`): it **turned B7 and B8 off**, kept B6, and re-shaped the desired-share
table — the jellyfish now wants **28% Charge** (its plan's own share, which round 1 had sharpened away),
so the jellyfish both fits far better and stops degrading after a strike.

## Ablation (8 seeds 6000-6007, fast fitness; heal here is single-strike, ±0.3)
Primary (`ablation.json`):
| variant | fitness | pass | heal m,s,c,t |
|---|---|---|---|
| full | 1.461 | 0.992 | .18 .13 .16 .56 |
| no lay homeostat (B1) | 1.054 | **0.528** | .81 −.11 .45 .50 |
| no egg choice (B2) | 1.428 | 0.983 | .45 −.16 .56 .37 |
| no regrowth (B6) | 1.537 | 0.992 | .19 .10 .28 .52 |
| everything off (= G2) | 0.107 | 0.370 | .16 −.49 −.16 .09 |
Add-ons on the primary (`addon_screen*.json`):
| variant | fitness | pass | heal | note |
|---|---|---|---|---|
| + wound (B7) | 1.186 | 1.000 | −.08 −.19 .47 .14 | helped round 1's whale (0.31→0.79, `screen1.json`), not this genome |
| + headcount at plan size (B8, h 0) | 0.965 | 0.927 | .32 .00 .17 −.49 | **puffer→dragonfly fails 8/8** |
| + B8 1.35× plan, selective at any deficit | 1.499 | 0.984 | .47 .23 .23 .21 | but every size stays 280 |
| + B8 1.35× plan, selective past deficit 0.1 (`headcount/`) | **1.624** | 0.984 | .36 .43 .45 .28 | jelly 172, dragonfly 148 |
`headcount/ablation.json`: removing B1 → pass 0.784; removing B8b (hard cap) → pass 0.922
(puffer→dragonfly 6/8 fail); removing B6 → 1.717 here but 1.485 vs 1.486 on 12 other seeds
(`headcount/select_hs03t1_noreg.json`) — noise.

**Read:** B1 (lay homeostat) carries the switches; B2 is minor; B6 regrowth is neutral (laying is
already saturated after a strike, so a boost has nothing to add); B7 wound sensing does not survive
selection. The heal gain came from the desired-share table, not from a new actuator.

## Findings worth carrying
1. **A hard headcount freezes switching.** A swarm at its plan size stops laying, and laying is the ONLY
   way its element mix can move (nothing may be culled) — so a 179-tadpole puffer culled toward Time
   never grows the Time it needs (8/8 fails). Any size cap must let the elements the swarm is short of
   keep breeding (B8b). With a majority-amplifying desired-share table "short" must mean short by a
   margin (0.1), or the majority element is always short and the cap never binds.
2. **Swarms cannot shrink**, so size carries history: a whale that loses its Mass majority to Space
   collapses with the cull to 177 and stays jelly-sized; a puffer→dragonfly stays 280.
3. **The single-strike heal is too noisy to search on** (±0.5 per seed pair). Use ≥ 4 strike directions
   per plan per seed (`evo16_heal.py`), and report recovered divergence beside the ratio — a well-formed
   body has more to lose, so its ratio understates it (the jellyfish).
4. **CPU**: an unpinned torch process here spreads over all 4 cores even at `set_num_threads(1)` /
   `OMP_NUM_THREADS=1`; four of them run ~18× slower EACH than one alone. `evo16_pool.pinned_pool`
   (sched_setaffinity before importing torch) runs 4 evaluations in the time of one. Pin BEFORE importing
   torch: one diagnostic pinned after the import ran >50× slow (another did not; not understood).
   EvoRule is also ~40× faster on swarm_eval when declared `stateless` (batched branches).
5. The container is reclaimed when the session idles; long runs must be resumable (they are).

## What a player would see
Four schools that look like their animal within a minute and keep doing so at every seed tried. Ram one
with a vessel and a third of it bursts into lime crystals; the gap refills in ~120 steps — the
pufferfish and dragonfly regain much of their silhouette (heal ~0.4), the whale refills but lumpier,
the jellyfish refills into a recognisable but rougher bell (it used to come back as a worse blob than the
wound itself). Eat a school's majority and within ~240 steps it reshapes into the new majority's
animal — all 12 switches, every seed. With `headcount/`, size also reads as species: whale and
pufferfish are big schools, jellyfish and dragonfly visibly smaller, and a whale eaten into a jellyfish
shrinks with it. Motion is the G2 rule's, unchanged.

## Recommendation for the next round
1. **Heal is a shape problem; this genome only controls how many eggs and which element.** Where an egg
   lands is still "away from the local centroid". Cross with field's designed attractor geometry: use the
   field's attractor as the egg-placement direction (and as the target a wound refills toward). That is
   the most promising route to heal ≥ 0.5.
2. Search heal only with `evo16_heal.py` (≥ 4 strikes) and the 16-transition pass as a hard gate.
3. Port B8b (selective headcount, deficit 0.1) to the C# side if species size matters for readability —
   it is production gating only and respects mass conservation; pick h on the size/heal trade-off.
4. Drop B7 as a search gene; B6 may be removed (neutral).
