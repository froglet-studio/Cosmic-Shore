# Slingshot (`GameModes.Slingshot = 64`)

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

**Where to fall.** The Stoat (`R_VesselActions/STOAT.md`) cruises at **60 u/s** on a flat 120°/s
turn — a 29 u pivot — and the barren race cell has nothing to skim, so it has **no speed of its own
past cruise**. Its speed is its ability: squeeze LT or RT and let go, and an attractor–repulsor
**wormhole pair** is laid across the hull (attractor on the trigger's side, repulsor on the other;
squeeze depth = size). The pull adds up to **90 u/s** (`BlackHoleConfig.maxVesselPullSpeed`)
toward the attractor and off the repulsor: **150 u/s slung, on a 72 u circle.**

A sling is a **throw**: it drags the hull off the line it was flying. So the race is not "can you
make the corner" — at this scale a cruising Stoat makes every corner the solver lays — it is
"lay the pair so the pull has you lined up when the next ring arrives". Lay it too big and the
throw carries you past the ring; too small and you crawl the leg at cruise.

The pull moves **only the Stoat that slung it** (`BlackHole.OwnerVessel`): a vessel may not move an
opposing vessel (`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). A rival's pair is scenery — its lens
bends your view and its attractor eats prisms — never a force on your hull.

## 2. The course, measured

`SlingshotCourse.ForIntensity` hands Headlong's solver Grizzly Time's corner profiles (the other
slow hull whose speed is something it lays down and rides) at a 600 u base circle — as small as
the 480..1080 race shell lets a circuit sit and still swing. Intensity is **how aimed the throw
must be**: sharper and more out-of-plane turns (the pair lies on the hull's own horizontal, so a
climbing turn asks the pilot to ROLL the sling into it), and a mouth closing on an 8 u stoat.

Measured by running the shipped solver over 400 seeds per level (`slingshot_course_harness`):

| Level | Mouth | Corner floor | Tightest corner, median (min) | Corners < 2× slung circle, per lap | Legs (min / median / max) | Lap | At cruise / slung |
|---|---|---|---|---|---|---|---|
| 1 | 48 u | 143 u | 322 u (297) | 0.00 | 444 / 480 / 632 u | 4005 u | 67 s / 27 s |
| 2 | 40 u | 115 u | 124 u (115) | 0.67 | 362 / 494 / 736 u | 4124 u | 69 s / 27 s |
| 3 | 32 u | 93 u | 116 u (101) | 1.96 | 473 / 558 / 675 u | 4450 u | 74 s / 30 s |
| 4 | 26 u | 86 u | 93 u (86) | 1.98 | 404 / 569 / 699 u | 4386 u | 73 s / 29 s |

Every one of the 1,600 courses: eight gates, gate 0 on the equatorial spawn ring's pole (the
fairness rule), every gate inside the shell, no corner under the floor or under twice the cruise
pivot. A two-lap race is ~2.3 min at cruise and ~1 min slung well — the gap is the skill.

## 3. Reused, not built

Everything but the cut is the platform's, exactly as Redline and Grizzly Time: `GateRaceController`
(rings, ordering, laps, the lead-runner rule, the AI approach), `RaceGateTurnMonitor`,
`RaceGateObjectiveProvider` (registered in `MiniGameHUD`), `GateRaceScoringRuleSO` (one more asset),
the barren race cell, the equatorial spawn ring, the shared race toasts (`DomainRaceToasts`), the
comeback (`ComebackRatePerScoreDeficit` 0.35: a quarter-of-race deficit, 4 gates, buys 1.4 element
levels — and Space widens the slung pair, so it lands on the mode's axis).

## 4. AI

An AI Stoat flies the course on the platform's gate approach (commit 160 / lead 180 / through
120 u, sized to the 72 u slung circle) and **slings**: `StoatSlingExecutor.AutopilotSling` lays a
pair, attractor on the side of the AI's target, whenever that target is at least 30° off the nose
and 120 u away, at most every 3 s, through the **replicated** press path (the Grizzly's autopilot
bomb is the model), at a fixed half squeeze. It is a turning aid, not a racing line — a human who
aims the throw beats it, which is the point. Tunable on `StoatSlingConfig` (Autopilot sling).

## 5. Status

**Authored headless, not yet run in the editor** (`/verify-unity` unavailable in the session that
built it). What is proven offline: the generator's `--check` and validation (comeback, target ×
laps, course constants vs the generator, scene carries only the Slingshot controller, fresh
project-unique Netcode hashes, the Stoat's AIPilot enabled, the AI sling wired through the
replicated path, the Stoat on the toy roster and a class asset), the course harness above, and the
offline refcompile. What only the editor can say: how the throw FEELS at these numbers — the pull
was tuned for a black hole spawned ahead of a camera, not for racing — and whether the AI's
heuristic sling helps or hurts it. **The first tuning knobs**, in order: `StoatSlingConfig`
(`maxStrength`, `aheadHorizons`, `halfGapHorizons`), `BlackHoleConfig.vesselPullScale` /
`maxVesselPullSpeed` (fleet-wide — they also move tool-spawned holes), then the course's mouths.

**Known limits.** The card wears the placeholder background until `/cardart` renders the mode;
the Stoat's ability-row icons are still the Squirrel's; the wormholes themselves are the
placeholder system (`Docs/BLACK_HOLE.md`), so a pair is laid on every peer from the replicated
press, not replicated as one object.
