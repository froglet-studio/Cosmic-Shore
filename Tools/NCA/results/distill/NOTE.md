# distill — can a LOCAL rule reproduce `field`? (round 2)

Branch `cece/swarm-x-distill`. Code (all new, nothing shared was edited):

| file | what |
|---|---|
| `distill_student.py` | the local student: perception, the MLP, designed mechanics, the local morphogens, every option below |
| `distill_teacher.py` | `field` (FieldSwarm, unchanged) as a LABELER, run on a shadow of whatever swarm is being simulated |
| `distill_train.py` | behaviour cloning + DAgger (mixture rollouts with the teacher, aggregated data) |
| `distill_finetune.py` | stage 3: short-horizon BPTT on `swarm_loss` with a BC anchor |
| `distill_evalcfg.py` | full 16-test eval of a checkpoint with config overrides (the ablations below) |
| `distill_publish.py` | writes this folder |

**Short answer.** A rule in which every tadpole sees only its neighbours + the population census can be taught
by `field` to grow all four bodies reliably and to make some honest switches: **7/16** on the full yardstick
(own plans 4/4, switches 3/12) and **7/8** on the stock one — against field's 13/13 feasible. It does NOT match
field: its bodies are 10× blurrier (divergence ~30 vs ~3), it overshoots headcount by 10–30%, and it **does not
heal** (strike heal −0.8 … −3.5). The gap is not where the brief expected it (assignment, "nearest empty slot");
it is in two things field gets for free from being global: **a stable body frame** and **a composition
controller that never misfires**. Details and evidence below.

## What the published student is

Each tadpole perceives (78 + 6 inputs):
* **itself**: element, domain, velocity, looks (facing / prism / tier / spindle), its own hidden channels;
* **neighbours within World.R = 8**: kernel-weighted counts per element and per domain, mean offset, mean offset
  of same-element neighbours, mean velocity, mean plan belief, the separation vector, nearest distance, neighbours'
  startle (mean, max);
