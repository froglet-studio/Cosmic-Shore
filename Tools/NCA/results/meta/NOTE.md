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
