# Regatta courses by the hundred, and their equations (2026-11-16)

Plan step: `time-arena-course`. Regatta flywheel round 1, worker W3 (hub thread "Get the Regatta flywheel spinning").
Asked by Garrett (2026-11-16): "really mutate the heck out the course and find tons of different solutions in a vast
space. then we want to find the equations that connect those solutions together so we can find more quickly. then we
will shuffle these."

## What was built

| file | what it is |
|---|---|
| `Tools/Compute/evaluators/regatta_judge_model.py` | the race judge's MODEL backend in the shared contract's shape (genome in, every seat's finish out). W1's `regatta_judge.py` imports it for `backend="model"`; the engine backend is the game's own code in Amoebius. |
| `Tools/Compute/evaluators/regatta_loss.py` | the loss, as code: per course ln(slowest/fastest) + race noise across seeds + length band + finishes + guards (+ a field-race term on the engine); per pool the cancellation (per-hull means within 1.10x) and no hull fastest everywhere. Negative controls in `--self-test`. |
| `Tools/Build/regatta_qd.py` | one command, search -> archive -> surrogate -> pool: MAP-Elites over the course genome (cells = who wins, how ridable, how sharp, how many feature kinds), a ridge-regression SURROGATE of each hull's ln(seconds) on inspectable course features with a held-out trust gate, the surrogate screen (it ranks 8x as many offspring as the judge can race), the pool pick (`regatta_pool_pick.py`) writing `pool.json` with each course's genome, and `EQUATIONS.md` in plain words. Backends `model`, `engine`, `mixed` (trusted hulls in the engine, the rest the model; every seat says which). |

`python3 Tools/Build/regatta_qd.py run --budget 1500 --backend mixed --out DIR` is the whole chain.
`engine-pass` races a designed set of courses (each feature kind alone at two strengths, then mixes) in the game's code
and the model side by side. W2's genome (`regatta_genome.py`, every spawner and flora kind) replaces this module's
operators the moment it is importable (`ops()`).

## What it found

### 1. The surrogate finds more, sooner (model backend, plumbing proof)

Same seed, same operators, 1,000 judge calls budgeted:

| judged | cells, plain | cells, screened | passing cells, plain | passing, screened | QD score, plain | QD, screened |
|---|---|---|---|---|---|---|
| 384 | 28 | 32 | 2 | 2 | 16.8 | 19.5 |
| 464 | 30 | 37 | 2 | 5 | 18.3 | 23.8 |
| 544 | 30 | 39 | 2 | 7 | 18.4 | 26.0 |
| 1,000 | 33 | (stopped at 544) | 3 | | 21.6 | |

The screened search at 544 judge calls beat the plain one at 1,000 on every count. It was stopped there on the hub's
direction: model runs only prove the plumbing.

Surrogate held-out R^2 (courses it never saw, model backend): Urchin 0.99, Squirrel 0.99, Stoat 0.99, Manta 0.97,
Serpent 0.79, Dolphin 0.34, Rhino 0.16. The drive-sim hulls (Dolphin, Rhino) are not linear in course features.

No pool cancelled on the model: the closest 5-course pool had per-hull means 1.17x apart, Serpent slowest everywhere,
and the archive's leaders were only Dolphin, Manta, Rhino, Stoat and Urchin.

### 2. The model misprices course features; the game's own code must train the equations

A designed pass on W1's engine judge (Amoebius headless, shipped AI, alone, I1 circuit and seed: the engine reads no
circuit until W2's genome is in the build). Seconds against today's course; game = the engine, model = the model:

| course | Urchin game | Urchin model | Squirrel game | Squirrel model | Serpent game | Serpent model |
|---|---|---|---|---|---|---|
| today | 51.7 | 50.3 | 82.9 | 75.0 | 95.8 | 91.1 |
| 15% neutral | +8.9 | +23.7 | +0.1 | 0 | 0 | 0 |
| 40% neutral | +53.4 | +43.4 | +5.2 | 0 | 0 | 0 |
| two 80 u rail gaps | **+93.7** | +6.0 | +6.6 | 0 | 0 | 0 |
| six 80 u rail gaps | +6.2 | +17.8 | +8.2 | +0.1 | -0.1 | 0 |
| four 60 u detours | +1.3 | +1.3 | **+18.9** | +1.3 | **+34.4** | +3.0 |
| four 140 u detours | **+115.3** | +5.8 | **+48.2** | +5.6 | **DNF** | +49.2 |
| half the lap plain rail | 0 | +11.6 | **+27.9** | 0 | 0 | 0 |
| half the lap shielded | 0 | +2.3 | +4.4 | 0 | 0 | 0 |
| two alternate rings | 0 | 0 | 0 | 0 | **+20.1** | -4.1 |

The model gets the sign and the size (within 2x, or both under 2 s) of 33 of 56 changes. Its worst misses are the
biggest effects: rail gaps and detours throw the Urchin off its rail for whole laps, detours and plain rail cost the
Squirrel 19-48 s it does not price at all, and an alternate ring slows the Serpent's AI by 20 s where the model has it
take the shortcut. **So the surrogate that screens the search must be fitted on engine races only** (`engine_only()`),
and the model stays a plumbing backend. A course whose rails fail the guard (the 140 u detours) is no longer raced on
the engine; it is recorded as a defect.

The two-gap and six-gap courses also show the Urchin's time is not monotonic in the feature (93.7 s against 6.2 s), so
the engine runs race two seeds per hull from now on to measure the noise.

## What comes next

Since this report: the search runs on W2's genome (`regatta_genome.py`: placements of every spawner kind, flora, circuit
overrides) and on W1's engine with its eight reliable hulls (Dolphin, Manta, Rhino, Serpent, Sparrow, Squirrel, Stoat,
Urchin; Scarab report-only), one seed per searched course. `regatta_qd.py report DIR` writes SEARCH.md (the spread
distribution, each hull's standing on every course, the outliers no course fixes) and the engine-fitted EQUATIONS.md.
Today's course on that judge: 4.07x (Rhino 32 s, Stoat 45, Urchin 51, Squirrel 79, Manta 83, Sparrow 92, Serpent 102,
Dolphin 129).

1. (Running) the search on W2's genome: ~200 random + mutated genomes on the engine for Urchin,
   Squirrel, Serpent and Rhino (Stoat, Dolphin and Manta join when W1 posts their new baseline): the spread
   distribution and which placement kinds move which hull.
2. Fit the equations on those engine races, screen the search with them, pick a pool, and hand it to round 2's
   course builder (`author_regatta_assets.py`).

Every number in section 1 is a MODEL's. Section 2's game columns are the game's code in Amoebius, one seed, I1, never
the Unity editor.
