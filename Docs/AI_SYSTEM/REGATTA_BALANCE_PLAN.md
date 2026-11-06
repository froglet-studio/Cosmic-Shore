# The Regatta balance plan: three searches, one level field

**The Time steps of [`AI_PLAN.md`](AI_PLAN.md)** (stage 1 = `time-arcade`, 2 = `time-arena-course`, 3 =
`time-arena-elements`). Since 2026-11-02: stage 1 is one universal genome per vessel scored on I1-I4 together and grown
from every genome that exists; no drive is trained inside Regatta (the three `drive-*-regatta` searches are retired);
stage 2 races the promoted Time genomes fixed.

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

## The direction since 2026-11-06 (Garrett): trained AIs first, a wide search, a pool of courses, one Regatta

Garrett played Regatta at I4 twice on 2026-11-06 and the Urchin won by a street both times. Gates threaded when the AI
Urchin finished its 24: first race Squirrel (AI) 11, Serpent 11, Rhino 7, Manta 6; second race Squirrel (Garrett) 14,
Serpent 10, Rhino 5, Manta 4, Dolphin 3, AI Squirrel 3. **Both races, and all 183 recorded Regatta flights, were on the
OLD I4 circuit** (the flight records' rings: 54 u mouths, no neutral rail; `Docs/Reports/REGATTA_GRID_LOSSES.md`), so
the circuits searched on `claude/regatta-overnight-balance` have never been raced by anyone, and the second race did
not show them making things worse: the build Garrett played laid the old course. Measured against the roster model on
that circuit, the real AIs were Urchin 1.00x, Squirrel 0.96x, Serpent 0.81x, Rhino 1.27x (cruising at 50 u/s for 78%
of the race) and Manta 1.48x (0.36 missed mouths a ring, 9-14 rail rams a race); traffic was not the cause (3 of 389
slow-downs beside another vessel). The searched circuits stay unshipped until the trained AIs race them. Garrett's
direction, in order (the plan in full: https://claude.ai/artifact/FdaKFy6jGwwTfcKp16hjE7):

1. **"Step one is making sure that each AI is trained well, otherwise training a course against AI that are so
   imbalanced will not work."** No course search until every Regatta hull's Time AI passes all four: independent
   restarts agree within 2% and none improves in its last 20% of evaluations; it beats every existing genome for that
   hull on the composite over I1-I4 of its own game; it is within 15% of the best human finished time where one
   exists; in the real game it misses no ring mouths alone AND in a six-AI grid, the model within 10%. Today no hull
   passes: the Rhino's search has converged (three runs by hand, within 0.1%, about 28 / 31 / 36 s at I1-I3 against
   the shipped drive's about 101 s mean) but stalls on one of three I4 seeds; the Manta, Dolphin and Squirrel searches
   are running by hand (Compute is paused); the Urchin, Stoat, Serpent, Sparrow, Scarab and Thresher need models.
2. **The search space is much more than ring angles and spacing**: rail sections (super-shielded, shielded, plain),
   gaps, detours, alternate gates, crystals on the racing line ("use sparingly, it is likely noise"), lifeforms. "Other
   ways of reducing the spread without introducing too much chaos."
3. **Search for several local minima** and shuffle between them: **arena games lose their intensities**; Regatta is ONE
   level that draws a course from a pool each match. Arena vessel selection becomes four options (Pick, Random, Draft
   with snake order, Random from a saved squad: the hull and maybe the pilot, never the element), built in its own
   thread. Arcade games keep I1-I4; that is where the AIs train.
4. **A pool passes only if its spreads cancel, never accumulate**: 1.25x on one course "is a lot" and acceptable only
   when the hull at the top of one course is at the bottom of another; each hull's mean time across the pool is held
   within about 1.10x of the others, and a hull fastest on every course fails the pool. A chaos penalty (each hull's
   time varying across seeds) keeps a coin-flip course out.
5. **Time levels last.** "If they still require tuning then it is probably something the vessel needs": a proposal to
   Garrett with the numbers, confirmed by him before any push.

Stages 2 and 3 above are read through this: stage 2 is the pool search on the widened space, against the trained AIs;
stage 3 is the last resort. The work runs as parallel threads, one brief each, in
`/mnt/project-files/regatta-training/BRIEFS.md` (the project's shared folder); every thread's numbers are model numbers
until an editor race says otherwise, and the editor is not available in the cloud.

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
| Manta | Redline | none | `manta_redline_sim.py` (the shipped Redline AI ported line for line, with its nose lag and skill throttle; `manta_drive` was about 3x too fast) | `redline-manta-time` (replaced `drive-manta-redline` 2026-11-03) | ready. Redline flies the Regatta steering since 2026-11-03 and a Soar-only ladder with Time crystals since 2026-11-04: on five laps the shipped AI finished 10 of 24 model races on the old ladder and steering, 24 of 24 now (`REDLINE.md` §5.1) |
| Dolphin | Switchback | 6+ (Yash, I1-I2; the learner writes `_Switchback_I<n>`) | none as an evaluator: `switchback_dolphin_sim.py` races recorded courses | - | **build**: port `SwitchbackCourse` the way `course_port.py` ported Headlong, then a `drive_fit` mode |
| Urchin | Skein | 1 | none | - | **build**: a rail-ride model of Skein |
| Squirrel | Skim Race | 7 (learned per intensity, `SkimRaceAIConfig_I1-I4`) | `skimrace_sim_harness` (C#, its `tuneall` is a search) | - | **build**: wrap the harness as a command job (needs `dotnet8`) |
| Stoat | Slingshot (Warpline seats the Meerkat since 2026-10-27) | 4 on Slingshot (1 finished), 15 on Warpline, none yet on the orbit-only kit | `Tools/Build/stoat_orbit_sim.py`, not yet a `drive_fit` mode | - | **next** (Yash, 2026-11-02; `MINIGAME_TRAINING_PLAN.md` §11): flies the orbit alone, AI 56-62 s in the model, races needed. The hold for Garrett's configuration (issue 1198) was lifted 2026-10-27; `.claude/skills/regatta-balance/SKILL.md` still says held |
| Thresher | Thresher Time | 15 editor flights, 8 finished (2026-11-02; I1 9 / 5, I2 3 / 1, I4 3 / 2), learned into `ThresherAIBoostPolicy_ThresherTime` | none | - | **after the Stoat** (Yash, 2026-11-02; `MINIGAME_TRAINING_PLAN.md` §12): its AI gets good in Thresher Time at I1-I4 first. Not on Regatta's card yet (`HULL_ORDER`); it goes there after that, with Garrett's yes |

Regatta's own variants (`<policy>_Regatta`, the drive each hull flies IN Regatta, which stage 2 races):
`drive-rhino-regatta`, `drive-manta-regatta`, `drive-dolphin-regatta` (ready).

**Promotion** (any of the core four approves, Yash is told; `Docs/Compute/README.md` section 8): `compute.py promote
<job>` shows the team's best; with that yes, the fields are written
into `Assets/Resources/AIBoostPolicies/<policy>_<Mode>.asset` with the learner's writer (`learn_drives_from_flights.py`),
never by hand, on the `ai` branch (`Docs/Compute/README.md` section 7.1); the daily AI learning sweep keeps imitating people on top of it wherever they
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
3. **The Stoat** joins stage 1 on the orbit-only kit once its Slingshot races are in (`MINIGAME_TRAINING_PLAN.md` §11). Garrett has not been asked about parking the field trajectory (`Docs/Studios/STOAT_FIELD_TRAJECTORY_PARKED.md`).
4. **The Thresher on Regatta's card** (Garrett, its owner): asked once its AI races Thresher Time well at I1-I4
   (`MINIGAME_TRAINING_PLAN.md` §12). Its ball threads rings as the hull does, which the Regatta design measures then.

## Status log

| Date | What moved |
|---|---|
| 2026-10-26 | Plan set (Garrett). Compute system, the three evaluators and the stage 1-3 jobs built (`Docs/Compute/README.md`); today's spread measured (2.05-2.18x) |
| 2026-11-02 | The Stoat is the next hull, on the orbit alone (Yash). `regatta_balance.py`'s `stoat_model` follows the kit (its Time 10 race 48.5 → 49.4 s at I1; the card's level is unchanged). No compute job yet: it needs Slingshot races first, then a `drive_fit` model from `stoat_orbit_sim.py` |
| 2026-11-02 | The Thresher is the hull after the Stoat (Yash). The hulls Yash picks to race Regatta: Squirrel, Dolphin, Stoat, Thresher (the Sparrow stays on the card). The rule holds: the Thresher trains in Thresher Time first, and Regatta's card and design wait for that (`MINIGAME_TRAINING_PLAN.md` §12) |
| 2026-11-06 | **Garrett's two I4 playtests**: the Urchin far ahead both times; both were on the OLD circuit (the searched circuits of `claude/regatta-overnight-balance` have never been raced; they stay unshipped). The real AIs were 0.8-1.5x the roster model, worst the Manta and Rhino (the section above; `REGATTA_GRID_LOSSES.md`). **Direction reset (Garrett)**: trained AIs first (the four-part test), a much wider course space, several local minima shuffled in ONE Regatta level (arena intensities go), spreads that cancel across the pool, Time levels last and only with his yes. Thirteen threads started on the briefs; `drive-rhino-headlong` run by hand three times (converged at I1-I3, stalls on one I4 seed); the Manta, Dolphin and Squirrel searches running by hand |
