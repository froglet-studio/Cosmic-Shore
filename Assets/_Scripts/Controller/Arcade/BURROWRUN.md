# Burrow Run (`GameModes.BurrowRun = 71`)

The **Meerkat's time race**, the Vessel Studio's Meerkat race (`Docs/Studios/VesselStudio/meerkat.html`) in the game. A
closed loop of **eight** switch rings is cut through the barren race cell; every pilot flies **two laps** of it in
order, and the first **DOMAIN** whose **LEAD RUNNER** threads the last ring of the last lap wins (golf: finish time).

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameBurrowRun.unity` (cloned from `MinigameWarpline`) |
| Controller | `BurrowRunController : GateRaceController` |
| Course | `BurrowRunCourseSource`: `SlingshotCourse.ForIntensity` (the studio's 600 u eight-ring circle and its intensity ladder) through the shared `HeadlongCircuit` |
| Metric | `ScoringMetric.SwitchesThreaded` (9), **reused**, golf |
| Target | `EndConditionOverridesSO.burrowRunGateTarget` = **16** (2 laps × 8 rings) - FrogletTools ▸ Game Modes ▸ End Game Conditions |
| Card | `_SO_Assets/Games/ArcadeGameBurrowRun.asset` - Vessels: `SO_Class_Meerkat` only |
| Generator | `Tools/Build/author_burrowrun_assets.py` (`--check`) |
| Vessel | `R_VesselActions/MEERKAT.md` (the wormhole) |

## 1. What the mode is asking

**How far to bend, and how long a tunnel the ring leaves room for.** The Meerkat cruises at 60 u/s on one thumb; its
only speed past that is its wormhole. The reticle on its nose is the tunnel RT would form; the right stick bends it
toward the next ring (up to 150°, on a bend of fixed radius), LT sets how long a straight shaft runs before the bend
(round 7: the umbrella handle), and the ride
rushes to 2.5 × the entry speed at the throat and comes out on the tunnel's last heading. A long straight tunnel needs a
ring far away; a sharp bend turns you round a corner the hull alone turns slowly.

## 2. Reused, not built

Everything but the cut is the platform's, exactly as Warpline: `GateRaceController` (rings, ordering, laps, the lead-
runner rule, the AI approach and the Regatta steering), `RaceGateTurnMonitor`, `RaceGateObjectiveProvider` (registered
in `MiniGameHUD`), `GateRaceScoringRuleSO` (one more asset), the barren race cell, the equatorial spawn ring, the shared
race toasts, the comeback.

## 2.1 Omnicrystals (2026-10-28)

The Meerkat's Omnicrystal ability (`R_VesselActions/MEERKAT.md` §7): `omniCount` gold omnicrystals lie half way between
the rings of a lap (`BurrowRunController.OnCourseRaised` → `MeerkatOmnicrystalField.ForCourse`). Fly through one and the
roots grow at once from you, sized by your last tunnel; you fly them on the left stick and come out of a tip.

## 3. AI

The platform's `AIPilot` flies the line (the gate approach, `AICommitFromApproachPoint`); the Meerkat's wormhole
autopilot (`MeerkatSolitonExecutor.Autopilot`, the studio's `aiInput` / `aiGatePlan`) forms the tunnels: once its
reticle is back and its ring is at least `aiBentMinDistance` away, it tries every bend and LT squeeze, keeps the one
whose way out heads within 0.26 rad of the ring with 60 u to spare, holds its line while it bends the reticle for
`aiShapeSeconds`, then presses RT through the **replicated** press path. Easy and Medium race on `GateRaceHandicap`
(`AIDifficultyRules.HasOwnHandicap`), the studio's two AI-level mistakes applied to rings.

## 4. Status

Authored by the generator; compiled headless (`Tools/Build/unity_refcompile`), **not yet run in the Editor** -
`/verify-unity` was not available in the session that wrote it (`Docs/UNITY_VERIFICATION_CHECKLIST.md`). The card
background is the shared placeholder until `/cardart` renders the circuit.
