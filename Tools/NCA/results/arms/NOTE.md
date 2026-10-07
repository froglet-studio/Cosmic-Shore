# Species 4: co-evolved predator and prey swarms (the arms race)

Branch `cece/swarm-x-arms`. Code: `Tools/NCA/arms_sim.py` (world + policies), `arms_seed.py` (generation 0),
`arms_train.py` (co-evolution), `arms_eval.py` + `arms_metrics.py` (measurement), `arms_plots.py` (figures).

## Headline (read this first)

- **The arms race is real and it did not cycle.** The cross-generation matrix covers 16 snapshots, generations
  0-1460 of run a3:
  - newer prey cut an older predator's catch rate in **98%** of cells (by 49 catches/min on average);
  - newer predators beat older prey in **69%** of cells (by 8 catches/min);
  - **0 of 560** generation triples are intransitive (no rock-paper-scissors).
- **Generation 0 is DESIGNED, not learned.** Run a1 started from random weights and the predators disengaged. Run
  a2 used random weights plus PFSP; the prey learned in 40 generations, but the predators never learned to pursue
  (a one-line scripted pursuer out-caught them 14x). So both species start from a behaviour clone of the simplest
  script, pursue-and-burst versus flee-and-graze, and evolution takes it from there.
- **What evolution added, with numbers** (seed g0 → g1460):
  - prey **juking**: turn rate with a predator within 15 u went from 76 to 327 deg/s;
  - prey **avoid the wall**: their time-share beyond 0.8 R fell from 0.26 to 0.13;
  - predators **pin prey on the membrane**: catches beyond 0.8 R are 3.9x the prey's time-share there, up from 2.1x;
  - predators **burst more and succeed less**: success per burst fell from 0.41 to 0.22 while bursts rose 39%;
  - prey **graze more and slow down when safe**: speed with no predator within 50 u fell from 50 to 27 u/s;
    grazing rose from 0.88 to 0.96 per prey per minute;
  - predators **ignore a vessel they cannot catch**: time-share within 40 u of it fell from 1.6% to 0.7% (the null
    is 0.8%).
- **What did NOT emerge**: schooling, splitting or decoys in the prey, and encirclement, ambush or relay chases in
  the predators. Each is measured, below, against its null. Four follow-up runs were built to provoke them:
  confusion 1.0 (a5), a slower burst (a6), confusion at 20 u (a7), and confusion on detection (a8). None produced a
  school or a ring. a6 made the predators lean even harder on the wall (catch ratio 5.1x), and a8 moved the prey 13%
  closer together.
- **Feel**: every evolved pair is inside swarm_feel's organic band for both species. The designed seed is not: its
  jerk_rel is 0.16, machine-smooth straight lines.
- **The open economy collapses without a designed satiety gate.** Predators breed from 8 to 20-30 and eat the prey
  to extinction in 2-4 minutes, then starve. A gate (a predator heavier than 1.25x its birth mass cannot burst)
  keeps both species alive for 10 minutes in all 4 seeds.
- **Cost**: 1.86 ms/step for 8 predators and 120 prey, numpy, one thread. Locality: local.
- **Most FUN pair: predators g1460 vs prey g1460, with the satiety gate.**
  - 46 catches/min, with 78% of bursts visibly juked.
  - Every catch is telegraphed by a red burst, 0.8 s ahead.
  - Organic feel, and it persists in the economy.
  - Port: the predators become a new swarm fauna that hunts the tadpole school, keeping the gate, the burst tell and
    the post-catch handling beat. Retrained on trail prisms, it becomes a baitable trail predator.

## Designed vs learned vs emergent (line by line)

| item | status |
|---|---|
| world physics (speeds, stamina, turning, perception radii, membrane, spacing) | DESIGNED |
| the asymmetry table above | DESIGNED |
| catch rule, confusion rule, handling time | DESIGNED |
| economy (food field, conserved pool, metabolism, starvation, birth by splitting) | DESIGNED |
| the satiety gate that makes the open economy persist | DESIGNED (an evaluation knob; the policies never trained with it) |
| perception wiring (which inputs each MLP sees, body frame, the 1-channel signal) | DESIGNED |
| generation 0: pursue + burst-when-close / flee + climb-food-gradient | DESIGNED, then behaviour-cloned into the MLPs |
| everything the MLPs do after generation 0 (steering, burst timing, signal use) | LEARNED (ES, 1460 generations) |
| prey juking (sharp turns at close range, not straight sprints) | EMERGENT: never in a fitness term; the seed flees straight |
| prey slowing down to graze when safe, and avoiding the membrane | EMERGENT |
| predators pinning prey against the membrane (catch ratio 2.1x → 3.9x) | EMERGENT. The seed already had some of it, by geometry |
| predators testing with short bursts and giving up (more, shorter, less successful bursts) | EMERGENT |
| evolved predators ignoring the uncatchable vessel; prey fleeing it | EMERGENT, and never trained against |

