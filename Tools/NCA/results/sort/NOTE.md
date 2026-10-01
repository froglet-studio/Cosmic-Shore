# sort — emergent cell sorting (positional information + fate + differential adhesion)

**Result: tier 1 passes at the new bar on every seed tried.** Own-plan divergences
**1.40 / 2.44 / 1.05 / 5.03** (whale / jellyfish / pufferfish / dragonfly; `swarm_eval`, 3 samples,
seed 7), and **12 of 13 feasible transitions** at the bar of 8 (tier 1 4/4, standard switches 4/4, other
switches 4/5; three are n/a). The one failure (dragonfly → jellyfish, 8.97 mean, passing in 1 of 3
samples) is domain conservation, explained below. Held-out seeds: see the table at the end.

Code: `Tools/NCA/sort_model.py` (the model), `sort_search.py` (CMA-ES), `sort_publish.py` (this folder),
`sort_ablate.py`, `sort_diag.py` (per-term breakdown), `sort_eval.py` (`swarm_eval.evaluate`, unchanged).
Nothing here edits a shared file. No network and no learning — `params.json` is the whole model.

## What it is

The diagnosis said the outline was fine and the gap was SORTING (which element and which domain sits
where). This direction attacks exactly that, with local rules and no slot assignment:

1. **A tadpole's TYPE = (element, region).** A region is the body part a domain plays in the current plan.
   The swarm picks the domain -> region map from its own census (the scorer minimises over that map
   anyway, so which TEAM is the dorsal side does not matter; that the dorsal side is one team does).
