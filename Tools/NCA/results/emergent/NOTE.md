# Species 3: the pure emergent swimmer (`cece/swarm-x-emergent`)

**Headline (published `rule.pt` = run em_c step 750; `emergent_eval.py`, seeds 7 / 23 / 41 / held-out 1000):**

| axis | result | bar / reference |
|---|---|---|
| SHAPE, strict (one fitted phase line over a 192-step window) | **8.94** (7.46 / 11.18 / 11.54 / 5.58): 2 of 4 seeds under 8 | four-plan bar 8; posinfo2's own plans 1.6-2.7. **Fails the bar on the mean.** |
| SHAPE, static (the best single frame, held) | 8.10 (6.57 / 10.48 / 10.63 / 4.71) | at age 600 the same rule averages 8.0 (`ablation_chem.json`) |
| ANIMATION gain (static - strict) | **-0.85: no pulse emerged** | > 0 would mean the body really cycles through the frames |
| TEMPO | **none.** The fit reports 140-151 steps (cv 0.04), but that is the phase fit settling on a slow admissible line, not a cycle | trained period: 64 steps per cycle |
| GROWTH | from 16 to 90% of its final size in 380-440 steps with no script; final size 280 (the world cap) at every seed | the plan has 88 |
| REGROWTH (swarm_probe strike, ~1/3 removed, 160 steps) | heal **0.69** (0.66 / 0.53 / 0.63 / 0.93); half-healed in 60-130 steps; 90% only at seed 1000 | posinfo2: 0.44-1.05; warm start: -0.13 |
| LOSSLESS | **0 self-inflicted deaths** in 793k tadpole-steps (grow + window + regrow) | goal 0 (death is penalised in training, never masked) |
| PERFORMANT | 14.6 ms/step at 280 tadpoles, 1 thread, Python/torch | posinfo2 5.2 ms at ~90-190 |
| EMERGENT | **organic band IN** (jerk_rel 1.99, osc 0.068, stuck 0.000, planar excess 0.00); locality **local** | evo jellyfish row: jerk_rel 1.93, osc 0.041 |

**In one line:** a fully local learned rule grows a recognisable jellyfish from 16 tadpoles, keeps it (static 8.1 mean,
4.7-10.6 by seed) and heals about two thirds of a vessel strike, with zero deaths and evo-like organic motion. It gets
**close to, but not under, the loss-8 bar** (strict 8.9; 2 of 4 seeds pass), and it does **not** swim: the animated
cycle never emerged. **By step 750 its self-secreted chemicals are load-bearing**: cutting them costs +2.9.

## What the model is

`emergent_model.EmergentRule`: one MLP shared by every tadpole (posinfo/G2 shape: inputs → 192 → 192 residual → outputs),
trained by BPTT through the simulation, the positions, the states and the chemical water.

A tadpole perceives only:
- **its neighbours within R = 8.** This is `swarm_nca.SwarmRule.perceive`, unchanged: its own state; same-domain and
  other-domain neighbour means; neighbour gradients; local density.
- **four chemicals that tadpoles themselves secrete.** Each lives on a fixed 32³ world grid (3 voxels per cell: the
  water, not a body frame). Each step it diffuses to the 6 neighbouring cells and decays. A tadpole reads the log
  concentration and the relative gradient at its own position. (`ESwarm.fld` carries the water with the swarm, so the
  shared probes clone, cull and strike it correctly.)

The population inputs (`glob`: headcount and element census) are removed. There is no centroid, body axis, clock,
slot, quota or attractor.

## Designed vs learned vs emergent, line by line

| part | status |
|---|---|
| the target: the jellyfish, its 8 frames, its element mix | DESIGNED (`swarm_nca.load_targets`, unchanged) |
| world physics: collision, membrane, per-element top speed, R, capacity 280, egg rules (bud when < 7 neighbours, egg life 6, breed-true domain, 0.5% element mutation) | DESIGNED (`swarm_nca.World`, unchanged) |
| the chemical medium: 4 channels on a fixed grid, explicit diffusion + decay, secretion ≥ 0 | DESIGNED (the existence of a stigmergic channel) |
| the chemicals' diffusion and decay rates | LEARNED (bounded); in practice barely moved from init (D 0.13 / 0.10 / 0.08 / 0.04, ranges 10 / 6 / 3 / 2 voxels) |
| the perception wiring (what a tadpole may read) | DESIGNED (local only, by construction) |
| velocity, facing, prism, Charge state, spindle, hatching, hidden channels | LEARNED (MLP) |
| laying gate (whether a parent lays) | LEARNED (World.learned_lay; gradient through the straight-through child weight) |
| death | LEARNED output, **penalised** (`w_survive` 60 on the death pull), never masked. It chose 0 deaths. |
| secretion of each chemical | LEARNED; load-bearing by step 750 (see below) |
| composition (who is laid as what) | EMERGENT from breed-true laying plus the learned per-tadpole laying gate. No quota or homeostat. It drifts: at step 550 the grown mix was 27 / 1 / 64 / 9% (plan 28 / 2 / 61 / 8%); at step 750 it is 39 / 1 / 56 / 5%, so Charge over-lays. |
| body outline, the element and domain regions (orange Charge rim, Space bell and tentacles) | EMERGENT (from local rules; in the loss) |
| headcount (fills the 280 cap) | EMERGENT (not in the loss except the one-sided floor at 88) |
| healing after a strike | EMERGENT, partly trained (8% of pool samples are struck during training) |
| motion feel | EMERGENT (never in the loss) |
| **training-only** loss terms: Sinkhorn divergence to the frame sequence at the best phase (`sn.anim_loss`, k0 free: no clock), the death pull, overflow `w_over`, speed term, one-sided body floor (`w_body` 5 below 88) | DESIGNED objective; none of it is an input or a force at run time |
| **the warm start** | the weights started from the `solo_space` specialist (G2 fine-tuned on the jellyfish) with its census inputs FOLDED INTO A CONSTANT BIAS, so the initial rule was already purely local. Lineage, not input; disclosed because the shape skill was mostly learned there. |

