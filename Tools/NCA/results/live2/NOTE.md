# LIVE 2 — hgrid2 and sort in the live page

Branch `cece/swarm-x-live2` (from `cece/gifted-curie-x2cpd0`). The gallery's live section, "A live swarm you can
eat", ran two models (the round-1 grid oracle and the evolved rule). It now runs four: the two new, accurate ones
first — **Grid morphogen, round 2 (hgrid2)** (the default) and **Cell sorting (sort)** — then the two old ones. Every
control works on every model (random seed with bias, element-filtered brush, graze, bite, vessel strike, census,
switch log), each model has its own tuning sliders, and picking a model shows a plain-language description of what
decides where a tadpole goes, what happens when you kill the majority, whether anything dies on its own and what
to watch for. Nothing was published; the coordinating session merges and publishes.

## What was built

| File | What |
|---|---|
| `swarm_live.js` | `Hgrid2Swarm` (extends the round-1 `LiveSwarm`): `hgrid2_model.Boid2` at `results/hgrid2/params.json` = `hgrid2_eval.BEST` — commit lock (60), `low_dmap`, the round-1 step with the real hysteresis held back (as the Python does: `hyst = 1e9`, `starve = 0` during the base step), staggered starvation (per-slot hunger `0.5 + U` from a fixed seed, class excess, element floor, small-element slack), the fine layer (per-class Gaussian bumps of the plan's units minus the live tadpoles, sigma = 1.2 x the plan's mean nearest-neighbour spacing, gain x fullness, feed-forward 1.5, frames interpolated, Time at period 16) and migrants. `SortSwarm` (stride 32, the full Python state): `sort_model.SortSwarm` at `results/sort/params.json` — dwell plan decision, sticky domain -> region pick, fate commitment to the most under-occupied well, Mahalanobis chemotaxis with clip, orphans on the whole-body mixture, dense collision / differential adhesion / Potts swaps, inertia, per-type look, the lay homeostat (Poisson count, fill tolerance, cross-laying, majority guard, headcount cap) and molting with region transfer. Both on flat typed arrays. The one base-class change: `periodOf(kind)` (round 1 returns `cfg.period`, so it is unchanged). CLI: `fidelity` / `bench` / `predation` take a MODEL argument (`-` grid, `evo_rule.json`, `hgrid2`, `hgrid2:12`, `sort`). |
| `live_export.py` | `sort` (exports `results/live2/sort_code.json`, 19 KB: the sort config and every plan's PlanCode — the k-means wells are built once in Python with their own fixed seed, so the port reads them instead of re-deriving them — plus the fallback look per element), `pyref N hgrid2|hgrid2_g12|sort switch`, `LIVE_TABLE=2 table`. |
| `viewer_live.py` | the four models (structured descriptions: summary + four labelled paragraphs), per-model tuning panels (hgrid2: grid 16/12, fine sorting, feed-forward, plan lock, regrowth, migrants, starvation; sort: well pull, tension, inertia, dwell, molting, lay rate, region transfer; round 1 unchanged), a Molts readout, `sort_code.json` embedded. |
| `live_check.js` | adds, for hgrid2 (G = 16 and 12), sort and round 1: grow a mostly-Mass seed to a whale, measure the step rate and sim ms/step at the grown whale, carve the majority with the element filter until another element leads, wait for the switch, then strike + graze + bite. |

Page: 12.06 MB (limit 16; +0.25 MB).

## Fidelity: JS states scored by the unchanged Python scorer

Protocol as `results/live/NOTE.md`: own plan = `seed_swarm` mix of 16, 240 steps; switch = the yardstick's cull
(`lose_majority` "excess" toward `SWITCH_TO`) then 240 more; loss to the wanted plan (`swarm_nca.decode` +
`swarm_loss`, default LossCfg). 12 seeds per row; different random streams, so compare distributions.

| model | test | Python: loss to wanted plan | n | JS port: loss | n | <=8 Py / JS |
|---|---|---|---|---|---|---|
| hgrid2 | mass | 1.3 +- 0.4, median 1.2 [0.7, 2.1] | 12 | 1.4 +- 0.5, median 1.4 [0.7, 2.4] | 12 | 1.00 / 1.00 |
| hgrid2 | space | 1.2 +- 0.7, median 0.8 [0.5, 2.9] | 12 | 2.0 +- 0.8, median 2.0 [0.6, 3.1] | 12 | 1.00 / 1.00 |
| hgrid2 | charge | 2.0 +- 0.8, median 1.8 [1.0, 3.4] | 12 | 1.9 +- 0.4, median 1.8 [1.4, 3.1] | 12 | 1.00 / 1.00 |
| hgrid2 | time | 3.0 +- 0.4, median 2.9 [2.3, 3.8] | 12 | 3.4 +- 1.1, median 3.1 [2.6, 6.8] | 12 | 1.00 / 1.00 |
| hgrid2 | mass -> space | 4.2 +- 0.2, median 4.2 [3.9, 4.4] | 12 | 4.1 +- 0.2, median 4.1 [3.9, 4.4] | 12 | 1.00 / 1.00 |
| hgrid2 | space -> charge | 2.4 +- 0.5, median 2.6 [1.7, 3.0] | 12 | 2.2 +- 0.4, median 2.0 [1.8, 2.9] | 12 | 1.00 / 1.00 |
| hgrid2 | charge -> time | 7.2 +- 0.5, median 7.2 [6.4, 8.0] | 12 | 7.3 +- 0.7, median 7.2 [6.4, 8.7] | 12 | 0.92 / 0.83 |
| hgrid2 | time -> mass | 8.5 +- 17.2, median 1.3 [1.0, 58.6] | 12 | 6.4 +- 11.6, median 1.3 [0.9, 32.7] | 12 | 0.83 / 0.83 |
| sort | mass | 1.5 +- 0.2, median 1.5 [1.2, 1.7] | 12 | 1.4 +- 0.2, median 1.4 [1.3, 1.9] | 12 | 1.00 / 1.00 |
| sort | space | 2.6 +- 0.3, median 2.5 [2.1, 3.2] | 12 | 2.6 +- 0.4, median 2.5 [2.2, 3.3] | 12 | 1.00 / 1.00 |
| sort | charge | 1.1 +- 0.2, median 1.0 [1.0, 1.3] | 12 | 1.1 +- 0.1, median 1.0 [0.9, 1.4] | 12 | 1.00 / 1.00 |
| sort | time | 4.0 +- 0.6, median 4.0 [3.1, 5.0] | 12 | 4.3 +- 0.8, median 4.4 [3.1, 5.7] | 12 | 1.00 / 1.00 |
| sort | mass -> space | 2.6 +- 0.4, median 2.5 [2.1, 3.4] | 12 | 2.9 +- 0.6, median 2.7 [2.2, 4.4] | 12 | 1.00 / 1.00 |
| sort | space -> charge | 1.3 +- 0.1, median 1.2 [1.1, 1.4] | 12 | 1.2 +- 0.1, median 1.2 [1.0, 1.4] | 12 | 1.00 / 1.00 |
| sort | charge -> time | 6.2 +- 0.4, median 6.3 [5.3, 6.9] | 12 | 6.1 +- 0.8, median 6.3 [4.9, 7.1] | 12 | 1.00 / 1.00 |
| sort | time -> mass | 6.4 +- 2.5, median 8.2 [3.0, 8.9] | 12 | 4.0 +- 1.6, median 3.8 [2.6, 9.0] | 12 | 0.42 / 0.92 |
| hgrid2 G=12 | mass | - | 0 | 2.6 +- 0.3, median 2.6 [2.1, 3.0] | 12 | - / 1.00 |
| hgrid2 G=12 | space | - | 0 | 2.0 +- 0.8, median 2.0 [0.6, 3.1] | 12 | - / 1.00 |
| hgrid2 G=12 | charge | - | 0 | 1.9 +- 0.4, median 1.8 [1.4, 3.1] | 12 | - / 1.00 |
| hgrid2 G=12 | time | - | 0 | 3.9 +- 1.2, median 3.5 [2.8, 7.0] | 12 | - / 1.00 |
| hgrid2 G=12 | mass -> space | - | 0 | 6.8 +- 1.3, median 7.1 [3.8, 8.0] | 12 | - / 0.92 |
| hgrid2 G=12 | space -> charge | - | 0 | 2.2 +- 0.4, median 2.0 [1.8, 2.9] | 12 | - / 1.00 |
| hgrid2 G=12 | charge -> time | - | 0 | 7.6 +- 0.6, median 7.4 [6.8, 8.6] | 12 | - / 0.75 |
| hgrid2 G=12 | time -> mass | - | 0 | 13.6 +- 22.9, median 1.2 [0.9, 61.7] | 12 | - / 0.75 |

**hgrid2 matches.** Bodies come out at the plan's exact size and mix in both (whale 192 = 19/126/25/22, jellyfish
88-91, pufferfish 179-182, dragonfly 76-77). The jellyfish row (1.2 vs 2.0 at n = 12) was re-run at 42 seeds each:
**Python 1.31 +- 0.73 (median 0.96), JS 1.57 +- 0.79 (median 1.29)**, t = 1.6 — the loss there tracks how many
surplus Mass tadpoles the small-element slack lets the body keep (plan 2, kept 2-5), and that count has the same
spread in both (traced step by step over the same seeds: Python 2/3/5/4/4/2, JS 3/3/4/4/5/2). The dragonfly ->
whale tail (58.6 Python, 32.7 JS) is the same near-tie race the round-1 note describes: a third element out-breeds
the declared new majority.

