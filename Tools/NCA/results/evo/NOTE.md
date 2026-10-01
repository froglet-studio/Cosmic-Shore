# evo — gradient-free search over the swarm rule (and over its BEHAVIOURS)

Direction: evolution strategies on what we actually want (the strict 8-test yardstick over the full
480-step horizon) instead of truncated BPTT on a proxy loss. Branch `cece/swarm-x-evo`.

## Headline

**8/8 strict tests on the shared yardstick** (`swarm_nca.rollout`, seed 7, `swarm_nca.tests_passed`,
both unchanged) — and **8/8 on every one of 16 held-out seeds** (`robust.json`). The trained G2 rule
alone scores 5/8 on the yardstick and 4.25/8 mean on the same held-out seeds. Same 280-tadpole swarms,
no deaths, no extinctions.

What did it: not weight-tuning. The winning genome ADDS one behaviour the backprop rule had no actuator
for — a **composition homeostat on laying** — and CMA-ES tuned it in ~30 minutes of CPU.

This folder's top level is the headline model (stage 2). `stage1/` (behaviour genes only), `compact/`
(a no-neural-net rule) and `stage2/` (= top level, with its own ablations) are the other runs.
`compare.json` scores all of them on one shared set of 12 unseen seeds.

## What was built (all new files; nothing shared was edited)

