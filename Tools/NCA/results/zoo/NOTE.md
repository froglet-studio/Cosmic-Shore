# zoo — a zoo of personalities (quality-diversity over `field`)

Code (all new, nothing shared was edited): `zoo_model.py` (field + nine switchable behaviour genes), `zoo_probe.py`
(behaviour descriptors), `zoo_search.py` (MAP-Elites, 4 workers), `zoo_pick.py` (archetype selection),
`zoo_publish.py` (map + full per-elite evaluation), `zoo_showcase.py` (playback page per elite),
`zoo_boundary.py` / `zoo_boundary_far.py` (how much of the genome box passes).
Open **`index.html`** (table, map, links) or any `elites/<slug>/showcase.html`.

## What it is

`ZooSwarm` is `field_swarm.FieldSwarm` with its step copied and extended, so every round-1 mechanism is intact:
Hungarian slot assignment, element-ratio plan choice with dwell hysteresis, true-breeding laying, molting, morph
vortex, startle cascade, per-element flee / mob, pufferfish inflate. The genome (54 genes, `zoo_model.GENES`) is
field's ~30 tunables (arrive, inertia, separation, alignment, speed scale, laying/molting rates, morph length and
vortex, dwell, sense, relay, per-element flee and mob, mob speed threshold, per-PLAN inflate) plus **nine new
behaviours, each an on/off bit + amplitude**:

| gene | what it does | in how many of the 142 map elites |
|---|---|---|
| breathe | the whole body pulses (home scale × 1 + a·sin) | 82 |
| wave | a travelling pulse runs down the body's long axis (peristalsis) | 25 |
| jitter | per-tadpole Ornstein–Uhlenbeck wobble | 58 |
| orbit | every tadpole circles its own slot on a private loop | 42 |
| burst | a switch starts with a radial explosion | 78 |
| curious | a ship at middle distance (1–3× sense) is approached | 64 |
| hunt | every element mobs a slow-enough ship | 83 |
| rush | a sudden headcount drop multiplies laying | 45 |
| bristle | every startled tadpole flashes DANGER | 72 |

Swimming (`wander`) is a presentation setting only (0.10–0.35 per elite in the showcases); the yardstick is
orientation-locked, so all scoring runs at 0, exactly as field did.

## Descriptors (`zoo_probe.py`; per plan on a grown body, averaged over the four)

* **stance** = log2(crowd / crowd of an inert body), crowd = share of the swarm within 2 ship radii of a ship parked
  1.2 RMS radii away for 80 steps. < 0 flees, > 0 mobs. field: +0.85 (only its Time mobs).
* **live** = mean idle speed (voxels/step). field 0.40.
* **drama** = peak RMS radius during a switch (`swarm_eval.cull_to` the standard target) / grown radius. field 1.09.
* **heal** = steps until the own-plan score is back within 1.2× + 0.5 after `swarm_probe.strike`. field 16.
* **loose** = mean distance tadpole → home slot; **touched** = share ever inside a ship crossing at 3 voxels/step
  (field 0.14); **latency** = steps after the cull until the new plan is closest; **ms** = numpy ms/step, worst plan.

Map axes: stance (6 bins) × live (5) × drama (5) = 150 cells; within a cell the lower mean wanted divergence wins.

## Search and the headline finding: the yardstick does not constrain personality

1,412 candidates evaluated (single-sample fail-fast swarm_eval, then descriptors; ~50 s each, 4 at a time, ~4.6 h of
wall time across four runs); **1,411 passed every feasible test (13/13)**, 142 of 150 cells filled. I widened the
gene bounds twice because nothing was being rejected. Two control experiments:

* `boundary.json`: **48/48 uniform-random genomes** from the final box pass 13/13 (not mutants of a passer).
* `boundary_far.json`: genes drawn from an interval **3× as wide** as the box: **39/40 pass**; the one failure fails
  all four own plans (separates on high accel, low arrive, low sense — a body that never settles on its slots).

