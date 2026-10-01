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
| learned vote NCA (decision learned, templates designed), 2 versions | 3/8 | 7.2 / (puffer) / 9.3 / (puffer) | 0/4 | not run |
| learned field NCA (whole 47-ch morphogen learned) | not scored | density error never got below "predict nothing" in 7 episodes | | |
| **e2e**: G2 rule conditioned on 9 grid samples, fine-tuned with swarm_loss (`e2e/`, step 150 published; best step 1050) | 7/8 (1 seed) | 7.8 / 13.9 / 9.4 / 9.7 | 3/4 | -0.35 / 0.75 / 0.07 / n.a. |
| **oracle + morph wave** (`oracle_wave/`) | 7/8 | as oracle | 3/4 | as oracle |

### Seed variance (`seeds.json`, 4 rollout seeds each; one rollout moves the summed divergence by 10-20,
### and the 8th test is unpassable - see below - so 7 is the ceiling)

| model | mean tests | worst seed | mean summed divergence |
|---|---|---|---|
| G2 | 4.25 | 4 | 276.7 |
| oracle (designed grid) | **7.0** | **7** | 166.0 |
| hybrid (G2 + grid composition) | 6.75 | 6 | 200.0 |
| e2e step 1050 | 6.5 | 6 | **162.0** (127-196) |

Every seed of every hgrid model gets own-plan 4/4. The designed grid is the robust one (7 on every
seed); fine-tuning the learned rule against the grid buys the best single runs (127, and 125 with the
wave) but a seed in two drops a switch.

### Morph wave (`hgrid_boid.WaveField`, `wave=1`)

The grid's spatial layer finally does something only a grid can: each cell holds which plan it
expresses. When the swarm's plan changes, the new plan nucleates in the cell where the new majority
element is densest (where the survivors are) and spreads to neighbouring cells (p=0.08 per step,
~60-steps to sweep a body; p=0.5 sweeps it in ~11 steps, too fast to read). The body re-forms behind
the front. One rule made it work: the BREEDING budget is always the decided plan's whole template -
a half-converted field asks for the old majority's element, breeds it back and reverses the switch
(the first wave scored 6/8 for exactly that reason, the same failure as the learned vote NCA).
Oracle+wave: 7/8 (summed 159-168 over runs); G2-e2e step 1050 + wave, no retraining: 7/8, summed
124.9, switches 11.1 / 4.2 / 12.1 (the best switches of any model).

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

* **A clump that blooms into a creature.** 16 tadpoles; within ~60 steps the class with the most room
  breeds fastest, eggs land where their class is missing, and the body fills in from the inside out at
  the plan's own size (not a 280 blob).
* **It moves like the creature.** With the flow channel the whale's units drift along its body, the
  dragonfly's Time units run laps around the wings (~0.44 voxel/step, the fastest), the jelly pulses.
  Collision keeps the spacing even, so it reads as one animal made of fish.
* **Eat it and it changes species - and drops loot.** Remove enough of the majority and the grid
  immediately re-targets: the new majority breeds, the old one stops breeding, and the misfits (a class
  the new body has no room for, sitting where it is not wanted) wither one by one into crystals. A
  whale culled of its Mass shed ~55 crystals while becoming a jellyfish; the four-creature chain
  sheds 126 over its life. That is a readable, rewarding reaction: "I ate the whale's mass and it
  turned into a jellyfish and spilled crystals."
* **Strike it and it heals.** Ram a third of it away and it regrows into the hole within ~120 steps
  (heal 0.72-0.99 on three plans; the pufferfish regrows to the right size but scores no better -
  its spines are pose-sensitive).
* Caveats: the switch is not a morph wave - the whole field flips in one step (the designed grid is a
  global decision); and starvation is a death the brief's "no imposed death" allows only as starvation
  (here: no niche in the body plan). Turn it off with `starve=0`: still 7/8, but the misfits hang around the new body
  (whale->jellyfish keeps 18 Mass + 31 Time tadpoles, divergence 31.4 vs 11.2 with starvation;
  pufferfish->dragonfly 39.6 vs 19.9 - a near miss against the pufferfish at 42.4).

## What failed / dead ends (and why - the useful part)