## Training curve (`curve.png`, `a3_log.jsonl`)

Catches per minute per encounter, current against current, 20-generation means:

| gen | 0 | 30 | 120 | 240 | 480 | 720 | 840 | 1080 | 1440 |
|---|---|---|---|---|---|---|---|---|---|
| catch/min | 52 | ~20 | 107 | 94 | 99 | 65 | 45 | 48 | 47 |

Three phases:

1. **Gens 0-40: the prey learn first.** Catches fall from 59 to 19 per minute.
2. **Gens 40-120: the predators answer.** Catches climb to ~110: burst timing, and pinning prey against the wall.
3. **Gens 700-850: the prey break through.** Juking halves the catch rate to ~47, where it holds, with the
   predators slowly regaining. In the matrix, predators after g900 catch the newest prey 35-51/min, against 21-31
   for predators from g200-800.

Neither side collapsed in training. The lowest 20-generation mean was ~20 catches/min; the highest share caught was
44%.

## The cross-generation play matrix (`matrix.json`, `matrix.png`)

Rows are predator generations, columns prey generations. Catches per minute per encounter, mean of 8 held-out
seeds × 30 s (sem 1-4).

- **Lower-left** (new predators vs old prey) is red; **upper-right** (old predators vs new prey) is pale. That is
  progress on both sides.
- **The prey column jumps at g700-800**: every predator before g900 loses more than half its catch rate against
  g800+ prey.
- **One exploit**: P100 catches 104/min against its own prey Q100 but only 49 against the seed Q0. Later predators
  generalise (110-121 vs Q0).
- **Mild forgetting at the end**: P1400-1460 catch Q100 at 84-93/min, against 131-133 for P600-800, while they
  beat the newest prey better than anyone.

**Anti-cycling: did the pool matter?** a3 ran with a PFSP history pool. The ablation a4 (`matrix_a4_nopool.json`)
ran 500 generations from the same seed with NO pool, only the current opponent:

| run | monotone share (newer beats older) | intransitive triples |
|---|---|---|
| a3 (with pool) | 98% (prey side), 69% (predator side) | 0 / 560 |
| a4 (no pool) | 96% / 96% | 0 / 165 |

At this scale the race did not cycle WITHOUT the pool either. The likely reason: the policy space here is smooth
and low-dimensional, and the seed already sits in a sensible basin. **Honest verdict**: the pool and PFSP were
necessary to keep a2 engaged from scratch (where they still were not enough), but from the designed seed no
evidence says they were needed. They cost 55% of each generation's encounters (48 of 88).

## Behaviours, named and measured (`eval.json` → `behave`, 3 seeds × 60 s each)

