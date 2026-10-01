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
