# AI System — Architecture (every vessel AI, as it is and where it is going)

Measured on `ai-system` (retired 2026-10-09; the work now lives on `Ys-bleeding-edge`) at the 2026-10-08 cut (perf `1f3c7e829` + Ys-bleeding-edge `be50ff6ba`). The
**roster in §2 is the source of truth for "which AIs exist"**: when a new one arrives from bleeding-edge,
it gets a row here first (`BRANCH_WORKFLOW.md` §4). Every count below was measured with `git grep`.
When a row and the code disagree, the code wins; fix the row. The layer-1 row, the gate scope and the Regatta rows were
re-measured on `Ys-bleeding-edge` on 2026-10-17.

Rules every AI on this branch obeys (unchanged, enforced by gates):
- **Input-only.** An AI reads what a pilot could see and writes only the vessel's input channels. It never
  writes game state a human could not. Gated by `Tools/Build/check_ai_no_state_writes.py --check` — today
  only for four folders: `Controller/AI/SkimRace`, `Utility/AITraining`, `Editor/AI` (the Third Eye) and
  `Controller/Arcade/Preview/Drill` (§3 item 6). `AIPilot` and `Controller/AI/Boost` are not gated.
- **No per-frame allocation**: no 3-argument `Mathf.Min/Max` (`MathfNoAlloc`, `check_mathf_params_alloc.py`),
  no closures or LINQ in a tick, `NonAlloc` physics queries.
- **No `MathF` in code a Burst job reaches** (`Docs/claude/ANTI_PATTERNS.md`, `SKIM_RACE_AI.md` §8.0k).
- **Logs on a `CSLogChannel`** (`AITraining`, `Input`…), never raw `Debug.Log`; nothing per frame.

---

## 1. The six layers

An AI vessel is built from up to six layers. Most hulls use only layers 0–2; the special pilots
replace layer 1 entirely.