| metric (null) | g0:g0 (designed) | g400:g400 | g810:g810 | **g1460:g1460** | g1460:g700 |
|---|---|---|---|---|---|
| catch/min | 72 | 102 | 47 | **46** | 66 |
| PREY polarisation, global (0 = gas) | 0.18 | 0.16 | 0.12 | 0.11 | 0.12 |
| prey polarisation, local (prey with ≥ 2 neighbours in 15 u) | 0.67 | 0.57 | 0.50 | 0.53 | 0.52 |
| social share: prey with ≥ 2 neighbours in 15 u (uniform null 0.003) | 0.26 | 0.22 | 0.29 | 0.18 | 0.23 |
| groups of ≥ 3 within 8 u; largest group share | 1.6; 0.05 | 1.8; 0.07 | 3.6; 0.06 | 2.4; 0.04 | 1.9; 0.05 |
| milling (groups ≥ 8) | 0.22 | 0.27 | 0.26 | 0.29 | 0.28 |
| turn rate, predator within 15 u (deg/s) | 76 | 179 | 328 | **327** | 263 |
| turn rate, no predator within 50 u (deg/s) | 158 | 407 | 405 | 477 | 355 |
| speed, predator close / far (u/s) | 58 / 50 | 56 / 30 | 58 / 29 | 59 / 27 | 57 / 33 |
| confusion-fail share of contacts | 0.00 | 0.01 | 0.00 | 0.00 | 0.00 |
| grazed per prey per minute | 0.88 | 0.76 | 0.96 | 0.96 | 0.92 |
| PRED victim encirclement 1 s before the catch (null) | 0.32 (0.37) | 0.30 (0.36) | 0.34 (0.38) | 0.32 (0.41) | 0.33 (0.38) |
| cooperative catches (≥ 2 predators within 60 u) | 0.44 | 0.38 | 0.57 | 0.45 | 0.41 |
| predator nearest-packmate distance (u); pack share (< 40 u) | 81; 0.33 | 83; 0.25 | 76; 0.28 | 84; 0.26 | 74; 0.32 |
| burst bouts per minute (all 8); mean length (s) | 174; 0.61 | 219; 0.61 | 255; 0.54 | 242; 0.57 | 213; 0.63 |
| burst success (bout ends in a catch within 1 s) | 0.41 | 0.51 | 0.23 | **0.22** | 0.34 |
| relay share (a different predator was nearest the victim 2 s before) | 0.64 | 0.50 | 0.38 | 0.50 | 0.55 |
| membrane catch ratio (catch share beyond 0.8 R / prey time-share there) | 2.1 | 3.5 | 3.2 | **3.9** | 3.6 |
| prey time-share beyond 0.8 R | 0.26 | 0.17 | 0.16 | 0.13 | 0.16 |
| ambush share (catcher < 40% cruise over [-3 s, -1 s]) | 0.00 | 0.00 | 0.00 | 0.00 | 0.00 |
| predator time in the richest food third / its volume share | 1.6 | 1.9 | 1.8 | 1.7 | 1.9 |

### Named, and backed by the numbers

- **Juking (prey).** Close-range turning rises 4.3x (76 → 327 deg/s) at unchanged top speed. Burst success halves
  at the same time (0.41 → 0.22). This is the g700-800 breakthrough in the matrix.
- **Wall avoidance (prey) vs wall pinning (predators).** The prey learned that the membrane is a trap: their
  time-share there halved. The predators made more of it anyway: half of all catches land beyond 0.8 R.
- **Probe-and-abandon (predators).** More, shorter bursts with lower success. A predator now commits to a chase
  only briefly, and an evolved predator ignores the 120 u/s vessel entirely (see the player test).
- **Grazing calm (prey).** Prey slow from 50 to 27 u/s and loop (400+ deg/s) inside food patches when no predator
  is near.
- **Patch watching (predators), weak.** Predators spend 1.6-1.9x their volume share in the richest food cells. It
  is present in the seed too (prey are there), so it is not evolved.

### Looked for, and NOT there

- **Schooling.** Global polarisation is 0.11-0.18 (a gas). Social share is 0.18-0.29: well above the uniform null,
  but that is clumping on food, not a school, and it does not grow. The confusion rule NEVER fires (fail share
  0.00), because prey never pack within 10 u of a target. Raising confusion to 1.0 (run a5, 400 generations) changed
  nothing: confusion-fail share 0.00, social share 0.26. Solo juking is the cheaper answer to a single pursuer.
- **Splitting, fountain, flash expansion.** Under 3 attack samples per run with ≥ 4 neighbours, so no evidence
  either way. Prey are too sparse for a group response.
- **Encirclement.** Victim coverage sits BELOW the uniform null in every pair (0.32 vs 0.41 at g1460). The 45% of
  catches that involve two or more predators come from the same side: a convoy behind the prey, not a ring.
- **Ambush.** 0% of catches came from a waiting predator. Even the seed burns stamina hunting, and the 0.25x burn
  weight never made waiting pay.
- **Relay chases.** The relay share is HIGHEST in the designed seed (0.64), so the handoffs that exist are incidental.

## FEEL (`feel.json`; swarm_feel's metrics in numpy; organic band jerk_rel [0.2, 2.5], osc ≤ 0.08, stuck ≤ 0.02)