**sort matches**, with one row that differs in the REFERENCE, not the port. Dragonfly -> whale is bimodal (~3.5 or
~8.5) and which mode a seed lands in is decided on the swarm's first step, by which region the seed's 16 tadpoles
assign to the domain that becomes the dragonfly's big (44-unit) region (`_pick_perm`; domain conservation, the
failure `results/sort/NOTE.md` already names). That first pick has the same distribution in both ports: 3000
random seeds give perm (2,1,0) 32.0% in JS, 32.7% in numpy, 31.3% over 1000 torch seeds of `seed_swarm`. The 40
torch seeds the Python reference uses (1000-1039) happen to give it 22/40 = 55% — the most extreme of 25 blocks
of 40 — so the Python row sits in the high mode more often than the model does (40-seed re-run of that row: Python
7.2 +- 4.1, 22/40 high). Everything else agrees within a tenth or two.

**G = 12** (the grid option in the hgrid2 tuning panel; 72 voxels across instead of 96): the whale's tail and fins
fall off the smaller grid (whale grown to 186-187 with Space 19 instead of 25), own whale loss 1.4 -> 2.6, whale ->
jellyfish 4.1 -> 6.8, dragonfly 3.4 -> 3.9, and the two weakest switches drop from 0.83 to 0.75 under the bar. The
jellyfish, pufferfish and jellyfish -> pufferfish rows are identical to the digit: a body that fits inside the
inner 12^3 cells sees exactly the same field (the outer cells were all zero), so only the long plans pay. It saves ~35% of the step
(below), which the page does not need — G = 16 stays the default and the verified model.

