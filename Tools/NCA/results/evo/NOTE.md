# evo — gradient-free search over the swarm rule AND its behaviours

Direction: evolution strategies (CMA-ES) on what we actually want — the strict 8-test yardstick over
the full 480-step horizon, plus grazing, strike-heal and liveliness — instead of truncated BPTT on a
proxy loss. Branch `cece/swarm-x-evo`. All code in new files; nothing shared was edited.

## Headline (this folder's top level = `stage4/`)

| | G2 (BPTT, the start point) | **headline (stage 4)** |
|---|---|---|
| yardstick `tests_passed` (seed 7, unchanged scorer) | 5/8 | **8/8** |
| 16 held-out seeds, mean tests | 4.25 | **8.00** (16/16 seeds at 8/8) |
| same, with the yardstick's alternative `ratio` cull (12 seeds) | 7.67 | **8.00** |
| switches under GRAZING (2 or 5 eaten/step, 8 seeds × 4) | 0.25 / 4 | **4 / 4** (32/32) |
| liveliness (heart motion / plan speed, 0–1, 12 seeds) | 0.48 | **0.90** (Time runners 0.21 → 0.62) |
| probe heal (strike, 120 steps, 12 seeds) | 0.04 | 0.08 (stage 2 had 0.43 — traded away) |

The G2 network's weights are not what changed. What won is **behaviours the backprop rule had no
actuator for**, switched on by evolution, plus ~100 evolved numbers on top of the frozen G2 MLP:
a composition homeostat on laying, egg-element choice, a predation-aware "fear" brake on breeding,
per-element swirl, and an output-layer re-weighting.

## Built
| file | what |
|---|---|
| `Tools/NCA/evo_model.py` | `EvoRule` = frozen G2 rule + a 111-float GENOME. Batched `fast_rollout` (yardstick protocol, 4 plans in one batch; optional batched strike probe, grazing test, liveliness). `EVO_CULL=ratio` switches to the alternative cull. |
| `Tools/NCA/evo_search.py` | CMA-ES (pycma), pop 10–12, 2 fresh seeds per generation shared by the population, 4 processes × 1 thread, held-out check (8 unseen seeds) every 5 generations, resumable; `--genes` picks gene groups, `--fix` pins genes, `--model compact`. |
| `Tools/NCA/evo_compact.py` | Track (b): a rule with **no neural network**, 31 evolved numbers. |
| `Tools/NCA/evo_publish.py` | Writes `summary.json` (`swarm_nca.rollout`, seed 7), `rollout.json` (`swarm_nca.pack`, exactly as swarm_gpu.py publishes — the viewer plays it), `probe.json` (`swarm_probe.probe`), `genome.npy/.json`, `robust.json` (16 held-out seeds + one-behaviour-off ablations), `evo_log.jsonl`. |
| `evo_compare.py`, `evo_graze.py`, `evo_motion.py` | Shared-seed comparison (`compare*.json`); gradual-predation sweep (`graze.json`); per-element motion stats. |

