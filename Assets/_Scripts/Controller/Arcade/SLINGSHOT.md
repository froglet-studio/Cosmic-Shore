# Slingshot (`GameModes.Slingshot = 64`)

> **Flown on the swing — the Stoat's only slingshot, for every pilot (2026-11-21, Yash: "make sure all AI uses our latest
> slingshot mechanics, and delete all other old pathfinding and other mechanics").** Press LT or RT and one small black
> hole is laid 150 u ahead and 35 u to that side; fly past beside it and it swings your heading toward itself until you
> have turned the angle your stick asks (15° away .. 52.5° at rest .. 90° toward; the dotted exit line shows it), then lets
> you go on that heading, keeping the swing's warp as momentum. One slingshot (2026-11-19): no second push at the exit.
> Every AI Stoat flies the same swing; nothing is drawn for an AI. The field dipole, the catch orbit and its aimed exit,
> the skim sling and the trampoline that Slingshot was flown on before (2026-10-09 .. 2026-11-11) are deleted from the game;
> their record is `R_VesselActions/STOAT_DIPOLE.md` §6 and git. The source of truth is `R_VesselActions/STOAT.md` and
> `STOAT_DIPOLE.md` §1b.

> **The Stoat's TIME race since 2026-10-27** (Yash: "Stoat will use Slingshot and Meerkat will use Warpline"). Warpline,
> its time race until then, now flies the Meerkat (`WARPLINE.md`). The AI Stoats train here (`Docs/AI_SYSTEM/MINIGAME_TRAINING_PLAN.md`):
> `SlingshotController.AICommitFromApproachPoint` is on (the scene's 180 u approach point sat outside its 160 u commit,
> Warpline's measured defect), and the AI flies Stoat run1's trained swing rows (`Resources/AIBoostPolicies/StoatSlingConfig_Slingshot`,
> 2026-11-17, `promote_trained_drives.py`). The course and its four intensities can be prototyped and saved in
> **FrogletTools > Game Modes > Time Race Prototyper** (`Docs/TIME_RACE_PROTOTYPER.md`).

## The Orrery (2026-11-03) - NOT editor-verified

Yash: "we need to make better and more dense environments in higher intensities, with unique structures, we can't have
always like ring and all like switchback of dolphin, we need unique environment for each vessels' time game ... something
that resembles and can take opportunity from stoat's orbit mechanics." Slingshot's environment is now a planetary system:
every corner is a body you whip round (`SlingshotOrrery`, laid by `SlingshotController.OnCourseRaised`).

**The move.** The orrery was laid for the catch pair's press point, 150 u ahead and 45 u to that side
(`SlingshotOrrery.PressAhead` / `PressSide`); the swing lays its hole 150 u ahead and 35 u aside, so a press as the moon
comes up puts the hole just inside it (D18 in `STOAT_DIPOLE.md` §8). Every turning corner (15 deg or more) has a MOON just
outside its ring, on the turn's inside (its surface 8 u from the rim). **The moons are only obstacles** (LOCKED: a game
mode never changes the vessel): they pull nothing.