| Layer | What it is | Code | Who uses it |
|---|---|---|---|
| **0. Seat and install** | Which domain and hull an AI seat gets, which pilot drives it, at what difficulty | `ServerPlayerVesselInitializerWithAI` (`GetBalancedDomain`, `ConfigureAIPilot`, `ConfigureAIPilotForMode` — the player-seek mode list is hard-coded there: Joust, Dog Fight, Broadside), `AIHullSeating`, `AIDifficulty` / `AIDifficultyRules` (`IsOfferedFor`), `AIDifficultyPicker`, `LobbySnapshot.AIDifficulty`, `GameDataSO.RequestedAI*` | Every AI seat |
| **1. Platform autopilot** | The default pilot on every hull: target choice, steering, avoidance, orbit break, aim telegraph, drift commit, boost policy | `Controller/AI/AIPilot.cs` (1,462 lines), `AIPilotBelief` (lobby difficulty on the platform autopilot), `PursuitReachability`, `AIBoostPolicySO` + 7 policy types (`Charge`, `Hold`, `Pellet`, `SkimRing`, `SnapDash`, `Ramp`, `BrittleStarSpeedMode`) with assets for Brittle Star, Dolphin, Rhino, Scarab, Serpent, Sparrow, Squirrel, Stoat (`Assets/_SO_Assets/AI Boost Policies/`; the Stoat's is a `SkimRing` copy that builds no driver), per-mode variants (`AIBoostPolicySO.ForMode`, `AIModeTuning`, `MantaSoarAITuningSO`) | 16 hull prefabs: Brittle Star, Butterfly, Dolphin, Falcon, Grizzly, Manta, Rhino, Scarab, Serpent, Shrike, Sparrow, Squirrel, Stoat, Termite, Thresher, Urchin |
| **2. Mode steering hooks** | A mode tells the autopilot where to fly / where to look, without its own pilot | `AIPilot.SetExternalTargetProvider`, `SetDriftLookTargetProvider` | 22 mode controllers (Astro League, Bends, Broadside, Cleave, Dog Fight, Dustup, Gate races, Hijack, Maelstrom, Rampage, Regatta, Scarab Scramble, Sirocco, Skein, Tapestry, Tollway, Undertow, Waystation, Wildlife Liberation, Wrecking Ball, …) |
| **3. Hull/mode AI helpers** | Pure decision code for one hull's kit in a mode, driven from the mode controller | `UrchinAutopilotDriver` + `UrchinRailAssessment` (`Controller/AI/Urchin/`; Hijack, Skein, Regatta), `ButterflyAutopilotModeDriver` (`Controller/AI/Butterfly/`; Dustup, Sirocco, Tapestry), `ScarabScrambleJukePlanner` (`Controller/AI/Scarab/`) | Urchin, Butterfly, Scarab |
| **4. Ability-level AI** | An ability executor that presses its own buttons when the hull is autopilot-driven | `GrizzlyTriggerBombExecutor` (`IsAutopilotDriven`), `MantaAnalogTurnBoostExecutor` (`IsAutopilotDriven`), `EchoSightActionExecutor` (holds while AIPilot drifts) | Grizzly, Manta, Dolphin |
| **5. Replacement pilots** | A dedicated pilot that stands `AIPilot` down for one mode | `SkimRacePilot` + `SkimRaceDriver` (`Controller/AI/SkimRace/`; Squirrel in Skim Race and Regatta, installed by `SkimRaceAIDeployment`); `TrainingPilot` (`Utility/AITraining/`, genetic training, installed by `TrainingDeploymentService` / the training runner) | Squirrel; training scenarios |

**How an AI presses a button.** Every layer acts through the same channel a human uses:
`R_VesselActionHandler` (its non-input-system entry points, for which `AIPilot` is the only caller) and
the vessel's input status. That is what "input-only" means in practice.

**Not AI decisions** (the sync report flags them because they name the autopilot; they only skip
human-only feedback when the autopilot flies): `MantaStingActionExecutor` (planting haptics/HUD),
`MantaBombMarker`, `ElementalTransfer` (comment on where AI guns run), the vessel HUD controllers.
A new file of this kind needs no roster row; say so in the intake note.

Cross-cutting, not a pilot: `AICinematicBehavior` (end-of-game flybys, 3 prefabs), the Skim Race race
recorder and benchmark runner (editor only).

## 2. Roster — every AI, its home and its tooling

| AI | Layer | Code | Doc | Offline tests | Profiler markers | Offline harness | Status |
|---|---|---|---|---|---|---|---|
| Platform autopilot | 1 | `AIPilot`, `PursuitReachability` | `Controller/AI/AI_ORBIT_BREAK.md`, `Docs/claude/FTUE_DIALOGUE_AI.md` | `AIPilotTests`, `PursuitReachabilityTests`, `AimTelegraphBindingTests` | **none** | none | Live on 13 hulls; cost never measured. 2026-10-10: one lap round an objective at most (`OrbitDetector` eccentric-orbit fix, `orbitSweepDegrees` 540 → 360, abandon a crystal after a full lap for `orbitAbandonSeconds`), not editor-verified |
| Boost policies | 1 | `Controller/AI/Boost/` | `Controller/AI/AI_BOOST.md` | `RhinoRampDriveTests`, `DolphinChargeDriveTests`, `RegattaDriveTuningTests` (+ AIPilot tests) | **none** | `Tools/Build/rhino_drive_sim.py`, `dolphin_drive_sim.py` (models) | 7 policy types, 8 assets |
| Rhino drive | 1 | `RampBoostAIPolicySO` (ramp on a straight that pays back, brake ON the ramp to a corner speed from `AIBoostContext.CornerDegrees`, let go at the corner) | `AI_BOOST.md` (Ramp) | `RhinoRampDriveTests`, `RhinoRampGradingTests` | **none** | `rhino_drive_sim.py` | First cut 2026-10-15, rebuilt 2026-10-17, retuned for Regatta's centre steering; Regatta corner speed 333 from Yash's six races (2026-10-24, `RhinoAIBoostPolicy_Regatta`); not editor-verified (T-20261016-01ec, T-20261024-2979) |
| Dolphin drive | 1 | `ChargeBoostAIPolicySO` (release the commit drift on a straight; hold the next drift off until the discharge peaks) | `AI_BOOST.md` (Charge) | `DolphinChargeDriveTests` | **none** | `dolphin_drive_sim.py` | Peak re-drift 2026-10-17 (`9b064721b`); Yash's one flight re-drifts at the peak too (2026-10-24); not editor-verified (T-20261016-b719) |
| Race lines (Regatta) | 2 | `RegattaRaceLine` (the gap between two rails, clear of all three), `RaceLineStyleSO` (`Resources/RaceLines/<Hull>_Regatta`, by `author_race_lines.py`; line, room, risk, per-seat risk), `RegattaController.TryRaceLine` via `GateRaceController.TryRaceLine` | `REGATTA.md` §6.2, `REGATTA_AI_ROADMAP.md` §3.0 | `RegattaRaceLineTests` | — | `learn_drives_from_flights.py` (ring crossings) | 2026-10-17; every styled hull (not Squirrel, not Urchin); from Yash's flights 2026-10-24: Manta 0.809 (learned), Rhino 0.676 and Dolphin 0.785 (set by hand); not editor-verified (T-20261017-8cc0) |
| Per-mode tuning | 1 / 4 | `AIBoostPolicySO.ForMode`, `AIModeTuning` (the Stoat's three configs), `MantaSoarAITuningSO.LoadForMode`: `Resources/AIBoostPolicies/<asset>_<mode>` | `AI_BOOST.md`, `SITUATION_RECORDING_PLAN.md` §9 | `RegattaDriveTuningTests` | — | — | Machinery on bleeding-edge (#1165). Two variants so far: `MantaSoarAITuning_Regatta` (Soar bands 0.975 / 0.975 learned from Yash's flights 2026-10-24; first 0.8 / 0.85, `f4e35748c`, Ys-bleeding-edge only, from `manta_drive_sim.py`); `RhinoAIBoostPolicy_Regatta` (`cornerSpeed` 333, set by hand 2026-10-24 from Yash's six races); Dolphin and Stoat have none |
| Flight recorder and learners | — (data) | `HumanFlightRecorder` (editor and development builds; editor send `BugHuntAutoSend`), `Tools/Build/learn_from_flights.py` (Squirrel), `learn_drives_from_flights.py` (Rhino, Dolphin, Manta, Stoat; Urchin report only), `situation_features.py` | `SITUATION_RECORDING_PLAN.md`, `RACING_LINE.md`, `LEARNED_FROM_FLIGHTS.md` | the scripts' `--self-test` | — | the Python scripts | Squirrel learned from 29 flights (2026-10-16); other hulls wait on flights |
| Skim Race pilot | 5 | `Controller/AI/SkimRace/` | `Docs/SKIM_RACE_AI.md` (+ `SKIM_RACE_AI_SESSION_LOG.md`) | `SkimRaceAITests`, `SkimRaceCourseQueryTests`, `SkimRaceShellTests`, `SkimRaceTeamAssignmentTests`, `SkimRaceHandicapTests`, `SkimRaceTrackFingerprintTests`, `AIDifficultyRulesTests` | `SkimRace.Pilot.*`, `SkimRace.Driver.*` | **yes** — `Tools/Build/skimrace_sim_harness/` (compiles the shipped driver) + editor benchmark + race recorder | Tuned, QA PASS 2026-10-08 (83 s / 86 s) |
| Urchin rail rider | 3 | `Controller/AI/Urchin/` (adapters in `HijackController`, `SkeinController`, `RegattaController`) | `Controller/Vessel/R_VesselActions/URCHIN_TRAIL_RIDER.md`, `Controller/Arcade/HIJACK.md`, `SKEIN.md`, `REGATTA.md` | `UrchinRailAssessmentTests`, `UrchinChainReactionTests` | **none** | none | **Intake 2026-10-08** (Regatta rail choice is new) |
| Butterfly mode switch | 3 | `ButterflyAutopilotModeDriver` | `Controller/Arcade/DUSTUP.md`, `BUTTERFLY.md` §8a (audit) | none | **none** | none | **Audited 2026-10-17**: works; open items in `BUTTERFLY.md` §8a (AI at 0.6 throttle in Dustup / Sirocco / Tapestry, no crystals in Dustup / Sirocco, no tests) |
| Butterfly fold drive (Waystation) | 2 / 4 | `WaystationController.TickAutopilotFolds` (aim held under asin(0.5 R / d), let go 30 u short of the ring's plane; full throttle) | `WAYSTATION.md` | `WaystationFoldTests` | **none** | none | Fixed 2026-10-17 (landed off the ring; added an AIPilot to human hulls); not editor-verified |
| Scarab jukes | 3 | `ScarabScrambleJukePlanner` (`Controller/AI/Scarab/`), reads `ScarabJukeController` | `Controller/Arcade/SCARABSCRAMBLE.md` | `ScarabScrambleJukePlannerTests` | **none** | none | **Intake 2026-10-08** (new) |
| Grizzly AI bombs | 4 | `GrizzlyTriggerBombExecutor` + `GrizzlyTriggerBombConfigSO` (the bomb pump it replaced is retired) | `R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md`, `Controller/Arcade/GRIZZLYTIME.md` | `GrizzlyTriggerBombTests`, `GrizzlyTimeCourseTests` | **none** | none | **Intake 2026-10-08** (new) |
| Stoat autopilot sling / dipole | 4 | `StoatSlingExecutor.AutopilotSling`, `StoatDipoleExecutor` (both triggers when the target is within 12° of the nose, up to 75°), `AutopilotHold01` on `StoatSlingConfigSO` / `StoatDipoleConfigSO` | `R_VesselActions/STOAT.md`, `STOAT_DIPOLE.md`, `Arcade/SLINGSHOT.md`, `WARPLINE.md` | `StoatSlingTests`, `StoatDipoleTests` | **none** | the Stoat Flight Studio (web, design numbers) | **Intake 2026-10-09** (arrived with Slingshot / Warpline; studio built, game AI not editor-verified). 2026-10-10: dipole autopilot capped at one lap round its sink (`autopilotMaxOrbitDegrees`) |
| Brittle Star autopilot | 3 | `BrittleStarExecutor.Autopilot` (presses the replicated controls: fire on rival mass / a rival hull ahead, Destruction in front of a dense wall, the ring into a cluster or whenever a stored charge makes it free, Creation otherwise) + `BrittleStarSpeedModeAIPolicySO` (Speed Mode on the boost law) | `R_VesselActions/BRITTLE_STAR.md` §6 | none (the rules it reads: `BrittleStarTests`) | **none** | none | **Intake** (2026-10-14, not yet watched in Unity) |
| Gate-race difficulty | 2 | `GateRaceHandicap` (`Controller/AI/`), applied in `GateRaceController.ArmRacers` to Easy / Medium AIs on cards `AIDifficultyRules.IsOfferedFor` names (Skim Race, Slingshot, Warpline, Regatta; the Arena cards Astro League, Brood Rush and Broadside get `AIPilotBelief` instead); the Skim Race's two belief mistakes on rings (noticed late, misjudged two radii off), numbers from `SkimRaceDifficultySO` | `Arcade/SLINGSHOT.md`, `WARPLINE.md`, `Arcade/REGATTA.md` | `GateRaceHandicapTests`, `AIDifficultyRulesTests`, `GateRaceSteeringTests` | **none** | the Stoat Flight Studio (levels measured there) | **Intake 2026-10-10**, not editor-verified. Since 2026-10-15 `GateRaceController` reports each AI's phase (`AIPilot.ReportObjective`: `Approach`, `Through`, `NotNoticed`, `Misjudged`, …), ring and lap, which the Third Eye reads. 2026-10-17: in Regatta (`AICommitFromApproachPoint`) the "through" point lies on the line from the pilot through the ring's centre, not on the ring's axis |
| Thresher | — | none: an AI Thresher races at cruise and never slings (`Arcade/THRESHERTIME.md` §4) | `THRESHERTIME.md` §4 | — | — | — | No AI drive yet; an autopilot sling is that doc's follow-up |
| Microgame ghost demo | 1 (+ a scripted press) | the platform autopilot flies; `DrillGhostDemo` (pure) picks the control and `DrillRunner` presses it through the replicated handler; `ModePreviewSession.HandleGhostDemoChanged` hands the stick over (`Controller/Arcade/Preview/Drill/`) | `Docs/ModePreview/TRAINING_PLAN.md` §10.7 | `Tools/Build/drill_harness` (ghost checks) | none (one press per demo, no per-frame decision) | `drill_harness` | Not run in the editor; input-only gate covers the drill folder |
| Manta turn boost | 4 | `MantaAnalogTurnBoostExecutor` | `R_VesselActions/MANTA_STING_KABLOOM.md` | none | **none** | none | Intake |
| Genetic training pilot | 5 | `Utility/AITraining/` (7,596 lines) | `Utility/AITraining/README.md`, `Docs/AI_TRAINING_CONSOLIDATION.md` | `AITrainingCoreTests`, `InputOnlyContractTests` | none | its own runner | **Dormant**: its runtime installer resolves its settings only in the editor, and its only archived genome (Squirrel / Skim Race / I4) is claimed by the Skim Race pilot. Fate is the user's call (§4.6). |

## 3. What the measurement says is wrong today

1. **Only one AI can be profiled.** Every marker in the AI code is Skim Race's. The autopilot runs on
   every AI seat in ~30 modes and its cost has never been measured; nor has any layer-3 or layer-4 AI.
2. **Three ways to give a seat its pilot**, and the two replacement pilots avoid each other through a
   hard-coded "Squirrel and `SkimRaceAIDeployment.Claims`" check inside `TrainingDeploymentService`.
3. ~~**Layer-3 code lives in three different places**: the Urchin driver under `Controller/AI/`, the
   Butterfly driver under `Controller/Arcade/ButterflyGames/`, the Scarab planner inside its controller.~~
   **Fixed 2026-10-17 (§4.3):** all three now live under `Controller/AI/<Hull>/`.
4. **Layer-4 AI is invisible from the AI folder**: three executors decide for the autopilot, two of them
   (Grizzly, Manta) through their own private `IsAutopilotDriven` property, and nothing lists them.
5. **The Skim Race decision core is one 1,612-line class** with 62 state fields (geometry, three planners,
   obstacle grid, recovery), and its track planner is the largest AI cost (`SKIM_RACE_AI.md` §8.0h:
   `TrackMpc` 1.45 ms avg of `Decide`'s 2.35 ms, two seats, editor Release).
6. **The input-only gate covers four folders.** `check_ai_no_state_writes.py` scans only
   `Controller/AI/SkimRace`, `Utility/AITraining`, `Editor/AI` and `Controller/Arcade/Preview/Drill` (81 files on
   2026-10-17). `AIPilot`, the boost policies (`Controller/AI/Boost`), the Urchin
   and Butterfly drivers, the Scarab juke planner and the layer-4 executors are not gated, so "every AI is
   input-only" is a rule there, not a checked fact. Widening the gate is intake step 1 for each of them
   (`DIAGNOSIS_PLAYBOOK.md` §3); expect findings to review, not to auto-fix.
7. **Dead code**: `AIPilot`'s "Unused Methods" region (`ShootLaser`, `CalculateRollAdjustment`,
   `SigmoidResponse`, `SetTargetCoroutine` — 0 callers); `AIGunner` has no behaviour (its only method is
   commented out; three serialized fields remain) and is still on `Assets/_Prefabs/Characters/AI/Gunner.prefab`.

## 4. Target architecture (proposals — each lands alone, measured, in this order)

Nothing here is done yet. Each step follows the `/refactor` method: measure, one concern per commit,
grade every hunk (provably no-op / no-op at the authored numbers / real change), prove it, record it.

| # | Step | Kind | Changes behaviour? | Proof |
|---|---|---|---|---|
| 4.1 | **Markers on every AI**: `AI.Autopilot.*`, `AI.Urchin.*`, `AI.Butterfly.*`, `AI.ScarabJuke.*`, `AI.GrizzlyBombs.*`, `AI.MantaTurnBoost.*` (Skim Race keeps `SkimRace.*`) | Diagnostics | No (provably) | Gates + compile; one `prof` capture per mode in the playbook |
| 4.2 | **One AI registry** (`AIPilotRegistry`): each replacement pilot declares the mode × hull it claims and how to install; seating asks once; the training installer asks the same registry; the hard-coded Squirrel check goes | Unification | No at today's data | Grep every install site to one; `ServerPlayerVesselInitializerWithAITests` |
| 4.3 | **One home for hull/mode AI**: `Controller/AI/<Hull>/` holds each pure decision class (Urchin is the model: pure driver + assessment + tests); the mode controller keeps only the adapter. Move `ScarabScrambleJukePlanner` and `ButterflyAutopilotModeDriver` there (same namespace, `.meta` guids kept) — **done 2026-10-17**: the Butterfly driver `git mv`'d to `Controller/AI/Butterfly/` (guid kept; the Dustup / Sirocco / Tapestry generators and `ai_branch_sync.py` point at the new path, and the generators mint the new folder's `.meta`); the planner extracted verbatim from `ScarabScrambleController.cs` to `Controller/AI/Scarab/ScarabScrambleJukePlanner.cs` | Reorganisation | No | `git mv` keeps guids; refcompile; tests unchanged |
| 4.4 | **One "is the autopilot driving" question** on the vessel status, replacing the two private `IsAutopilotDriven` copies (Grizzly, Manta; measured 2026-10-17); the roster lists every layer-4 site | Consistency | No (each copy measured equal first) | Enumerated-consumer grep to zero |
| 4.5 | **Split the Skim Race core** (geometry, obstacle field, one shared rollout, recovery) then **Burst the rollouts** on the main thread | Restructure, then perf | Split: no (simulator byte-identical). Burst: floats only | Simulator identity; then 20-seed benchmark no worse per intensity × frame time; `prof` shows Burst |
| 4.6 | **Dead code and the dormant training stack**: delete the `AIPilot` unused region; propose `AIGunner` + `Gunner.prefab` and the training stack for the user's verdict (keep / editor-only / retire) | Vestige | No | 0 callers; guid sweep; user decision |
| 4.7 | **An offline harness per AI that has decision math** (Urchin rail choice, Scarab juke plan, Grizzly bomb timing): pure-C# tests first, a simulator only where a race-level question needs one | Diagnostics | No | Tests run headless (`DIAGNOSIS_PLAYBOOK.md` §2) |

**What does not change**: the input-only rule, the mode steering hooks (layer 2 — the pattern works and
22 modes use it), the Skim Race policies and difficulty ladder, and every serialized field name (renames
of serialized data need their own migration).
