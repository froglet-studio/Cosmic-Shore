# Warpline (`GameModes.Warpline = 65`)

The **Meerkat's Time race**, and its only Time game since 2026-11-22 (Yash). A closed loop of **five** switch rings is
cut through the barren race cell; every pilot flies **two laps** of it in order, and the first **DOMAIN** whose
**LEAD RUNNER** threads the last ring of the last lap wins (golf: finish time).

The mode was built as the Stoat's race and re-seated on 2026-10-27 (Yash: "Stoat will use Slingshot and Meerkat will
use Warpline"). The circuit, the scene and the steering did not change then. What the Stoat's races here measured is
kept in section 6; nothing in sections 1-5 is about the Stoat. Burrow Run (`BURROWRUN.md`), eight rings a lap on the
same circuit, was the Meerkat's second race until 2026-11-22 and now seats the Jewel Wasp.

| | |
|---|---|
| Scene | `Assets/_Scenes/Multiplayer Scenes/MinigameWarpline.unity` (cloned from `MinigameSlingshot`) |
| Controller | `WarplineController : GateRaceController` |
| Course | `WarplineCourseSource`: `TimeRaceCourseConfigSO.Circuit` over `SlingshotCourse.ForIntensity` → the shared `HeadlongCircuit.Generate`, target/laps = 5 rings |
| Metric | `ScoringMetric.SwitchesThreaded` (9), **reused**, golf |
| Target | `EndConditionOverridesSO.warplineGateTarget` = **10** (2 laps × 5 rings) — FrogletTools ▸ Game Modes ▸ End Game Conditions |
| Card | `_SO_Assets/Games/ArcadeGameWarpline.asset` — Vessels: `SO_Class_Meerkat` only, I1-I4 |
| Preview | `_SO_Assets/Mode Previews/ModePreview_Warpline.asset` |
| Generator | `Tools/Build/author_warpline_assets.py` (`--check`) |
| Vessel | `R_VesselActions/MEERKAT.md` (the wormhole) |
| Prototyping | FrogletTools ▸ Game Modes ▸ Time Race Prototyper (`Docs/TIME_RACE_PROTOTYPER.md`): the course and its intensities |

## 1. What the mode is asking

**How to dig each leg.** The Meerkat flies one-thumbed at a 60 u/s cruise. Its only speed past that is its wormhole
(`MEERKAT.md`): the reticle on its nose is the tunnel it would dig. The right stick bends the reticle toward the next
ring, LT shortens it, and RT forms the tunnel and dives in. The ride rushes to 2.5 × the entry speed at the throat and
comes out at the speed it went in with. Time is the ride's rush (× 1 at rest to × 2 at Time 10). That is the race:
**every second you are not in a tunnel, you are flying at cruise.**

Five rings a lap on Slingshot's 600 u circle make every leg about 750 u, longer than one straight 600 u tunnel. So each
leg is a choice:

- **One long tunnel and a turn**: dig straight, come out short of the ring and steer the rest at cruise.
- **A bent tunnel onto the ring**: bend the reticle so the way out faces the mouth.
- **A short tunnel** (LT): less ride, but it ends where you want it.

The barren race cell carries few prisms and creatures, so what the tunnel does to them (`MEERKAT.md`) is a hazard here
and not the point of the mode. The mode changes nothing about the Meerkat's kit.

## 2. Reused, not built

Everything but the cut is the platform's, exactly as Slingshot: `GateRaceController` (rings, ordering, laps, the
lead-runner rule, the AI approach), `RaceGateTurnMonitor`, `RaceGateObjectiveProvider` (registered in `MiniGameHUD`),
`GateRaceScoringRuleSO` (one more asset), the barren race cell, the equatorial spawn ring, the shared race toasts
(`DomainRaceToasts`), and the comeback (`ComebackRatePerScoreDeficit` 0.45: a quarter-of-race deficit, 2.5 gates, buys
1.1 element levels; Time sets the ride's rush, so it lands on the mode's axis).

No new ability, no cell of its own, no scoring metric and no circuit generator. There are no Warpline cell configs: the
intensity ladder is Slingshot's circuit settings (`SlingshotCourse.ForIntensity`), so a change there changes Slingshot too.
Warpline's own laps, rings a lap and four rungs are set in the Time Race Prototyper (`TimeRaceCourseConfigSO`, entry
`Warpline`): with that entry on they replace the shipped ones, for this mode only.

## 3. AI

- **Steering.** `WarplineController.AICommitFromApproachPoint` is true: Regatta's gate steering. The AI commits no
  earlier than the approach point, aims through the ring's centre and re-attacks after a missed mouth. The approach
  numbers are Slingshot's (commit 160 / lead 180 / through 120 u). Why this steering: section 6.1.
