# creature — a vessel-reaction shell on a swarm body (round 3)

**Headline.** Two playable creatures were built and measured. (A) `CreatureRule` = evo's learned body + the shell:
16/16 (seed 7 and held-out 11/23/37), 8/8 old yardstick, but blobby bodies (own-plan divergence 14-25). (B)
`CreatureField` = field's slot body + field's own startle + the SAME shell (`creature_field.py`): field's
13/13 feasible, crisp bodies (divergence 1-5), and the best parting measured in any direction: a ship flying through
touches **2.6-21%** of the school (inert 28-37%, field alone 8-28%, shell alone 18-32%). **B is the one to build.**
Showcases: `showcase.html` (A), `showcase_field.html` (B, recommended).


Code: `Tools/NCA/creature_model.py` (the model), `creature_probe.py` (interaction probe), `creature_extra.py`
(switch latency, escort, gradual predation), `creature_heal.py` (8-seed strike probe), `creature_eval.py`
(16-test yardstick), `creature_showcase.py` + `creature_showcase.html.tpl` (lives + page), `creature_publish.py`.
Regenerate: `python creature_publish.py && python creature_showcase.py` (~3 min), probes ~10 min.

## What it is

* **Body = evo, unchanged.** `CreatureRule` subclasses `evo_model.EvoRule` (G2 `rule.pt` + `results/evo/genome.npy`).
  Growth, look, composition and switching are all the learned rule + evolved lay homeostat / egg choice.
* **Shell = a player-facing reaction layer applied as an ELASTIC OFFSET:** `off <- off*(1 - k_ret*calm) + reaction`,
  `pos += off_new - off_old`. A startled tadpole is pushed off its learned place and springs back once the ship is
  gone; the learned rule keeps perceiving the displaced positions. With no ship near and no majority change the
  model is **bit-for-bit evo** (asserted: identical positions over 240 growth steps; identical strike probe).
* Startle: within `sense`(3.0) x ship radius now OR ahead on its path (10-step look-ahead), relayed neighbour to
  neighbour (a startle wave), decays 0.9/step.
* Temperaments, per element of each tadpole (flee gain C/M/S/T = 0.5/0.3/2.0/2.0) plus a plan response keyed on the
  swarm's majority:
  - **Whale (Mass)**: ponderous; minority elements tuck into the body, the Mass hull closes toward the ship's side.
    A ship cruising PAST it (1-2.4 vox/step, not heading at it, within 4 RMS radii) is **followed** (escort).
  - **Jellyfish (Space)**: Space units flee in pulsed JETS (2 of every 6 steps, gain 3); the bell contracts and the
    whole body jets away on each pulse.
  - **Pufferfish (Charge)**: holds; the body INFLATES (radial swell 1 + 0.55*threat) and Charge units show DANGER
    spikes while startled; deflates when the ship leaves.
  - **Dragonfly (Time)**: darts with an evasive zig-zag; a LOITERING ship (< 1 vox/step) is MOBBED (Time units orbit it
    at 1.4 ship radii, stream home when it leaves); tags along with a cruising ship.