## Performance

| model (whale, 192 or 181 tadpoles) | node, 1 core | headless Chromium (SwiftShader, CPU GL) |
|---|---|---|
| hgrid2, G = 16 | 6.9 ms/step (144 steps/s) | 6.0 ms/step sim; 23.5 steps/s achieved at speed 40 |
| hgrid2, G = 12 | 4.4 ms/step (225 steps/s) | 4.3 ms/step sim; 29.6 steps/s achieved |
| sort | 0.5 ms/step (2000 steps/s) | 0.63 ms/step sim; 38.4 steps/s achieved |
| round-1 grid | 7.1 ms/step (141 steps/s) | 5.05 ms/step sim; 21.8 steps/s achieved |

(node numbers with the browser check running beside them; the round-1 grid measured 2.2-3.5 ms on an idle
machine in the first round.) hgrid2 costs the same as round 1: the coarse 16^3 grid (13 deficit gradients,
blurs, 59-channel looks) is the step; the fine layer and migrants add well under a millisecond because they only
compare a tadpole with the units and tadpoles of its own class. sort has no grid at all and is ~15x cheaper. In
the headless check the achieved rate is set by SwiftShader drawing the frame (a few frames per second on a CPU
renderer, 3 steps per frame at most), not by the simulation; on a GPU every model runs at the speed slider's
40 steps/s with room to spare. So no G = 12 is needed; it is offered for weak machines with its accuracy cost
stated beside it.

## Browser check (`check.json`)

Flags (`browser_check.json` `ok`): no_errors PASS, rate FAIL, carve_kills PASS, switched PASS, evo_runs PASS, strike_kills PASS, evo_switched PASS, hgrid216_whale PASS, hgrid216_switched PASS, hgrid216_interactive PASS, hgrid212_whale PASS, hgrid212_switched PASS, hgrid212_interactive PASS, sort_whale PASS, sort_switched PASS, sort_interactive PASS, grid_whale PASS, grid_switched FAIL, grid_interactive PASS.

| model | grown (mostly-Mass seed, step > 240) | sim ms/step | steps/s at speed 40 | filtered carve | switch | after 240 more steps |
|---|---|---|---|---|---|---|
| hgrid2, G = 16 | 192 = 19/126/25/22 (mass) | 6.01 | 23.5 | 27 passes, majority lost | mass -> space | 91 (space) |
| hgrid2, G = 12 | 186 = 19/126/19/22 (mass) | 4.33 | 29.6 | 13 passes, majority lost | mass -> time | 73 (time) |
| sort | 181 = 19/119/22/21 (mass) | 0.63 | 38.4 | 32 passes, majority lost | mass -> space | 83 (space), molts 49 |
| round-1 grid | 200 = 20/129/27/24 (mass) | 5.05 | 21.8 | 40 passes, majority kept | none | 198 (mass) |

