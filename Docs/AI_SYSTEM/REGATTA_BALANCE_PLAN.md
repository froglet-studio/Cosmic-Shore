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
| 2026-11-06 | Manta nose lag (`time-arcade`, decision 7): in the Redline model the AI nose rate is a pure handicap. At 9/s the composite goes 22.2 to 7.3 on 12 fresh courses an intensity (I4 539 to 97 s). Two `redline-manta-nose` restarts agree within 0.6%, with the rates at the range top; the bands are flat. Proposal for Yash: a per-intensity / difficulty nose row on RedlineController (`Docs/Reports/REDLINE_MANTA_NOSE.md`) |
| 2026-11-07 | Regatta is one level that shuffles a course POOL (Garrett, 2026-11-06): the host deals a circuit per match from `Regatta Course Pool.asset` (today the four old intensity circuits), every machine lays it, intensity no longer picks it (`REGATTA.md` §4.1; report `Docs/Reports/REGATTA_COURSE_POOL.md`). Plan step: time-arena-course |
| 2026-11-06 | Why each AI loses in a grid, measured from 183 flights (`regatta_rival_report.py --grid`): Garrett's 11-06 races flew the OLD I4 circuit; Manta (misses, rail rams) and Rhino (50 u/s cruise) lose, traffic does not; three AI faults; MOUTH_ALPHA should be per hull. `Docs/Reports/REGATTA_GRID_LOSSES.md` |
| 2026-11-06 | Lifeforms became a Regatta course setting, off on every shipped course (sites beside the spine, species, counts, fauna corridor; `regatta_lifeforms.py`, model-blind by design): `Docs/Reports/REGATTA_LIFEFORMS.md` |
| 2026-11-06 | Scarab (`time-arcade`, brief 7): the AI nose lead (`AIPilot.NoseLeadGain`, off; per hull in `RaceLineStyleSO`) takes the model Scarab at Time 10 from 563-877 s I1-I4 with 2-119 misses a race to 212-221 s with under 2; not raced in Unity (`Docs/Reports/SCARAB_NOSE_LEAD.md`) |
| 2026-11-06 | Course space widened (brief 10, `Plan step: time-arena-course`): rail sections (kind, neutral colour, detour, gap), alternate rings and line crystals in the C# behind empty defaults, ported and priced per hull in `regatta_features.py`, job `regatta-course-features` proposed; `Docs/Reports/REGATTA_COURSE_FEATURES.md` |
| 2026-11-07 | Serpent: a Serpentine model and search (`serpenttime-serpent-time`); two restarts agree, 6-11% faster than what the mode flies, in the model; details `Docs/Reports/SERPENT_TIME_AI.md` |
| 2026-11-07 | Rhino I4 stalls: the cause is Headlong's gate steering (`AICommitFromApproachPoint` off), not the drive; with it on, two restarts finish all 12 courses at ~38 s composite (model; `Docs/Reports/RHINO_HEADLONG_I4_STALLS.md`) |
| 2026-11-07 | Thresher Time model + `threshertime-thresher-time` (proposed, run by hand): in the MODEL the shipped Thresher Time genome finishes no nest race and a searched "plant beside the nest" genome finishes all 48 fresh courses (best two of six restarts within 0.9%); editor soak pending (`Docs/Reports/THRESHERTIME_MODEL_2026-11-06.md`) |
| 2026-11-06 | The Stoat's Slingshot AI searched on the trampoline (`slingshot-stoat-time`, run by hand; `slingshot_stoat_sim.py` ports the C#): a warp press on long legs takes the model from 141-153 s to 62-71 s at I1-I4, restarts 0.1% apart; a proposal for Yash, `Docs/Reports/SLINGSHOT_STOAT_AI.md` |
| 2026-11-07 | The Sparrow (brief 4): not rail rams (0.4-7 s a race since the line) but its base drive letting go of the afterburner every 0.6 s (35 vs 197.5 u/s). Breakwater drive trained (`breakwater-sparrow-time`, by hand): hold the afterburner; model Breakwater 115-124 s vs the variant 158-162, Regatta I1 113.8 → 74.3 s. A proposal for Yash; `Docs/Reports/SPARROW_AI.md` |
| 2026-11-07 | Urchin: Skein model, `skein_fit` and `skein-urchin-time` (proposed, run by hand). Shipped Skein I3/I4 never finish (the mode's aim misses pinned rings); restart B 19.1 → 16.4 on hold-out cables, still not well trained; Regatta's aim + a rail seek in Skein: 8.8, then 7.9 with a genome. Genome shared with Regatta. `Docs/Reports/SKEIN_URCHIN_AI.md` |