So with field's composition layer (slots + hysteresis + molting + true-breeding laying) the 16 transitions are
essentially guaranteed, and motion/reaction/morph style is free design space. The "keep only passers" filter
is real but almost never binds; the job of QD here is coverage, not survival. That is the most useful thing this
direction found: **you can author creatures by hand on top of field without risking the plan logic.**

## The seven named elites (all: eval16 **13/13 feasible at 3 samples**, n/a = space→mass, charge→mass, time→charge; strict rollout tests_passed **7/8**, the same failing dragonfly→whale test that field documents as an invalid cull)

| creature | stance | idle speed | drama | heal steps | loose | touched | latency | ms/step | mean div | strike heal W/J/P/D |
|---|---|---|---|---|---|---|---|---|---|---|
| field (reference) | +0.85 | 0.40 | 1.09 | 16 | 0.38 | 0.14 | 20 | 4.9 | 6.80 | 1.07/1.04/0.92/1.06 |
| Glass Minnows | **−2.12** | 0.45 | 1.18 | 54 | 0.27 | **0.008** | 10 | 4.8 | 4.97 | 1.06/0.99/0.86/0.99 |
| Phoenix | **+3.51** | 0.43 | **2.06** | 35 | 0.23 | 0.07 | 10 | 4.6 | 5.85 | 1.33/1.19/0.93/1.07 |
| Hornet Cloud | +2.59 | **1.06** | 1.49 | 40 | 1.87 | 0.11 | 18 | 4.8 | 5.14 | 1.09/1.00/0.99/1.08 |
| Old Drifter | +2.48 | **0.24** | 1.22 | 33 | 1.83 | 0.20 | **38** | 5.6 | 6.67 | 0.95/1.01/1.06/0.96 |
| Hydra | +0.47 | 0.60 | 1.32 | **11** | 0.64 | 0.07 | 18 | 6.6 | 4.82 | 1.00/1.40/0.81/1.05 |
| Murmur | +1.85 | 0.35 | 1.22 | 20 | **2.55** | 0.05 | 20 | 4.6 | 5.81 | 1.14/1.00/0.93/1.60 |
| Sparkler | +1.74 | 0.96 | 1.36 | 59 | 1.11 | 0.11 | 18 | 6.6 | **3.74** | 1.00/1.08/1.02/0.97 |

(strike heal = `swarm_probe.probe`, 1 = back to the pre-strike score; every plan regrows to its exact headcount
192/88/179/76.) Two tests passed 2 of 3 samples rather than 3 of 3: Hydra space→time, Murmur charge→space; every
other test of every elite passed all 3. Cost is per swarm, numpy, measured while 4 workers shared the CPU; all are
within 1.0–1.4× field's, i.e. the C# port's 0.1–0.6 ms/swarm-step budget still applies (the new genes add O(n)
work, no new O(n²) pass).

**What a player experiences** (also on each page):

* **Glass Minnows** — the shy one. Every element flees and nothing mobs; the startle relays to 76% of neighbours,
  so a ship that crosses it touches 0.8% of the school (field 14%). Fly at it and a tunnel opens well before you
  arrive; park beside it and you sit in a bubble of empty water. Crisp, quick to switch, slow to heal. You herd it.
* **Phoenix** — the showman. A switch opens with a radial explosion and a 200-step vortex; the swarm swells to
  2.06× its radius before the new animal condenses. Park and ~89% of the body wraps your hull in an orbiting shell;
  fly past fast and it still parts. Tight body.
* **Hornet Cloud** — the buzz. 2.6× field's idle motion (16-step breath + every tadpole looping its slot at 1.5× top
  speed, no alignment), so it shimmers rather than streams. Half the body swarms a parked ship.
* **Old Drifter** — the slow giant. Lazy arrival, strong alignment and a 27-step dwell: idle speed 0.24 and a switch that takes 38
  steps to read. No startle relay, so a hit on one flank does not alarm the other; ships plough through it.
