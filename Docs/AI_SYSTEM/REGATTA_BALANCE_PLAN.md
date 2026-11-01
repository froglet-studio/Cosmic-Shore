# The Regatta balance plan: three searches, one level field

**Set by Garrett, 2026-10-26. Yash follows it; Claude sessions guide it (`/regatta-balance`).** Garrett's words:

> "Later we are going to want to learn the course for regatta that minimizes the disparity between the fastest and the
> slowest AI flying different vessels trained on their respective time Arcade games. Then after that converges to a
> minimum, we will learn the minimal elemental tuning needed to close that gap. Then we will set those values as the new
> elemental baseline to balance the vessel's mobility."

It is Yash's own direction made measurable (`MINIGAME_TRAINING_PLAN.md`: train each hull in its own racing minigame at
four intensities, then rebuild Regatta from what the trained vessels can do). Every stage is a compute search that any
collaborator's idle machine works on (`Docs/Compute/README.md`); the live state is `Docs/Compute/BOARD.md`.

| Stage | Searches | Loss | Done when |
|---|---|---|---|
| **1. The AIs** | each hull's AI drive, on its own Time game(s), I1-I4 | mean ln(race seconds) over I1-I4 and course seeds | every Regatta hull's drive finishes all four intensities of its own game in the model, is promoted with Yash's go, and the editor agrees |
| **2. The course** | Regatta's circuit, per intensity (`RegattaCourse.ForIntensity` + seed) | ln(slowest AI / fastest AI), keeping the rails legal, the race's length and the intensity's character | the four courses converge, are promoted, and editor races of the trained AIs show the same spread |
| **3. The elements** | ONE Time level per hull (its mobility element), for every intensity | mean ln(slowest / fastest) + minimality x mean level | converged; the levels become each vessel's elemental baseline with each hull owner's go |

"Time games" are the cards whose genre petal is Time (`ModeGenre.cs`: a race - crystals, goals, switches threaded).
A hull's OWN Time game is a single-hull arcade card; Regatta is the arena Time game they all share.

## Where it starts (the models, today's shipped numbers, 2026-10-26)

`regatta_course` on today's circuits, each hull on its offline model (`Tools/Compute/evaluators/race_models.py`):

| | Manta | Squirrel | Urchin | Rhino | Dolphin | slowest / fastest |
|---|---:|---:|---:|---:|---:|---:|
| I1 (s) | 24.8 | 48.5 | 48.3 | 50.9 | 54.0 | **2.18x** |
| I2 | 30.7 | 54.6 | 54.5 | 63.0 | 64.5 | **2.10x** |
| I3 | 35.4 | 59.2 | 59.1 | 75.1 | 73.0 | **2.12x** |
| I4 | 35.1 | 59.5 | 59.3 | 61.1 | 71.9 | **2.05x** |

The Manta is twice as fast as the field on every course, and that is the gap the plan closes. Models: the Manta, Rhino
and Dolphin on their drive sims with the shipped Regatta drives; the Urchin on its rail at 300 u/s; the Squirrel on
`regatta_balance.py`'s rail-skim model (no AI race model yet). These are MODELS (`race_models.MODEL_LIMITS`); the
editor is the measurement.

## Stage 1: the AIs

| Hull | Own Time game | People's races (2026-10-26) | Offline model | Compute job | State |
|---|---|---|---|---|---|
| Rhino | Headlong | none | `rhino_drive` (axis steering) | `drive-rhino-headlong` | ready. First local run: the shipped drive did not finish I4 on any course seed in the model |
| Manta | Redline | none | `manta_drive` | `drive-manta-redline` | ready |
| Dolphin | Switchback | 6+ (Yash, I1-I2; the learner writes `_Switchback_I<n>`) | none as an evaluator: `switchback_dolphin_sim.py` races recorded courses | - | **build**: port `SwitchbackCourse` the way `course_port.py` ported Headlong, then a `drive_fit` mode |
| Urchin | Skein | 1 | none | - | **build**: a rail-ride model of Skein |
| Squirrel | Skim Race | 7 (learned per intensity, `SkimRaceAIConfig_I1-I4`) | `skimrace_sim_harness` (C#, its `tuneall` is a search) | - | **build**: wrap the harness as a command job (needs `dotnet8`) |
| Stoat | Warpline, Slingshot | 4 | none | - | **held**: the Stoat's AI waits for Garrett's configuration (issue 1198) |

Regatta's own variants (`<policy>_Regatta`, the drive each hull flies IN Regatta, which stage 2 races):
`drive-rhino-regatta`, `drive-manta-regatta`, `drive-dolphin-regatta` (ready).

**Promotion** (any of the core four approves, Yash is told; `Docs/Compute/README.md` section 8): `compute.py promote
<job>` shows the team's best; with that yes, the fields are written
into `Assets/Resources/AIBoostPolicies/<policy>_<Mode>.asset` with the learner's writer (`learn_drives_from_flights.py`),
never by hand, on the `ai` branch (`Docs/Compute/README.md` section 7.1); the hourly AI learning sweep keeps imitating people on top of it wherever they
play. Each promotion adds an editor race to `Docs/UNITY_VERIFICATION_CHECKLIST.md`.

## Stage 2: the course

Jobs `regatta-course-i1` .. `i4` (status `blocked` until stage 1 is promoted; their `after` lists the stage-1 jobs).
Space: the eight corner angles (normalised to 360), the mouth, the corner floor, the angular spread, the lateral
swing, the gate presentation, the radial swing and the seed. Penalties keep every rail through every mouth
(RegattaCourseTests' rule), the mean race within 0.8-1.25x today's, and the hardest corner at least 0.85x today's.

**Before unblocking:** point each job's `evaluator.args.roster` at the promoted drives (the `asset` of each hull), so
the course is levelled for the TRAINED AIs; then set `status: ready`. **Promotion** (Yash): the settings become
`RegattaCourse.ForIntensity`'s row (and seed); re-run `regatta_course_harness/run.sh` and `export_course.sh`,
`author_regatta_assets.py --check`, then editor races.

## Stage 3: the elements

Job `regatta-elements` (`blocked` until the four courses are promoted). **Before unblocking:** put the promoted
settings and seeds into `evaluator.args.courses` / `seeds`. One Time level per hull in [-5, 10]; the minimality
weight (0.02 per level of mean) keeps the tuning as small as the gap allows. **Promotion**: each hull's owner agrees
(`who_works_where.py query` on its assets); the levels become its elemental baseline through
`author_regatta_assets.py`'s StartingElements and the vessel's own assets.

## Decisions waiting

1. **The target spread** (Garrett / Yash): stage 2 minimises; what spread counts as "level" for the plan to move on?
   The proposal: 1.15x at every intensity, else stage 3 closes the rest.
2. **Stage 3 at AI fidelity**: the race models do not take an element level yet, so stage 3 runs on the
   competent-pilot model. Teaching `race_models` the Time level (from `regatta_balance.MODELS`) is a build opportunity
   the scout lists; the plan can run stage 3 either way.
3. **The Stoat** stays out of stages 1-3 until Garrett's configuration lands.

## Status log

| Date | What moved |
|---|---|
| 2026-10-26 | Plan set (Garrett). Compute system, the three evaluators and the stage 1-3 jobs built (`Docs/Compute/README.md`); today's spread measured (2.05-2.18x) |