| | What it adds | What it asks | Prisms a course (model, 8 seeds) |
|---|---|---|---|
| **I1 Moons** | a 9 u moon per corner (a lit voxel shell of 5 u cubes) and a BEACON - three small cubes - at the press point | press timing, wrapping a body | about 230 |
| **I2 Belts** | a 10 u moon inside a debris belt (one row of 6 u cubes, 30 u outside the ring's far rim), open 70 deg round each leg that crosses it; no beacons | aim the launch through the gap | about 390 |
| **I3 Comet tails** | belts of two rows, gaps 52 deg, 12% danger; debris tubes (radius 58 u, 55% full) round the middle of every straight | carry momentum through a corridor | about 760 |
| **I4 Clockwork** | belts of three rows, gaps 40 deg, 18% danger; tighter tubes (46 u, 75% full); two SHEPHERD moonlets (2 x 2 danger cubes) circling outside every belt whose sweep is clear, a lap every 9 s on the shared clock | time the gap, risk against reward | about 1,260, of them about 40 moving |

**Built from Blast Run's machinery**, nothing new to sync: the layout is a `SwitchbackFeatureLayout` laid by
`SpawnableSwitchbackFeatures` (the same prefab, wired on the `SlingshotController`), and the shepherds are `BlastRunMover`
spins posed by `SwitchbackBlastRunRuntime` off the server clock (its finish line now names its mode). Budgets: Blast Run's
(`TotalCap` 900 / 1,600 / 2,400 / 3,000, at most 140 moving prisms). Every piece keeps 30 u off every other ring's mouth and
40 u off every other leg's line, and out of the nucleus; a belt keeps them all. `SlingshotOrreryTests` holds it;
`Tools/Build/orrery_port.py` ports it for the model.

**The AI** flies it on its swing rule (§4). The orrery was measured on the orbit kit (`stoat_orbit_sim.py --orrery`,
2026-11-03: 79 / 79 / 87 / 91 s at I1-I4, every race finished, no moon hit); the swing on the plain course in
`stoat_orbit_sim.py --swing` (Slingshot's trained rows 62 / 71 / 74 / 76 s). A moon-relative press rule was built and
measured worse, so it was taken out.

**Not done:** corners that alternate left-right (binary moons) need a course that alternates its turns: the solver decides
that today. The course itself is unchanged (legs 340-660 u).

The **Stoat-only circuit race**. A closed loop of eight switch rings is cut through the barren race
cell; every pilot flies **two laps** of it in order, and the first **DOMAIN** whose **LEAD RUNNER**
threads the last ring of the last lap wins (golf: finish time).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameSlingshot.unity` (cloned from `MinigameRedline`) |
| Controller | `SlingshotController : GateRaceController` |
| Course | `SlingshotCourse` → the shared `HeadlongCircuit.Generate` |
| Metric | `ScoringMetric.SwitchesThreaded` (9), **reused**, golf |
| Target | `EndConditionOverridesSO.slingshotGateTarget` = **16** (2 laps × 8 rings) — FrogletTools ▸ Game Modes ▸ End Game Conditions |
| Card | `_SO_Assets/Games/ArcadeGameSlingshot.asset` — Vessels: `SO_Class_Stoat` only |
| Generator | `Tools/Build/author_slingshot_assets.py` (`--check`) |
| Course harness | `Tools/Build/slingshot_course_harness/run.sh` (the shipped solver, 400 seeds × 4 levels) |
| Tests | `SlingshotCourseTests` (written, not run in the editor) |

## 1. What the mode is asking

**Where to turn.** The Stoat (`R_VesselActions/STOAT.md`) cruises at **60 u/s** at full throttle
(30 at rest sticks — the game's throttle sits at half) on a flat 48°/s stick turn (×0.4 of the Squirrel's 120;
`SlingshotCourse.StoatTurnScaler` follows it), and the barren race cell has nothing to skim. Its speed and its turns are
its ability. **Press** LT or RT and a small black hole is laid ahead on that side; **fly past beside it** and it swings
your heading toward itself, faster the closer you pass, until you have turned the angle your stick asks; then it lets you
go on that heading, and the hole collapses. While the hole is up your flight clock warps (half from the press, all of it
and 15% more while it swings you), and after it the swing's warp is kept as momentum for 6 s.

So a corner is a decision about WHERE to lay the hole and HOW FAR to turn: aim the dotted exit line at the next ring and
you leave on it. A fresh press as one swing ends chains into the next turn.

The hole moves **only the Stoat that laid it** — through its swing; an owned hole pulls nobody
(`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). A rival's hole is scenery: its lens bends your view and it eats prisms.

## 2. The course, measured

`SlingshotCourse.ForIntensity` hands Headlong's solver Grizzly Time's corner profiles (the other
slow hull whose speed is something it lays down and rides) at a 600 u base circle — as small as
the 480..1080 race shell lets a circuit sit and still swing. Intensity is **how aimed the throw
must be**: sharper and more out-of-plane turns (the hole is laid on the hull's own horizontal, so a
climbing turn asks the pilot to ROLL the swing into it, or to aim the stick up), and a mouth closing on an 8 u stoat.

Measured by running the shipped solver over 400 seeds per level (`slingshot_course_harness`):

| Level | Mouth | Corner floor | Tightest corner, median (min) | Corners < 2× slung circle, per lap | Legs (min / median / max) | Lap | At cruise / slung |
|---|---|---|---|---|---|---|---|
| 1 | 48 u | 358 u | 361 u (358) | 0.00 | 433 / 465 / 583 u | 3709 u | 62 s / 25 s |
| 2 | 40 u | 286 u | 297 u (287) | 1.37 | 362 / 450 / 660 u | 3684 u | 61 s / 25 s |
| 3 | 32 u | 233 u | 249 u (233) | 2.00 | 406 / 496 / 553 u | 3733 u | 62 s / 25 s |
| 4 | 26 u | 215 u | 226 u (215) | 2.00 | 344 / 513 / 596 u | 3703 u | 62 s / 25 s |

Re-measured 2026-10-13 on the shipped 48°/s turn (a 72 u cruise pivot, a 179 u slung circle). The
first ladder was cut for the round-4 Stoat's 120°/s (a 29 u pivot), so its floors (143 / 115 / 93 / 86 u)
fell under twice the shipped cruise pivot from level 2 up — `SlingshotCourseTests` caught the drift.

Every one of the 1,600 courses: eight gates, gate 0 on the equatorial spawn ring's pole (the
fairness rule), every gate inside the shell, no corner under the floor or under twice the cruise
pivot. A two-lap race is ~2 min at cruise and under a minute slung well — the gap is the skill.