* **Hydra** — the regenerator. Rush laying + 2.2× top speed: back in 11 steps after a strike (jellyfish ends crisper
  than before, 1.40). Every startled tadpole bristles red; the pufferfish inflates 1.4×, the dragonfly 0.8×, the whale 0.4×. You can't wear it down.
* **Murmur** — the cloud. The loosest body (2.6 voxels off its slots: a very soft arrival under a fast breath): a murmuration that has a whale's
  shape rather than a whale. Fastest morph in the set (15 steps) — a ripple, not a show.
* **Sparkler** — the crisp one. Lowest divergence in the map (3.74 vs field 6.80): stiff arrival plus a fast breath
  and a light wobble make it glitter while staying exactly on plan. Pays for it with slow healing (59 steps).

## What failed / caveats

* **The quality filter is nearly vacuous** (above). The search therefore optimises crispness inside cells; it
  does not prove that a personality is robust in situations the yardstick does not test (crowding, several ships).
* **Drama has a ceiling of ~2×** with these genes: burst/vortex accelerations are clamped by top speed, and the
  morph always converges inside its window. A truly theatrical switch (scatter to the membrane, re-form from a
  stream) would need a scripted path, not a force.
* **Stance hinges on one threshold**: `mob_speed` vs the probe ship's 0.3 voxels/step. Glass Minnows' "never mobs" is
  mob_speed 0.30 exactly at the probe speed — a slightly slower ship would be mobbed by its Time tadpoles. In game the
  threshold should be relative to the player's actual speeds.
* Search ran 1 sample per test; only the seven named elites were re-checked at 3 samples (all still 13/13).
* Farthest-point selection in descriptor space kept returning near-duplicate "hunt" creatures; the seven were picked
  by archetype criteria instead (`zoo_pick.py`), which is a human judgement, stated as such.
* Swimming, growth-in, multi-ship and multi-swarm interactions are not scored.
* Strike-heal and descriptors use other seeds than eval16; numbers are single-seed per plan.

## Recommendation for the next round

1. **Ship field's composition layer as the fixed core and treat personality as data**: a per-species asset with
   these genes (they are all cheap scalar knobs on the step). Three or four of the elites here are already distinct
   enough to be different fauna sharing one code path: Glass Minnows (prey you herd), Phoenix (a show + a ship-hugger),
   Hydra (a defender you must out-damage), Murmur (ambient beauty). One species could also switch personality with its
   ELEMENT or with the cell's phase (calm = Murmur, frenzy = Hornet).
2. Since the yardstick no longer discriminates, **the next yardstick should be player-facing**: multi-ship chases,
   a predator that eats selectively over time, two swarms meeting, a swimming (rotated) body — and a fun/readability
   score from a human pass over the seven pages. QD with those as constraints is where this search would start to bind.
3. Combine with whichever round-2 direction produces *better plans or motion primitives* (learned plan clouds,
   flocking styles): plug them in as more genes; the map shows the composition layer will tolerate them.
4. For drama, add a scripted morph path gene (scatter-to-shell, stream-through-a-point) rather than more force.

## Files

`index.html` · `map.json` / `map.svg` (142 elites with genomes and descriptors) · `elites.json` (the seven, with
genomes, descriptors, cards) · `elites/<slug>/{showcase.html, eval16.json, probe.json, summary.json, params.json}` ·
headline creature (Glass Minnows) at the top level: `summary.json`, `rollout.json` (sn.pack, viewer-compatible),
`probe.json`, `eval16.json`, `params.json` · `boundary*.json` · `inert_crowd.json` (stance baseline).
Regenerate: `python zoo_search.py --hours N` (resumes from runs/zoo/log.jsonl, gitignored), then
`python zoo_pick.py && python zoo_publish.py finalize && python zoo_showcase.py`.
