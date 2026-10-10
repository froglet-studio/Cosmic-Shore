# AI System — Architecture (every vessel AI, as it is and where it is going)

Measured on `ai-system` (retired 2026-10-09; the work now lives on `Ys-bleeding-edge`) at the 2026-10-08 cut (perf `1f3c7e829` + Ys-bleeding-edge `be50ff6ba`). The
**roster in §2 is the source of truth for "which AIs exist"**: when a new one arrives from bleeding-edge,
it gets a row here first (`BRANCH_WORKFLOW.md` §4). Every count below was measured with `git grep`.
When a row and the code disagree, the code wins; fix the row.

Rules every AI on this branch obeys (unchanged, enforced by gates):
- **Input-only.** An AI reads what a pilot could see and writes only the vessel's input channels. It never
  writes game state a human could not. Gated by `Tools/Build/check_ai_no_state_writes.py --check` — today
  only for the Skim Race and training folders (§3 item 6).
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
| **1. Platform autopilot** | The default pilot on every hull: target choice, steering, avoidance, orbit break, aim telegraph, drift commit, boost policy | `Controller/AI/AIPilot.cs` (1,216 lines), `PursuitReachability`, `AIBoostPolicySO` + 5 policy types (`Charge`, `Hold`, `Pellet`, `SkimRing`, `SnapDash`) with assets for Dolphin, Scarab, Serpent, Sparrow, Squirrel (`Assets/_SO_Assets/AI Boost Policies/`) | 13 hull prefabs: Butterfly, Dolphin, Falcon, Grizzly, Manta, Rhino, Scarab, Serpent, Shrike, Sparrow, Squirrel, Termite, Urchin |
| **2. Mode steering hooks** | A mode tells the autopilot where to fly / where to look, without its own pilot | `AIPilot.SetExternalTargetProvider`, `SetDriftLookTargetProvider` | 22 mode controllers (Astro League, Bends, Broadside, Cleave, Dog Fight, Dustup, Gate races, Hijack, Maelstrom, Rampage, Regatta, Scarab Scramble, Sirocco, Skein, Tapestry, Tollway, Undertow, Waystation, Wildlife Liberation, Wrecking Ball, …) |
| **3. Hull/mode AI helpers** | Pure decision code for one hull's kit in a mode, driven from the mode controller | `UrchinAutopilotDriver` + `UrchinRailAssessment` (`Controller/AI/Urchin/`; Hijack, Skein, Regatta), `ButterflyAutopilotModeDriver` (`Controller/Arcade/ButterflyGames/`; Dustup, Sirocco, Tapestry), `ScarabScrambleJukePlanner` (a static class **inside** `ScarabScrambleController.cs`) | Urchin, Butterfly, Scarab |
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
| Platform autopilot | 1 | `AIPilot`, `PursuitReachability` | `Controller/AI/AI_ORBIT_BREAK.md`, `Docs/claude/FTUE_DIALOGUE_AI.md` | `AIPilotTests`, `PursuitReachabilityTests`, `AimTelegraphBindingTests` | **none** | none | Live on 13 hulls; cost never measured |
| Boost policies | 1 | `Controller/AI/Boost/` | `Controller/AI/AI_BOOST.md` | (via AIPilot tests) | **none** | none | 5 policies, 5 hulls |
| Skim Race pilot | 5 | `Controller/AI/SkimRace/` | `Docs/SKIM_RACE_AI.md` (+ `SKIM_RACE_AI_SESSION_LOG.md`) | `SkimRaceAITests`, `SkimRaceCourseQueryTests`, `SkimRaceShellTests`, `SkimRaceTeamAssignmentTests`, `SkimRaceHandicapTests`, `SkimRaceTrackFingerprintTests`, `AIDifficultyRulesTests` | `SkimRace.Pilot.*`, `SkimRace.Driver.*` | **yes** — `Tools/Build/skimrace_sim_harness/` (compiles the shipped driver) + editor benchmark + race recorder | Tuned, QA PASS 2026-10-08 (83 s / 86 s) |
| Urchin rail rider | 3 | `Controller/AI/Urchin/` (adapters in `HijackController`, `SkeinController`, `RegattaController`) | `Controller/Vessel/R_VesselActions/URCHIN_TRAIL_RIDER.md`, `Controller/Arcade/HIJACK.md`, `SKEIN.md`, `REGATTA.md` | `UrchinRailAssessmentTests`, `UrchinChainReactionTests` | **none** | none | **Intake 2026-10-08** (Regatta rail choice is new) |
| Butterfly mode switch | 3 | `ButterflyAutopilotModeDriver` | `Controller/Arcade/DUSTUP.md` | none | **none** | none | **Intake** |
| Scarab jukes | 3 | `ScarabScrambleJukePlanner` in `ScarabScrambleController.cs`, reads `ScarabJukeController` | `Controller/Arcade/SCARABSCRAMBLE.md` | `ScarabScrambleJukePlannerTests` | **none** | none | **Intake 2026-10-08** (new) |
| Grizzly AI bombs | 4 | `GrizzlyTriggerBombExecutor` + `GrizzlyTriggerBombConfigSO` (the bomb pump it replaced is retired) | `R_VesselActions/GRIZZLY_TRIGGER_BOMBS.md`, `Controller/Arcade/GRIZZLYTIME.md` | `GrizzlyTriggerBombTests`, `GrizzlyTimeCourseTests` | **none** | none | **Intake 2026-10-08** (new) |
| Stoat autopilot sling / dipole | 4 | `StoatSlingExecutor.AutopilotSling`, `StoatDipoleExecutor` (both triggers when the target is within 12° of the nose, up to 75°), `AutopilotHold01` on `StoatSlingConfigSO` / `StoatDipoleConfigSO` | `R_VesselActions/STOAT.md`, `STOAT_DIPOLE.md`, `Arcade/SLINGSHOT.md`, `WARPLINE.md` | `StoatSlingTests`, `StoatDipoleTests` | **none** | the Stoat Flight Studio (web, design numbers) | **Intake 2026-10-09** (arrived with Slingshot / Warpline; studio built, game AI not editor-verified). 2026-10-10: dipole autopilot capped at one lap round its sink (`autopilotMaxOrbitDegrees`) |
| Manta turn boost | 4 | `MantaAnalogTurnBoostExecutor` | `R_VesselActions/MANTA_STING_KABLOOM.md` | none | **none** | none | Intake |
| Genetic training pilot | 5 | `Utility/AITraining/` (7,596 lines) | `Utility/AITraining/README.md`, `Docs/AI_TRAINING_CONSOLIDATION.md` | `AITrainingCoreTests`, `InputOnlyContractTests` | none | its own runner | **Dormant**: its runtime installer resolves its settings only in the editor, and its only archived genome (Squirrel / Skim Race / I4) is claimed by the Skim Race pilot. Fate is the user's call (§4.6). |

