# Play-hardened learned swarm (direction "play", branch `cece/swarm-x-play`)

**Goal:** make a swarm that is fun to fight and fly through. It should heal when struck and react to
something moving through it, while keeping the body-plan and switch behaviour of the best learned
rule, G2.

**Headline:** the published rule (`rule.pt` = p2 step 1500) does two new things:

- It **opens a tunnel** in front of an approaching predator and **seals it behind**. Kills per pass
  fall from 13.4 to 3.6, and crowding inside the danger zone halves. A control with the predator
  inputs zeroed proves this is a learned reaction and not a side effect.
- It **heals strikes** about 4× better than G2: post-strike excess 2.99 → 0.76.

**Cost:** G2's switch behaviour, which was only 1 of 3 seeds to begin with, is lost. Strict
`tests_passed` is 4/8 on all three rollout seeds, against 5/4/4 for G2. All four own-plan tests pass
every time, with wider margins than G2.

## What was built (all new files; nothing shared was edited)

**`play_swarm.py`.** Contains `PlayRule`, which subclasses `SwarmRule`, plus a fine-tune loop and
the probes.

- **Predator physics.** A sphere of radius r moves straight at a constant speed. Inside its core
  (0.75 r) it kills each live tadpole with probability 0.5 per step; a dead tadpole leaves a crystal
  and counts as a death. It shoves everything else out of its sphere (push 0.8 per step).
- **Predator sensing.** Each tadpole gets 7 new inputs, which are zero when no predator is near:
  - the predator's relative position, scaled by its sense radius (3 r) and weighted by falloff;
  - the falloff f;
  - the predator's velocity × f.

  These enter at zero weight from G2, so on the shared yardstick (no predator) the rule starts out
  identical to G2. Since p2 the inputs are amplified ×3 (`pred_gain`); see "What failed".
- **Damage in the training pool,** regenerating-lizard style. Each picked sample may get any of the
  following, at a random step of a 48–96-step rollout. Because the loss is taken at the end, healing
  is scored at many different delays after damage.
  - a `swarm_probe.strike` sphere (0.6–1.2 × the swarm's RMS radius): p 0.25
  - a random element-blind cull of 10–40%: p 0.08
  - a blast that shoves without killing: p 0.12
  - a predator pass (speed 0.5–1.5, radius 4–8) timed to cross the centroid inside the
    backpropagated window: p 0.35–0.5

  After a strike or cull the label follows the current majority. G2's switch culls stay on (sticky
  labels, ratio/tie culls).
- **Reaction objective.** While a predator is near, the loss adds
  `w_avoid · mean over the window of Σ_alive relu(1 − d / (gap · r))²`, i.e. "keep out of a gap of
  `gap` radii around it". Nothing explicitly says "close behind it": the ordinary body-plan loss at
  the end of the window does that.
- **Unchanged from G2:** the G2 loss itself (Sinkhorn + overflow + body floor + survival), the
  learned laying gate, and per-tensor normalised gradients.

**`play_compare.py`.** A multi-seed comparison on identical seeds:
- `tests_passed` at rollout seeds 7, 8 and 9;
- strikes at 4 seeds;
- an undisturbed drift control at 4 seeds;
- predator passes at 3 seeds.

**`play_render.py`.** Renders a predator pass side-on (`predator_pass.png`, and
`predator_pass_g2.png` for the G2 control).

**Runs, on 4 CPU cores at about 4 s/step:**

| Run | Warm start | Steps | Settings |
|---|---|---|---|
| p1 | G2 | 1375 (stopped) | `w_avoid=2`, `gap=1.6`, predator speed 1–3 |
| p2 | p1 step 1375 | 1500 | `w_avoid=8`, `gap=2`, `pred_gain=3`, predator speed 0.5–1.5 |

## Results (multi-seed; `compare.json`)

