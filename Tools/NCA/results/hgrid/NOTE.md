# HGRID — a cellular automaton steering a collision automaton

Branch `cece/swarm-x-hgrid`, research direction "hierarchy in space". All numbers below are the
shared yardstick, unchanged: `swarm_nca.rollout` + strict `swarm_nca.tests_passed` (8 tests:
own plan x4, switch x4), default `LossCfg` (not scale-invariant, same as G2), and
`swarm_probe.probe` (vessel strike + 120 steps of healing).

## What was built

| File | What |
|---|---|
| `hgrid_core.py` | the grid: a 16^3 lattice of 6-voxel cells that snaps to the swarm's centroid; trilinear `splat`/`sample`, gradients, blur; `PlanFields` rasterises every plan/frame into 47 channels (wanted density per element x slot, and per-element attribute fields: raw prism, Charge tier, facing, spindle) + 12 optional FLOW channels (the plan's own unit velocity, from the targets' frame-to-frame motion) |
| `hgrid_boid.py` | `FieldBoid`: a DESIGNED collision boid whose every behaviour is read from the grid (below); `OracleField`: a designed morphogen (majority element -> that plan's field, with optional hysteresis); `HSwarm` carries the grid layer's state (plan, slot->domain map, grid, grid centre) through pools, rollouts and probes |
| `hgrid_hybrid.py` | G2's LEARNED rule (as shipped, no retraining) for motion/look/hatch/death + the grid layer for composition only |
| `hgrid_vote.py` | a small LEARNED 3D NCA that holds the plan decision cell by cell; the field is the per-cell choice over the four designed templates |
| `hgrid_nca.py` | a LEARNED 3D NCA that must produce the whole 47-channel field itself (DAgger against the oracle) |
| `hgrid_chain.py` | one swarm eaten four times in a row: whale -> jellyfish -> pufferfish -> dragonfly -> (whale) |
| `hgrid_eval.py` | yardstick runner + publisher (`rollout.json` in `swarm_nca.pack` format, as `swarm_gpu.py` writes it) |

### The boid layer (designed, ~60 lines of rule)

Per tadpole of class c = (element, domain), with the grid's slot->domain map:

* **move** up the gradient of its class DEFICIT (wanted - actual density, both on the grid) x3, plus the
  all-class deficit x1, plus the plan's flow field at its cell x4, velocity persistence 0.6, noise, the
  element's top speed, designed collision and membrane (as swarm_nca). Far outside the body: drift home.
* **lay** with probability 0.1 x (relative deficit of its class in the WHOLE swarm, read off the grid's
  integral); the egg goes toward its class deficit. A class with room breeds true; 25% of births (and
  every birth from a class with no room) take the most-wanted element of the parent's domain. Domain
  ALWAYS breeds true.
* **look**: prism, Charge tier, facing, spindle ease toward the field's attribute channels.
* **starve** (on by default): a tadpole whose class the swarm holds 15% more of than the plan wants,
  AND that sits where its class is not wanted, gains hunger and withers to a crystal.

The key mechanism is the laying rule: **the element MIX is regulated by the grid**, so after a cull the
new majority is fed and the old one stops breeding (and, with starvation, sheds its misfits as
crystals). This is exactly what G2's switches lacked.

## Results

| Model | Tests | own-plan divergence (mass/space/charge/time) | switches passed | probe heal (m/s/c/t) |
|---|---|---|---|---|
| G2 (baseline, `results/swarm_coevo_g2`) | 5/8 | 13.9 / 17.5 / 15.3 / 20.7 | 1/4 | 0.33 / -0.87 / 0.67 / n.a. |
| **oracle**: designed boid + designed grid (`oracle/`) | **7/8** | 7.2 / 9.8 / 10.3 / 17.7 | 3/4 | 0.72 / 0.99 / -0.08 / 0.82 |
| **hybrid**: G2 rule + grid composition (`hybrid_g2/`) | **7/8** | 7.2 / 17.2 / 6.8 / 11.1 | 3/4 | 0.94 / 0.99 / 0.30 / 1.73 |
| learned vote NCA (decision learned, templates designed) | see below | | | |
| learned field NCA (whole morphogen learned) | see below | | | |

Bodies come out at the plan's size and mix, not at the 280-slot cap (oracle: 196/192, 91/88, 185/179,
80/76 tadpoles, element mixes within a few units of the plan; G2 runs every plan at 280). In the
geometry-only table the oracle's diagonal is 4.5 / 4.7 / 5.6 / 10.9.

**Chain demo** (`oracle/chain.json`, `hybrid_g2/chain.json`): the same swarm, eaten four times, becomes
whale (8.4) -> jellyfish (11.7) -> pufferfish (10.0) -> dragonfly (18.0), each closest to its plan by
a wide margin (next best 54+); 4/4 for both the oracle and the hybrid. The fifth phase is the test
below.

### The one test no faithful swarm can pass (dragonfly -> whale)

The dragonfly plan's mix is Charge 2 / Mass 3 / Space 18 / Time 53. The test culls Time until MASS
leads Time - but Space (18) then leads Mass (3), so the true new majority is Space and the rule "grow the
plan of the majority" says JELLYFISH. Both hgrid models do exactly that (jellyfish divergence 11.1 and
16.8 against 63-72 for the whale). A swarm can only pass this test if its dragonfly holds more Mass
than Space, i.e. if it grew the wrong mix. Recommend fixing the yardstick: cull toward `majority_plan`
after the cull, or cull Space with Time.

## What a player would see

(to be completed)

## What failed / dead ends

(to be completed)

## Recommendation for the next round

(to be completed)