## 3. Reused, not built

Everything but the cut is the platform's, exactly as Redline and Grizzly Time: `GateRaceController`
(rings, ordering, laps, the lead-runner rule, the AI approach), `RaceGateTurnMonitor`,
`RaceGateObjectiveProvider` (registered in `MiniGameHUD`), `GateRaceScoringRuleSO` (one more asset),
the barren race cell, the equatorial spawn ring, the shared race toasts (`DomainRaceToasts`), the
comeback (`ComebackRatePerScoreDeficit` 0.35: a quarter-of-race deficit, 4 gates, buys 1.4 element
levels — and Space widens the swing's reach, so it lands on the mode's axis).

## 4. AI

Slingshot offers the lobby AI difficulty (`WARPLINE.md` §3 describes it; it applies here the same way).

An AI Stoat flies the course on the platform's gate approach (commit 160 / lead 180 / through 120 u) and **swings**:
`StoatSlingExecutor.AutopilotSwing`, the Stoat's only autopilot, presses the trigger on the side of its target whenever
the target is at least `aiSlingMinTurnDegrees` off the nose and `aiSlingMinDistance` away (judged on the race line's
bearing at the ring's range), or for the warp alone on a leg longer than `aiWarpMinDistance`, at most every
`aiSlingIntervalSeconds`, and lets go of the trigger at once — the hole stays up. Steering for its target it passes beside
the hole and is swung; it is let go once it faces its target within 8°. Both edges go through the **replicated** press
path. Slingshot's numbers are Stoat run1's, trained in the real game (2026-11-17): it hardly presses to turn (154°) and
presses for the warp on almost any leg (11.5 u). The AI draws nothing.

## 5. Status

**Authored headless, not yet run in the editor** (`/verify-unity` unavailable in the session that
built it). What is proven offline: the generator's `--check` and validation (comeback, target ×
laps, course constants vs the generator, scene carries only the Slingshot controller, fresh
project-unique Netcode hashes, the Stoat's AIPilot enabled, the AI sling wired through the
replicated path, the Stoat on the toy roster and a class asset), the course harness above, and the
offline refcompile. What only the editor can say: how the throw FEELS at these numbers — the pull
was tuned for a black hole spawned ahead of a camera, not for racing — and whether the AI's
swing helps or hurts it. **The first tuning knobs**, in order: `StoatSlingConfig` (`swingReach`,
`swingDegreesPerSecond`, `swingSide`, `catchWarpShare`), then the course's mouths.

**Known limits.** The card wears the placeholder background until `/cardart` renders the mode;
the black hole itself is the placeholder system (`Docs/BLACK_HOLE.md`), so a hole is laid on every peer
from the replicated press, not replicated as one object.