Every single-trajectory number in this system is chaotic. The same rule run twice differs in the
first decimal after a few hundred steps, from float threading order alone. The per-snapshot evals in
`progress.json` are therefore only indicative; this table is the evidence.

| Rule | `tests_passed` (seeds 7/8/9) | Own / switch per seed | Strike: cut → excess after 120 steps (0 = healed) | Drift, no strike | Predator kills / pass | Gap | Crowd in 1.6 r | Shape hurt during pass |
|---|---|---|---|---|---|---|---|---|
| G2 (baseline) | 5 / 4 / 4 | 4,4,4 / 1,0,0 | 2.74 → **2.99** | 0.76 | **13.4** | 4.44 | 45.7 | 0.48 |
| p1 step 625 | 5 / 5 / 4 | 4,4,4 / 1,1,0 | 1.83 → **0.29** | 0.51 | 12.3 | 4.46 | 44.8 | 0.23 |
| p1 step 1375 | 4 / 4 / 4 | 4,4,4 / 0,0,0 | 4.30 → 2.76 | 0.15 | 12.0 | 4.29 | 45.1 | 0.22 |
| p2 step 1000 | 4 / 4 / 4 | 4,4,4 / 0,0,0 | 3.41 → 1.40 | 0.10 | 2.6 | 4.97 | 21.4 | 1.41 |
| **p2 step 1500 (published)** | **4 / 4 / 4** | 4,4,4 / 0,0,0 | 3.10 → **0.76** | 0.11 | **3.6** | 4.94 | **20.5** | 1.53 |
| p2 step 1500, predator inputs zeroed | 4 / 4 / 4 | 4,4,4 / 0,0,0 | 3.10 → 0.76 | 0.11 | 11.7 | 4.55 | 46.1 | 0.25 |

How to read the table:

- **The blind control is the key row.** The same weights with the 7 predator inputs zeroed go back
  to G2-level kills (11.7) and crowding (46). The dodge is therefore a learned response to the
  sensed predator. It is not a sparser body: drift and strike numbers are identical with and without
  sensing.
- **"Shape hurt" rises from 0.5 to 1.5 during a pass.** That is the tunnel: the body deforms on
  purpose to let the predator through. It is back to its pre-pass score within 120 steps (after −
  before = −0.01).
- **Healing:** G2 ends 3.0 worse than before a strike, while p2 ends 0.76 worse against a drift of
  0.11. Lower drift also matters: the p1/p2 bodies wander less when untouched (0.76 → 0.11), so
  their heal numbers are not flattered by drift.
- **p1 step 625 is the best "heal-only" rule** (`rule_p1_heal_only.pt`): 5/5/4 tests and strike
  excess 0.29. But it has no predator reaction, and its drift is 0.51.

Canonical yardstick for the published rule (`summary.json`, seed 7):
- **Own-plan divergences** are 12.7 / 10.9 / 10.3 / 14.4, against 52–78 off-diagonal (G2: 13.9 /
  17.5 / 15.3 / 20.7).
- **All four switches fail by reverting.** For example, after the jellyfish loses its Space
  majority it ends Space-majority again ([100, 7, 133, 40]) and jellyfish-shaped (18.0). The rule
  re-lays the old element faster than the new majority grows. This is the composition-drift failure
  the README already describes, made a bit worse by healing training. Healing teaches "return to
  what you were"; switching needs "become what your ratios say", and with sticky labels set after a
  hit the two conflict.

`probe.json` holds `swarm_probe.probe` at the canonical seed, plus the multi-seed strike, drift and
three predator passes.

## What a player would see (`predator_pass.png` vs `predator_pass_g2.png`)

**Flying through the published rule:**

- **The swarm parts ahead of you.** Tadpoles start sliding sideways when the ship is about 3 radii
  away (the sense range). The jellyfish and the pufferfish show a clean tunnel bored along the
  flight line, with the body intact either side.
- **The tunnel seals within about 60 steps** after the ship leaves, and the body re-forms. Ramming
  through kills about 4 tadpoles instead of about 13. The swarm reads as alive and evasive, not as a
  cloud the ship sweeps up.