* **Switch tell**: when the living majority changes the body SHIVERS for 36 steps (jitter + slow swirl, own RNG so it
  never shifts the rule's stream), and a **pre-tell** shivers it whenever the top two elements' shares are within 8%
  (the majority is about to flip).
* **Wound memory** (eggs placed into the holes a strike left): built, measured worse, **shipped OFF** (`wounds=0`).

## Shared yardsticks

| | creature | evo (round 1) |
|---|---|---|
| `swarm_eval --full` 16 transitions, seed 7, 3 samples | **16/16** (all rates 1.0) — `eval16.json` | 16/16 |
| same, held-out rollout seeds 11 / 23 / 37 (`heldout/`) | **16/16, 16/16, 16/16** | (not re-run) |
| old 8-test `rollout` + `tests_passed` | **8/8** (summed 200.8) — `summary.json` | 8/8 |
| `swarm_probe` strike heal, seed 11 (whale/jelly/puff/dragon) | 0.46 / -1.17 / 0.94 / 0.60 (= evo, shell inert without a ship) | same |
| strike, 8 seeds, mean residual damage (recovered - before; lower better) | **0.84** shipped; 1.96 with wound memory ON | 0.84 |

The single-seed strike heal is noise-dominated (the cut often barely changes the score, so the ratio explodes); the
8-seed residual is the honest number. Wound memory looked great on seed 11 (space -1.17 -> 0.68) and was worse on
average (residual 0.84 -> 1.96; anchoring wounds to survivors, element-matching, shorter reach: 1.57-1.96). Negative
result: the evo body already regrows at the 280 cap; forcing eggs into stale wound sites fights its own re-arrangement.

## Interaction probe (`interaction.json`, seed 5; shell ON / OFF=bare evo)

Ship radius 0.6 RMS (fly-by/ram, 3 vox/step through the centroid), circling 0.8 vox/step at 1.6 RMS, chase at
1.2 vox/step hovering 0.8 RMS off. *touched* = share of the school ever inside the ship; *readable* = share of
scored frames during the encounter at which the body is still strictly closest to its own plan.

| plan | fly-by touched | ram touched / killed-share | readable during every encounter | fly-by worst score (on/off) | reform after |
|---|---|---|---|---|---|
| whale | 0.35 / 0.44 | 0.36 / 0.44 | 1.00 | 16.6 / 16.6 | 6 steps |
| jellyfish | **0.11 / 0.51** | 0.10 / 0.49 | 1.00 | 24.8 / 21.8 | 8 |
| pufferfish | **0.11 / 0.32** | 0.11 / 0.33 | 1.00 | 18.7 / 15.1 (it inflates) | 2 |
| dragonfly | **0.10 / 0.30** | 0.11 / 0.31 | 1.00 | 28.8 / 24.5 | 1 |

* Reaction latency (steps from first contact until the tadpoles in range move at 1.5x their pre-contact speed):
  1-10 steps (whale slowest by design); the jellyfish usually parts **before** the contact test triggers (reported
  as None) — the look-ahead clears the ship's path early.
* Chase: touched 0.004-0.036 vs 0.09-0.14 inert (the school keeps away from a pursuer). Circling a dragonfly: the mob
  costs shape (worst 41 vs 25) and re-forms 20 steps after the ship leaves — that is the show.
* Ram heal in this probe (score-based): whale 0.78 vs -0.10 inert, jelly 0.21 vs -1.16, dragonfly 0.86 vs 0.43 —
  mostly because the agile plans DODGE the ram (in the showcase a ram kills 152 whale tadpoles, 44 pufferfish, 6
  jellyfish, 2 dragonfly).
* The body stays its own plan in 100% of scored frames of every encounter, every plan.

## Extra probes (`extra.json`, `gradual.json`)

* **Escort** (ship cruises past alongside for 90 steps, travelling 144 voxels): centroid travels with it
  whale **39.2** (5 inert), dragonfly 16.5 (-1.6), jellyfish 5.6 (5.6), pufferfish 1.2 (1.1).
* **Switch latency after a fair cull: <= 10 steps in all 12 switches.** The yardstick's scorer is composition-
  dominated (`w_elem` 60), so once the cull leaves the new majority the new plan is "closest" almost immediately; the
  36-step tell therefore plays mostly AFTER that moment. The body's SHAPE keeps re-forming for ~100+ steps (end
  scores 30-50 vs 14-25 for a grown plan).
* **Gradual predation** (eat N majority tadpoles per step until it flips): at 2/step **no plan ever flips** — the
  evolved lay homeostat out-breeds the predator; at 4/step only the jellyfish (step 188) and dragonfly (392) flip,
  with **24 and 61 steps of pre-tell shivering before the flip**. The jellyfish that flipped to Charge was still
  closest to the jellyfish 120 steps later (the composition-only switch is not enough at that pace) — a real failure
  the fair-cull yardstick does not see.

## What a player sees (`showcase.html`, one scripted life per plan; drag to orbit, click events)