| file | what |
|---|---|
| `Tools/NCA/evo_model.py` | `EvoRule` = the G2 rule (weights untouched) + a GENOME (108 floats): behaviour switches, behaviour parameters, a desired-composition table, output-layer gain/bias. Batched `fast_rollout` (the yardstick protocol, all four plans in one batch, + a batched vessel-strike probe + a liveliness measure). |
| `Tools/NCA/evo_search.py` | CMA-ES (pycma), population 10–12, 2 fresh rollout seeds per generation shared by the population (common random numbers), 4 worker processes x 1 torch thread, held-out check (8 unseen seeds) every 5 generations, resumable. |
| `Tools/NCA/evo_compact.py` | Track (b): a compact rule with **no neural network**, 31 evolved numbers (see below). |
| `Tools/NCA/evo_publish.py` | Writes `summary.json` (yardstick rollout), `rollout.json` (`swarm_nca.pack`, exactly what swarm_gpu.py's publisher writes — the viewer plays it), `probe.json` (`swarm_probe.probe`), `genome.npy/json`, `robust.json` (16 held-out seeds + ablations). |
| `Tools/NCA/evo_compare.py`, `evo_motion.py` | Shared-seed comparison (tests / heal / liveliness) and per-element motion stats. |

### The behaviours (genome switches — "do we only fiddle with weights, or add and subtract behaviours?")
Each behaviour has an on/off gene; CMA-ES can delete it by pushing the gene negative.

- **B1 lay homeostat.** A parent's laying probability is multiplied by
  `sigmoid(k·deficit/0.1 + b) / sigmoid(b)`, where `deficit` = (desired share of the parent's element
  under the swarm's current majority, table `D`) − (current share). Production gating only: nothing
  is culled, nothing dies on a timer.
- **B2 egg choice.** A share of eggs take the element the swarm is shortest of (`softmax(β·deficit)`).
  Domain always breeds true.
- **B3 majority lock.** Hysteresis on which majority the homeostats steer by.
- **B5 output re-weighting.** Per-output-channel gain and bias on the MLP's last layer (motion, hatch,
  death, prism, laying gate) — the "fiddle with weights" half.
- **B6 swirl.** Per-element rotation about the plan's long axis (a new motion actuator, for liveliness).
- `D` (16 numbers): the desired element share for each majority, initialised from the plans' mixes.

### Fitness
`tests passed (strict) + 0.5·mean per-test margin` where the margin is
`(best other − wanted)/(best other + wanted)` in [−1, 1] (−1 if the swarm is under 32 tadpoles).
Stage 2 adds `+0.5·mean probe heal` (vessel strike, 120 steps of regrowth) and `+0.5·mean liveliness`
(8-step heart displacement over the plan's per-frame speed, per element, capped at 1).

## Evolution curves (held-out = 8 unseen seeds, tests passed per seed)

Stage 1 — behaviour genes only (25 dims), from the default genome, ~31 min:

| gen | best fit | mean fit | mean passed (train) | held-out, CMA mean | held-out, gen best |
|---|---|---|---|---|---|
| 4 | 6.65 | 5.73 | 5.65 | 7 7 6 7 7 7 7 7 | 6 6 5 5 6 7 6 7 |
| 9 | 7.17 | 6.69 | 6.55 | 7 7 7 7 7 6 7 7 | 7 7 6 7 7 7 7 7 |
| 14 | 8.20 | 6.96 | 6.80 | 8 7 8 8 8 8 8 8 | 7 7 8 8 8 8 8 7 |

Stage 2 — all 107 genes (+ output layer + swirl), from stage 1, fitness + heal + liveliness, 33 gens / 1.6 h:

| gen | best fit | mean fit | mean passed (train) | held-out, CMA mean | held-out, gen best |
|---|---|---|---|---|---|
| 4 | 8.67 | 8.24 | 7.79 | 8 8 8 8 8 8 8 8 | 8 8 8 8 8 8 8 8 |
| 14 | 8.70 | 8.48 | 7.96 | 8 8 8 8 8 8 8 8 | 8 8 8 8 8 8 8 8 |
| 24 | 8.81 | 8.27 | 7.71 | 8 8 8 8 8 8 8 7 | 8 8 8 8 8 8 8 8 ← kept (held-out 8.73) |
| 29 | 8.80 | 8.56 | 8.00 | 8 8 8 8 8 8 8 8 | 8 8 8 8 8 8 8 8 |

Compact (no NN), 31 dims, ~35 min: held-out 6.4 → 7.6 → **8.0** (gen 14, kept) → 7.75.

## The best genome (genome.json)
Behaviours kept by evolution: **lay homeostat, egg choice (8.7% of eggs), output re-weighting, swirl**.
**Deleted by evolution: the majority lock** (its margin evolved to ~1% of the headcount, i.e. off — every
run drove it there; a lock makes the swarm refuse the switch right after a cull).
Lay gate k = 3.0, b = −0.51. The `D` table became a **majority amplifier**:

| majority | desired C M S T | plan's own mix |
|---|---|---|
| Charge | 0.70 0.02 0.02 0.27 | 0.73 0.01 0.09 0.17 |
| Mass | 0.01 0.88 0.09 0.02 | 0.10 0.66 0.13 0.11 |
| Space | 0.27 0.05 0.64 0.04 | 0.28 0.02 0.61 0.08 |
| Time | 0.01 0.10 0.01 0.89 | 0.03 0.04 0.24 0.70 |

Read it as: *whatever element leads, breed more of it* — a positive feedback that turns a one-tadpole
lead after a cull into a decisive new majority within ~100 steps, so the composition never drifts back
(the exact failure the backprop runs had). The second element of each row is set so the NEXT switch the
yardstick asks for is the natural one (see the finding below).

## Ablations (16 held-out seeds, mean strict tests passed)

| genome | stage 2 | stage 1 |
|---|---|---|
| best | **8.00** | 7.81 |
| without egg choice | 7.69 | 7.19 |
| without lay homeostat | 5.00 | 5.44 |
| without output re-weighting | 8.00 | – |
| without swirl | 8.00 | – |
| all behaviours off (= G2) | 4.25 | 4.25 |

The lay homeostat carries ~3 of the 4 gained tests. Output re-weighting and swirl do not change test
results; they were bought for heal and liveliness (next table).

## Shared-seed comparison (12 unseen seeds, `compare.json`)

| model | tests | probe heal | liveliness | Time runners |
|---|---|---|---|---|
| G2 (trained, BPTT) | 4.33 | 0.04 | 0.48 | 0.21 |
| stage 1 (behaviour genome) | 7.92 | −0.20 | 0.48 | 0.20 |
| **stage 2 (headline)** | **8.00** | **0.43** | **0.64** | 0.25 |
| compact (no NN, 31 numbers) | 7.75 | 0.41 | 0.33 | 0.17 |

## Probe (swarm_probe.probe on the headline)
Strike removes 65–108 of 280. heal = mass **1.46** (ends better than before the strike), space 0.18,
charge — (the strike did not worsen its score: 19.9 → 18.2), time **1.49**. All refill to 280.

## The yardstick finding (please read before the next round)
Test 8 (dragonfly → whale) **contradicts the dragonfly's own composition.** The dragonfly plan's second
element is Space (24%); Mass is 4%. `lose_majority(..., to=Mass)` culls Time until Mass leads Time — but
then Space leads everything, so a swarm that grows a faithful dragonfly correctly becomes a JELLYFISH
(that is what the brief asks for: switch to the new majority's plan). Stage 1 sat at 7/8 on exactly this
test until CMA moved `D[Time]` to carry Mass (10%) above Space (1%). That is yardstick-fitting, not
biology. The same applies, more weakly, to whale→jelly (Charge must stay below Space). The other 6
tests are honest. **Fix for the yardstick:** cull with `mode="ratio"` toward the NEW plan's mix, or
choose `to` = the grown swarm's actual second element.

Also honest: in the geometry-only table (element and domain costs zeroed) the headline's dragonfly is
by SHAPE marginally closer to the pufferfish (11.46 vs 11.92); the other three own-plan rows are
shape-correct. Composition (`w_elem = 60`) still carries the scores — the homeostat wins mostly by
fixing composition, and the G2 network does the shape.

## What a player would see
A 280-tadpole cloud that settles into its element's body plan, mostly a slow churn in place (hearts move
~0.1–0.3 per step; G2 was the same). When a player kills off the majority element, the swarm *visibly
converts*: the cull opens 120–210 free slots and they refill to the 280 cap in under 40 steps, almost
entirely with the new leading element (measured, seed 42: the new element's share goes 0.36→0.58,
0.44→0.62, 0.47→0.70, 0.34→0.73; with the behaviours off it goes 0.16→0.19, 0.49→0.50, 0.45→0.39,
0.06→0.05). The crystal mix of the cloud tips over and the shape re-forms into the new plan. Then the
composition FREEZES: a full swarm lays nothing and nobody dies. So the switch is decided in one refill
burst — see `graze.json` for whether it survives gradual, one-at-a-time predation. A vessel strike leaves a
bite that refills from the edges in ~120 steps. Weak spots: the dragonfly's Time "runners" do not run
laps (liveliness 0.25 of the plan's speed) — the cloud reads as a body, not as a creature with
fast-moving parts. Stage 3 (running) targets exactly that.

## Compact rule (results/evo/compact) — a distinct lifeform candidate
No neural net. Each tadpole: swim toward its home = centroid + scale·(μ_g + L_g·u), where (μ_g, L_g) is
a per-frame Gaussian for its (element, domain) group in the current majority's plan (fitted once from
the plan data, like the plan mixes) and u is the tadpole's private random anchor; plus per-element swirl,
jitter, the same lay homeostat, and a body cap. 31 evolved numbers. Yardstick **7/8** (whale→jelly flips
back on a near-tie), **7.75/8 held-out**, heal 0.41. It is cheap (no MLP), fully explainable and
trivially portable to C#/Burst — but its shapes are Gaussian blobs, so geometry alone does not separate
the plans well; it rides on composition.

## Recommendation for the next round
1. **Adopt the lay homeostat as a platform behaviour** (it is production gating, so it sits inside the
   no-imposed-death law). It is the single biggest lever found by any direction here: +3.6 tests on
   G2 with zero retraining. Put it (and egg choice) into the BPTT training loop so the network learns
   shape *given* a composition controller, instead of fighting composition drift.
2. **Fix the yardstick's switch targets** (finding above) before anyone optimises further against it.
3. **Combine:** (a) the BPTT direction that has the best SHAPE (geometry-only table) + (b) this
   homeostat + (c) ES fine-tuning of the full-horizon objective with heal/liveliness terms. ES is cheap
   here: ~20 s per 4-plan rollout on one core, 4 cores, ~1 generation / 2.5 min.
4. For liveliness, the network needs an actuator for coordinated motion (B6 swirl is a crude one); the
   loss/yardstick never rewards motion, so no direction will get lively swarms for free.