- **Tunnels.** The AI forms its tunnels through the replicated press path (`MeerkatSolitonExecutor.Autopilot`,
  `MeerkatSolitonConfig.aiWormhole`). The generator's `--check` fails if the Meerkat prefab's `AIPilot` is off, the
  autopilot leaves the replicated path, `aiWormhole` is off or the form action is not bound.
- **Difficulty.** The card offers the lobby's Easy / Medium / Hard picker (`AIDifficultyRules.IsOfferedFor`). Easy and
  Medium race on `GateRaceHandicap` with `SkimRaceDifficultySO`'s numbers: a ring noticed 0.5 / 0.25 s late, and 9.9% /
  4.5% of rings misjudged (believed two ring-radii to one side, flown past, turned back for). Hard races the true
  course. The platform's pilot belief is off here (`AIDifficultyRules.HasOwnHandicap`).
- **Training.** The shipped Meerkat AI is the race controller's run1 genes plus the wormhole autopilot, trained and
  searched in Burrow Run while that was the Meerkat's race (2026-11-19: the autopilot search found nothing better than
  the shipped values, `Docs/ElementalLoop/REDESIGN.md` § Meerkat). No AI has been trained in Warpline itself yet;
  `TrainingModeCatalog` lists it ("Warpline · Meerkat · I4"), with `Scenario_Warpline` and `FitnessProfile_Warpline`.

## 4. Where it stands on the elemental loop

`Docs/ElementalLoop/STATUS.md`, Meerkat Time:

- **Step 1, fun: redo.** The Meerkat is flagged for a redesign (Garrett, 2026-11-18): about 4.0 × slower than the
  field in Regatta. The limit is the 60 u/s cruise, since the ride comes out at the speed it went in.
- **Step 2, the I1-I4 ladder: no sign-off.** The card spans I1-I4 on Slingshot's circuit settings.
- **Step 3, AI: built**, on Burrow Run's training (section 3). With Burrow Run moved, the next training round is
  Warpline's.

Open: whether Warpline lays the Meerkat's Omnicrystals between its rings, as Burrow Run did (`MEERKAT.md`, the
Omnicrystal ability). The only fun score on record for this mode (5.8, `Docs/AI_SYSTEM/TIME_GAME_FUN_SCORES.md`) was
taken with the Stoat.

## 5. Status

Authored by the generator. **Not run in the Editor with the Meerkat** (`Docs/UNITY_VERIFICATION_CHECKLIST.md`: seat an
AI Meerkat in Warpline; it forms tunnels and finishes the race). The card background is the mode's own render
(`Assets/_Graphics/ARCADE/CardBackgrounds/Warpline.png`).

## 6. History: the Stoat's races here (2026-10-09 to 2026-10-27)

Kept because the steering in section 3 was measured in them. The Stoat no longer flies this mode; its Time race is
Slingshot (`SLINGSHOT.md`, `R_VesselActions/STOAT_DIPOLE.md`).

### 6.1 The steering fix (2026-10-26)

In Yash's 11 races (I1-I3) the 22 AI Stoat rivals finished none: 2.6 rings a minute, stalled on ring 0, against Yash's
79-121 s. Raced offline on those courses (`Tools/Build/switchback_dolphin_sim.py --mode Warpline`), the scene's gate
approach finished 0 of 11: the approach point sat 180 u out against a 160 u commit, and on its 167 u turning circle the
Stoat circled the 32-48 u mouths. That is the defect Switchback, Breakwater and Waystation had. With
`AICommitFromApproachPoint` true it finished 11 of 11. Test: `SwitchbackAISteeringTests.Warpline_*`.

### 6.2 The Stoat's Warpline autopilot copies

- `StoatDipoleConfig_Warpline` and `StoatSlingConfig_Warpline` (`Resources/AIBoostPolicies`) were learned from Yash's
  three fastest races (`Docs/AI_SYSTEM/LEARNED_DRIVES_Warpline.md`) and loaded in Warpline only.
- 2026-10-27: the dipole copy's let-go distance was set to 250 u by hand. Warped to about 140 u/s on a 167 u turning
  circle, the AI could not line up on a 26-48 u ring from the base 60 u let-go; at 250 u it finished all 15 recorded
  courses in 82-92 s with no missed mouth (Yash: median 92 s).
- Since the re-seat neither is flown here, and both are gone from the tree (`StoatSlingTests` asserts it): the dipole
  copy with the field dipole (2026-11-21), the sling copy on 2026-11-23, after it had seeded the Stoat's Slingshot copy.

### 6.3 The Stoat's two-pair limit

The black-hole budget is four holes (`BlackHolePhysics.NativeWells.Capacity`), so at most two Stoats held a pair at
once here (`STOAT_DIPOLE.md` §3). It does not apply to the Meerkat's tunnels.
