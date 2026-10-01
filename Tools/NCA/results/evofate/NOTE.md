# evofate — the evolved rule, given a FATE

**Result: the evolved rule now passes the bar and stays organic at every seed.** The published model
(**C3**, `params.json`, found by CMA-ES on the full yardstick) passes **tier 1 at every seed tried** and
**12 / 11 / 12 of 13 feasible transitions** at seeds 7 / 23 / 41 under the loss-8 bar, is **lossless**
(0 self-inflicted deaths), costs **5.0 ms/step** at its grown size (126 tadpoles, 1 thread), and sits
**inside the organic band of `swarm_feel` at all three seeds** (the previous pick, C2, now in `c2/`, was out on
planar excess at seed 23). Motion numbers next to evo's own:

| | own losses (whale / jelly / puffer / dragonfly), seed 7 | 16-test | jitter | coherence | osc | jerk_rel | planar excess | organic band |
|---|---|---|---|---|---|---|---|---|
| evo (results/evo) | 19.5 / 19.8 / 14.9 / 24.8 | 0/16 | 2.53 | 0.12 | 0.058 | 1.93 | – | in |
| sort (results/sort) | 1.40 / 2.44 / 1.05 / 5.03 | 12/13 | – | – | – | – | – | – |
| **evofate C3 (published, CMA v3)** | **2.68 / 3.61 / 1.47 / 5.92** | **12/13** | **2.62** | **0.43** | **0.024** | **2.30** | **0.053** | **in (3/3 seeds)** |
| evofate C2 (`c2/`, hand-placed) | 2.95 / 3.65 / 1.58 / 6.28 | 12/13 | 2.57 | 0.40 | 0.044 | 2.44 | 0.119 | in (2/3 seeds) |
| evofate C1 (`c1_cma/`, CMA v2 point) | 3.20 / 4.01 / 2.29 / 6.30 | 11/13 | 2.45 | 0.40 | 0.060 | 2.31 | 0.123 | in |

Scorecard (`scorecard.json`): `accurate 0.85 worst seed | lossless True (0.0/1k) | 5.041 ms/step @126 |
organic True | mixed`. **Feel at every seed** (`feel_heldout.json`, official `swarm_feel` at the seed itself):
seed 7 osc 0.024 / planar excess 0.053 / jerk_rel 2.30; seed 23 0.029 / 0.086 / 2.34; seed 41 0.022 / 0.011 / 2.37
— all in band, with margin on every check (C2 was 0.192 planar at seed 23). Own plans at the three seeds:
2.68/3.61/1.47/5.92, 2.72/3.73/1.36/5.86, 2.60/3.92/1.55/5.30 — all twelve lower than C2's at the same seed.
The failures are C2's, unchanged: **dragonfly → jellyfish at every seed** (10.38 / 10.38 / 8.76) and
mass → dragonfly at seed 23 (8.16, a hair over). Old yardstick (`summary.json`): **7/8** (dragonfly → whale,
the known `lose_majority` artefact). C3 vs C2 in genes: a weaker pull (1.43 vs 2.0) with a smaller per-step
gradient, `k_well` 0.21 vs 0.41 and `well_clip` 0.84 vs 0.52 — a gentler but longer-reaching pull), tighter wells
(`cov_scale` 0.69 vs 0.89), more adhesion (0.66 vs 0.35, now affordable because the softer pull left jerk_rel room),
dead zone 1.73 vs 1.5, Time genes about the same (te0 0.27, tmix 0.62).

Code (all new files, nothing shared edited): `evofate_model.py` (the model), `evofate_fast.py`
(batched 8-test screen), `evofate_curve.py` (pull-strength curve + feel), `evofate_full.py` (full
16-transition yardstick + feel per config), `evofate_search.py` (CMA-ES with a feel term, screen fitness), `evofate_search2.py` (CMA-ES v3: full 16-transition
yardstick + official feel as fitness — found C3),
`evofate_publish.py` (this folder; the scorecard split across processes).

## What it is

`EvoFate` subclasses evo's `EvoRule`: **G2's trained weights and the evo genome are unchanged**
(`genome.npy` is a copy of `results/evo/genome.npy`). Per step:

1. **Motion = G2.** The learned rule writes every state channel and proposes each tadpole's
   velocity, exactly as in evo (per-element swirl from the evo genome kept). Its learned DEATH is
   suppressed (`no_death`), and its own laying is off.