| pair | prey jerk_rel / osc / stuck | prey organic | pred jerk_rel / osc / stuck | pred organic |
|---|---|---|---|---|
| g0:g0 designed | 0.16 / 0.00 / 0.00 | **no** (too smooth) | 0.14 / 0.00 / 0.00 | **no** (too smooth) |
| g400:g400 | 0.79 / 0.00 / 0.00 | yes | 0.25 / 0.00 / 0.00 | yes |
| g810:g810 | 0.93 / 0.01 / 0.00 | yes | 0.30 / 0.00 / 0.00 | yes |
| g1460:g1460 | 1.10 / 0.00 / 0.00 | yes | 0.32 / 0.00 / 0.00 | yes |

Coherence is 0.05-0.37 and jitter 0.93-1.12, so both species read as a gas of individuals, not a school. The
evolved prey are as "brownian" as evo's organic reference (jerk_rel 1.9). The predators are smoother: big bodies,
turn-limited.

## Economy: collapse or not (`eval.json` → `eco`, `eco_*.png`; 4 seeds, 480 prey and 48 predator slots)

| run | after | prey (4 seeds) | predators | predator births / starved (per seed) | mass residual |
|---|---|---|---|---|---|
| g0:g0, no gate | 5 min | 0, 32, 0, 30 | 0, 0, 0, 0 | 17-19 / 25-27 | 4e-4 of 243 |
| g1460:g1460, no gate | 5 min | 0, 0, 0, 0 | 0, 0, 0, 14 | 22-34 / 28-31 | 7e-4 |
| g1460, costlier predator birth (split at 3x) | 5 min | 14, 0, 0, 67 | 15-22 | 12-17 / 2-5 | 3e-3 |
| **g1460, satiety gate 1.25x** | 5 min | 149, 137, 137, 149 | 6, 9, 8, 5 | 1-2 / 1-5 | 1e-3 |
| **g1460, satiety gate 1.25x** | 10 min | 181, 130, 145, 167 | 2, 9, 5, 3 | 2-4 / 3-8 | 2e-3 |
| **g0, satiety gate 1.25x** | 10 min | 129, 131, 124, 133 | 7, 8, 8, 8 | 0 / 0-1 | 6e-3 |

- **Without a gate it is a textbook predator overshoot.** Predators split every 4 catches above their base, reach
  20-30, strip the pond in 2-4 minutes, then starve. The Ecology program saw the same thing in its living-cell
  round, where the naive sum was a siege, and fixed it there with hunger-gated predators.
- **The satiety gate fixes persistence.** Even then, the EVOLVED predators slowly dwindle (8 → 2-9 over 10 min),
  because they burst more and so outspend a capped intake. The designed seed's predators hold at 7-8.
- **Training never saw any of this.** It is the main thing the next round should fold back into the fitness (see
  the recommendation).
- **Mass is conserved** to float32 precision in every run; the residual is under 0.003% of total mass.

## Performance and locality

- **1.86 ms/step** for one encounter of 8 predators and 120 prey: numpy, batch 1, one thread, dense O(N^2)
  neighbour matrices, both MLPs (~2.3k parameters each) evaluated every step. A Burst port with a spatial hash is
  the game's real cost. The two MLPs are 2 small matmuls per agent, cheaper than the neighbour gather.
- **Locality: local** (declared). Inputs are the agent's own state, neighbours within its perception radius (prey
  50 u, predators 80 u, in a 200 u cell), the food at its own position, and the membrane only when it is within
  R. All vectors are in the agent's body frame. No census, no global frame.

## Readability for a player (Living Ecology words)

- **Telegraph: yes, but short.** 98.5% of g1460 catches are preceded by a burst. The burst is drawn red in the
  viewer, and in game it would be the speed streak. It begins a median **0.8 s** before the catch, and the victim
  has been inside the catcher's perception for a median 3.1 s (1.7 s in the seed). The predators do NOT gather or
  circle first: within 40 u of the victim there are 0.15 predators 3-4 s out and 1.3 at the catch.
  - **So what reads is a chase, not a setup.** That is closer to the bestiary's "noquorum" pack (telegraph 0.4 s)
    than to its encircling pack (4.9 s).
- **Counterplay: yes, for the prey, and it is visible.** The juke is a hard turn at 5-6 u radius that a 33 u
  turn-radius predator cannot follow. 78% of bursts fail. A player who knew this could use the same move, and a
  vessel turns tighter than a predator.
