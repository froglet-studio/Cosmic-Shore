# meta: metamorphosis + selective laying (INTERIM - run in progress)

Code: `Tools/NCA/meta_swarm.py` (`MetaRule`, a SwarmRule subclass; G2 weights load exactly -
selftest proves `p_meta=0` reproduces G2 bit for bit over 60 steps).

Actuators added: metamorphosis (12-step, slowed, capped at 8% concurrent, target from learned
softmax over the other 3 elements, score-function gradient credited at COMPLETION through a
value-zero carrier channel), selective laying (`learned_egg` on new PREF channels, p_cross 0.1).
Domain always breeds true.

Baseline G2 (re-measured here): 5/8, probe heal mass 0.04 / space n/a / charge -0.53 / time 0.55.

m1 (p_meta 0.05, bias -4, no prior), warm from G2, evals every 250 steps:

| step | tests | metamorphs after the cull (ma/sp/ch/ti) |
|---|---|---|
| 250 | 4 | 0/0/1/0 |
| 500 | 5 | 5/1/2/2 |
| 750 | 4 | 7/58/7/40 |
| 1000 | 4 | 6/48/17/195 |
| 1250 | 4 | 6/5/1/34 |

Published here for now: m1 step 500 (5/8, same single switch as G2: jellyfish -> pufferfish).
The rule learned to metamorph in bursts after a disturbance, but toward the WRONG element as
often as the right one (time->mass ended Space-majority after 195 metamorphs). No switch gain.
Next: m2 adds a conformity prior on the metamorph target (+1.5 log share).

## m2: learned metamorph + conformity prior (+1.5 log share, bias -3) - SOUP, negative

| step | tests | metamorphs completed per 480-step rollout (ma/sp/ch/ti) |
|---|---|---|
| 250 | 4 | 65/573/115/56 |
| 500 | 5 | 740/652/675/644 |
| 750 | 4 | 179/683/58/538 |

With more exploration the learned drive saturated the 8% cap and churned: hundreds of tadpoles
changing element every rollout, compositions no closer to any plan, probe heal went NEGATIVE
(-0.3 to -5.7: the swarm got worse after regrowth). Stopped at 750.

## oracle: DESIGNED conformity metamorph on FROZEN G2 - 7/8 with zero training

`meta_oracle.py`: each step, tadpoles of the element most over the live majority's plan mix start
metamorphosing (same 12-step / 8%-cap / slowed process) into the element most under it.
Results in `results/meta/oracle/`. Own plan 4/4; switches 3/4; probe heal 0.71 / 0.99 / 0.87 / 0.86
(G2: 0.04 / n.a. / -0.53 / 0.55). Geometry-only, the switched jelly->puffer (5.1) and
puffer->dragonfly (10.4) truly reshape; whale->jelly passes on composition but keeps a whale-ish
shape (12.9 whale vs 13.3 jelly). The failing time->mass test is a YARDSTICK quirk: the
dragonfly seed holds Space 66 vs Mass 11, so the eval's cull leaves SPACE as the real majority;
a majority-following swarm correctly becomes a jellyfish and is scored against the whale.

m3 (running): the oracle homeostat ON during training, so the learned rule only has to learn
SHAPE under a composition that is held to the plan.

## m3: designed homeostat ON in training, shape learned - 7/8, PUBLISHED (step 500)

`results/meta/{summary,rollout,probe}.json` + `rule.pt` are now m3 step 500 (oracle=1 in the
checkpoint; load with `meta_swarm.load_meta`). Strict tests 7/8, summed close 169.9 (G2 274.0,
oracle-on-G2 197.9). Own plan: 11.8 / 14.8 / 5.2 / 12.9 (G2 13.9 / 17.5 / 15.3 / 20.7).
Geometry-only, EVERY passing switch now really changes shape: whale->jelly 8.3 (vs 17.2 whale),
jelly->puffer 5.1, puffer->dragonfly 8.1. Probe heal 0.90 / 0.77 / 0.71 / 1.08.

## Robustness over rollout seeds 7-11 (`meta_seeds.py`, `results/meta/seeds.json`, 1 thread)

| model | tests passed, seeds 7/8/9/10/11 | summed close |
|---|---|---|
| G2 | 4/4/4/4/4 (own 4/4, switches 0/4) | 258-301 |
| designed homeostat on frozen G2 | 7/7/7/7/7 | 185-198 |
| m3 step 500 | 7/7/7/7/7 | 169-173 |

G2's published 5/8 is NOT robust: single-threaded, seed 7 gives 4/8 (thread-count nondeterminism
flips its one marginal switch). The metamorph models are 7/8 on every seed. The failing test is
the same everywhere: dragonfly -> whale, where after the yardstick's cull the live majority is
SPACE on every seed (`majority_after`), so a majority-following swarm becomes a jellyfish.

## How fast may it transform? Cap sweep on m3@500 (`results/meta/capsweep.txt`)

| simultaneous-metamorph cap | tests | what happens after the cull |
|---|---|---|
| 2% | 5/8 | conversion slower than the old majority re-lays; the homeostat then LOCKS IN the old plan |
| 4% | 5/8 | same race lost on 2 of 4 |
| 8% (trained) | 7/8 | switches hold |
| 15% | 7/8 | switches hold, slightly worse closeness (181.99 vs 169.9) |

Conformity is positive feedback: it amplifies whichever element leads, so a switch is a RACE
between conversion and re-laying in the first few dozen steps after the loss. That is a game knob
with a clear threshold (between 4% and 8% of the swarm transforming at once).

## Control: selective LAYING alone (no metamorphosis; element fixed at birth) - `meta_laying.py`

Designed homeostat on the egg element only (each egg takes the element most under the live
majority's plan mix), frozen G2: **also 7/8** (close 210.4; metamorph homeostat 197.9). So the
decisive ingredient is the COMPOSITION HOMEOSTAT (conformity to the majority's plan mix), not the
metamorph actuator. Metamorphosis still buys: tighter mixes (whale->jelly ends [67,28,153,32] by
laying vs [78,7,172,23] by metamorph), a real shape change on whale->jelly (laying: geometry
still whale-closest, 10.9 vs 12.6), and much better healing (laying 0.89/0.16/0.43/0.25 vs
metamorph 0.71/0.99/0.87/0.86). Laying-only also needs free slots: a swarm at capacity with no
turnover cannot rebalance by laying. (`results/meta/laying_control.txt`)

m3 continued to step 1260: 7/8 at 500 / 750 / 1250 (close 173.9 / 187.0 / 172.4), 6/8 at 1000 -
a noisy plateau, no improvement on the published step 500. Stopped to train l3 = laying
homeostat + learned shape, the fair comparison.