1. **Learned vote NCA: the composition feedback that gives the designed grid its hysteresis makes a
   learned grid's mistakes permanent.** The grid's choice sets which elements breed; the majority
   follows the field; the label (the majority) then agrees with the field. A field that hesitates in
   its first steps (an untrained cell's argmax is "mass"; a soft mixture of plans asks for a mixed
   element budget, which favours the two big plans) breeds Mass/Charge, and the swarm walks into the
   whale or the pufferfish and stays there. Per-cell accuracy against the live majority reached
   0.88-0.90, yet the closed loop scored 3/8 twice (v1 soft field; v2 hard per-cell argmax + 5%
   hysteresis): Space and Time seeds grew pufferfish, and the whale never released Mass after a cull.
   Labelling with the *intended* plan instead (the seed's, or the cull's - sticky) is the honest label,
   but in 40 episodes it did not learn (accuracy 0.3-0.5): the cue (the mix at the moment of the cull)
   is gone by the time the error shows. This is G2's credit-assignment problem moved one level up, not
   solved by it.
2. **Learned field NCA (grow the whole morphogen): too slow on 4 CPU cores.** First version diverged
   (unbounded state); bounded + leaky + value-parameterised attributes was stable, but after 7
   episodes (~1700 field steps) its density error was still ~1.0 relative (= predicting empty space).
   The task is a conditional growing-NCA with four 3D shapes, 47 output channels and a moving frame;
   it needs GPU-scale training. Paused (resumable: `runs/hgrid_nca`).
3. **Engineering traps worth carrying:** (a) the field the boids follow must be detached when training
   the grid through its own loss - otherwise every position keeps the whole autograd history and the
   process grew to 14 GB and was OOM-killed; (b) relu on a zero-initialised density readout passes no
   gradient (regress the raw state where density is wanted); (c) a density field cannot express
   circulation - the formed swarm sat still (0.05 voxel/step) until the plan's own frame-to-frame unit
   velocity became a FLOW channel (now 0.13-0.44 voxel/step, Time fastest).

## Recommendation for the next round

1. **Ship the two-level split, with the decision designed and the body learned.** The single biggest
   lever found here is the grid's COMPOSITION control: wanted counts per (element, slot) class read off
   a field; a class with room breeds, a class without does not, births cross over to the most-wanted
   element at a small rate, misfits starve to crystals. Bolted onto G2 with no retraining it lifts
   G2 from 4.25 to 6.75 tests (4-seed mean) and makes every body the plan's size and mix. Every other
   direction in the portfolio that grows a body should borrow it (it is ~40 lines,
   `FieldBoid._lay` + the starvation block, and needs only the plan templates).
2. **Combine with the best per-tadpole learner, not the grid NCA.** Fine-tune that learner WITH the
   grid in the loop, as `hgrid_e2e.py` does (9 grid samples as zero-init inputs, sticky labels,
   G2's loss). Here it bought the best single runs (127, 125 with the wave) but higher variance than the
   designed boid; give it a GPU-sized run and select checkpoints on a multi-seed eval (one rollout is
   +-10-20 noise; `hgrid_seeds.py`).
3. **Keep the plan DECISION designed** (majority element, optional hysteresis). Two learned decision
   layers (vote NCA, full-field NCA) failed for a structural reason: the decision feeds the composition
   it is judged on, so early mistakes become self-confirming. If a learned decision is wanted, train it
   open-loop on clean inputs and only then close the loop, or give it an explicit latched memory cell.
4. **Use the space the grid gives you for game feel, not for the decision**: the morph wave (switch
   nucleates where the survivors are, sweeps the body), per-cell flow (wings beat, Time units run
   laps), and the misfits' wither-to-crystal are what a player reads. Next: make the wave's front
   visible (a colour/brightness channel the renderer reads), and let a vessel strike leave a wound
   the field remembers for a while (a damage channel that slows regrowth) - "carve and watch it heal".
5. **Fix the yardstick's dragonfly -> whale test** (unpassable by a faithful dragonfly; see above):
   cull toward the plan the cull actually produces, or cull Space along with Time.

## Reproduce

```
python Tools/NCA/hgrid_eval.py oracle [--set wave=1] --probe --out results/hgrid/oracle
python Tools/NCA/hgrid_hybrid.py --probe
python Tools/NCA/hgrid_e2e.py eval --ckpt Tools/NCA/results/hgrid/rule_e2e_01050.pt [--set wave=1]
python Tools/NCA/hgrid_e2e.py train --run runs/hgrid_e2e --hours 2      # warm-starts from G2
python Tools/NCA/hgrid_chain.py oracle --set wave=1
python Tools/NCA/hgrid_seeds.py --seeds 4 --models g2,oracle,oracle_wave,hybrid,e2e:<ckpt>,e2e_wave:<ckpt>
```
Top-level `summary.json` / `rollout.json` / `probe.json` are the designed-grid (oracle) model; each
subfolder holds its own model's files (`rollout.json` in the viewer's `swarm_nca.pack` format,
`chain.json` = the four-creature chain).
