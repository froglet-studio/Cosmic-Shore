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

## Where it stands (2026-11-06): stage 2 and 3 run on the MEASURED roster

Garrett, 2026-11-06: "the urchin was still overpowered ... make a whole new course and start evolving it to minimize
the spread, then get the last mile with the elemental differences." Stage 1's trained drives are not promoted, so stages
2 and 3 ran on today's AIs as the real game races them, not on the offline models (which were 1.5-2.6x fast for every
hull but the Urchin, so a search on them could not see it).

**The roster** (`Tools/Build/regatta_measured_roster.py`, `--check`): each Regatta-ready hull ANCHORED at its measured
editor race (Regatta Hull Benchmark, I1, the course it ran on is pinned) and moved on a new course by the geometric mean
of the race-line model's and the lap model's ratios; the Urchin fitted to its three neutral-rail races (288 u/s on its
rail, 113 off it). The Sparrow is raced and reported but not scored: its ~2x gap is its AI ramming rails.

**Stage 2** (`regatta_course` VERSION 2: NeutralShare joins the space; sep-CMA-ES, 600 evaluations per intensity, run
in a session to prove the evaluator, then promoted with Garrett's ask): a new circuit per intensity (new corners, floor,
spread, swing, mouth, presentation, seed step) and a neutral share at every intensity.

| | Shipped before (Sparrow out / in) | Promoted (Sparrow out / in) | Urchin before -> after | Hardest corner |
|---|---:|---:|---:|---:|
| I1 | 1.49x / 1.99x | **1.44x** / 1.93x | 74 -> 75 s (neutral 0.3 -> 0.40) | 98 deg |
| I2 | 2.18x / 2.91x | **1.42x** / 1.88x | 57 -> 78 s (0 -> 0.26) | 110 deg |
| I3 | 2.24x / 2.89x | **1.42x** / 1.89x | 62 -> 93 s (0 -> 0.42) | 114 deg |
| I4 | 2.25x / 2.89x | **1.41x** / 1.88x | 62 -> 90 s (0 -> 0.40) | 119 deg |

Every hull threads all 24 rings on every promoted circuit in the race-line model; the harness's 60-seed sweep and gate
coverage pass; `course_port --self-test` matches the C# harness.

**Stage 3** (Time levels on the promoted circuits; only the Stoat and the Squirrel have a measured Time response, every
other hull's Time is spent - Manta at its -5 floor, Serpent and Sparrow at 10 - or measured to do nothing (Dolphin) or
to hurt (Rhino: misses)). The Stoat at **Time 3 at every intensity** (the lowest level a measured race backs; the model's
Time 10 at I2-I4 made it a 50 s hull, a second Urchin) and the Squirrel at **rest** (its Time is worth ~5%).

**What is left, and what closes it.** The remaining 1.41-1.44x is the Manta (68-83 s, at its Time FLOOR) against the
Rhino / Dolphin / Serpent group (92-111 s). No course shape the generator can lay and no Time level reaches it:
(a) a lower Time floor for the Manta (`ElementalScaling` Soar floor 0.7; a vessel change, its owner's yes), or
(b) the group's AIs (stage 1). The Sparrow needs its AI's rail rams fixed. Then: editor races of the new circuits.

## Stage 1: the AIs

| Hull | Own Time game | People's races (2026-10-26) | Offline model | Compute job | State |
|---|---|---|---|---|---|
| Rhino | Headlong | none | `rhino_drive` (axis steering) | `drive-rhino-headlong` | ready. First local run: the shipped drive did not finish I4 on any course seed in the model |
| Manta | Redline | none | `manta_redline_sim.py` (the shipped Redline AI ported line for line, with its nose lag and skill throttle; `manta_drive` was about 3x too fast) | `redline-manta-time` (replaced `drive-manta-redline` 2026-11-03) | ready. Redline flies the Regatta steering since 2026-11-03: the shipped AI finished 4 of 12 model races on the old one, 12 of 12 on it (`REDLINE.md` §5.1) |
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
| 2026-11-03 | **Measured in the real game** (Garrett's balance pass; branch `claude/regatta-overnight-balance`; `Docs/Reports/REGATTA_AI.md` 2026-11-03 has the table). The Regatta Hull Benchmark races each AI alone headless, I1. The models were 2-4x faster than the editor because they have no missed mouths: the AI line is now capped at 0.35 of the way out and seven hulls finish with no miss. Stoat AI fixed (a stuck trigger; a Regatta warp variant). The Urchin is levelled by a course feature, `RegattaCourse.NeutralShareFor` (I1 0.3: 51 -> 73-76 s). Card at I1: Stoat Time 3, Squirrel Time 10 (measured; `author_regatta_assets.py` `MEASURED_TIME`). Solo spread of the competent field at I1: 1.5x (Manta 62-72 to Serpent 98-102) from 2.9x; Sparrow 117-150 s is the outlier. Time is spent on Sparrow, Scarab, Serpent (10) and Manta (-5); the Rhino's Time slows its AI (misses). No AI was trained in Regatta: bug fixes and card values, proposals for Yash. I2-I4 not measured |
| 2026-11-06 | **Stages 2 and 3 run and promoted on the measured roster** (Garrett's ask; branch `claude/regatta-overnight-balance`). New circuits at I1-I4 and a neutral rail at every intensity: spread (Sparrow out) 1.49 / 2.18 / 2.24 / 2.25x -> 1.44 / 1.42 / 1.42 / 1.41x; the Urchin from first by 13-22 s at I2-I4 to mid-field. Card: Stoat Time 3 everywhere, Squirrel at rest. `regatta_course` VERSION 2; the four course jobs ready on the measured roster, starting from the promoted circuits. Model numbers: editor races of the new circuits are next |
