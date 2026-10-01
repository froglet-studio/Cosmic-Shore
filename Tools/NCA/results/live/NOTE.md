# LIVE — a swarm you seed and carve, simulated in the page

Branch `cece/swarm-x-live`, direction "live". The gallery only PLAYED BACK precomputed rollouts; this adds a
section, **"A live swarm you can eat"**, where the simulation runs in the browser in real time: a random seed,
a 3D kill brush (with an element filter), a graze predator, a one-click "bite" (the yardstick's cull), a vessel
strike (the probe's sphere), live tuning sliders, pause/speed/orbit, a live census and a switch log. Two models are selectable: the **grid morphogen** (hgrid oracle) and the **evolved rule** (G2 + the
CMA-ES genome). Both are line-for-line ports, checked against the unchanged Python scorer. Nothing was published;
the coordinating session merges and publishes.

## What was built

| File | What |
|---|---|
| `swarm_live.js` | `LiveSwarm`: the hgrid oracle step (hgrid_core + hgrid_boid `FieldBoid`/`OracleField`, cfg = `results/hgrid/oracle/summary.json` meta.cfg) on flat Float32Arrays; plan fields built once per (plan, frame) from the targets and cached (a field relative to its grid does not depend on where the grid sits, because the grid snaps to the centroid). `EvoSwarm`: `evo_model.EvoRule` (G2's 232-192-192-35 MLP on the 37-channel neighbourhood perception, fire rate 0.5, learned laying gate, lay homeostat + egg choice, output gain folded in). Seeding (`seedPlan` = `swarm_nca.seed_swarm`; `seedRandom` = random ball, Dirichlet(2) element and domain mix, optional bias), `killRay`/`killBall` (element filter), `loseMajority` (= `swarm_nca.lose_majority` "excess"), census, decode (= `swarm_nca.decode`), state export. Node CLI: `fidelity`, `bench`, `predation`. |
| `viewer_live.py` | the gallery section (HTML + script + CSS); embeds swarm_live.js, swarm_model.js (the same generator draws the tadpoles), the plans (`results/live/targets.json`, 310 KB) and the evolved weights (`results/live/evo_rule.json`, 462 KB, float32). |
| `build_viewer.py` | a 3-line hook after the swarm gallery: `benchsw += benchlive`, `scriptsw += scriptlive`, CSS appended. Page 11.5 MB (limit 16). |
| `live_export.py` | `targets` / `evo` export, `score` (JS states -> `swarm_nca.decode` + `swarm_loss`, default LossCfg), `pyref` (the Python model's own distribution, optional switch phase), `table`. |
| `live_check.js` | headless Chromium check (Playwright): errors, step rate, a real mouse drag that carves, an element-filtered carve that flips the plan, a biased seed, and the evolved model. Screenshots in `screens/`, report in `browser_check.json`. |

## Fidelity: same loss distribution as the Python model

Own plan = 16 tadpoles at the plan's mix, 240 steps; switch = the yardstick's cull (`lose_majority` "excess" toward
`SWITCH_TO`) then 240 more steps; scored against the wanted plan with the unchanged scorer. Different random
streams, so compare distributions.

| model | test | Python: loss to wanted plan | n | JS port: loss | n | <=8 Py / JS |
|---|---|---|---|---|---|---|
| grid | mass | 9.2 +- 1.5, median 9.5 [6.2, 12.4] | 16 | 9.4 +- 1.7, median 9.4 [6.4, 12.1] | 16 | 0.31 / 0.25 |
| grid | space | 8.1 +- 1.0, median 7.9 [6.0, 10.1] | 16 | 8.1 +- 1.0, median 8.3 [6.5, 9.8] | 16 | 0.69 / 0.44 |
| grid | charge | 8.8 +- 0.9, median 9.0 [7.0, 10.2] | 16 | 9.3 +- 1.0, median 9.1 [7.1, 11.4] | 16 | 0.19 / 0.06 |
| grid | time | 17.6 +- 1.5, median 17.0 [15.7, 21.4] | 16 | 18.2 +- 1.1, median 18.6 [15.6, 19.8] | 16 | 0.00 / 0.00 |
| grid | mass -> space | 15.4 +- 14.6, median 11.1 [8.9, 63.8] | 12 | 10.6 +- 2.2, median 10.1 [8.5, 16.3] | 12 | 0.00 / 0.00 |
| grid | space -> charge | 13.6 +- 1.2, median 13.5 [11.7, 16.0] | 12 | 13.7 +- 1.3, median 13.5 [12.4, 16.8] | 12 | 0.00 / 0.00 |
| grid | charge -> time | 19.6 +- 1.3, median 19.3 [17.8, 22.4] | 12 | 19.6 +- 2.0, median 19.3 [17.1, 24.7] | 12 | 0.00 / 0.00 |
| grid | time -> mass | 23.3 +- 19.8, median 16.0 [9.5, 70.0] | 12 | 15.2 +- 4.7, median 15.6 [9.1, 20.9] | 12 | 0.00 / 0.00 |
| evo | mass | 18.6 +- 1.1, median 18.7 [16.6, 20.0] | 6 | 19.5 +- 1.6, median 19.5 [17.0, 21.8] | 6 | 0.00 / 0.00 |
| evo | space | 21.8 +- 1.7, median 22.0 [18.8, 23.9] | 6 | 22.0 +- 0.6, median 21.9 [21.1, 22.9] | 6 | 0.00 / 0.00 |
| evo | charge | 15.8 +- 1.4, median 16.1 [13.2, 17.4] | 6 | 15.1 +- 1.4, median 15.1 [12.8, 16.8] | 6 | 0.00 / 0.00 |
| evo | time | 24.9 +- 0.6, median 24.9 [24.1, 25.8] | 6 | 24.8 +- 0.6, median 24.7 [24.2, 25.6] | 6 | 0.00 / 0.00 |
| evo | mass -> space | 32.4 +- 1.6, median 32.4 [30.0, 34.9] | 6 | 35.5 +- 3.7, median 34.8 [31.7, 42.5] | 6 | 0.00 / 0.00 |
| evo | space -> charge | 21.0 +- 2.0, median 20.6 [17.9, 24.2] | 6 | 18.7 +- 1.3, median 18.5 [17.1, 21.1] | 6 | 0.00 / 0.00 |
| evo | charge -> time | 30.9 +- 2.2, median 31.7 [26.8, 33.3] | 6 | 30.2 +- 2.1, median 30.8 [25.8, 32.1] | 6 | 0.00 / 0.00 |
| evo | time -> mass | 28.5 +- 3.9, median 30.0 [21.6, 32.5] | 6 | 29.8 +- 5.5, median 28.0 [23.4, 38.0] | 6 | 0.00 / 0.00 |

After the cull, the "wanted plan" is the one `lose_majority` declares at the moment of the cull (the runner-up's).
The two heavy Python tails (whale -> jellyfish 63.8, dragonfly -> jellyfish 70.0) are near-tie races the culled
swarm lost afterwards - another element out-bred the declared new majority and the body became a dragonfly / a
pufferfish instead (Python 3 of 48 culled swarms end closest to another plan, JS 0 of 48; Fisher p = 0.12, so not
significant, but a small difference in near-tie races cannot be ruled out); the medians agree (whale -> jellyfish
11.1 Python / 10.1 JS, dragonfly -> jellyfish 16.0 / 15.6).

The grid port is indistinguishable from the Python oracle (own-plan means within 0.0 to 0.6, inside one sd; the
largest gap, Time 17.6 vs 18.2, is t = 1.3 over 16 seeds each). Bodies come out at the plan's size and mix in both
(JS mean tadpoles 198 / 92 / 183 / 81 against Python 196 / 92 / 184 / 80). The evolved rule's port matches too
(own plan within 0.9; it fills the 280 slots like the Python one). Neither model passes the loss-8 bar more than
the Python original does - the port adds accuracy nothing and loses nothing: the grid oracle is under 8 in
25-44% of Mass/Space seeds and never on Time; the evolved rule never.

## Performance

| | node (V8, 1 core) | headless Chromium (SwiftShader, CPU GL, the check machine) |
|---|---|---|
| grid, ~80-200 tadpoles | 2.2-3.5 ms/step (290-450 steps/s) | 2.7 ms/step sim + 0.6 ms/frame draw (JS side); **12.5 steps/s** at the default speed 12, 39.7 at the max 40 |
| evolved rule, 280 tadpoles | 33-36 ms/step (28 steps/s) | 14-18 ms/step; **11-12 steps/s** at speed 12 |

Rendering interpolates every tadpole between its last two steps each frame (instanced meshes, one realise per
step, positions shifted per frame), so motion is smooth at any sim rate. The sim keeps its own clock (several steps
in a slow frame, within a 40 ms budget). A real GPU will only be faster than SwiftShader.

## Browser check (`browser_check.json`)

All flags pass (final page): no page errors; **12.5 steps/s at the default speed 12 and 39.7 at the max 40** (so
the sim is not the limit); a real mouse drag through the swarm's centre with an 8-voxel brush killed 54 of 78
(lime crystals appear and fade over 4.5 s); eating the majority with the element filter flipped a dragonfly into a
jellyfish (step 534); graze, the tuning sliders and the vessel strike run (the strike took 9 of 86); the evolved
rule grew a 280-tadpole whale from a mostly-Mass seed (11.1 steps/s at speed 12, 18 ms/step) and the Bite button
turned it into a jellyfish (step 297). Screens: `screens/1_grown` .. `7_evo_after_eat`, `section.png`.

## What the lead will see

* **New random seed** scatters 24 tadpoles in a ball (random element mix and domains; the seed panel names the
  majority and the creature it will become). Over 60 random seeds with no interaction, **60/60 grew the plan of
  the seed's majority, 0 switched spontaneously**, sizes 197 / 93 / 185 / 80 (whale / jelly / puffer / dragonfly),
  elements absent from the seed appear through cross-breeding, and a third domain withers in the 2-domain plans
  (e.g. domains 91/91/3).
* **Grid morphogen**: the scatter pulls together (home drift outside the body), blooms from the inside out at the
  plan's own size, Time runners lap the body. Stragglers of unwanted classes hang at the edge and wither to lime
  crystals one by one. Carve it and the hole refills from the inside.
* **Evolved rule**: a loose, always-moving cloud that fills the 280-slot budget and only suggests the creature;
  after a switch, Space-majority bodies comb into near-parallel rods (screens/7_evo_after_eat.png).

## The finding worth the most: grazing never flips it, a bite does

`node swarm_live.js predation`: grow a body, then every step a predator eats `rate` random tadpoles of the current
majority element, for up to 600 steps (4 seeds each, grid model):

| plan | 0.25/step | 0.5 | 1 | 2 | 4 |
|---|---|---|---|---|---|
| whale (Mass) | 0/4 switched (≈148 eaten) | 0/4 (≈306) | 0/4 (600) | 0/4 (1200) | 0/4 (2400) |
| jellyfish (Space) | 0/4 | 0/4 | 0/4 | 0/4 | **4/4 in 10-15 steps** |
| pufferfish (Charge) | 0/4 | 0/4 | 0/4 | 0/4 | 0/4 (2400 eaten) |
| dragonfly (Time) | 0/4 | 0/4 | 0/4 | 1/4 | **4/4 in 13-19 steps** |

A grown body regrows its majority faster than any steady grazer eats it: a class short of its plan lays at up to
`p_lay` x its relative deficit per parent (~12 eggs/step for a 120-strong Mass class), and a quarter of all eggs
cross-breed into whatever the parent's domain is shortest of - which, after grazing, is the majority. Turning
cross-breeding off (`p_cross = 0`) barely moves it (jellyfish and dragonfly flip at 2/step in 2/3; whale and
pufferfish still never). Only a big, sudden loss flips the creature: the yardstick cull, a wide brush stroke
filtered to the majority, or the panel's **Bite** button (eat just enough that the runner-up
leads).

The evolved rule fails differently (2 seeds each; `results/live/predation_evo.json`):

| plan | 1/step | 4/step | 8/step |
|---|---|---|---|
| whale | 0/2 | 0/2 (2400 eaten) | 2/2 in 238-288 steps |
| jellyfish | 0/2 | 2/2 in 188-192 | 2/2 in 65-78 |
| pufferfish | 0/2 | 2/2 in 498-559 | 2/2 in 122-144 |
| dragonfly | 0/2 | 2/2 in 313-436 | 2/2 in 102-107 |

It refills its 280 slots within ~8 steps of any loss and its homeostat wants ~90% majority, so a single bite of
2/3 of a 237-strong Mass majority left it a whale - but a steady 4/step predator wears three of the four plans
down within a few hundred steps, which the grid model never allows (whale and pufferfish never flip at 4/step).
So the two models offer opposite "how do you convert a swarm" stories: the grid model yields to one big bite and
ignores attrition; the evolved rule ignores a bite and yields to sustained attrition. **For the game this is a design choice the lead should make deliberately**: as is, a
swarm "defends its species" and conversion is a reward for a big coordinated bite, not for attrition. The panel's
Tuning sliders (regrowth, cross-breed, hysteresis, starvation) let it be felt live.

## What failed / caveats

* Accuracy is the Python models' own: the port changes nothing about the loss-8 bar (see the table). The value
  here is interaction, not a better body.
* Random streams differ (mulberry32 + Box-Muller vs torch), so fidelity is distributional, not bit-exact.
* The 'kill' event leaves a crystal only for a hatched tadpole (an egg never lived). Crystals are visual only in
  the page (they are not food or pickups).
* The headless check runs on SwiftShader; the measured rate there (12 steps/s at speed 12) is the floor, not the
  ceiling. The evolved rule at 280 tadpoles costs ~14 ms/step, so its speed is capped to 10 on selection.

## Recommendation for the next round

1. **Make conversion a tunable rule, not an accident.** The two models tell opposite stories (grid: one big bite
   converts, attrition never does; evolved rule: attrition converts, a bite does not) and neither was designed.
   Decide which the game wants. If attrition should convert, cap the grid's regrowth by FOOD (eggs cost mass the
   swarm has eaten - conserved mass) rather than by deficit alone, so a grazed swarm can starve its majority; if a
   bite should, the grid already does it. Test with `node swarm_live.js predation` and the panel's Graze / Bite.
2. **Put the accurate models into this panel as they land.** The panel takes any model with `step()` over the
   shared arrays; the next accurate + organic candidate (a local sorting rule) should get a JS port and the same
   `live_export.py score` fidelity row before it is shown, so the lead's feel review is always of the real model.
3. **Use the live page for the feel review itself**: seed bias + brush filter + Bite reproduce every yardstick
   transition by hand, and the switch log records what happened.