## What a player would see

A loose, shimmering cloud of tadpoles (`swim.gif`; viewer: `rollout.json`, the "space" row) grows over about 400
steps into a jellyfish-shaped body with:
- a Space-blue dome on top;
- a ring of orange Charge tadpoles around its rim;
- a skirt of Space tentacles trailing below;
- a few Time runners in the dome.

It is about three times the plan's headcount, a big soft jellyfish rather than a crisp one. The motion is evo-like:
everyone does the same thing slightly differently, nobody freezes, and nobody marches in step. **It does not pulse.**
The bell stays at a frame-0/7-like pose, so this reads as a drifting jellyfish rather than a swimming one.

Strike it with a vessel and the hole half-closes within 60-130 steps; about two thirds of the damage is gone after 160, and the body grows back to the cap. Nothing ever dies.
Seeded with another plan's mix, it still grows a jellyfish (every row of `summary.json` is closest to the jellyfish).
The rule is a jellyfish attractor, as a one-target rule should be.

## The full yardstick, and what failed

`eval.json` (step 750), `eval_step550.json`, `eval_step650.json`, `eval_step800.json`, `eval_warm_start.json` (step 0), `probe.json`, `feel.json`.

1. **SHAPE misses the bar on the mean: 8.9 strict / 8.1 static against 8.**
   - Seeds 7 and 1000 pass: 7.5 and 5.6 strict.
   - Seeds 23 and 41 do not: 11.2 and 11.5.
   - Training took the local warm start from strict 15.5 to 8.9, and its regrowth from -0.13 to 0.69.
   - Checkpoints are noisy:

     | step | strict, 2 seeds (7 / 23) | strict, 4 seeds |
     |---|---|---|
     | 150 | 12.8 | |
     | 250 | 11.4 | |
     | 350 | 15.3 | |
     | 450 | 12.4 | |
     | 550 | 10.9 | 10.3 |
     | 650 | | 11.8 |
     | 750 | 9.3 | 8.9 (published) |
     | 800 | 10.7 | 8.7 |

   Step 800 (`eval_step800.json`) is within noise on shape, and its heal falls to 0.31 (seed 41: -0.34). Step 750
   stays the published rule as the better balance.

   - Run em_c's training sink fell from 19 to about 12.7 and was flattening at the end. Step 750 was the last snapshot
     scored.
2. **ANIMATION failed outright.**
   - The jellyfish frames differ by ~4 in divergence (bell open vs closed), so a real pulse would pay.
   - The phase-free animated loss never produced a coherent oscillation in 750 BPTT steps. Strict is worse than static
     at every checkpoint and every seed.
   - A pulse needs a synchronised per-tadpole oscillator, plus the motion keyed to it. Neither exists in the warm start,
     and at this budget gradient descent did not find them from zero.
3. **TEMPO: none to report** (see the table).
4. **STIGMERGY became load-bearing late** (`ablation_chem.json`, `emergent_chem_ablate.py`). Cutting every chemical
   input, at age 300 / 600:

   | rule | as trained | chemicals cut |
   |---|---|---|
   | step 550 | 8.04 / 8.09 | 8.31 / 7.96 (noise) |
   | step 750 | 10.6 / 8.03 | 12.2 / 10.88 (seed 41: 9.1 → 16.4) |

   The learned chemical input weights are still ~17x smaller than the neighbour inputs, and the diffusion and decay rates
   barely moved. So the channel is being picked up, weakly. This is the first sign that a self-secreted field can stand
   in for what the global inputs carried.
5. **Size is not controlled**: every body fills the 280 cap.
   - Capping is not the fix: re-running the warm start with capacity 100-200 made the shape worse (`runs/capdiag.py`).
   - The divergence prefers more tadpoles at the plan's spacing.
6. LOSSLESS, ORGANIC and LOCAL all pass.

### Which single non-local input buys the most? (`emergent_ablate.py`, `ablation_census.json`)

Measured on the `solo_space` specialist, the closest relative that has non-local inputs: jellyfish grown 300 steps,
seeds 7 / 23 / 41.