### The behaviours (each has an on/off gene CMA-ES can push negative = "subtract a behaviour")
- **B1 lay homeostat** — parent's laying probability × `sigmoid(k·deficit/0.1 + b)/sigmoid(b)`,
  deficit = desired share of its element under the current majority (table `D`, 16 evolved numbers,
  initialised from the plans' mixes) − current share. Production gating; nothing is culled.
- **B2 egg choice** — a share of eggs take the element the swarm is shortest of. Domain breeds true.
- **B3 majority lock** — hysteresis on the majority. **Evolution deleted it in every run** (margin → ~1%):
  a lock makes the swarm refuse the switch right after a cull.
- **B5 output re-weighting** — per-output gain/bias on the MLP's last layer (the "fiddle with weights" half).
- **B6 swirl** — per-element rotation about the plan's long axis (a motion actuator).
- **B7 fear** — `lay × exp(−g · loss_rate_e)`, loss_rate_e = leaky memory (τ) of element e's
  losses per step as a fraction of e. An element that is being eaten stops breeding. Evolved:
  g ≈ 1300, τ ≈ 16 steps — losing more than ~0.3% of yourself per step shuts your breeding off.

Headline genome: B1, B2 (6.8% of eggs), B5, B6, B7 on; B3 off. Evolved `D` (a majority amplifier):

| majority | desired C M S T | plan's own mix |
|---|---|---|
| Charge | 0.92 0.00 0.00 0.07 | 0.73 0.01 0.09 0.17 |
| Mass | 0.02 0.82 0.14 0.01 | 0.10 0.66 0.13 0.11 |
| Space | 0.10 0.03 0.84 0.02 | 0.28 0.02 0.61 0.08 |
| Time | 0.01 0.07 0.01 0.91 | 0.03 0.04 0.24 0.70 |

## Stages and evolution curves (held-out = 8 unseen seeds; tests per seed, or [tests, grazing] pairs)
| stage | genes | fitness | time | held-out |
|---|---|---|---|---|
| 1 | B1–B3 + D (25) | tests + 0.5·margin | 31 min, 15 gens | gen 4: 7 7 6 7 7 7 7 7 · gen 9: 7 7 7 7 7 6 7 7 · gen 14: 8 7 8 8 8 8 8 8 |
| 2 | + B5, B6 (107) | + 0.5·heal + 0.5·live | 1.6 h, 33 gens | 8×8 from gen 4 (one seed at 7 in gens 19, 24) |
| 3 | all | + 0.5·heal + 1.5·live | 35 min, 10 gens | 8×8 at gens 4 and 9 |
| 4 | B1–B3 + D + B7 (28) | + 1·(grazing switches + 0.5·margin) | 55 min, 15 gens | gen 4: [8,3][8,2][8,3][8,3][8,3][8,2][8,2][8,1] · gens 9, 14: [8,4] × 8 |
| compact | 31 | tests + 0.5·margin | 35 min, 20 gens | mean 6.4 → 7.6 → 8.0 (gen 14, kept) → 7.75 |

Margin = (best other plan − wanted)/(sum), in [−1, 1]; −1 if under 32 tadpoles. Per-generation logs:
`*/evo_log.jsonl`. Stage 1 alone already took held-out from 4.25 to 7.8 in half an hour.

## Ablations of the headline (16 held-out seeds, mean yardstick tests)
| genome | tests |
|---|---|
| headline | **8.00** |
| without lay homeostat (B1) | 6.31 |
| without egg choice (B2) | 7.44 |
| without output re-weighting (B5) | 8.00 |
| without swirl (B6) | 7.94 |
| without fear (B7) | 8.00 on the yardstick — but **0/4 grazing** (= stage 3, `graze.json`) |
| all behaviours off (= G2) | 4.25 |

B1 carries the yardstick, B7 carries grazing, B5/B6 buy liveliness. Every behaviour earns its place
on some axis except B3, which evolution removed.

## Findings worth more than the scores

**1. Most of G2's switch failures were the yardstick's cull, not the rule.** The default `excess` cull
leaves the new majority ONE tadpole ahead; with element-blind laying the old majority (still the biggest
parent pool) out-breeds it back. With `mode="ratio"` (cull every element toward the new plan's mix),
plain G2 scores 7.67/8 (`compare_ratio.json`). The homeostat is what makes a one-tadpole lead stick.

**2. Test 8 (dragonfly → whale) contradicts the dragonfly's own mix.** Its second element is Space (24%),
Mass is 4%. Culling Time until Mass leads Time hands the majority to SPACE, so a faithful dragonfly
correctly becomes a jellyfish. Stage 1 sat at 7/8 on exactly this until CMA set `D[Time]` to carry Mass
above Space. That is yardstick-fitting. Same, more weakly, for whale → jelly. Fix: cull with `ratio`,
or set `to` = the grown swarm's real second element.

**3. A composition homeostat makes a swarm immune to grazing — and fear fixes it.** At its 280 cap the
swarm refills each eaten slot within a few steps, and the homeostat refills it with the CURRENT majority.

| eaten per step (8 seeds × 4) | G2 | stage 2 | stage 3 | **stage 4 (fear)** |
|---|---|---|---|---|
| 0.5 | 0.38 | 0.00 | 0.00 | 0.00 |
| 2 | 0.25 | 0.00 | 0.00 | **4.00** (after ~200 eaten) |
| 5 | 0.25 | 0.00 | 0.00 | **4.00** |
| 12.5 | 0.25 | 0.50 | – | – |

Stage 2/3 lost 400 tadpoles (1.4× the swarm) without changing plan. Fear, evolved with grazing in the
fitness, converts 32/32 at 2 and 5 per step. At 0.5 per step it holds its identity — fear has a rate
threshold. **That threshold is a design dial**: chip at it and it shrugs; hunt it and it turns into
something else.

**4. Composition is where the score lives.** In the geometry-only table (element and domain costs zeroed)
the headline's dragonfly is marginally closer by SHAPE to the pufferfish (12.1 vs 14.7); the other three
are shape-correct. `w_elem = 60` makes the yardstick mostly a composition readout. The homeostat wins by
fixing composition; the frozen G2 network supplies the shape.

**5. A switch is decided in one refill burst.** After a yardstick cull the 120–210 free slots refill to
the cap in under 40 steps, almost entirely with the new leader; then the composition freezes (a full
swarm lays nothing, nobody dies). Measured (stage 2, seed 42): the new element's share went
0.36 → 0.58, 0.44 → 0.62, 0.47 → 0.70, 0.34 → 0.73; with all behaviours off 0.16 → 0.19, 0.49 → 0.50,
0.45 → 0.39, 0.06 → 0.05.

**6. ES is cheap and well-suited here.** ~20 s per 4-plan rollout on one core; a generation of 12 × 2
seeds takes 2–3.5 min on 4 cores; every stage converged in under 2 hours; held-out tracked training
closely (apart from finding 2).

## Probe (`swarm_probe.probe`, headline, seed 11)
Strike removes 63–79 of 280. heal: mass 0.38, space −0.01, charge 3.07 (ends better than before),
time −0.73 (worse after regrowing). Every swarm refills to 280. Stage 2 (`stage2/probe.json`) healed
better: 1.46 / 0.18 / – / 1.49. Heal and liveliness traded against each other across stages.

## What a player would see (headline)
A 280-tadpole cloud in its element's body plan, its Time tadpoles visibly circling the body (0.7–1.1
per step against ~0.35 for everyone else — readable "runners"). Strike it with a vessel and a bite opens
that refills from the edges within a few steps, but the shape is not restored well. Hunt one element
steadily and that element stops breeding: the cloud's element mix tips over and the body re-forms into
the new majority's plan after ~200 kills. Kill its majority all at once and it converts in under 40
steps. Nibble slowly and it keeps its identity. Not seen: deaths, dropped crystals, or any reaction to
the vessel itself — the swarm reacts to LOSSES, not to the player's position.

## Compact rule (`compact/`) — a distinct, cheap lifeform candidate
No neural net. Each tadpole swims to home = centroid + scale·(μ_g + L_g·u): (μ_g, L_g) is a per-frame
Gaussian for its (element, domain) group in the current majority's plan, fitted once from plan data;
u is its private random anchor. Plus per-element swirl, jitter, the B1/B2 homeostat and a body cap.
31 evolved numbers. Yardstick 7/8 (whale → jelly flips back on a near-tie), held-out 7.75/8, `ratio`
cull 8/8, heal 0.41, liveliness 0.33. Trivially portable to C#/Burst and fully explainable; but its
bodies are Gaussian blobs, so shape alone separates the plans poorly. A runtime fallback, not a showpiece.

## Recommendation for the next round
1. **Make B1 (lay homeostat) + B7 (fear) platform behaviours** for any element-driven fauna swarm. Both
   are production gating (no imposed death), each ~10 lines, and together they give a swarm a stable
   identity that only a determined predator can change — which is also good play. Port them into the
   BPTT trainer so the network learns SHAPE given a composition controller, instead of fighting drift.
2. **Fix the yardstick** before anyone optimises further against it: `ratio` cull or a real
   second-element target (finding 2), and add a grazing test (finding 3) — it is the test that actually
   separates models now.
3. **Combine** the direction with the best geometry-only table (shape) + B1/B7 + a final ES pass on the
   full-horizon objective with heal and liveliness in the fitness from the start (they trade:
   stage 2 heal 0.43 / live 0.64; stage 4 heal 0.08 / live 0.90).
4. Liveliness needs a motion actuator (B6 is crude) and the loss never rewards motion. A reaction to the
   vessel (flee / scatter / regroup) is the missing "reactive" behaviour and the obvious B8.