Grow from 16 into a 280-tadpole school. Fly at it: a white startle wave runs through the school before you arrive
and it opens a tunnel along your path; the pufferfish swells and its gold plates turn red and spiky; the jellyfish
pulses away in jets; the dragonfly's Time units dart and, if you loiter, swarm around your ship like gnats and stream
home when you go. Cruise past a whale and it turns and follows. Ram it: the whale is too heavy to dodge (you carve a
hole, lime crystals spill and are drawn into your ship as you come back for them); the dragonfly and jellyfish mostly
get out of the way. Eat its majority element and it shivers (a cyan tint on the page) and re-assembles as another
animal. Caveat a player WILL notice: the learned bodies are recognisable as distinct, colour-coded forms rather than
crisp animals (the evo plans score 14-25 Sinkhorn; field's slot bodies score 1-6), and they sit at the 280 cap.

## What failed / caveats

* Wound memory (above). Score-based reaction latency is noisy when the school parts before contact.
* The tell cannot precede a one-shot cull; it only warns under gradual predation, which this body mostly resists.
* Gradual-predation switch of the jellyfish failed; the 16/16 is a statement about the fair-cull protocol only.
* Crystal drift-to-ship lives in the showcase recorder, not the model (crystals are not in `Swarm`).
* `rollout.json` = `swarm_nca.pack` frames every 4 steps + extra keys (`vessel` per frame [x,y,z,r], `flags` int8
  [startle x100, danger, mob], `tell`, `events`, `crystal_track`, `every`), one scripted life per plan.

## (B) The shell on field's slot bodies (`creature_field.py`; `field_*.json`, `showcase_field.html`)

Same shell code, applied to `FieldSwarm` (params `results/field/params.json`). Field's own predator response is a
second layer (`native=1`, default): it is the one thing an external shell cannot do - it CALMS the body's homing
while startled, so the slots stop pulling tadpoles back into the ship's path. The shell adds the jets, the pufferfish
spikes, the escort (which also moves field's body frame, `mem.anchor`), the pre-tell and the elastic flee.

| fly-by touched (seed 5) | whale | jellyfish | pufferfish | dragonfly |
|---|---|---|---|---|
| inert (field body, no reaction) | 0.37 | 0.31 | 0.28 | 0.36 |
| shell only | 0.32 | 0.23 | 0.18 | 0.18 |
| field native only (round 1) | 0.28 | 0.08 | 0.11 | 0.11 |
| **native + shell (shipped B)** | **0.21** | **0.045** | **0.05** | **0.026** |

* 16-test yardstick: **13/13 feasible** (`field_eval16.json`; n/a space->mass, charge->mass, time->charge, as field).
* Every encounter keeps the body its own plan in 100% of frames; fly-by worst own-plan score 5.5-11 (inert 2-6),
  back within 4-9 steps; ram heal 1.04-1.19 (field regrows to the plan's headcount).
* Escort over 144 voxels of ship travel: whale 35.6, dragonfly 18.3, jellyfish/pufferfish 0 (inert 0).
* Lesson: **a reaction shell needs ONE hook into the body - a "calm" input that relaxes its shape-keeping while
  startled.** Without it the body fights the shell (field + shell only: 0.18-0.32; the learned evo body has no such
  hook either, which is why A's whale stays at 0.35).

## Recommendation (next round / Unity)

1. **Ship the shell, not the network, first.** Everything a player reacts to here is the shell: ~200 lines of
   per-tadpole arithmetic (startle relay, per-element flee/jet/dart/mob, plan swell/tuck/escort, elastic offset).
   It is body-agnostic: port it as a Burst job over the fauna spatial index and put it on top of **field's** slot
   bodies (crisp shapes, C# port exists, 0.1-0.6 ms) — measured above as (B), the best of everything tried. Give
   the body a `calm` input (field's `desired *= 1 - 0.8*startle`) so it stops defending its shape while startled.
2. If the learned body is kept, its lay homeostat makes a swarm nearly unconvertible by gradual predation (good for a
   "boss" creature, bad for the "eat its majority" fantasy). Tune its gain per game mode, or let a player-visible
   event (a crystal steal, an ability) suppress laying for a few seconds.
3. Fix the yardstick's blind spot: add a gradual-predation switch test (cull over time, score shape 120 steps
   after the flip) — the fair cull is passed by composition alone.
4. Make the switch tell anticipatory in-game by tying it to the pre-tell (share gap), which works on any body.
5. Crystals: route the dead tadpole's lime crystal through the game's crystal system with a vessel attraction radius
   (`Crystal` already exists; the showcase uses 4 ship radii, 0.35 vox/step pull).