2. **FATE** (sort's actuator, the brief's request). Each hatched tadpole holds a sticky discrete
   fate — one of sort's positional-information wells for its (element, region) type in the current
   plan (`sort_model.PlanCode`, K = 12 wells per type, ≤1 per 4 units). A stale fate (newborn, new
   plan, new region, molted) is chosen by a census of the type's under-occupied wells. Fates live on
   the model, not in `sw.s`, because G2 rewrites every channel every step.
3. **A small designed steering term** pulls each tadpole up its fated well:
   `pos += pull · clip(−k_well · ∇E_fate, well_clip)`, with three shaping terms that turned out to
   be the whole story for the FEEL (see "What made it organic"):
   * **dead zone** `e0`: no pull while the well's energy (½ Mahalanobis²) is under `e0`, full by
     2·e0 — inside its well a tadpole is pure G2;
   * **rectifier** `rect`: while outside the dead zone, G2 may not move a tadpole AWAY from its
     well (the outward component of its learned velocity is removed) - kept in `params.json`, but
     the ablation shows it is inert in C2;
   * **sync**: the designed move happens only on the steps the tadpole's G2 cell fires (×2, same
     mean pull).
   Plus a weak **differential adhesion** (sort's Steinberg matrix ×0.35) and two Time-only genes:
   the Time runners' dead zone is 0.3× the others' (`te0`) and their G2 speed is ×0.7 (`tmix`).
4. **Look**: the visual channels (facing, prism, tier, spindle) are set to the type's look from
   sort's code; the hidden channels stay G2's.
5. **Composition, designed** (the round-3 cross-family finding: designed composition + local shape):
   sort's joint (element × region) lay homeostat with a headcount cap, cross-laying, molting and
   region transfer for switches. Domain always breeds true. Nothing dies on a clock.

Locality: **mixed** — positional information needs the body's centre (a swarm-wide average), the
role map and the fate census read the swarm's own counts; everything else is local.

## The trade-off curve (fate pull vs own-plan loss vs feel)

Pure fate pull, no shaping (`curve_screen.json`, batched screen, seed 7; feel = official
`swarm_feel` at seed 7; "+sw" = the 4 standard fair-cull switches):

| pull | own losses | own + sw passed | osc | planar excess | coherence | jitter | band |
|---|---|---|---|---|---|---|---|
| 0.0 (no fate) | 22.7 / 25.3 / 20.5 / 34.3 | 0 + 0 | 0.022 | 0.0 | 0.44 | 2.65 | in |
| 0.1 | 18.2 / 14.2 / 11.5 / 31.1 | 0 + 0 | 0.153 | 0.0 | 0.41 | 1.60 | out |
| 0.2 | 12.7 / 8.2 / 6.6 / 23.7 | 1 + 1 | 0.149 | 0.055 | 0.44 | 1.35 | out |
| 0.35 | 7.5 / 6.1 / 3.2 / 14.6 | 3 + 2 | 0.159 | 0.249 | 0.50 | 1.31 | out |
| 0.5 | 5.8 / 3.6 / 2.4 / 10.3 | 3 + 3 | 0.150 | 0.345 | 0.52 | 1.18 | out |
| 1.0 | 4.0 / 4.0 / 1.5 / 6.2 | 4 + 4 | 0.120 | 0.430 | 0.62 | 0.98 | out |
| 2.0 | 4.0 / 4.8 / 1.5 / 5.6 | 4 + 4 | 0.091 | 0.444 | 0.68 | 0.82 | out |
| 3.0 | 3.3 / 4.9 / 1.6 / 5.7 | 4 + 4 | 0.129 | 0.472 | 0.66 | 0.82 | out |

**Fate is the decisive actuator** (0 → 4/4 own by pull 0.75–1.0), but a naive pull buys accuracy by
spending the organic texture: jitter halves (2.65 → 0.8), coherence climbs, the body packs into flat
tissue sheets (planar excess 0.43), and — the surprise — **any pull at all, even 0.1, triples the
reversals** (osc 0.02 → 0.15), which is the "jerky" signature the lead disliked in evo compact.

Full yardstick + feel for every shaped variant tried: `full_yardstick_table.md` /
`full_yardstick_runs.jsonl` (24 rows). The frontier, seed 7:

| variant | 16-test | own losses | osc | planar ex. | jerk_rel | band |
|---|---|---|---|---|---|---|
| pull 2, dead zone 1.5 (all elements) | 9/13 | 3.75 / 4.46 / 2.35 / 7.6 | 0.079 | 0.134 | 1.88 | in |
| + Time dead zone 0 | 11/13 | 3.70 / 4.48 / 2.00 / 6.49 | 0.140 | 0.481 | 2.41 | out |
| + adhesion 1.0 | 12/13 | 2.25 / 3.48 / 1.53 / 5.39 | 0.072 | 0.240 | 2.77 | out |
| adhesion 1.6 (accuracy end) | 12/13 | **2.12 / 3.30 / 1.43 / 4.99** | 0.086 | 0.200 | 3.00 | out |
| adhesion 1.0, Time dead zone 0.4 | 12/13 | 2.26 / 3.41 / 1.40 / 6.10 | 0.043 | 0.0 | 2.75 | out (jerk_rel) |
| CMA point (C1) | 11/13 | 3.20 / 4.01 / 2.29 / 6.30 | 0.060 | 0.123 | 2.31 | in |
| adhesion 0.35, Time dead zone 0.4 | 11/13 | 2.95 / 3.64 / 1.64 / 6.96 | 0.045 | 0.076 | 2.42 | in |
| **adhesion 0.35, Time dead zone 0.3, Time speed 0.7 (C2)** | **12/13** | 2.95 / 3.65 / 1.58 / 6.28 | 0.044 | 0.119 | 2.44 | **in** |

The two ends: the most accurate rule (adhesion 1.6: own losses below sort's on jelly, close on the
rest, 12/13) is *more* brownian than evo (jerk_rel 3.0 > the band's 2.5); the most evo-like rule
(osc 0.05, planar 0) loses the dragonfly. C2 is the knee.

## What made it organic (the mechanisms, in the order found)

1. **The reversals come from G2's firing rhythm, not from the pull's strength.** G2 updates a
   random half of the tadpoles each step (fire rate 0.5). A pulled tadpole moves out on a fired step
   and is pulled straight back on the next idle step — a >120° reversal. Fate momentum (`f_inertia`)
   makes it WORSE (osc 0.12 → 0.14–0.19). **`sync`** (pull only on fired steps) removes the
   alternation (osc 0.112 → 0.044 in the ablation). The **rectifier** (G2 may not push a tadpole
   away from its well) looked like the natural partner and helped a little before sync existed, but is
   inert in C2 (ablation: identical numbers) — delete it in a port.
2. **The dead zone gives the swarming texture back.** Inside its well a tadpole is pure G2, so the
   tissue keeps evo's jitter (2.4–2.7) instead of being packed to well centres. It also removes the
   planar excess (0.43 → ~0.1): the sheets were tadpoles compressed against their well centres.
3. **The dragonfly needs its own genes.** Its Time runners zip at vmax 2 through thin wings: a full
   dead zone lets them drift off (dragonfly 9.9, fails); no dead zone flattens the wings (dragonfly
   planar 0.97 vs the plan's 0.49). A Time dead zone of 0.3× and Time G2 speed ×0.7 hold both.
4. **Differential adhesion sorts but jitters.** It lowers every own loss (sort's Steinberg finding
   again), but each extra neighbour force adds high-frequency jerk: adhesion 1.0 → jerk_rel 2.75,
   0.6 → 2.56, 0.35 → 2.42. 0.35 is the largest in band.

## Ablations (run on C2, the mechanism set C3 shares; full yardstick seed 7 + feel; `ablations.jsonl`)

| variant | 16-test | own losses | osc | planar ex. | jerk_rel | jitter | coherence | band |
|---|---|---|---|---|---|---|---|---|
| **C2 (published before CMA v3)** | 12/13 | 2.95 / 3.65 / 1.58 / 6.28 | 0.044 | 0.119 | 2.44 | 2.57 | 0.40 | in |
| no fate (pull 0) | 0/13 | 15.17 / 13.13 / 11.89 / 26.74 | 0.009 | 0.0 | 2.012 | 2.748 | 0.405 | in |
| no dead zone | 12/13 | 2.07 / 3.73 / 1.50 / 5.19 | 0.324 | 0.424 | 2.743 | 1.535 | 0.163 | out |
| no fire-sync | 12/13 | 3.02 / 3.65 / 1.60 / 6.32 | 0.112 | 0.163 | 1.869 | 1.673 | 0.548 | out |
| no rectifier | 12/13 | 3.07 / 3.53 / 1.58 / 6.28 | 0.045 | 0.115 | 2.4 | 2.548 | 0.403 | in |
| no adhesion | 11/13 | 3.69 / 4.30 / 2.12 / 7.16 | 0.081 | 0.263 | 2.265 | 2.27 | 0.41 | out |
| no Time genes (te0 1, tmix 1) | 8/13 | 3.10 / 3.83 / 1.72 / 8.99 | 0.043 | 0.0 | 2.426 | 2.573 | 0.4 | in |
| G2's own visuals (look 0) | 12/13 | 2.77 / 4.29 / 1.76 / 5.28 | 0.101 | 0.187 | 2.538 | 2.47 | 0.21 | out |
| no molting | 7/15 | 3.60 / 5.08 / 1.88 / 7.22 | 0.049 | 0.152 | 2.429 | 2.519 | 0.398 | out |

Reading it: **fate is the whole accuracy** (0/13 without it). **The dead zone and fire-sync are the whole
feel**: without the dead zone accuracy is unchanged (12/13, dragonfly even better) but reversals explode
(osc 0.32), jitter halves and the body packs flat; without sync osc triples (0.112) and coherence jumps to
0.55. **The rectifier is inert once those two are on** (identical numbers) - delete it. **Molting is the
switches** (7/15 feasible without it: own plans pass, the switches fail - the old majority's surplus stays the wrong element). **Adhesion is worth one transition and the
planar margin.** **The Time genes are the dragonfly** (8.99 without them, 8/13). Surprise: **the designed look is part of the motion** - writing the type look into the visual channels changes what G2 perceives; with G2's own visuals
the own losses barely move but osc rises to 0.10 and coherence halves (0.21): the designed look calms G2.

## Probe (`probe.json`, C3, vessel strike, 120 steps to heal)

| plan | before | killed | right after | after 120 | heal |
|---|---|---|---|---|---|
| whale | 2.49 | 46 of 181 | 8.05 | 2.48 | 1.00 |
| jellyfish | 4.15 | 19 of 83 | 11.59 | 4.31 | 0.98 |
| pufferfish | 1.29 | 47 of 169 | 6.88 | 1.44 | 0.97 |
| dragonfly | 6.88 | 24 of 72 | 16.47 | 4.24 | 1.28 |

Against evo's 0.46 / −1.17 / 0.94 / 0.60 (C2: 1.03 / 0.84 / 1.01 / 1.44). Fates make healing directional: the
wound's wells are now the under-occupied ones, so the next eggs are fated INTO the hole. Headcount refills to the
body. The dragonfly heals to better than it was (its pre-strike body had a few Time runners stuck off-wing; the
regrowth re-fates them).

## CMA v3 (`cma_v3_log.jsonl`, `evofate_search2.py`)

CMA-ES (pop 8, σ 0.5) around C2 over 8 genes (adhesion, Time dead zone, Time speed, pull, dead zone, cov_scale,
k_well, well_clip), **fitness = the full `swarm_eval` yardstick** (16 transitions, 3 samples, loss-8 bar) on a
fresh seed per generation (4000 + gen) **+ 0.5 × mean own margin − 3 × graded organic-band violation** (official
`swarm_feel` at that seed: osc over 0.07, planar excess over 0.13, jerk_rel over 2.45). ~4.2 min per generation
on 4 cores; 8 generations in the first run. In-band 12/13 at gens 0, 1, 3; in-band 13/13 (on those seeds)
at gens 6 and 7. C3 = gen 7's best; it and the gen-7 search mean were then re-scored at seeds 7/23/41
(`feel_heldout.json`): both 12/11/12, both in band at all three seeds (the mean: planar 0.036 / 0.140 / 0.089 —
less margin), so the best was published. Direction the search took from C2: **softer per-step pull, longer reach,
tighter wells, more adhesion** — the softer pull is what bought the room for adhesion inside the jerk_rel bound.
It did not move the two failing transitions: they are structural (below), not a tuning residue.

**Resumed for two more generations** (gens 8–9; stopped at 22:16 UTC for time). Gen 9's best — **G9**:
pull 0.98, dead zone 1.17, adhesion 0.76, cov_scale 0.47, k_well 0.35, well_clip 0.62, te0 0.32, tmix 0.53 —
re-scored at seeds 7/23/41 (`cma_v3_validation.jsonl`): **12 / 12 / 12 of 13** (passes mass → dragonfly at seed 23
too) with own losses **1.93/2.75/1.26/4.75, 1.94/2.68/1.17/4.88, 1.73/3.30/1.32/4.37** — sort's are
1.40/2.44/1.05/5.03, so G9 beats sort on the dragonfly — **but out of the organic band on planar excess at all three
seeds** (0.166 / 0.211 / 0.153 against the 0.15 bound; osc 0.053–0.058 and jerk_rel 2.40–2.48 in band, jitter 2.50–2.56).
So the full-yardstick frontier now reads:

| point | 16-test (7/23/41) | own losses seed 7 | osc | planar ex. (7/23/41) | band |
|---|---|---|---|---|---|
| **C3 (published)** | 12 / 11 / 12 | 2.68 / 3.61 / 1.47 / 5.92 | 0.024 | 0.053 / 0.086 / 0.011 | in, 3/3 |
| G9 (accuracy end) | 12 / 12 / 12 | 1.93 / 2.75 / 1.26 / 4.75 | 0.055 | 0.166 / 0.211 / 0.153 | out (planar), 0/3 |

The step from C3 to G9 is tighter wells (cov_scale 0.69 → 0.47) and a smaller dead zone (1.73 → 1.17): the tissue packs
closer to its wells, which is accuracy and flatness together — the same trade the pull curve showed, now at the far
better end of it. G9 is the one to show if the lead says the planar check is too strict for these plans.

## What a player would see

*Inferred from the metrics and the logged rollouts; I had no renderer this session (`rollout.json`
is in the viewer's format — please watch it).* A seed hatches and grows into a cloud that churns like
evo's — same jitter, same restless gas-like texture, almost no reversals (osc 0.044 vs evo's 0.058)
— but the colours SETTLE: each element drifts into its tissue band and the domains into their
regions, so the whale's two-tone back and belly, the pufferfish's hemispheres and the jellyfish's
Charge rim become legible while every crystal keeps fidgeting inside its tissue. The motion is less
school-like than field or hgrid (coherence 0.40 vs 0.71–0.77) and more school-like than raw evo
(0.12) because tadpoles inside one well share a drift. Time runners are calmer than evo's (×0.7
speed), so the dragonfly reads as a shape rather than a spray. Strike it and the wound refills from
the inside out. A switch is a visible re-sort: molting crystals change colour in place and migrate
to their new tissue.

## What failed / limits (honest)

- **dragonfly → jellyfish fails at every seed** (8.9–10.5): three domains into two regions; the
  third domain's tadpoles keep their colour (domain breeds true). Same wall as sort.
- **Feel margin: fixed in C3** (in band at all 3 seeds; C2 was out on planar at seed 23). Three seeds is
  still a small sample for a band that C2 left at one of them.
- **The organic band is tight at the accurate end.** Every rule with own losses near sort's broke the
  band on jerk_rel (too brownian) or planar (too flat). C3's own losses are ~1.2 above sort's on the
  whale/jelly, ~0.9 on the dragonfly.
- **Coherence 0.40 is not evo's 0.12.** Even with no fate at all (pull 0) the evofate body reads 0.44:
  that is the composition change (sort's plan-sized body, 72–181 tadpoles, vs evo's 280-filled
  blob), not the fate. The band does not judge coherence; the lead's eye should.
- **CMA-ES found the published point only in v3.** Earlier runs (`cma_v1_log.jsonl`, `cma_log.jsonl`): v1 searched
  on code without sync/rectifier and traded the feel away; v2 found C1 (in band, 11/13) by gen 2,
  then the structure was clear enough to place C2 by hand from the full-yardstick table (24 rows).
  v3 (above) then ran that full-yardstick search around C2 and found C3.
- **Designed parts**: composition, the fate census, the wells (sort's code, fit from the targets),
  and the steering shape are designed; G2 is the only learned part. The search tuned how the two
  meet, not the code.
- Positional information reads the swarm centroid (mixed locality). sort showed a local-consensus
  centre passes tier 1 but costs switches.

## Recommendation for the next round

1. **Ready for the lead's second scene test? Yes** — C3 is the first evolved-rule swarm under the bar
   (12/11/12 of 13, tier 1 at all seeds), lossless, 5.0 ms/step, and in the organic band at every seed tried,
   with fewer reversals than evo itself (osc 0.024 vs 0.058). The lead's eye should judge coherence (0.43 vs
   evo's 0.12) — the one evo number it does not match, and a composition effect, not a fate effect. Port path:
   G2 inference already ran in the game for the first scene test; add the per-tadpole fate (one int),
   sort's wells (a table per plan) and the four steering terms (dead zone, rectifier, fire-synced
   pull, weak adhesion) — all O(N) per step except adhesion (neighbour list G2 already builds).
2. **Combine with hgrid2's grid instead of sort's centroid wells** to make it LOCAL: hgrid2 reads a
   coarse class-deficit grid + per-class morphogen at the tadpole's own position; its morphogen could
   replace the well gradient and its deficit grid the census, removing the swarm-wide centre.
3. **Continue CMA v3** (`evofate_search2.py` resumes from `runs/evofate/cma3/state.pkl`): it was still
   improving at gen 7 (13/13 in band on two fresh seeds). Fitness on 2 seeds per generation would cut noise.
4. **dragonfly → jellyfish** needs a rule decision, not tuning: either let a region accept a
   foreign domain's colour (a relaxed constraint) or let the third domain form a distinct appendage.