2. **Positional information (Wolpert).** Each type reads a small mixture of Gaussian "morphogen wells" in
   body coordinates (origin = the body's centre, world-fixed axes as polarity cues). The wells are a
   compressed code of the plan: K wells per type, at most one per 4 units of that type (K = 12 published,
   K = 4 compact). Nothing tells a tadpole WHERE in its type's region to go.
3. **Fate commitment.** A newborn commits to ONE well of its type — the one its type currently
   under-occupies (lateral inhibition read through a census). Without this, tadpoles settle in the
   nearest well and far parts of the body (the whale's tail fluke) stay empty: own losses 9–15, 0.7/13.
4. **Local neighbour sorting.** Collision, a (type x type) differential-adhesion matrix within
   `R_adh`, and Potts-style swaps (two touching tadpoles that would each sit better in the other's spot
   slide past one another). CMA-ES turned ALL adhesion repulsive and made it **more repulsive between
   different types (-0.080) than within a type (-0.050)** — Steinberg's differential adhesion in its
   relative form: unlike cells push apart harder than like cells, so like cells end up packed together
   and boundaries between tissues sharpen.
5. **Composition homeostat** (evo's idea, joint over element x region): a parent lays while its class is
   short of the plan and within `fill_tol` of its region's least-filled class; a parent whose class is
   full lays its domain's most-needed element instead (`p_cross`). Total headcount capped at the plan's
   (x `over` = 0.94: the search chose a slightly small body). No non-majority element may ever tie the
   majority through laying or molting (otherwise a 9-tadpole survivor regrowing a whale flips plan).
6. **Switches (relaxed constraints).** Plan = majority element with a 12-step dwell. Surplus tadpoles
   MOLT (re-form their crystal into a deficit element of their own domain); a tadpole whose region has no
   deficit — a domain the plan has too many of, or no region for — may TRANSFER to the neediest region
   (it keeps its domain and pays the colour, but not the element). Domain always breeds true; nothing
   dies on a clock.

## Per-term breakdown (`terms.json`, seed 7)

| plan | pos only | + element | + domain | full | n |
|---|---|---|---|---|---|
| whale | 0.62 | 0.87 | 0.90 | 1.42 | 181 |
| jellyfish | 1.65 | 1.90 | 1.94 | 2.56 | 83 |
| pufferfish | 0.61 | 0.43 | 0.50 | 1.03 | 169 |
| dragonfly | 1.63 | 2.55 | 3.62 | 5.06 | 72 |

Against the diagnosis: G2 7.0 / 13.6 / 10.8 / 6.3 with elements, evo 14.7 / 13.0 / 10.5 / 13.6,
field 0.5 / 0.0 / 0.0 / 1.5. **Sorting closes the gap: adding the element term costs this rule 0.25 / 0.25
/ ~0 / 0.9, against G2's 4–10.** What is left is visuals (constant per type: +0.5) and the dragonfly's
three-region domain split.

## Search (`search_log.jsonl`; CMA-ES, pop 10, 17 continuous genes, one fresh seed per generation, all 16 transitions)

| gen | pop mean fitness | pop mean passed /16 | held-out (CMA mean, 4 seeds) |
|---|---|---|---|
| 0 | 16.9 | 10.1 | |
| 4 | 19.2 | 11.4 | 12, 12, 12, 11 |
| 9 | 20.7 | 11.9 | 12, 12, 12, 12 |
| 14 | 21.8 | 12.8 | 13, 13, 13, 11 |
| **19 (published)** | 21.6 | 12.6 | **13, 13, 13, 12** |
| 29 | 20.9 | 12.0 | 13, 13, 13, 11 |

Compact code (K = 4, `compact_k4/`): 7.3 -> 11.9 mean passed by gen 14; held-out 12, 12, 12, 11;
held-out own losses 2.09 / 2.66 / 1.83 / 5.06. **Untuned, K = 4 failed tier 1 (7.1 / 6.0 / 3.9 / 14.1).**

## Ablations (`ablations.json`, 3 seeds, every plan grown once and every switch branched from it)

| variant | own losses (whale/jelly/puff/dragon) | own passed | passed / feasible | switch loss |
|---|---|---|---|---|
| **published (K = 12)** | 1.35 / 2.55 / 1.21 / 4.57 | 4.0 | **12.7 / 13** | 4.15 |
| no adhesion | 1.71 / 3.73 / 1.66 / 5.53 | 4.0 | 11.3 | 4.91 |
| no swaps | 1.32 / 2.70 / 1.22 / 4.45 | 4.0 | 12.7 | 4.21 |
| no fate (climb the whole mixture) | 15.2 / 12.9 / 9.1 / 15.4 | 0.3 | 0.7 | 13.7 |
| K = 1 (a single French flag per type) | 9.6 / 7.7 / 7.4 / 18.5 | 1.7 | 4.0 | 12.7 |
| K = 2 | 3.4 / 6.1 / 5.3 / 10.5 | 3.0 | 7.7 | 8.4 |
| K = 4 (with K = 12's tuning) | 2.3 / 2.6 / 1.4 / 8.1 | 3.7 | 9.7 | 5.4 |
| K = 8 | 1.8 / 2.6 / 1.4 / 4.2 | 4.0 | 12.7 | 4.2 |
| local centre (no swarm-wide average) | 1.4 / 3.3 / 2.3 / 5.3 | 4.0 | 7.0 | 10.8 |
| Time laps (look option) | 3.1 / 2.6 / 4.7 / 20.3 | 3.0 | 9.0 | 10.1 |
| no molting | 2.0 / 4.1 / 2.4 / 6.6 | 4.0 | 7.0 / 14 | 19.3 |
| molting without region transfer | 1.35 / 2.6 / 1.2 / 4.8 | 4.0 | 9.0 | 7.2 |
| no cross-laying | 1.6 / 16.0 / 8.6 / 13.7 | 2.7 | 8.7 | 9.9 |

Compact K = 4 (`compact_k4/ablations.json`): published 2.1 / 2.4 / 2.0 / 5.4, 11.3/13; **no adhesion
3.1 / 3.9 / 3.1 / 7.5, 8.7/13**; no swaps unchanged; local centre own 4/4 but switches 6.7/13.

What matters, in order: **fate commitment** (the decisive mechanism), **code size** (K >= 8 for the
dragonfly; K = 4 passes tier 1 only once the neighbour terms are tuned), **differential adhesion**
(30% lower own loss at K = 12, and the difference between the compact code passing and the dragonfly
sitting at the bar), **composition** (cross-laying for own plans, molting + transfer for switches).
**Swaps are inert** — CMA kept them, but they change nothing; I would delete them. The local-consensus
centre (each tadpole carries a centre estimate, averages its neighbours' and leaks toward its implied
centre = position minus its fated well) works for growing a plan from scratch but costs switches,
because a culled, fragmented body has no single consensus for several steps.

## Probe (`probe.json`, vessel strike)

| plan | before | killed | right after | after 120 steps | heal |
|---|---|---|---|---|---|
| whale | 1.85 | 78 of 181 | 13.86 | 1.28 | 1.05 |
| jellyfish | 2.56 | 20 of 83 | 8.99 | 2.26 | 1.05 |
| pufferfish | 1.33 | 46 of 169 | 8.23 | 1.03 | 1.04 |
| dragonfly | 4.54 | 28 of 72 | 24.74 | 3.54 | 1.05 |

Every plan heals to slightly BETTER than before (the survivors keep their fates; newborns take the
holes' fates because those wells are now the under-occupied ones — the strike wound is exactly where
the next eggs are fated). Headcount refills to the body's size.

## Old yardstick (`summary.json`, `swarm_nca.rollout`): 7/8

Own 4/4. The failure is dragonfly -> whale: `lose_majority(..., to=Mass)` leaves Space the majority, the
swarm correctly grows a jellyfish (8.72). Same finding as evo and field; on the fair cull it passes
(time -> mass 1.94 in `eval16.json`).

## What failed / limits (honest)

- **dragonfly -> jellyfish (8.97, 1 of 3 samples).** The dragonfly has three domains, the jellyfish
  two regions. The third domain's tadpoles can transfer and molt, but they keep their colour, so
  ~25% of the body pays the domain term. Only a domain change (forbidden) or culling them would fix
  it. Switches INTO the dragonfly cost ~6 for the mirror reason (an empty third region).
- **Visuals are a constant per type** (prism, tier, facing, spindle = the type's mean in the plan).
  That is ~0.5 of every loss. Facing could be decoded from position (outward normal) for free.
- **The code is fit to the plan.** K = 12 wells per type at most one per 4 units is a coarse
  description of each plan, not slots — but it IS designed from the targets, like field's fields.
  The search tunes how a swarm REALISES the code, not the code. K = 1 (a true French flag) fails.
- **The centre is a swarm-wide average by default** (positional information needs an origin). The
  local-consensus centre is implemented and passes tier 1, but costs switches.
- **Scoring is orientation-locked**, so the body never turns or swims here.
- **Laps.** Time runners can advance their fate along a loop through their wells (`lap_every`,
  staggered, sprinting at Time's top speed): dragonfly 5.06 -> 7.2 at `lap_every = 48`, still under 8, but
  every lap setting costs score, so it is OFF for scoring and a look option.

## A provenance mistake (fixed)

The first publish of this folder mixed two candidates: `sort_publish.py` re-read the CMA vector file
for every section while the resumed search was still running and overwrote it (gen-14 best -> gen-19
best) mid-publish. The publisher now loads the vector once; everything here was regenerated from a frozen
copy of the gen-19 vector (`cma_vec.npy`). Earlier commits of `results/sort/` on this branch should be
ignored.

## What a player would see

*Inferred from the mechanics and the logged rollouts; I had no renderer in this session and did not watch
it in the viewer (`rollout.json` is in the viewer's format).*

A seed of 16 tadpoles hatches eggs at a steady clip; every newborn appears beside its parent and then
SWIMS ACROSS THE BODY to the place its fate names — a constant migration of little crystals through
the forming creature, each element threading to its own tissue. Like cells push one another apart less
than unlike cells, so tissues tighten into clean bands and patches with sharp borders (the whale's
two-tone back and belly, the pufferfish's two hemispheres, the jellyfish's rim of Charge octahedra).
A vessel strike tears out a chunk; the wound refills from the inside out, because the next eggs are
fated precisely to the holes — the creature visibly heals in 10–30 steps to better than before. When a
predator eats the majority, nothing pops: after a 12-step hesitation the survivors re-assign regions,
surplus crystals re-form one by one into the new body's elements, and the whole school migrates into a
different animal. It is lively while growing and while healing; at rest it is calm (only collision
jostle), which is the weakness — a resting creature does not swim (see recommendation).

## Recommendation for the next round

1. **Combine sort's fate-committed positional code with field's motion layer.** Field already has
   swimming (the anchor travels and the plan yaws), predator reactions and inflation; sort has no
   global assignment and heals about as well (1.04–1.05 vs field's 0.92–1.07). The wells are just a cheaper
   field: rotate the code with the body exactly as field rotates its slots.
2. **Fate is the transferable idea for the learned rules (G2 / evo).** Give each tadpole a sticky
   discrete fate channel chosen at birth by a census of under-occupied fates, and let the learned rule
   steer by its fate's well. That is the actuator the diagnosis says G2 lacks.
3. **Drop swaps; keep differential adhesion** (tuned negative and type-asymmetric).
4. **Decode facing/prism from position within the well** (outward normal, along-axis): the ~0.5 floor.
5. **The domain-conservation switches need a design call**, not tuning: either a slow domain re-dye
   for orphan regions, or accept that a three-team dragonfly cannot become a two-team jellyfish.
