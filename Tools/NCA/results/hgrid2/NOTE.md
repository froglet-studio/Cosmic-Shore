# HGRID2 — the grid morphogen, round 2: accurate AND organic

Branch `cece/swarm-x-hgrid2`. Every number is the shared yardstick, unchanged: `swarm_eval.evaluate`
(16 transitions, fair cull, 3 samples, the loss-8 bar), `swarm_nca.rollout` + `tests_passed` (old 8),
`swarm_probe.probe`, default `LossCfg`. Everything here is designed; nothing is learned.

## Headline

| model | own plans (mass/space/charge/time) | 16-transition yardstick | held-out seeds | probe heal (m/s/c/t) | organic band |
|---|---|---|---|---|---|
| **hgrid2** (`results/hgrid2/`) | **1.2 / 2.0 / 2.9 / 3.0** | **16/16, all feasible** | 15/15 (s1000), 15/15 (s2000), 15/15 (s3000) | 0.98 / 0.87 / 1.00 / 0.97 | **yes** |
| **evo body + grid** (`results/hgrid2/evo_grid/`) | 2.2 / 3.2 / 1.9 / 4.9 | 13/16 (only the 3 into-dragonfly switches fail, 8.3–9.8) | 12/15 (s1000), 12/15 (s2000) - same 3 misses | 1.02 / 0.91 / 0.94 / 0.91 | **yes, evo-like** |
| round-1 hgrid oracle | 7.2 / 9.1 / 7.9 / 18.1 | tier 1 fails | | 0.72 / 0.99 / -0.08 / 0.82 | yes |
| designed field model (round 1) | 3.1 / 1.6 / 1.4 / 4.6 | 13/13 feasible | | | no (too clean) |
| evo (round 1) | 16–24 | 0/16 under the bar | | 0.46 / -1.17 / 0.94 / 0.60 | yes |

The tier-1 bar ("a loss under 8 in each element before moving on") is met with a wide margin, and so is
every switch. Switch losses for hgrid2 (seed 7): whale/jelly/puffer targets 1.2–4.2; into the dragonfly
7.4–7.6 (see the domain floor below). The old 8-test yardstick reads 7/8: its one miss is
dragonfly->whale via `lose_majority`, which round 1 already showed no faithful swarm can pass (Space,
not Mass, becomes the majority); the fair-cull version of that transition passes at 1.9.

## What was built (all designed, `Tools/NCA/hgrid2_*.py`)

`hgrid2_model.Boid2` keeps round 1's coarse grid (16^3 cells of 6 voxels: plan decision, composition,
laying, looks) and adds, in the order they were found to matter:

1. **Fine morphogen** (`k_fine`, `sigma_rel`): per class (element, domain), phi_c(x) = Gaussian bumps of
   the plan's units of class c MINUS bumps of the live tadpoles of class c, sigma = 1.2 x the plan's own
   unit spacing. A tadpole climbs grad phi of its own class. No unit is ever assigned a site: sites are
   claimed by being occupied, so two tadpoles that want one site push it between them.
2. **Continuous target + feed-forward** (`interp`, `k_ff`): the plan's units move continuously between
   its 8 frames instead of jumping every 8 steps, and a tadpole takes the velocity of the wanted units
   of its class around it. This alone took the dragonfly (whose wing units move 10 voxels a frame) from
   18 to 8.9; with a 16-step frame period for the dragonfly, to 3.
3. **Migrants** (`k_mig`, `mig_th`, `mig_L`): a tadpole standing where its class is barely wanted heads
   for the nearest site where its class is MISSING (score = deficit - distance/40). This is the piece
   that fixed SORTING: the whale's Charge and Space minorities were trapped 20+ voxels from their regions
   inside the dense Mass body, and no local gradient or neighbour swap can carry a unit across a packed
   body (measured: a post-switch body was stuck at +5 element cost for 600 steps). With migrants: whale
   6.1 -> 1.2, and the stuck switches pass.
