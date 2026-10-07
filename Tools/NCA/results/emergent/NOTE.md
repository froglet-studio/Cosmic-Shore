# Species 3: the pure emergent swimmer (`cece/swarm-x-emergent`)

**Headline (published `rule.pt` = run em_c step 550; `emergent_eval.py`, seeds 7 / 23 / 41 / held-out 1000):**

| axis | result | bar / reference |
|---|---|---|
| SHAPE, strict (one fitted phase line over a 192-step window) | **10.28** (10.89 / 10.96 / 10.03 / 9.24) | four-plan bar 8; posinfo2's own plans 1.6-2.7. **Fails the bar.** |
| SHAPE, static (the best single frame, held) | 9.26 (9.72 / 9.90 / 9.19 / 8.22) | at age 300-600 the same rule averages 8.0-8.1 (`ablation_chem.json`) |
| ANIMATION gain (static - strict) | **-1.03: no pulse emerged** | > 0 would mean the body really cycles through the frames |
| TEMPO | **none.** The fit reports 140 steps at every seed, but that is the phase fit settling on its slowest admissible line, not a cycle | trained period: 64 steps per cycle |
| GROWTH | from 16 to 90% of its final size in 380-420 steps with no script; final size 280 (the world cap) at every seed | the plan has 88 |
| REGROWTH (swarm_probe strike, ~1/3 removed, 160 steps) | heal **0.50** (0.56 / 0.27 / 0.52 / 0.67); half-healed in 100-160 steps; never 90% within 160 | posinfo2: 0.44-1.05; warm start: -0.13 |
| LOSSLESS | **0 self-inflicted deaths** in 817k tadpole-steps (grow + window + regrow) | goal 0 (death is penalised in training, never masked) |
| PERFORMANT | 16.2 ms/step at 280 tadpoles, 1 thread, Python/torch | posinfo2 5.2 ms at ~90-190 |
| EMERGENT | **organic band IN** (jerk_rel 1.99, osc 0.072, stuck 0.000, planar excess 0.00); locality **local** | evo jellyfish row: jerk_rel 1.93, osc 0.041 |

**In one line:** a fully local learned rule grows a recognisable jellyfish from 16 tadpoles, keeps it (static 8-10) and
heals about half a vessel strike, with zero deaths and evo-like organic motion. It does **not** get under the loss-8 bar
(strict 10.3), and it does **not** swim: the animated cycle never emerged.

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
| secretion of each chemical | LEARNED (but unused, see below) |
| composition (who is laid as what) | EMERGENT from breed-true laying. The seed is laid at the plan's mix and stays near it (grown 27 / 1 / 64 / 9% vs the plan's 28 / 2 / 61 / 8%). No quota or homeostat. |
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

Strike it with a vessel and the hole half-closes over 100-160 steps; the body grows back to the cap. Nothing ever dies.
Seeded with another plan's mix, it still grows a jellyfish (every row of `summary.json` is closest to the jellyfish).
The rule is a jellyfish attractor, as a one-target rule should be.

## The full yardstick, and what failed

`eval.json` (step 550), `eval_step650.json`, `eval_warm_start.json` (step 0), `probe.json`, `feel.json`.

1. **SHAPE fails the bar: 10.3 strict / 9.3 static against 8.**
   - At its best age the body is around 8: the static mean is 8.04 at age 300 and 8.09 at age 600, with seeds ranging
     5.8 to 11.2.
   - It drifts slowly while it sits at the 280 cap.
   - Training took the local warm start from strict 15.5 to 10.3, and its regrowth from -0.13 to 0.50.
   - Checkpoints are noisy: 2-seed strict scores ran 12.8 / 11.4 / 15.3 / 12.4 / 10.9 at steps 150-550, and step 650
     scored 11.8 on four seeds.
2. **ANIMATION failed outright.**
   - The jellyfish frames differ by ~4 in divergence (bell open vs closed), so a real pulse would pay.
   - The phase-free animated loss never produced a coherent oscillation in ~650 BPTT steps. Strict is worse than
     static at every checkpoint and every seed.
   - A pulse needs a synchronised per-tadpole oscillator, plus the motion keyed to it. Neither exists in the warm start,
     and at this budget gradient descent did not find them from zero.
3. **TEMPO: none to report** (see the table).
4. **STIGMERGY was not used** (`ablation_chem.json`, `emergent_chem_ablate.py`).
   - Cutting every chemical input changes the static loss by noise: 8.04 → 8.31 at age 300, 8.09 → 7.96 at age 600.
   - The learned chemical input weights are ~20x smaller than the neighbour inputs.
   - The channel is there and differentiable end to end, but it never became load-bearing. This answers idea 1
     negatively, for this budget.
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
- **persistence**: the body is ~8 at age 300 and drifts after;
- **size**: the cap;
- **the absent pulse**: about 1 of strict loss.