* **No page errors** across every phase and all four models (plus the evolved rule's phase, unchanged:
  5.8 steps/s at speed 12, 16.3 ms/step; the Bite turned its whale into a jellyfish).
* Every new model grew the plan's own whale from a random mostly-Mass seed, carved with the Mass filter until
  another element led, and switched: hgrid2 G = 16 to a jellyfish, G = 12 to a dragonfly (the runner-up after the
  carve differs per run), sort to a jellyfish with 49 molts and no starvation crystals of its own (every crystal
  on the counter is a kill). Graze, strike and Bite ran on each (the Bite flipped the regrown body again every time).
* **round-1 grid: 40 filtered brush passes did NOT flip the whale** (129 Mass before, 127 after: it bred back
  between passes, which at SwiftShader's frame rate are ~1 s apart) — the grazing finding of the first live note,
  met through the brush. The Bite button flipped it to a jellyfish. Not a port failure: in the default-model
  phase (hgrid2, a random seed that grew a 184-tadpole pufferfish) 5 filtered passes flipped it to a dragonfly.
* `rate` FAIL: the default-speed rate (speed 12) measured 7.5 steps/s in the first phase, against
  the old check's 12.5 — frame-bound, not sim-bound: the sim took 6.7 ms/step while SwiftShader drew
  ~2-3 frames a second, and the loop runs at most 3 steps per frame (dt clamp 0.25 s). At the grown whale, speed 40,
  every model reached 21.8-38.4 steps/s (the `_interactive` flags, >= 20). On a GPU the frame costs ~1 ms of JS
  (draw_ms 1.0-1.1).
* Screens: `screens/m_<model>_grown.png`, `m_<model>_switched.png` (after the switch, strike, graze and bite).

## Grazing (`predation_hgrid2.json`, `predation_sort.json`; `node swarm_live.js predation`, 4 seeds)

Every step a predator eats `rate` random tadpoles of the current majority for up to 600 steps:

| plan | hgrid2 1/step | 2 | 4 | sort 1/step | 2 | 4 |
|---|---|---|---|---|---|---|
| whale | 0/4 | 0/4 | 0/4 (2400 eaten) | 0/4 | 0/4 | 0/4 (2400 eaten) |
| jellyfish | 0/4 | 0/4 | **4/4 in 9-14 steps** | 0/4 | 0/4 | 0/4 (1 of 4 eaten to extinction) |
| pufferfish | 0/4 | 0/4 | 0/4 | 0/4 | 0/4 | 0/4 |
| dragonfly | 0/4 | 1/4 | **4/4 in 11-17 steps** | 0/4 | 0/4 | 0/4 (**4 of 4 eaten to extinction** in 48-115 steps) |

hgrid2 tells the round-1 story exactly (a grown body out-breeds any steady grazer; the small plans flip under a
fast one). **sort never switches under grazing at all** — the majority stays the majority because the lay homeostat
refills its class first and the molting homeostat converts surplus INTO the deficit — but its regrowth is slow
(at most 5 eggs a step, 8% of the headcount) and capped at the plan's size, so a fast grazer on a small plan eats
it to extinction instead of converting it. For the game that is a third behaviour: grid models convert on a big
bite, the evolved rule converts under attrition, sort converts only on a bite and dies under heavy attrition.

## What the lead will see

* **hgrid2 (default).** A random scatter pulls together as a loose school, fills up, and then crystallises: as the
  headcount reaches the plan's size the fine field takes over and every colour moves into its own place (the
  sharpest bodies on the page). Eat the majority with the brush and the plan switches immediately (the 60-step lock
  only stops a switch within 60 steps of the last one), the survivors re-sort, and the surplus starves into lime
  crystals one by one rather than all at once.
* **sort.** No crystals unless you make them: when you eat the majority the new majority must lead for 12 steps,
  then surplus tadpoles flicker to new elements (the Molts counter climbs) and the body re-forms at the new plan's
  size. Carve a hole and it heals in place — newborns are fated to the emptiest wells, which are the hole.
* Picking a model now shows what it is, in four short paragraphs (Where a tadpole goes / When you kill the majority
  / Does anything die on its own / Watch for), and only that model's tuning sliders.

## Caveats

* Random streams differ (mulberry32 + Box-Muller against torch / numpy), so fidelity is distributional. sort's
  wells come from Python (exported), not re-derived in JS; everything that happens per step is ported.
* The page caps hgrid2 and sort at the Python's 280 tadpole slots (round 1 keeps its 400).
* The headless rates are SwiftShader's floor. Not tried on a real GPU in this session.