## 3. What the measurement says is wrong today

1. **Only one AI can be profiled.** Every marker in the AI code is Skim Race's. The autopilot runs on
   every AI seat in ~30 modes and its cost has never been measured; nor has any layer-3 or layer-4 AI.
2. **Three ways to give a seat its pilot**, and the two replacement pilots avoid each other through a
   hard-coded "Squirrel and `SkimRaceAIDeployment.Claims`" check inside `TrainingDeploymentService`.
3. **Layer-3 code lives in three different places**: the Urchin driver under `Controller/AI/`, the
   Butterfly driver under `Controller/Arcade/ButterflyGames/`, the Scarab planner inside its controller.
4. **Layer-4 AI is invisible from the AI folder**: three executors decide for the autopilot, each with its
   own `IsAutopilotDriven` property, and nothing lists them.
5. **The Skim Race decision core is one 1,612-line class** with 62 state fields (geometry, three planners,
   obstacle grid, recovery), and its track planner is the largest AI cost (`SKIM_RACE_AI.md` §8.0h:
   `TrackMpc` 1.45 ms avg of `Decide`'s 2.35 ms, two seats, editor Release).
6. **The input-only gate covers two folders.** `check_ai_no_state_writes.py` scans only
   `Controller/AI/SkimRace` and `Utility/AITraining` (64 files). `AIPilot`, the boost policies, the Urchin
   and Butterfly drivers, the Scarab juke planner and the layer-4 executors are not gated, so "every AI is
   input-only" is a rule there, not a checked fact. Widening the gate is intake step 1 for each of them
   (`DIAGNOSIS_PLAYBOOK.md` §3); expect findings to review, not to auto-fix.
7. **Dead code**: `AIPilot`'s "Unused Methods" region (`ShootLaser`, `CalculateRollAdjustment`,
   `SigmoidResponse`, `SetTargetCoroutine` — 0 callers); `AIGunner` is an empty component (every member
   commented out) still on `Assets/_Prefabs/Characters/AI/Gunner.prefab`.