* **population signals** SwarmRule already perceives: headcount, element mix;
* **a ship** only inside its sensing radius (vessel reaction is learned from field's startle labels).

There is no slot table, no assignment, no swarm-wide "empty slot". The body plan exists only in the MLP weights
(3 × 256 SiLU → 32 outputs: velocity, look deltas, lay gate + egg direction, molt gate + molt target, startle).

Designed, local components (each one a statement of what was NOT learnable, see "the ladder"):
1. **Z — a centre estimate by local consensus.** Each tadpole keeps an estimate of the body's centre and averages
   it with its neighbours' (2 sweeps/step, a = 0.6, a 0.004 leak toward its own position). The published version
   uses **`z_motion = 0`: the estimate stays put in the world** (see below — this one switch was worth 5 → 7 tests).
2. **CLK** — an internal clock inherited from the parent (animation phase).
3. **Census plan belief** (`plan_mode = census`): every tadpole reads the element mix and keeps its own dwell
   counter (12 steps) before it believes a new majority — field's hysteresis, computed per tadpole.
4. **Genome features** (`genome = 1`): own element's plan share − its live share, the full deficit vector and the
   headcount gap, all under the tadpole's own plan belief. The plan's mix and headcount are constants of the body
   plan, i.e. part of the genome; no new perception.
5. **Freeze while contested** (`freeze_contested = 1`): a tadpole whose own dwell counter is running neither lays
   nor starts a molt.
6. Mechanics: true-breeding eggs at r_bud along the learned direction; molting over 10 steps (field's relaxed
   constraint); World collision + membrane.

Sanity check of the simulator: the teacher's actions executed through the STUDENT's mechanics pass field's own
7/8 on the stock yardstick (summed divergence 112.8 vs field 103.6), so nothing in the mechanics costs tests.

## Results — the 16-test matrix at each stage (swarm_eval, 3 samples, full)

| stage | own | std sw. | other sw. | total | note |
|---|---|---|---|---|---|
| BC, learned plan belief (teacher-driven data only) | 1/4 | 0/4 | – | 1 | compounding error: wrong mix (mass seed → 127 Charge), bodies explode (RMS 50 vs 17) |
| + DAgger 8 iters (it 6 / it 7), learned plan belief | 3/4 | 1/4 | 1/8 | 5/16 | its "switches" are into the whale only — a sink, not a switch (`learned_*` files) |
| census plan + genome, BC | 1/4 | 0/4 | 0/8 | 1/16 | |
| census plan + genome, DAgger it 5 / 6 / 7 | 4/4 | 0–1 | 0 | 5/16, 4/15, 5/15 | own plans now 4/4 every time |
| + freeze while contested | 4/4 | 1/4 | 0/7 | 5/15 | no change: contest was not the bottleneck |
| + freeze + census composition gate (designed lay/molt gate) | 4/4 | 0/4 | 0/6 | 4/14 | exact headcounts, still no switches |
| + contest counter as an INPUT (genome=2, DAgger it 3) | 2/4 | 0/4 | 0/7 | 2/15 | |
| **+ freeze + static centre estimate (z_motion = 0)** — **published** | **4/4** | **2/4** | **1/8** | **7/16** | |
| … + 1 DAgger iter on the static estimate | 4/4 | 1/4 | 0/7 | 5/15 | (more iterations running at writing time) |
| (field, the teacher) | 4/4 | 4/4 | 5/5 feasible | 13/13 | |
| stage 3 fine-tune (BPTT on swarm_loss, 70 iters) | – | – | – | 4/16 light | BC loss drifted 0.75 → 0.95, sink noisy; stopped |

Published matrix (rows = grown, columns = wanted; rate over 3 samples):

```
from \ to      mass   space   charg    time
mass          1.00+   0.33-   0.67+   0.00-
space         0.00-   1.00+   1.00+   0.00-
charge        0.00-   0.33-   1.00+   0.00-
time          1.00+   0.33-   0.50-   1.00+
PASSED 7/16
```

Stock yardstick (`summary.json`): **7/8**, own divergence 33.8 / 44.7 / 30.8 / 32.6 (field 3.1 / 2.8 / 1.3 / 5.5),
summed wanted 407.9 (field 103.6). Headcounts 212 / 115 / 201 / 95 vs plans 192 / 88 / 179 / 76.

Strike probe (`probe.json`): **heal −0.91 / −3.54 / −1.72 / −0.83** (whale / jelly / puffer / dragonfly). The
swarm keeps its headcount but loses shape after the cut; it does not heal (field: ~1.0 everywhere).

Single-sample ("light") evals are badly noisy at this level: one checkpoint read 9/13 light and 5/15 full. Every
number in the table above is the full 3-sample eval.

## What the student could not learn locally — the ladder

1. **Plan choice is not the problem.** Learned plan belief mushed (0.4 / 0.4 between two plans after a cull);
   computing it per tadpole from the census with field's dwell (allowed signals only) made own plans 4/4. This is
   easy to give a local rule and should simply be given.
2. **Off the teacher's states, the teacher's velocity is not locally predictable.** On teacher-driven data the
   velocity error is 0.013; on the student's own (DAgger) states it is **0.127 — 10×**. Field's velocity there is
   "go to your Hungarian slot relative to the global anchor"; the slot is global, so a local rule regresses to an
   average direction. DAgger rescued own plans, not switches.
3. **"Pause while contested" cannot be learned from the labels.** Contested states are 0.5% of rows, and the
   teacher LAYS MORE there (1.5% vs 0.3%), because the student's census is contested exactly when the teacher has
   already committed elsewhere. Designing the pause in changed nothing (5/15 → 5/15).
4. **The real killer is the body frame.** Long-horizon runs (jellyfish, 480 steps, no cull):

   | step | local Z (dynamic consensus) | ORACLE true centroid (not local) | static Z (`z_motion=0`) |
   |---|---|---|---|
   | 80 | RMS 15.2, own 19.3 | 15.3, 20.5 | 13.8, 21.5 |
   | 160 | 26.5, 40.7 | 18.6, 28.9 | 20.1, 29.3 |
   | 240 | 43.4, 82.4 | 24.8, 43.7 | 30.8, 52.3 |
   | 480 | 59.2, 129.4 | (≈25, 47 at 400) | 37.6, 75.6 |

   With the centre estimate riding each tadpole's own motion (textbook dynamic average consensus), a tadpole that
   drifts outward carries its estimate with it, so its offset from "the centre" never changes and nothing pulls it
   back: the body dissolves silently (centre estimates end 54 voxels apart). More consensus sweeps (8 per step)
   did NOT help — it is not convergence speed, it is lost positional feedback. A centre estimate that stays put in
   the world and only diffuses restores the feedback; that one change took the yardstick from 5 to 7 tests and
   made switches real (whale → puffer, jelly → puffer, dragonfly → whale). It is still not the oracle: estimates
   drift apart ~14 voxels by step 480, so the body still slowly blurs, and a switch (whose body is rebuilt at a
   new frame) loses its shape before the 240 switch steps end (switched swarms score ~110–120 against everything).
5. **Composition overshoots.** Even with the oracle centre the jellyfish grows to 144 (plan 88) and over-breeds
   Charge (49 vs 25): the learned lay gate does not hit zero sharply at the plan headcount. A designed gate
   (`census_gate`) gives exact headcounts (192 / 89 / 179 / 77) but on its own does not save switches (4/14),
   because the frame decays anyway.

## What a player would see

A knot of 16 that, within ~80 steps, lays itself into a recognisable whale / jellyfish / puffer / dragonfly
(fuzzier than field's — the silhouette is right, the details are soft) and visibly animates off its internal clock.
It reacts to a passing ship (learned from field's startle labels; not separately measured here). Then, over a few
hundred steps, it **slowly spreads like ink in water**, and a ram leaves a wound that never closes. Eat its
majority and it does turn into the new creature in about half the switches — molting colours ripple through it —
but the new body is blurrier still. Honest summary for a player: alive, legible at a glance, not robust. Field is
the better creature; this is a research result, not a shippable one.

## Recommendation for the next round

1. **Do not try to learn the body frame or the plan choice.** Give a local rule (a) the census plan + dwell, and
   (b) a positional signal that holds its shape. The cheapest honest local frame is a morphogen with a FIXED
   source, not a consensus: e.g. the heart-of-the-swarm tadpole (oldest / seed) emits a hop-count or diffusing
   gradient that every tadpole reads; or the static Z here plus a slow re-centring. Measure it with the oracle-Z
   long-horizon test (`/tmp`-style harness in this NOTE's table): a frame is good enough when it matches the oracle
   column at step 480.
2. **Combine with `field`'s composition controller as a designed layer** (the hybrid result from round 1 says the
   same): learning laying/molting from demonstrations misfires exactly in the rare states that decide a switch.
3. **Then** distil only the MOTION (the part that genuinely is local: boids + arrive toward a density field in the
   body frame). With a stable frame and designed composition, the student's own-plan fit was already 4/4.
4. If the learned route is kept: train with the frame used at test time from the start (the published student was
   trained with the riding-Z estimator and only RUN with the static one), and evaluate with ≥3 samples — single
   rollouts misled me twice by ±4 tests.

## Reproduce

```
python Tools/NCA/distill_train.py bc     --run runs/distill/gbc --genome 1 --plan_mode census --batches 12 --epochs 10
DISTILL_LIGHT_EVAL=1 python Tools/NCA/distill_train.py dagger --run runs/distill/gdag --init runs/distill/gbc/student.pt --iters 8 --batches 6 --epochs 6
python Tools/NCA/distill_evalcfg.py runs/distill/gdag/student_07.pt freeze_contested=1 z_motion=0
python Tools/NCA/distill_publish.py --ckpt <that checkpoint with the cfg saved> --out Tools/NCA/results/distill
```
About 3 hours of 4-core CPU end to end (data collection ~25 s per 8-episode batch when the CPU is not shared).
`student.pt` here carries its config; `learned_*` are the fully learned (learned plan belief) student's files.