Positional information (posinfo2's body frame) is what took the hybrid from ~7 to ~1.7. It is a body-wide reduction,
not local.

## Comparison with posinfo2 (species 2, frozen)

| | posinfo2 (hybrid) | emergent (this) |
|---|---|---|
| task | 4 plans + 12 switches | 1 plan, animated (relaxed) |
| jellyfish shape | own 1.62-1.82 (static, at its native 88) | strict 10.3, static 9.3 (~8 at best age) |
| animation | not asked | asked; not achieved |
| composition | DESIGNED homeostat (quota, molting, majority guard) | emergent from breed-true laying; mix within ~3 points of the plan |
| death | masked | learned and penalised; 0 deaths |
| perception | neighbours + body frame + element census (**mixed**) | neighbours + self-secreted chemicals (**local**) |
| headcount | exact (homeostat) | fills the 280 cap |
| strike heal (jellyfish) | 0.95 | 0.50 |
| feel (jellyfish row) | jerk_rel 2.02, osc 0.012, coherence 0.20, speed 0.10 | jerk_rel 1.99, osc 0.072, coherence 0.15, speed 0.18; nearer evo (1.93 / 0.041 / 0.12 / 0.17) |
| cost | 5.2 ms/step | 16 ms/step (280 tadpoles + a 32³×4 grid) |

posinfo2 is ~6x more accurate on the jellyfish, and it gets there with exactly the parts this brief forbids: a body
frame and a designed composition controller. The pure rule is the more alive-looking of the two: faster and closer to
evo's motion. But it is a bigger, blurrier jellyfish that does not pulse.

## Recommendation

1. **Not yet a game creature on accuracy.** It is the first fully local, lossless, uncontrolled rule to hold a
   recognisable plan near the bar. As "ambient jellyfish drift" it is viable today.
2. **For the pulse, give the rule an oscillator to learn WITH, not a clock to obey.** Two candidates:
   - a 2-channel rotation in the hidden state, initialised but trainable, so tadpoles start as oscillators and the
     loss only has to learn coupling and motion;
   - a chemical that the rule can make excitable, an activator-inhibitor pair, so a pulse can travel through the
     water.

   Then a birth-clock curriculum like particle_nca's swim2d: fresh seeds held to the clock for the first cycles.
3. **For size**, a local satiety cue is the honest analogue of the census; the trained rule ignored the one it had.
   Shape the secretion so concentration tracks the body (calib r = 0.83), or put a small explicit penalty on laying
   while the local chemical is high. The census ablation says size, not the mix, is what a global input would have
   carried.
4. Compute: ~650 BPTT steps at 12-25 s each fit this night. The learning curve was still falling (training sink 19 →
   11-13). A GPU or a longer CPU run of the same `emergent_train.py --stage anim` is the cheapest next step.

## Files

- Code (all new): `emergent_model.py` (rule + water), `emergent_train.py` (BPTT, pool, strikes, curriculum flags),
  `emergent_eval.py` (this yardstick), `emergent_publish.py`, `emergent_ablate.py` (census), `emergent_chem_ablate.py`.
- Here: `rule.pt`, `eval.json`, `probe.json`, `feel.json`, `summary.json` + `rollout.json` (`swarm_nca.rollout` +
  `pack`, as the GPU publisher writes; rows other than "space" are the same rule from another plan's seed), `swim.gif`
  (left: the swarm; right: the target frame at the fitted phase), `ablation_census.json`, `ablation_chem.json`,
  `eval_warm_start.json`, `eval_step650.json`, `log.jsonl` + `config.json` (run em_c).
- Run history (logs in `runs/`, gitignored):
  - em_a: static stage from a noisy warm start; no progress, and killed by a container restart.
  - em_b: animated stage, lr 3e-4. It made the rule WORSE: it learned "never lay" (fixed by the body floor) and
    drifted to strict 19.
  - em_c: animated stage, lr 1e-4 → 3e-5, body floor 5. Published.
- Reproduce:

  ```
  python Tools/NCA/emergent_train.py --run runs/em_c --stage anim --steps 1000 --init-solo 1 --lr 1e-4 --lr-end 3e-5 --min-body 88 --w-body 5
  ```

  (stopped at step 650; step 550 published), then `python Tools/NCA/emergent_eval.py --rule <rule> --out eval.json`.
