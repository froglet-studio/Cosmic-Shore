---
name: vessel-ai
description: The rules and the test kit for ANY vessel AI in Cosmic Shore - making a new AI for a vessel, changing how an existing one flies, adding or tuning difficulty levels (Easy / Medium / Hard), setting or checking finish / place times, wiring an AI into a mode (backfill seats, GateRaceController steering, a replacement pilot), and testing an AI by WATCHING it from a third eye - the Unity editor's Third Eye window (FrogletTools > AI > Third Eye: Chase / Follow / Free cameras on any pilot while you play, with each AI's aim drawn over the view) and the Vessel Studio artifact (the same cameras and AI-thinking colours in a browser). Loads the non-negotiables (input-only, difficulty edits belief never the stick, the AIPilot getter trap), the common requirements every new AI owes, the testing ladder from unit test to user playtest, and the shared colour vocabulary. Trigger on Assets/_Scripts/Controller/AI/**, AIPilot, SkimRacePilot / SkimRaceDriver, an IsAutopilotDriven / AutoPilotEnabled branch in an executor, AIDifficulty / AIDifficultyRules, a *DifficultySO or *Handicap, ServerPlayerVesselInitializerWithAI, a mode's SetExternalTargetProvider, Assets/_Scripts/Editor/AI/** (ThirdEye*), Docs/AI_SYSTEM/**, or "the AI flies wrong / is too easy / too hard / test the AI / watch the AI".
---

# Vessel AI: the rules every AI follows, and how to test one

Every vessel can be flown by an AI, and every AI here is built and judged the same way. This skill is
the checklist and the test kit. The deep references stay where they are, and this file points at them
instead of copying them:

| Read | For |
|---|---|
| `Docs/AI_SYSTEM/README.md` | Start here: the branch AI work lives on (`Ys-bleeding-edge`), the first command of every AI session (`python3 Tools/Build/ai_branch_sync.py`) |
| `Docs/AI_SYSTEM/ARCHITECTURE.md` | The six layers (§1) and **the roster of every AI** (§2, the source of truth). A new AI gets a row there first |
| `Docs/AI_SYSTEM/DIAGNOSIS_PLAYBOOK.md` | The five tiers of evidence, the intake checklist, the user's test-script template, the status board |
| `Docs/SKIM_RACE_AI.md` | The most developed AI (the Squirrel's). Its benchmark (§1), difficulty (§10) and simulator are the model for the rest |
| `Docs/Studios/VESSEL_STUDIO_ROLLOUT.md` | Which vessels have AI, and the order their studios are built in |
| `/vessel-studio` | The browser studio: AI race panel, levels (§1.5.7), cameras (§1.5.9) |
| `/vessel`, `/arcadegame`, `/arenagame` | The hull's kit, the mode recipe, and the fleet facts (which hulls an autopilot can drive) |

## 1. The non-negotiables

1. **Input-only.** An AI reads what a pilot could see and writes only what a pilot could press: the
   stick, the throttle and the vessel's buttons through `R_VesselActionHandler`. It never moves the
   hull, never edits a crystal, never touches a score. Gate: `python3 Tools/Build/check_ai_no_state_writes.py
   --check`. Its scope covers only the Skim Race and training folders, so **add the new AI's files to
   its `SCOPES`** and review every finding (`DIAGNOSIS_PLAYBOOK.md` §3).
2. **Difficulty changes what the AI BELIEVES, never its stick or its hull.** Easy and Medium are Hard plus
   human-shaped mistakes in decisions (late notice, a misjudged target), so a mistake costs what it
   costs a human. Never jitter the stick, slow the throttle or cap the speed to make an AI easier (§3).
3. **No per-frame allocation**: no 3-argument `Mathf.Min/Max` (`check_mathf_params_alloc.py`), no LINQ or
   closures in a tick, `NonAlloc` physics queries. **No `MathF` in code a Burst job reaches.**
4. **Logs on a `CSLogChannel`**, never raw `Debug.Log`, nothing per frame (CLAUDE.md).
5. **Randomness is the AI's own**: `System.Random` seeded per bind, never `UnityEngine.Random`, whose global
   state the track generators seed.
6. **Every AI exposes a read-only diagnostics struct**: at least the point it is steering for, its state,
   and the reason for its last recovery (`SkimRaceDriver.Diagnostics` is the model). That is what lets
   the Third Eye and the studio draw it (§4.2). An AI that cannot say where it is going cannot be watched.
7. **Never read `VesselStatus.AIPilot` from a tool or a reader.** Its getter is `GetOrAdd<AIPilot>()`: reading
   it ADDS an autopilot to a human's ship. Use `status.TryGetComponent(out AIPilot pilot)`.

## 2. Where a new AI goes (the lowest layer that works)

The six layers are in `ARCHITECTURE.md` §1. Pick the lowest one that answers the need:

| Need | Layer | Example |
|---|---|---|
| The mode only needs to say WHERE to fly | 2: `AIPilot.SetExternalTargetProvider` / `SetDriftLookTargetProvider` | 22 modes; every `GateRaceController` race (commit / lead / through distances per mode) |
| The hull's speed or main mechanic is an ABILITY the autopilot never presses | 4: an autopilot drive inside the executor, through the REPLICATED press / release | Manta Soar (`MantaAnalogTurnBoostExecutor`), Grizzly bomb-jump (`GrizzlyTriggerBombExecutor`), Stoat sling (`StoatSlingExecutor.AutopilotSling`) |
| A pure decision about the hull's kit in a mode | 3: a pure class in `Controller/AI/<Hull>/` plus an adapter in the mode controller | `UrchinAutopilotDriver` + `UrchinRailAssessment` (the model: pure, tested) |
| The whole race needs its own planner | 5: a replacement pilot that stands `AIPilot` down for one mode × hull | `SkimRacePilot` (installed by `SkimRaceAIDeployment`) |

**A hull whose speed is an ability races at cruise without a layer-4 drive** (the autopilot writes stick and
throttle only). A card that lists such a hull has said an all-AI domain cannot win there. Check
`/arenagame` §1's fleet table before seating an AI hull anywhere.

## 3. What every new AI owes (the common requirements)

Copy this list into the AI's doc and tick it.

- [ ] **Roster row** in `ARCHITECTURE.md` §2 and a **status-board row** in `DIAGNOSIS_PLAYBOOK.md` §5.
- [ ] **Input-only gate** widened to its files, findings reviewed (§1.1).
- [ ] **Profiler markers** `AI.<Name>.*` around its per-frame decision and any search inside it, in a
      commit of their own (provably no behaviour change).
- [ ] **A diagnostics struct** (§1.6), and a branch in `ThirdEyePilots.Read` that draws it (§4.2).
- [ ] **Tests for the decision a playtest would argue about** (which rail, when to fire, when to dash).
      A simulator only when a race-level question (finish times, the difficulty ladder) cannot be answered
      by a test. The Skim Race one took days, and earned them.
- [ ] **It can win.** An all-AI domain must be able to finish and win the mode it is seated in, at every
      intensity. If it cannot, the card says so.
- [ ] **Difficulty**, decided explicitly (below).
- [ ] **Finish / place times**, measured and written down (below).
- [ ] **A user test script** (tier 4 + 5) in `Docs/UNITY_VERIFICATION_CHECKLIST.md`, using the Third Eye
      (`DIAGNOSIS_PLAYBOOK.md` §4 is the template).
- [ ] **A studio**, when the vessel has one or is next in `VESSEL_STUDIO_ROLLOUT.md` (`/vessel-studio`).

### 3.1 Difficulty levels

- **Today only Skim Race offers a difficulty** (`AIDifficultyRules.IsOfferedFor(GameModes.SkimRace)`). In
  every other mode the AI's skill follows the **intensity** (Grizzly Time: skill = intensity × 0.25, throttle
  0.7 → 1.0). Say which one your AI uses.
- **To add a difficulty to a mode**, follow Skim Race's pattern (`SKIM_RACE_AI.md` §10) and do not invent a
  second one:
  - **Hard is the shipped pilot, unchanged**, and must be the fastest on every course (prove it, or the
    levels decide nothing).
  - **Medium and Easy are Hard plus the same kinds of mistake, more of them**: late notice of a new target
    (Skim Race: 0.25 s / 0.5 s mean, × 0.5–1.5 per target) and a misjudged target flown to where it was
    believed (4.5% / 9.9%). A vessel-specific mistake (the Stoat's pair judgement) is allowed; say so.
  - **One setting per level for every intensity** (a `*DifficultySO` in `Resources/`, authored by a
    generator), so a new track inherits it untouched.
  - **Tune by measurement against a target time**, bisecting the mistake chance (`run.sh handicap` in the
    Skim Race harness), on fresh seeds after the search.
  - Register the mode in `AIDifficultyRules.IsOfferedFor` + `AIDifficultyRulesTests`. The lobby row
    (`AIDifficultyPicker`) then appears on its card (`Docs/ArcadeLaunch/ARCHITECTURE.md` §3.3).
- **Level colours**, the same in the game tools and the studios: Hard red, Medium amber, Easy green
  (`ai_race_panel.js` `data-lv`).

### 3.2 Finish and place times

- **Define the benchmark first** (`SKIM_RACE_AI.md` §1 is the model): the scene, the seats (host idle on its own
  domain + one AI, so the domain's result is the AI's own work), the target count, the clock (the game's
  own race clock, never a stopwatch), the success rule, the time limit per intensity, and what is
  physically possible (route length ÷ top speed).
- **Report a distribution, never one race**: seat median, p10–p90, winner median, seats finished, per
  intensity × difficulty, with the seeds and the frame time. Small samples of a race are noisy (Skim Race:
  a 6-race sample read 101 s where 40 seeds read 90.5 s).
- **The Skim Race reference table** (`SKIM_RACE_AI.md` §10, 2 AI seats, 20 seeds per intensity): Hard I1
  68.2 s, I2 80.2 s, I3 188.2 s, I4 150.5 s; Medium is 14–19% slower than Hard everywhere; Easy 36–54%
  slower on the short tracks and 23–25% on the long ones. A new AI states its own table in the same shape.
- **Measure in the editor too**, through the normal launch path (`FrogletTools > AI > Skim Race AI
  Benchmark` is the model: N races, one JSON record each, a report script). Editor numbers need an
  uncapped, focused editor; a throttled background editor invalidates every race (`SKIM_RACE_AI.md` §8.0a).
- **Write every number with its tier and its conditions** (simulator / editor / player; seeds; fps).

## 4. Testing an AI: the ladder

Climb it in order; each rung catches what the one below cannot.

| Rung | What | Answers |
|---|---|---|
| 1 | Gates + compile (`unity_refcompile`, `/asset-surgery` §4) | Does it build, is it input-only, does it allocate |
| 2 | Edit-mode tests of the decision math | Is the decision right on the cases a playtest would argue about |
| 3 | Offline simulator / harness (only if needed) | Finish-time distributions, difficulty ladder |
| 4 | **Watch it**: the Vessel Studio (browser) and the **Third Eye** (Unity, §4.1) | Does it LOOK right: aim, line, recoveries, mistakes |
| 5 | Profile (`diag` / `prof` console, the AI's markers) | What it costs per frame, per seat |
| 6 | The user's playtest script (`UNITY_VERIFICATION_CHECKLIST.md`) | Does it feel right against a human |

### 4.1 The Third Eye window (Unity)

**FrogletTools > AI > Third Eye** (`Assets/_Scripts/Editor/AI/ThirdEyeWindow.cs`). Open it, dock it beside the
Game view, enter Play mode and play normally: the window shows the same match from a camera of its own.

- **Pilot**: the dropdown, `<` `>`, `[` `]` or Tab. You first, then other humans, then the AI.
- **Camera**: Chase (behind the hull, rolls with it), Follow (wider, world-up), Free: **Fly** (drag to look,
  I J K L or W A S D, U O or Q E down / up, Shift fast, wheel = speed) or **Orbit** (drag round the pilot,
  wheel = distance). C cycles. F frames the pilot. These are the studio's cameras (`/vessel-studio` D16).
- **AI thinking**: a line from each AI hull to the point it is steering for, and a cyan ring on the
  crystal it is really after. The bottom strip reads the watched AI's state, heading error, recoveries
  and its difficulty's mistakes.
- **Labels**: each pilot's name, hull and speed; off-screen pilots pinned to the edge you would turn to.
- **Paused** (the editor's pause button), it keeps rendering on demand: fly round a frozen frame.
- Settings (distances, smoothing, FOV, resolution) live in `UserSettings/ThirdEyeSettings.asset`, per machine.
- **Known limit**: the occlusion corridor follows the GAMEPLAY camera, so prisms between that camera and your
  hull may look dithered from the Third Eye. Lower Resolution if the second render costs too much.

**What to look for** (each one has been a real defect somewhere):
- The aim point **behind** the hull, or jumping between two points every frame: the planner is flip-flopping.
- A **violet loop** that keeps coming back: a recovery that does not recover.
- **Amber that never turns green**: a notice delay that never ends (Easy / Medium only).
- The aim on the crystal while the hull **leaves the racing line**: an AI chasing the crystal loses every
  skim (`/vessel-studio` §7).
- An AI hull that **never steers** at all: its `AIPilot` serialized disabled (the Grizzly's was).
- A hull at **cruise on every straight**: its speed is an ability with no layer-4 drive (§2).

### 4.2 Making a new AI visible in the Third Eye

`ThirdEyePilots.Read` (`Assets/_Scripts/Editor/AI/ThirdEyePilots.cs`) turns a pilot into a `ThirdEyeThinking`
(aim, state, colour, real target, detail). It knows the Skim Race pilot and the platform autopilot. For a new
AI, add a branch above the autopilot one that reads the AI's diagnostics struct (§1.6), using the colours
below. Keep it a reader: no `GetOrAdd`, no writes, nothing per frame beyond reading fields.

### 4.3 The colour vocabulary (Third Eye and every studio)

| Colour | Meaning |
|---|---|
| Green (`Lime`) | Flying for its target |
| Amber (`Gold`) | Has not noticed the new target yet (difficulty) |
| Red (`Ruby`) | Misjudged the target (difficulty) |
| Violet | Working its mechanic or recovering: Skim Race recovery, autopilot orbit break, the Stoat's pair |
| Blue (`Azure`) | Platform autopilot seeking its objective |
| Cyan ring | The target the AI is really after |

## 5. Traps

- **The `AIPilot` getter adds a component** (§1.7). A reader that called it put autopilots on human ships.
- **`AIPilot` serialized disabled**: steering runs in `Update`, so the hull never steered (Grizzly, `GRIZZLYTIME.md` §4).
- **Two replacement pilots avoid each other through a hard-coded check** (`TrainingDeploymentService` vs
  `SkimRaceAIDeployment.Claims`), until the registry in `ARCHITECTURE.md` §4.2 lands. A new replacement pilot
  must add itself there, or both install.
- **An off-screen camera comes up with URP's defaults** (no post, wrong volume): use `OffscreenCameraSetup`.
- **Never borrow the gameplay camera to watch** (`CameraManager.BeginWindowedPlayerCamera` lends the rig
  that IS the screen in a match). The Third Eye has its own.
- **Editor benchmarks in a background editor are invalid** (throttled to ~8 fps).

## 6. Keep it alive

When an AI lands or changes: its roster and status-board rows, its numbers in its own doc, a branch in
`ThirdEyePilots.Read` if it has a new diagnostics shape, and a costly trap added to §5 here. This is the
only AI skill: fold new AI guidance in here (studio-page rules stay in `/vessel-studio`).