| inputs | static loss |
|---|---|
| both global inputs, as trained (headcount + element census) | **7.69** |
| both replaced by consistent constants (a purely local rule) | **7.72** |
| headcount live, mix constant | 9.57 |
| mix live, headcount constant (88 / 150) | 11.7 / 12.1 |

So **no single global input buys anything once the rule is consistent without it**: the census is worth 0.03. The
mismatched half-ablations are worse only because the rule was trained on the joint signal. The gap to the bar is
therefore not a missing census. It is:
- **persistence** (seed spread 4.7-10.6);
- **size**: the cap;
- **the absent pulse**: about 1 of strict loss.

Positional information (posinfo2's body frame) is what took the hybrid from ~7 to ~1.7. It is a body-wide reduction,
not local.

## Comparison with posinfo2 (species 2, frozen)

| | posinfo2 (hybrid) | emergent (this) |
|---|---|---|
| task | 4 plans + 12 switches | 1 plan, animated (relaxed) |
| jellyfish shape | own 1.62-1.82 (static, at its native 88) | strict 8.9, static 8.1 (seeds 4.7-10.6) |
| animation | not asked | asked; not achieved |
| composition | DESIGNED homeostat (quota, molting, majority guard) | emergent from breed-true laying + learned gate; Charge drifts +11 points over the plan |
| death | masked | learned and penalised; 0 deaths |
| perception | neighbours + body frame + element census (**mixed**) | neighbours + self-secreted chemicals (**local**) |
| headcount | exact (homeostat) | fills the 280 cap |
| strike heal (jellyfish) | 0.95 | 0.69 |
| feel (jellyfish row) | jerk_rel 2.02, osc 0.012, coherence 0.20, speed 0.10 | jerk_rel 1.99, osc 0.068, coherence 0.10, speed 0.19; nearer evo (1.93 / 0.041 / 0.12 / 0.17) |
| cost | 5.2 ms/step | 14.6 ms/step (280 tadpoles + a 32³×4 grid) |

posinfo2 is ~4-5x more accurate on the jellyfish, and it gets there with exactly the parts this brief forbids: a body
frame and a designed composition controller. The pure rule is the more alive-looking of the two: faster and closer to
evo's motion. But it is a bigger, blurrier jellyfish that does not pulse.

## Recommendation

1. **Close on accuracy, not there.** It is the first fully local, lossless, uncontrolled rule to hold a recognisable
   plan at the bar on some seeds (2 of 4). As "ambient jellyfish drift" it is viable today.
2. **For the pulse, give the rule an oscillator to learn WITH, not a clock to obey.** Two candidates:
   - a 2-channel rotation in the hidden state, initialised but trainable, so tadpoles start as oscillators and the
     loss only has to learn coupling and motion;
   - a chemical that the rule can make excitable, an activator-inhibitor pair, so a pulse can travel through the
     water.

   Then a birth-clock curriculum like particle_nca's swim2d: fresh seeds held to the clock for the first cycles.
3. **For size**, a local satiety cue is the honest analogue of the census; the chemicals only started to matter at
   step 750.
   Shape the secretion so concentration tracks the body (calib r = 0.83), or put a small explicit penalty on laying
   while the local chemical is high. The census ablation says size, not the mix, is what a global input would have
   carried.
4. Compute: 750 BPTT steps at 12-25 s each fit this night. The 4-seed score was still improving (10.3 at 550 → 8.9 at
   750), and the chemicals had only just become useful. A GPU or a longer CPU run of the same `emergent_train.py --stage anim` is the cheapest next step.

## Files

- Code (all new): `emergent_model.py` (rule + water), `emergent_train.py` (BPTT, pool, strikes, curriculum flags),
  `emergent_eval.py` (this yardstick), `emergent_publish.py`, `emergent_ablate.py` (census), `emergent_chem_ablate.py`.
- Here: `rule.pt`, `eval.json`, `probe.json`, `feel.json`, `summary.json` + `rollout.json` (`swarm_nca.rollout` +
  `pack`, as the GPU publisher writes; rows other than "space" are the same rule from another plan's seed), `swim.gif`
  (left: the swarm; right: the target frame at the fitted phase), `ablation_census.json`, `ablation_chem.json`,
  `eval_warm_start.json`, `eval_step550.json`, `eval_step650.json`, `log.jsonl` + `config.json` (run em_c).
- Run history (logs in `runs/`, gitignored):
  - em_a: static stage from a noisy warm start; no progress, and killed by a container restart.
  - em_b: animated stage, lr 3e-4. It made the rule WORSE: it learned "never lay" (fixed by the body floor) and
    drifted to strict 19.
  - em_c: animated stage, lr 1e-4 → 3e-5, body floor 5. Published at step 750.
- Reproduce:

  ```
  python Tools/NCA/emergent_train.py --run runs/em_c --stage anim --steps 1000 --init-solo 1 --lr 1e-4 --lr-end 3e-5 --min-body 88 --w-body 5
  ```

  (stopped at step 750; step 750 published), then `python Tools/NCA/emergent_eval.py --rule <rule> --out eval.json`.