- **Variety: moderate.** Catches spread over the whole pond. Half are at the membrane, and cooperative chases are
  45% of catches, so no two hunts look alike. But there is one predator tactic (chase + pin), not a repertoire.
- **What it looks like in a living pond** (`living_g1460.gif`, and the viewer's third run): with the gate on, the
  pond breathes. Over 120 s the prey go from 119 to 141 (grazing, splitting), and 8 predators patrol, bursting red,
  pinning tadpoles on the membrane. They turn lazy (no bursts) while fed, and lose one of their number to
  starvation.
- **Payoff: unclear in this sim.** In game, a fed predator (mass above its birth mass) is the payoff target: it
  carries the eaten mass, and the satiety gate makes it slow and lazy, which is readable.

## Player test (`eval.json` → `player`; scripted wanderer vessel at 120 u/s, never trained against; 3 seeds × 40 s)

Prey perceive the vessel as a predator and predators perceive it as prey, and it is always solid. Control: the
same flight, invisible to both, solid only.

| | g0:g0 seen | g0:g0 unseen | g1460 seen | g1460 unseen | null |
|---|---|---|---|---|---|
| predator time-share within 40 u of the vessel | 1.6% | 1.0% | **0.7%** | 0.7% | 0.8% |
| predator steps whose nearest "prey" is the vessel | 2.2% | - | **0.5%** | - | |
| predator hull contacts (per 40 s) | 5.0 | 0.7 | 1.7 | 1.3 | |
| prey time-share within 30 u of the vessel | 0.2% | 0.5% | 0.1% | 0.5% | 0.34% |
| prey "shadowing" (near prey on the far side from their nearest predator) | 0.33 | 0.29 | 0.31 | 0.15 | |

- **The designed predators DO turn on the vessel**: 7x more hull contacts than when it is invisible.
- **The evolved predators IGNORE it**, at chance. Evolution taught them to drop chases they cannot win, and a
  120 u/s target is one. For a game this matters: an evolved predator swarm is not a vessel threat unless it is
  trained as one.
- **The prey flee the vessel** (near-share falls 5x); they do not hide behind it. The shadow numbers rest on very
  few samples (0.1% of prey-time is near the vessel) and are not evidence of hiding.


## The setup

One 3D cell: a 200 u pond, a sixth of a game cell's radius, with a soft membrane. 120 prey tadpoles and 8 predators.
Units are game units (`Tools/Ecology/common/arena.py`: a tadpole is ~5 u, a vessel cruises at ~120 u/s). The step
is 0.1 s.

### The asymmetry, and why

| | prey (tadpole grazer) | predator (hunter) | why |
|---|---|---|---|
| population | 120 | 8 | Biomass pyramid: one predator needs many prey. 8 is a "pack" a player can count. |
| body radius | 2.5 u | 5 u | The predator is bigger. It is the readable silhouette a player tracks. |
| top speed | 60 u/s, sustained | 45 u/s cruise; 100 u/s BURST on stamina (3 s full, 8 s to refill) | The classic sprint-vs-endurance asymmetry. A predator that is not bursting cannot catch a healthy prey, so every catch is a visible burst: a built-in telegraph. |
| acceleration | 300 u/s^2 | 150 u/s^2 | Big bodies are sluggish. |
| turn rate | 10 rad/s (6 u turn radius at full speed) | 3 rad/s (33 u at burst) | Agility versus speed: the prey's counterplay is the juke. It keeps the race from being won on speed alone. |
| perception radius | 50 u | 80 u | Hunters see farther. Prey see predators in time to react but cannot see the whole pond. Both stay far below the 400 u cell diameter, so the policies are LOCAL. |
| eats | the food field (grazing) | prey (a catch moves the prey's whole mass into the predator) | |

A pursuit check calibrated this before training. A scripted pursue-and-burst predator against scripted fleeing prey
catches ~70-88 per minute per encounter, so the physics leaves room for either side to win.

### The economy (mass is conserved; nothing dies on a timer)

- **Food field**: a coarse 12^3 grid of grazeable mass in 6 patches. Prey graze it (Michaelis-Menten, shared out when
  a cell is crowded). It regrows only from a nutrient pool.
- **Metabolism**: both species burn mass, a base rate plus a speed^2 term. Burnt mass goes to the pool. In
  training the burn is only ledgered and charged in fitness. In the open-economy evaluation it is deducted.
- **Catch** = active mass transfer. A predator in contact with a prey catches it with probability
  `1/(1 + 0.3 * crowd)`, where crowd is the other prey within 10 u of the target. This is the confusion effect, a
  DESIGNED perceptual limit of the predator. The prey's mass moves into the predator, which then handles it for
  1 s (slowed, cannot catch).
- **Starvation** (open economy only): below half a birth mass, an agent dies and its body returns to the pool.
- **Birth** (open economy only): at twice a birth mass, an agent splits in two. Births are funded only by mass eaten.
- The audit `food + pool + prey + predators` is constant to float precision (see `eval.json` eco
  `mass_residual_max`).

**Fixed roster in training, open economy in evaluation.** Training encounters are 30 s with no births and no
starvation. Within 30 s a birth or a starvation is a rare, high-variance event, and an evolution-strategies
gradient needs a stable denominator ("share of the 120 prey caught"). The open economy, with births and
starvation over 5 minutes, is where collapse is tested: prey extinction, or predators starving out.

### The policies (LOCAL, one shared MLP per species)

Each policy is a 2-layer tanh MLP (32 hidden). The prey MLP has 31 inputs and 4 outputs; the predator MLP has 36
inputs and 5 outputs. Every vector is expressed in the agent's own body frame, a forward/right/up frame carried by
parallel transport, so there is no world "up".

- **Self**: speed; stamina and handling (predator only); the membrane's proximity and outward direction, only when
  the membrane is within R; the food at its own position and the food gradient.
- **Own kind within R**: count, mean offset, mean velocity, nearest offset, and the mean of the neighbours' 1-channel
  SIGNAL. The signal is the only secretion: a call each agent emits and its neighbours hear.
- **Other kind within R**: count, mean offset, nearest offset, the nearest's relative velocity. Predators also see
  the crowding at the nearest prey.
- **Outputs**: acceleration (3, body frame) and the signal. Predators also output a burst gate.

**Locality, declared: local.** There is no census, no global frame, and no cell centre unless the membrane itself
is within perception.

### Training

- **OpenAI-ES**, antithetic, one parameter vector per species, Adam. 4 CPU cores. torch is not installed here, so
  numpy runs everything batched over encounters.
- **Factorial blocks against the current opponent.** Four encounters share one seed: (pred ± e) × (prey ± d).
  Each species' antithetic difference is averaged over the other's two signs, so both species learn from the same
  rollouts.
- **Anti-cycling: an opponent pool.** Both policies are snapshotted every 10 generations. Each generation also
  plays antithetic pairs against pool opponents, chosen by **PFSP** (prioritised fictitious self-play, as in
  AlphaStar). A predator draws prey snapshots weighted by s(1-s), where s is its success. Prey draw the predator
  snapshots that still hurt them.
- **Fitness.**
  - Predator: catches minus 0.25 × burn, per predator.
  - Prey: minus the share caught, plus 0.5 × net mass grazed.

## Two follow-up hypotheses (warm-started from a3 g1460; each with its own matrix)

**a6: "a lone burst too slow to catch forces encirclement"** (`pred_burst` 100 → 75 u/s, 600 generations,
`matrix_a6_burst75.json`). The bestiary found that geometry, not speed, is how a slower pack catches a faster pilot.

| | a6 g0 (a3 g1460 policies at burst 75) | a6 g600 |
|---|---|---|
| catch/min | 16 | 23 |
| burst success | 0.05 | 0.14 |
| victim encirclement (null) | 0.20 (0.42) | 0.20 (0.39) |
| cooperative catches | 0.56 | 0.49 |
| membrane catch ratio | 4.4 | **5.1** |
| prey social share; nearest-neighbour distance | 0.20; 25 u | 0.29; 20 u |

- **Rejected.** The predators adapted, with catches up 40%, but not by surrounding the prey. They leaned harder on
  the membrane, which becomes the second predator: a wall cannot be juked past.
- The prey answered by packing slightly tighter, but still with no school.
- The matrix is flat and noisy: newer predators beat older prey in 67% of cells, and 2 of 35 triples are
  intransitive. With catches this rare, 600 generations bought little.

**a7: "confusion that bites at a looser spacing makes schooling pay"** (confusion 1.0, radius 10 → 20 u, 500
generations, `matrix_a7_conf1r20.json`).

| | a7 g0 | a7 g500 |
|---|---|---|
| catch/min | 42 | 39 |
| confusion-fail share | 0.013 | 0.017 |
| prey social share; global polarisation; milling | 0.20; 0.11; 0.28 | 0.20; 0.09; 0.24 |

- **Rejected.** The prey did not school.
- Their gains (newer prey cut older predators' catch in 93% of cells, by 7/min) came from the same solo juking.


**a8: "confusion on DETECTION makes grouping pay"** (detection confusion 2.0 within 20 u, plus a7's contact
confusion; 600 generations; `matrix_a8_detect.json`). Each step, a predator's perceived nearest prey is swapped,
with probability `1 - 1/(1 + 2 * crowd)`, for a random prey within 20 u of it. A predator chasing one member of a
group keeps losing track.

| | a8 g0 | a8 g600 |
|---|---|---|
| catch/min | 43 | 42 |
| prey social share; nearest-neighbour distance; groups; largest share | 0.18; 25.4 u; 2.1; 0.04 | 0.22; 22.2 u; 2.5; 0.04 |
| global polarisation; milling | 0.10; 0.24 | 0.12; 0.27 |
| predator nearest-packmate distance; cooperative catches | 88 u; 0.41 | 75 u; 0.53 |

- **Mostly rejected.** The prey drift toward each other: nearest-neighbour distance falls 13%, and the social share
  rises 23% relative. But nothing like a school forms: polarisation stays a gas at 0.12.
- The predators answer by staying closer together.
- **The first sign of cycling in any run.** Newer predators beat older prey in only 38% of cells, and 3 of 35 triples
  are intransitive. On a flat plateau, where neither side can gain, the race starts to wander.

Across a3, a5, a6, a7 and a8 the conclusion is consistent. **With a single shared policy per species and
per-encounter fitness, the prey's best answer to a turn-limited pursuer is an individual juke, and the
predators' best answer is the membrane.** Schooling and encirclement need a reason this world does not provide:
- detection-based confusion (the predator loses its target among many, before contact);
- many-eyes alarm (a neighbour's flight is information);
- prey that cannot out-turn a single predator.
Those are the next round's levers.


## What failed (all logs in this folder)

1. **a1, random init, uniform pool, burn weight 1** (`a1_failed_log.jsonl`). The predators DISENGAGED: their catch
   rate went 13 → 1/min in 180 generations and fell against the pool too. The sparse catch signal was swamped by the
   burn cost, so "move less" was the only gradient.
2. **a2, random init + PFSP + burn 0.25** (`a2_scratch_log.jsonl`). The prey learned to evade by generation 40; the
   predators peaked at 25/min, then were out-raced (4/min by gen 150). The diagnosis: a scripted pursuer catches
   54/min from a2's g170 prey, the learned predators 3.8/min. **ES did not discover pursuit from scratch in
   ~200 generations.** Hence the designed seed.
3. **No schooling, encirclement, ambush or relay emerged**, including in the three runs designed to provoke them:
   a5 (confusion 1.0), a6 (slower burst), a7 (confusion 1.0 at 20 u) and a8 (confusion on detection). a8 moved
   the prey 13% closer together, and that is the only grouping response measured.
4. **The open economy collapses** unless a designed satiety gate is added, and the evolved predators dwindle even
   with it.
5. **Infrastructure**: a container restart killed a1 mid-run, because a background job does not survive an idle
   session. Every run is resumable from `state.npz` every 10 generations, and later runs were babysat in the
   foreground.

## Recommendation

**Most FUN pair: predators g1460 against prey g1460, with the satiety gate.** Not the strongest predators: P400
and P800 kill twice as fast against their own prey. Why this pair:
- **Most contested chases**: 242 bursts a minute, of which 78% visibly fail to a juke.
- **A catch rate that leaves a school standing**: 46/min in the 200 u pond.
- **Readable**: catches cluster at the membrane, where a player can see them pinned.
- **Organic band** for both species.
- **It survives the economy** once gated.

If playtests want more payoff (more catches to interrupt), use **P1460 vs Q700**: 66/min, burst success 0.34,
still organic.

How it ports to the game's Fauna:
- **One species as the swarm predator of the other.** Both MLPs are about 2.3k floats, and their inputs are local
  neighbour summaries in the body frame, which is exactly the spatial-hash gather the round-7 swarm port already
  does. The prey port as the existing tadpole school's steering, the predators as a new fauna with a burst
  ability, stamina and a big-body turn limit.
- **Keep these designed parts:**
  - **The satiety gate**: no burst above 1.25x birth mass. Measured necessary for persistence here.
  - **The burst tell**: the red streak. It is the hunt's whole telegraph.
  - **The handling slowdown after a catch**: a readable "it just ate" beat, and a window for a player to strike the
    fed predator.
- **Against the player's trail mass**: the evolved predators would ignore a fast vessel, but a trail prism is a
  stationary "prey". A predator retrained with trail prisms as perceived prey would grow a trail-eating
  behaviour that a player can bait. Bait toward the membrane and the pinning tactic does the rest. That is a
  direct counterplay hook.
- **Next round**:
  1. **Train in the open economy, with the gate**, and fitness = persistence plus mass gained, so the predators
     stop outspending their intake.
  2. **Give coordination a reason to exist.** A slower burst alone did not (a6). Remove the wall as an ally (a
     spherical pond with a soft, unbounded edge, or prey that read the membrane earlier), so the only "second
     predator" left is a packmate.
  3. **Give schooling a reason.**
     - Contact-time confusion did not (a5, a7). Detection confusion only nudged the prey together (a8, 600
       generations).
     - The prey already perceive their neighbours' velocities and signals, so "many eyes" information was available
       and went unused.
     - The untried lever is a POPULATION of distinct prey genomes in each pond, with individual fitness (Hamilton's
       selfish herd). A single shared policy scored on the species mean cannot reward being the safer one in the
       middle, which is the classic origin of grouping.


## Files

| file | what |
|---|---|
| `policy_a3_g1460_pred_and_prey.npz` | the recommended pair (`thp` predator, `thq` prey; arms_sim.mlp layout) |
| `seed_g0_designed.npz` | generation 0: the behaviour clones of the scripted heuristics |
| `policy_a5_conf1_g400.npz` | a5's final pair (confusion 1.0) |
| `snaps_a3/`, `snaps_a4_nopool/`, `snaps_a6_burst75/`, `snaps_a7_conf1r20/`, `snaps_a8_detect/` | the snapshots each matrix uses |
| `matrix.json` (a3), `matrix_a4_nopool.json`, `matrix_a6_burst75.json`, `matrix_a7_conf1r20.json`, `matrix_a8_detect.json` | play matrices |
| `eval.json` | curve, behaviours, economy, perf, player test, telegraph, locality |
| `feel.json` | the organic band per species and pair |
| `curve.png`, `matrix.png`, `eco_*.png` | figures |
| `arms_viewer.html` | the shared Ecology viewer (`Tools/Ecology/common/viewer.py` template), 5 runs: the g1460 encounter, a vessel through g1460, the LIVING pond (g1460, open economy + satiety gate, 120 s, population on the HUD), the g0 encounter and a vessel through g0. Pilot, chase, orbit and fly cameras. |
| `encounter_g1460_g1460.json`, `vessel_g1460_g1460.json`, `living_g1460.json` | the recordings (viewer data contract; rebuild with `python Tools/Ecology/common/viewer.py out.html <json>...`; `arms_living.py` re-records the living pond) |
| `encounter_*.gif`, `vessel_*.gif` | short GIFs (orange = predator, red = bursting, cyan = prey, white ring = a catch, triangle = vessel) |
| `a1..a8_log.jsonl`, `a*_config.json` | every training run, failed ones included |

Reproduce:

```
python Tools/NCA/arms_seed.py
python Tools/NCA/arms_train.py --tag a3 --init runs/arms_seed.npz --gens 1460 --blocks 10 --pool_pairs 12 --secs 30 --sigma 0.02 --lr 0.005 --wd 0.0
python Tools/NCA/arms_eval.py --run runs/arms_a3 all --gens 1460:1460,0:0
python Tools/NCA/arms_eval.py --run runs/arms_a3 ecogate --gens 1460:1460   # the satiety-gate table (g0 row: --gens 0:0)
python Tools/NCA/arms_telegraph.py; python Tools/NCA/arms_plots.py --run runs/arms_a3
```