- **The dragonfly (Time, fast units) is the most dramatic.** Its tadpoles clear about 7.6 units
  from the predator's centre (gap 7.6 against a radius of 6).

**Flying through G2:** the ship ploughs a hole, the swarm closes around the hull while it is still
inside, and about 13 tadpoles die per pass.

**After a vessel strike** removes a third of the swarm, p2 regrows the missing region back to the
plan, where G2 regrows to a wrong shape.

**Fun caveats:**
- The dodge is a sidestep, not a dramatic burst. Tadpole top speed is 0.8 per step (Time 2.0), so
  against a fast ship (speed 3) they mostly cannot get out of the way.
- Nothing schools or swims yet; the body holds station.

## What failed / lessons

1. **A weak reaction signal learns nothing.** p1 (`w_avoid=2`, zero-initialised predator columns,
   predators up to speed 3) showed no reaction after 1375 steps: kills, gap and crowd stayed at G2
   levels, and the predator columns of `w1` stayed about 5× smaller than the rest. Three changes
   together produced the reaction within 125 steps:
   - `w_avoid` 2 → 8;
   - `pred_gain=3` on the inputs;
   - slower predators (0.5–1.5), so that dodging is physically possible.
2. **Single-snapshot evals are noise.** p1's per-snapshot strike excess bounced between −0.4 and 3.1.
   The `heal` fraction in `swarm_probe` divides by (cut − before), which is often about 0 or negative
   after a one-third strike because the body's own drift is as large as the damage. It produced
   values like 19.9 or −31. Report `recovered − before` against a drift control instead.
3. **Healing training costs switching.** See above. p1 step 625 kept G2's one switch; every later
   snapshot lost it.
4. **Process:** a `nohup`'d job died with the shell at step 80. Run jobs as tracked background tasks
   with snapshots every 125 steps.

## Recommendation for the next round

1. **Combine this rule's damage and predator pool with a composition fix,** i.e. whichever direction
   wins switching: `learned_egg`, or a rule-level or mechanism-level fix for composition. The
   healing pressure here makes reversion worse, so it needs that counterweight. Concretely:
   fine-tune `rule.pt` with `learned_egg=1` (Time/egg channels 26–29 are currently hidden state, so
   expect a short relearning dip), keeping `p_strike`, `p_pred` and `w_avoid` as here.
2. **Keep predator sensing as an explicit input,** in the game too. It is cheap: 7 floats per
   tadpole from one sphere. Without it no reaction can be learned. The same channel could carry the
   player's vessel, which makes "the swarm parts around my ship" a direct product of this work.
3. **Add a schooling/swim objective next** (e.g. the centroid must follow a slow moving waypoint
   while holding the plan) so the swarm is lively when nobody is attacking.
4. **Train against faster predators gradually** (curriculum 0.5 → 3) once the dodge exists; the
   sidestep is not yet useful against a full-speed vessel.
5. **Use `play_compare.py` (multi-seed + blind/drift controls) as the yardstick** for any
   reaction/heal claim.

## Files here

- `rule.pt`: the published rule (p2 step 1500; `play` block holds `pred_gain=3`). Load it with
  `play_swarm.load_play`.
- `rule_p1_heal_only.pt`: p1 step 625, the best rule for the yardstick plus healing, with no
  reaction.
- `summary.json`, `rollout.json`: the shared yardstick (rollout seed 7), in `swarm_gpu.py`
  publisher format.
- `probe.json`: strike probe, multi-seed strike, drift control, predator passes.
- `predator.json`: the recorded predator pass, in the same frame packing plus `predator` (track and
  radius per frame, `null` when absent).
- `predator_pass.png`, `predator_pass_g2.png`: side-on strips.
- `compare.json`: full multi-seed comparison.
- `progress.json`, `log_p1.jsonl`, `log_p2.jsonl`: training records.
