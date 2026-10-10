# Slingshot (`GameModes.Slingshot = 64`)

> **Flown on the field dipole since 2026-10-09.** The Stoat's triggers now hold its round-15
> sink–source pair (`R_VesselActions/STOAT_DIPOLE.md`), not the orbit sling §1 and §4 describe; its AI
> is `StoatDipoleExecutor.Autopilot`. The course, the target and the rule are unchanged. The Stoat's
> time race on the same circuit is **Warpline** (`WARPLINE.md`).

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
(30 at rest sticks — the game's throttle sits at half) on a flat 120°/s stick turn, and the barren race
cell has nothing to skim. Its speed and its turns are its ability. **Press** LT or RT and an attractor
is laid beside the hull on that side with a repulsor mirrored on the other; **hold** and the Stoat
orbits the attractor — the squeeze sets the circle live, 150 u at a touch down to 40 u buried, and the
hole's strength is whatever makes that a circular orbit at the speed being flown; **let go** and it
slingshots out along the tangent with up to 0.9 × its speed on top, while the pair falls together
and annihilates.

So a corner is a decision about WHERE to start the orbit, HOW TIGHT to squeeze and WHEN to let go:
release on the line to the next ring and the boost carries you there; hold a beat too long and you
leave pointed past it. A chained press on the other trigger slings straight into the opposite turn.

The pair moves **only the Stoat that laid it** — through its orbit; the pull of an owned drift pair
reaches nobody (`Docs/ELEMENTAL_ECONOMY.md` §9, LOCKED). A rival's pair is scenery: its lens bends
your view and its attractor eats prisms. But anything that FLIES INTO a black hole — a rival included,
by its own flying — comes out of its white hole.

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

**2026-10-10:** Slingshot offers the lobby AI difficulty, and the Stoat's autopilot watches its path. Both are
described in `WARPLINE.md` §3, and both apply here the same way.

An AI Stoat flies the course on the platform's gate approach (commit 160 / lead 180 / through
120 u) and **slings**: `StoatSlingExecutor.AutopilotSling` presses the trigger on the side of its
target whenever the target is at least 30° off the nose and 120 u away (at most every 3 s), HOLDS for
the arc the orbit needs to swing the nose round to it at its fixed half squeeze
(`StoatSlingMath.AutopilotHoldSeconds`), and releases — both edges through the **replicated** press
path (the Grizzly's autopilot bomb is the model). It is a turning aid, not a racing line — a human who
times the release beats it. Tunable on `StoatSlingConfig` (Autopilot sling).

## 5. Status

**Authored headless, not yet run in the editor** (`/verify-unity` unavailable in the session that
built it). What is proven offline: the generator's `--check` and validation (comeback, target ×
laps, course constants vs the generator, scene carries only the Slingshot controller, fresh
project-unique Netcode hashes, the Stoat's AIPilot enabled, the AI sling wired through the
replicated path, the Stoat on the toy roster and a class asset), the course harness above, and the
offline refcompile. What only the editor can say: how the throw FEELS at these numbers — the pull
was tuned for a black hole spawned ahead of a camera, not for racing — and whether the AI's
heuristic sling helps or hurts it. **The first tuning knobs**, in order: `StoatSlingConfig`
(`orbitRadiusWide` / `orbitRadiusTight`, `slingBoostMin` / `Max`, `radialCorrectionRate`), then the
course's mouths. The web studio flies the same orbit (`Docs/Studios/StoatFlightStudio.html`).

**Known limits.** The card wears the placeholder background until `/cardart` renders the mode;
the Stoat's ability-row icons are still the Squirrel's; the wormholes themselves are the
placeholder system (`Docs/BLACK_HOLE.md`), so a pair is laid on every peer from the replicated
press, not replicated as one object.
