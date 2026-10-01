# evo — gradient-free search over the swarm rule (interim, 13:35 UTC)

**Result so far: 8/8 strict tests on the shared yardstick** (`swarm_nca.rollout`, seed 7, `tests_passed`
unchanged), and **7.81/8 mean over 16 held-out seeds** (13×8, 3×7). G2 alone scores 5/8 on the
yardstick and 4.25/8 mean on the same held-out seeds.

## What was built
- `evo_model.py` — `EvoRule`: the trained G2 rule (unchanged weights) wrapped by a 95-float GENOME
  that adds BEHAVIOURS the backprop rule had no actuator for, each with an on/off gene CMA-ES can
  push negative to delete it:
  - B1 **lay homeostat**: a parent's laying probability is scaled by
    `sigmoid(k·deficit/0.1 + b)/sigmoid(b)`, deficit = desired share of its element (from table
    `D[locked majority]`) − current share. Production gating only; nothing is culled.
  - B2 **egg choice**: a share of eggs take an element drawn from `softmax(β·deficit/0.1)`; domain
    breeds true.
  - B3 **majority lock**: the majority the homeostats steer by changes only past a margin.
  - B5 output-layer gain/bias (the "fiddle with weights" half) — not searched yet.
  - `D` (16) is evolved, initialised from the four plans' own element mixes.
- `evo_search.py` — CMA-ES (pop 10, σ0 0.5), 2 fresh rollout seeds per generation shared by the
  population, 4 worker processes, held-out check (8 unseen seeds) every 5 generations, resumable.
  Fitness = tests passed + 0.5·mean per-test margin ((best other − wanted)/(sum), −1 if < 32 tadpoles).
- `evo_publish.py` — writes this folder. `evo_compact.py` — track (b), see below.

## Evolution curve (stage 1, behaviour genes, 25 dims, ~31 min)
| gen | best fit | mean fit | mean passed | held-out (CMA mean, 8 seeds) |
|---|---|---|---|---|
| 0 | 6.63 | 5.54 | 5.45 | |
| 4 | 6.65 | 5.73 | 5.65 | 7,7,6,7,7,7,7,7 |
| 9 | 7.17 | 6.69 | 6.55 | 7,7,7,7,7,6,7,7 |
| 14 | 8.20 | 6.96 | 6.80 | 8,7,8,8,8,8,8,8 |

## Best genome (results/evo/genome.json)
Behaviours kept: lay homeostat + egg choice (26% of eggs). Lock evolved to a 1% margin (effectively off).
`D` was sharpened from the plans' mixes into a **majority amplifier** (whale wants 90% Mass, plan has
66%). Gate k=3.0, b=0.34.

## Ablation (16 held-out seeds, mean tests passed)
| genome | mean passed |
|---|---|
| best | **7.81** |
| without egg choice | 7.19 |
| without lay homeostat | 5.44 |
| all behaviours off (= G2) | 4.25 |

## The yardstick finding: test 8 (dragonfly → whale) contradicts the dragonfly's own mix
The dragonfly plan's second element is Space (24%), Mass is 4%. `lose_majority(..., to=Mass)` culls
Time until Mass leads Time — but Space then leads both, so a swarm that grows a faithful dragonfly
loses its majority to SPACE and (correctly, per the brief) becomes jellyfish-like. Stage 1 sat at 7/8
on exactly this test until CMA moved `D[Time]` to carry Mass (10%) above Space (7%). That is
yardstick-fitting, not biology; the same holds weakly for whale→jelly (Charge must stay below Space).
**Recommendation for the yardstick:** cull to the NEW plan's element mix (`mode="ratio"`) or choose
`to` = the grown swarm's actual second element.

## Probe (vessel strike, then 120 steps)
heal: mass 0.46, space −1.17 (worse after regrowing), charge 0.94, time 0.60. Swarms refill to 280.

(Final NOTE — compact track, stage 2, what a player sees, recommendation — follows.)