## 4. Target architecture (proposals — each lands alone, measured, in this order)

Nothing here is done yet. Each step follows the `/refactor` method: measure, one concern per commit,
grade every hunk (provably no-op / no-op at the authored numbers / real change), prove it, record it.

| # | Step | Kind | Changes behaviour? | Proof |
|---|---|---|---|---|
| 4.1 | **Markers on every AI**: `AI.Autopilot.*`, `AI.Urchin.*`, `AI.Butterfly.*`, `AI.ScarabJuke.*`, `AI.GrizzlyBombs.*`, `AI.MantaTurnBoost.*` (Skim Race keeps `SkimRace.*`) | Diagnostics | No (provably) | Gates + compile; one `prof` capture per mode in the playbook |
| 4.2 | **One AI registry** (`AIPilotRegistry`): each replacement pilot declares the mode × hull it claims and how to install; seating asks once; the training installer asks the same registry; the hard-coded Squirrel check goes | Unification | No at today's data | Grep every install site to one; `ServerPlayerVesselInitializerWithAITests` |
| 4.3 | **One home for hull/mode AI**: `Controller/AI/<Hull>/` holds each pure decision class (Urchin is the model: pure driver + assessment + tests); the mode controller keeps only the adapter. Move `ScarabScrambleJukePlanner` and `ButterflyAutopilotModeDriver` there (same namespace, `.meta` guids kept) | Reorganisation | No | `git mv` keeps guids; refcompile; tests unchanged |
| 4.4 | **One "is the autopilot driving" question** on the vessel status, replacing the three private `IsAutopilotDriven` copies; the roster lists every layer-4 site | Consistency | No (each copy measured equal first) | Enumerated-consumer grep to zero |
| 4.5 | **Split the Skim Race core** (geometry, obstacle field, one shared rollout, recovery) then **Burst the rollouts** on the main thread | Restructure, then perf | Split: no (simulator byte-identical). Burst: floats only | Simulator identity; then 20-seed benchmark no worse per intensity × frame time; `prof` shows Burst |
| 4.6 | **Dead code and the dormant training stack**: delete the `AIPilot` unused region; propose `AIGunner` + `Gunner.prefab` and the training stack for the user's verdict (keep / editor-only / retire) | Vestige | No | 0 callers; guid sweep; user decision |
| 4.7 | **An offline harness per AI that has decision math** (Urchin rail choice, Scarab juke plan, Grizzly bomb timing): pure-C# tests first, a simulator only where a race-level question needs one | Diagnostics | No | Tests run headless (`DIAGNOSIS_PLAYBOOK.md` §2) |

**What does not change**: the input-only rule, the mode steering hooks (layer 2 — the pattern works and
22 modes use it), the Skim Race policies and difficulty ladder, and every serialized field name (renames
of serialized data need their own migration).