4. **Plan lock** (`lock` = 60 steps): once the plan changes it holds while the composition settles. Without
   it a culled pufferfish's Charge re-bred inside the jellyfish's Charge allowance, regained the lead and
   flipped the plan back. (A count-margin hysteresis instead blocked the cull's own switch.)
5. **Staggered starvation, exact** (`stagger`, `starve_slack`=0, `small_slack`=2, `elem_floor`): misfits
   wither one by one (per-tadpole hunger rate in [0.5,1.5] x, never more deaths per class than its
   excess), and the body ends at the plan's EXACT element mix, except an element the plan holds at most
   2 of keeps 2 spare - so a jellyfish's 2 Mass can still become a whale after a cull. (Exact for tiny
   elements too made 3 of 16 culls infeasible: honest n/a, but fewer tests; this keeps all 16.)
6. **Yardstick-aware slot->domain map** (`dmap_low`): see "finding 1".

`hgrid2_evo.EvoGrid` is direction (b): the round-1 EVOLVED body (`evo_model.EvoRule`, genome
`results/evo/genome.npy`) moves, hatches and looks exactly as shipped (its own laying off); the grid adds
composition (laying, staggered starvation with hunger kept on the model - the rule owns every state
channel), plan + lock, and a STEER = coarse class gradient x0.7 + fine layer x1.0 + migrants.

## Findings worth carrying

1. **Yardstick property: a 2-slot plan only scores domains {0,1}.** `swarm_loss` searches slot->domain
   perms over `range(T.slots)`, so a jellyfish/whale/pufferfish body built from domains {0,2} scores
   every domain-2 unit as a mismatch even when perfectly sorted (time->jellyfish: correct counts, correct
   regions, +11 from domain alone). `dmap_low` maps a k-slot plan onto domains 0..k-1. Recommend the
   yardstick search all 3-domain injections for k<3 plans (it is a label, not a shape).
2. **The dragonfly domain floor.** A whale or pufferfish has 2 domains; the dragonfly has 3 slots
   (26/6/44). Domain always breeds true, so the 6-unit slot can never be filled: ~2.0 of unavoidable
   divergence on every into-dragonfly switch from a 2-slot plan. That is why those three sit at 7.4-7.6
   while everything else is at 1-4.
3. **Sorting failures were long-range, not local.** Neighbour exchange (pairs circle each other and trade
   places, `k_swap`) bought almost nothing; migrants bought everything. The "outline fine, sorting wrong"
   diagnosis of round 1 is a TRANSPORT problem: minorities trapped inside a dense body.
4. **The coarse grid no longer needs to steer.** Ablation (own plans, 2 seeds): k_class=0 gives the same
   1.2/1.5/2.5/2.9. Without migrants the whale goes back to 5.5; without feed-forward or the continuous
   target the dragonfly goes to 5.3-5.9; without the fine layer it is 11. The coarse grid's job is now
   composition (which class breeds, egg element, plan, starvation), not motion.
5. **A stochastic body + a deterministic steer = twitching.** The evolved rule updates each tadpole with
   probability 0.5 per step; steering every step made units alternate between rule-moves and grid-moves
   (direction reversals 0.16, outside the organic band). Steering a tadpole only on the steps its own rule
   fires (`steer_sync`, x2 gain) brought reversals to 0.03 and kept the evolved body's gas-like signature.
   The fast Time units are still steered every step (`sync_elems`), which the dragonfly needs.
6. **Two evaluation bugs I hit and fixed in my own code** (not in shared files): (a) a model that keeps
   per-swarm state must reset it when it SEES clock 0 - the evo wrapper first ran the inner rule (clock
   becomes 1), so the plan-lock timestamp leaked from the previous test and froze every later switch
   (0/12 -> 15/16); (b) a learned body owns all its hidden channels, so a grid layer's hunger counter
   must live on the model, not in s[25].
7. **CPU oversubscription is catastrophic here**: two 4-thread torch jobs on 4 cores ran ~30x slower than
   one. Run N one-thread workers instead (`runs/hgrid2/par.sh`).

## Organic: `swarm_feel.py` (direction c)

Documented metrics on a grown body over 64 steps: jerk and jerk/speed, crystal-cluster planarity
(PCA of 8 nearest neighbours; flatness < 0.1 = sheet) measured as EXCESS over the plan's own planarity
(the plans themselves are 38-69% sheet-like), neighbourhood coherence, jitter, phase diversity, stuck
tadpoles, and reversals (osc). Calibrated band (`swarm_feel.BAND`): jerk_rel in [0.2, 2.5], osc <= 0.08,
stuck <= 0.02, planar excess <= 0.15. It sorts the lead's four reviews exactly:

| approach (lead's words) | jerk_rel | osc | stuck | planar excess | coherence | jitter | in band |
|---|---|---|---|---|---|---|---|
| evo ("beautifully organic") | 1.93 | 0.058 | 0 | 0.00 | 0.12 | 2.53 | yes |
| evo compact ("very jerky, planar") | 1.31 | **0.167** | 0 | **0.23** | 0.73 | 0.73 | no |
| field ("too clean, mistakes feel like bugs") | **0.15** | 0.035 | **0.096** | 0.03 | 0.77 | 0.66 | no |
| hgrid oracle ("accurate and organic") | 0.30 | 0.010 | 0 | 0.13 | 0.71 | 0.97 | yes |
| **hgrid2** | 0.58 | 0.031 | 0 | 0.08 | 0.63 | 0.93 | **yes** |
| **evo body + grid** | 1.66 | 0.031 | 0 | 0.00 | 0.28 | 2.32 | **yes** |

"Too clean" turned out to be measurable as machine-smooth motion (jerk/speed 0.15) plus tadpoles that
freeze in a moving body (10%) - the field's slot assignment parks losers. "Jerky" is reversals, not raw
jerk (evo has high raw jerk and reads as organic: it is brownian, not twitching). Coherence/jitter are
descriptive: they separate a school (hgrid2 0.63) from a gas-like swarm (evo, evo+grid 0.12-0.28).

## What a player would see

* **hgrid2**: a seed clump blooms into the creature at its exact size and mix; minorities visibly SWIM
  across the body to their regions (migrants), the whale's Charge streaming to the tail; the dragonfly's
  wings flap continuously instead of stepping frame to frame. It moves as a school (coherence 0.63):
  readable, still imperfect (jitter ~0.9, nobody frozen). Eat its majority: the plan flips, the new
  majority breeds, misfits wither ONE BY ONE into crystals, the body reshapes - every transition within
  8, most within 4. Strike it: it regrows to its pre-strike score in 120 steps (heal 0.87-1.0).
* **evo body + grid**: the lead's favourite motion - gas-like, every unit doing its own thing - now
  actually forms the four animals (2-5 instead of 16-24). Slightly looser bodies, a softer dragonfly.
* **Chain demo** (`chain.json`, `evo_grid/chain.json`, viewer-packed): one swarm eaten four times in a
  row. hgrid2: whale 1.7 -> jellyfish 4.2 -> pufferfish 2.0 -> dragonfly 7.6 -> (cull toward Mass; Space is
  the true majority, see above) jellyfish 1.2, shedding 79 crystals one by one over its life. Evo + grid:
  2.1 -> 6.6 -> 2.7 -> 8.8 -> 3.4. Measured speeds per step (C/M/S/T): hgrid2 0.07/0.08/0.17/0.39, evo+grid
  0.10/0.11/0.18/0.58 - Time zips in both.
* **Ship** (`hgrid2_ship.py`, `ship.json`): a ship flying through at 2.5 voxels/step writes a threat blob
  + fading wake into a grid channel; tadpoles flee its gradient. The swarm PARTS around the ship (it
  touched 0-3 tadpoles in a full pass), divergence rises a little (e.g. charge 1.8 -> 4.0 at worst) and
  the same morphogen closes the hole within 30-60 steps. Nothing dies.

## Game uses (direction d)

* **The ship writes into the same field** (above): one extra channel, no new creature rule; the reaction
  (part, then re-close) is emergent from flee + the existing deficit.
* **A cell-scale morphogen herding several swarms** (`hgrid2_cell.py`, `cell.json`, `cell_fast.json`):
  three swarms (whale, jellyfish, pufferfish) in one cell. The Cell owns one 24^3 field of 8-voxel cells:
  a SHARED density channel (each swarm is pushed down the gradient of the OTHERS' density) and a home per
  swarm. The first version pushed members toward home and failed (home error grew to 45): the body's own
  fine field, anchored on its centroid, pulled it back. What works is letting the Cell write WHERE the body
  is - the swarm's morphogen centre walks toward its home (<= 0.3 voxel/step, never leading the body by
  more than 6) and the body swims after its own field. With homes orbiting the cell (~87 voxels in 600
  steps): every swarm stays within 2.5-7 voxels of its moving home, bodies stay accurate (1.4-4.4), and
  zero tadpoles ever sit within 2.5 voxels of another swarm. Limit: at 2.5x that speed (0.36 voxel/step)
  the bodies lag 8-37 voxels and the jellyfish loosens to 7.8 - herd at <= ~0.15-0.2 voxel/step.
  This is the shape for the game: the Cell writes territories / a nucleus orbit / "flee the ship" into
  one field; creatures never know about each other.
* **Cost at game scale** (per swarm per update, n <= 200 tadpoles, M <= 192 plan units):
  fine layer ~ n x M Gaussians (~40k exp + 120k mul-add); migrants ~ k x M for the few migrants;
  starvation/laying O(n); the coarse grid 16^3 x 12 classes = 49k floats (200 KB) splatted from 8n
  corners, blurred and differentiated (~0.5M flops). Total well under 1 MFLOP per swarm per update: a
  Burst job in the 0.05-0.2 ms range, and the grid can update every 2-4 frames. The plan targets are
  tiny (4 plans x 8 frames x <= 192 units) - no baked plan grids are needed if the composition integral
  is taken from the plan's unit counts directly. Python reference: 21-31 ms/step/swarm, all overhead.
  Collider budget impact: none (tadpoles are the existing fauna; the grid is pure data).

## What failed

* Finer coarse grid (cell 4 or 3): worse (12.9-14.2 on the jellyfish) - the gradient loses reach.
* `lay_major` (scaling laying pressure by class size): starves small classes, swarms never grow.
* Spatial slot->domain choice (`dmap_space`): no effect (counts already decided it).
* Neighbour swaps (`k_swap`): +-0.5, not the bottleneck.
* Count-margin hysteresis: blocks the cull's own switch; replaced by a time lock.
* Evo + grid with looks eased toward the grid (`ease_look` 0.2): worse accuracy AND part of the twitch.

## Recommendation for the next round

1. **Port hgrid2 to C#** as the accurate creature: fine layer + migrants + staggered starvation + plan
   lock are each a few dozen lines, O(n x M), no assignment solver. Drop coarse-grid steering (finding 4).
2. **Ship the evo body + grid as the "wilder" species variant** - it is the lead's preferred motion and
   now accurate on 13/16 (dragonfly switches 8.3-9.8 are the gap; the designed boid passes them).
   Tried this round: feed-forward gain, frame period, fine gain, migrant threshold, starvation rate and
   small-element slack - the three dragonfly switches move between 6.8 and 10 with seed and setting, i.e.
   they sit ON the bar (about 2.2 of each is the domain floor, finding 2). Fixing the yardstick's domain
   floor would very likely take the evo body to 16/16.
3. **Fix the yardstick's 2-slot domain search** (finding 1) and decide whether the dragonfly domain
   floor (finding 2) should be scored at all - it penalises every whale/puffer -> dragonfly by ~2.
4. **Game:** port the ship-threat channel and the cell-level anchor + shared-density field with the C#
   creature (both are a few grid ops per cell per update); herd at <= 0.2 voxel/step. Measure fun in the editor: the parting-and-closing reaction
   and the one-by-one withering are the two moments most worth watching.

## Files

`results/hgrid2/`: `summary.json` + `rollout.json` (viewer, `swarm_nca.pack`), `eval16.json`,
`robust_1000.json`, `robust_2000.json`, `robust_3000.json`, `probe.json`, `feel.json` (+ the calibration table),
`feel_calibration.json`, `params.json`, `ship.json`; `round_a/` is the first tier-1 pass (fine layer +
feed-forward only, 6/15). `evo_grid/`: the same set for the evo body + grid.
Code: `hgrid2_model.py`, `hgrid2_evo.py`, `hgrid2_eval.py` (score + publish), `hgrid2_diag.py` (per-term),
`hgrid2_sweep.py`, `hgrid2_switch.py`, `hgrid2_robust.py`, `hgrid2_feelcal.py`, `hgrid2_feelsweep.py`,
`hgrid2_ship.py`, `hgrid2_cell.py`, `hgrid2_chain.py`, `swarm_feel.py`. `cell.json` / `cell_fast.json`: the herding runs.
