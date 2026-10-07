# FTUE, Dialogue & AI Opponents

> Moved verbatim from the root `CLAUDE.md`, which indexes every topic file. Paths in this file are relative to the repository root.

### FTUE (First-Time User Experience) — the QUEST GRAPH

**The adapter/step-handler tutorial system this section used to describe is DELETED** (25 files:
`IFlowController`, `ITutorialExecutor`, `TutorialFlowController`, `TutorialStep`,
`TutorialSequenceSet`, `FTUEIntroAnimator`, the four step handlers, `TutorialUIView`,
`InGameTutorialFlowView` and the rest), along with the older `Quest` / `QuestSystem` /
`UserJourneySystem` / `SO_QuestChain` prototype beside it. Do not reintroduce any of them, and do
not take a stale doc naming one as evidence it exists.

What replaced it is a **QUEST GRAPH** at `Assets/FTUE/` — a visually authored node graph rather
than a hand-wired sequence, so a designer changes the onboarding by editing an asset instead of
adding a handler class:

- **Data**: `QuestSO` (an ordered sequence of phases) → `QuestPhaseGraphSO` (one graph per phase)
  → `QuestNodeSO` subclasses joined by `QuestEdge`. The shipped chain is
  `DataContainer/Quests/MainQuest.asset` over `DataContainer/Phases/MainQuest_Phase0..5.asset`.
- **Nodes** (`Scripts/Graph/Nodes/`): dialogue, show-instruction, navigate, enter/exit freestyle,
  set-arcade-constraints, lock-modes, lock-navigation, unlock-mode, and the wait family
  (input, skim, drift, intensity, mode-unlocked, game-launch, game-played, user-action).
- **Runtime** (`Scripts/Graph/Runtime/`): `QuestGraphRunner` (the one driver — `TryStart` is the
  single choke point both start paths funnel through), `QuestRuntimeContext`,
  `QuestProgressStore` (local PlayerPrefs mirror + optional cloud), `QuestArcadeConstraints`
  (the arcade funnel, persisted so it survives a Menu → game → Menu round trip),
  `QuestPlayRecorder`.
- **UI** (`Scripts/UI/`): `QuestDialoguePanelView`, `QuestInstructionView`,
  `QuestRewardRevealView`, `QuestToastNotifier`. **All four hide themselves in `Awake`**, which
  is what makes standing the runner down safe.
- **Editor** (`Assets/FTUE/Editor/`): `QuestGraphEditorWindow` (the graph authoring tool),
  `QuestDefaultContentBuilder`, `QuestPhase0UIWirer`, `QuestRunnerSetup`, `QuestGraphLayout`.
- **Events**: `FTUEEventManager` survives, narrowed to three — `InitializeFTUE` (an external
  start trigger) plus `OnQuestPhaseCompleted` / `OnQuestCompleted`, consumed only by
  `QuestToastNotifier`. Nothing blocks on them.

**Progression is a separate system from the graph** and is the thing the graph unlocks:
`SO_UnlockList` + `SO_UnlockData` (formerly `SO_GameModeQuestList` / `SO_GameModeQuestData`) drive
`GameModeProgressionService`, tuned by `SO_ProgressionConfig`. There is no XP — quest completion
is the only currency.

**THE MASTER DEVELOPER UNLOCK IS ON BY DEFAULT — read this before debugging any lock.**
`DeveloperUnlockGate.AllUnlocked` (`_Scripts/System/Progression/`) opens every entitlement at
once — all vessels, all game modes, every intensity tier, the Vessel Hangar — **and stands the
quest graph down entirely**, because everything the graph applies is a lock that gate exists to
open. It defaults ON until the FTUE is designed, so *the onboarding does not run in a default
checkout*, and a lock that appears not to work is this switch before it is a bug. Flip it in the
Froglet Toolbox (Quest Debug or Vessel Unlock tab); it announces itself once per session as a
warning for exactly that reason. It gates six choke points rather than teaching ~20 call sites
about itself, and each intercepted read keeps a RAW twin for the code that MANAGES entitlement
(`SO_Vessel.IsLockedByEntitlement` beside `SO_Vessel.IsLocked`) — because gating a read also
reaches the write path's guards, and without the twin every vessel unlock would be a silent
no-op. When the FTUE ships, `DefaultAllUnlocked` flips to false and it becomes an opt-in cheat.
It is NOT `ProgressionBackendGate`, which decides whether progression is PERSISTED rather than
whether it is ENFORCED.

### Dialogue System

Custom dialogue system spanning two locations:

- **Editor & assets**: `Assets/_Scripts/DialogueSystem/` — animation controllers, shader graphs (SpriteAnimation, UI_NoiseDissolve), SO dialogue data assets, prefab
- **Runtime code**: `Assets/_Scripts/System/Runtime/` — `DialogueManager`, `DialogueEventChannel`, `DialogueUIAnimator`, `DialogueViewResolver`, `DialogueAudioBatchLinker`
- **Models**: `Assets/_Scripts/System/Runtime/Models/` — `DialogueLine`, `DialogueSet`, `DialogueSetLibrary`, `DialogueSpeaker`, `DialogueVisuals`, `DialogueModeType`, `IDialogueService`, `IDialogueView`, `IDialogueViewResolver`
- **Views**: `InGameRadioDialogueView`, `MainMenuDialogueView`, `RewardDialogueView`
- **Editor tools**: `DialogueEditorWindow`, `DialogueLineDrawer` (in `_Scripts/Editor/`)

### AI Opponent System

Runtime-configurable AI opponents at `Assets/_Scripts/Controller/AI/`:
- `AIPilot` controls AI vessel behavior
- `AIGunner` controls AI targeting/shooting
- AI profiles configured via `SO_AIProfileList` (`MainAIProfileList.asset`)
- AI profiles used for score cards and multiplayer backfill
- Configurable AI ship selection and behavior at runtime

**Where the AI work lives now (2026-10-07) - read these before touching an AI:**

| AI | Code | Read first |
|---|---|---|
| The platform autopilot every hull flies by default | `AIPilot`, `AIGunner`, `PursuitReachability`, `AIObjectiveScoring` | `AI_ORBIT_BREAK.md` (below), `AI_BOOST.md` (per-hull boost policies: `AIPilot.boostPolicy`, one `AIBoostPolicySO` per boost mechanic, each listing the modes where it stands down) |
| **The Skim Race pilot** (the Squirrel in Skim Race, and Regatta's rings) | `Controller/AI/SkimRace/`: `SkimRacePilot` (lifecycle, sensing, actuation), `SkimRaceDriver` (the decision core, pure C#), `SkimRaceObjective` (`CrystalTrackObjective` / `RegattaRingObjective` - the only mode-aware part), `SkimRaceTeamPlan` (AI teammates split the crystals), `SkimRaceHandicap` (Easy / Medium mistakes), `SkimRaceReplanGate` (one planner re-plan per frame) | `Docs/SKIM_RACE_AI.md` - §3 architecture, §10 the host's difficulty row, §11 map fingerprints and `skimrace_retune.py`, §12 per-frame cost and Profiler markers, §13 team play, §14 frame rate (policies are tuned across 62/36/20 fps with the game's 0.04 s contact step) and the editor-mode cost; §8.0f-i the editor measurements |
| The Urchin autopilot (Hijack) | `Controller/AI/Urchin/`: `UrchinAutopilotDriver`, `UrchinRailAssessment`, `UrchinAutopilotConfigSO` | the class headers (no separate doc yet) |

Rules every AI shares: **input-only** - an AI reads what a pilot can see and writes only the vessel's
input channels (`Tools/Build/check_ai_no_state_writes.py` gates it); no 3-argument `Mathf.Min/Max` in
per-frame code (`MathfNoAlloc`; `check_mathf_params_alloc.py`); a per-frame cost gets a `ProfilerMarker`
(`SkimRace.Pilot.*`, `SkimRace.Driver.*`). The Skim Race pilot has an **offline simulator**
(`Tools/Build/skimrace_sim_harness/run.sh`: it compiles the shipped driver; `SKIMRACE_RUNTIME=mono` predicts
the editor's cost), an in-editor benchmark (`FrogletTools > AI > Skim Race AI Benchmark`), and a recorder
that saves every hand-played editor race with its frame time (`BenchmarkResults/SkimRaceAI/manual_*.jsonl`).
The host picks the AI difficulty on the Skim Race card (`AIDifficulty`, `AIDifficultyRules`, replicated in
`LobbySnapshot.AIDifficulty`); AI seats are placed per `ServerPlayerVesselInitializerWithAI.GetBalancedDomain`
(fewest pilots first) and the host moves them with **Add AI** (`Docs/SKIM_RACE_AI.md` §13 lists every 4-seat shape).

**A pursuing AI ORBITS anything inside its own minimum turn radius, and that is geometry rather
than tuning** (`AI_ORBIT_BREAK.md`, `PursuitReachability`). A vessel at speed `v` with max turn
rate `ω` cannot fly tighter than `R = v/ω`, so pure pursuit can never reach a target inside either
circle of radius `R` tangent to its velocity — it turns as hard as it can, forever. **Turning
harder is exactly the wrong response, which is why the failure survived every tuning pass.** It is
the Dubins (1957) reachability condition and the fix is the manoeuvre pilots use — *extend and
re-attack*: fly out, come around, come back in. Five things to carry: (1) the test collapses to
**`|d| < 2R·sin θ`** (the circle test with the `R²` cancelled — proven exact against the long-hand
definition over 20k cases), and its **exit condition falls out of the same line**, since `sin θ ≤ 1`
means `2R` of separation is a GUARANTEE of reachability rather than a tuned threshold; (1b) **an
objective is not a POINT — it has a capture radius `c`, and leaving it out is a defect rather than
a simplification**: at `c = 0` the test asks whether the vessel can fly onto an infinitely small
target, which 20u out is false for any bearing error over 14°, so the AI peels away from crystals
it was about to collect. The generalisation is exact (`|d|² + 2Rc − c² < 2R·|d⊥|`, guaranteed
separation `2R − c`) and its OTHER root, `|d| ≤ c`, IS the don't-peel-away-on-final-approach case —
not a special case bolted on but the second half of the same solution. Err GENEROUS on `c`: too
small peels off with nothing to catch it, too large just means the orbit detector catches it a
beat later; (2) **`R` is
a property of the vessel AT THIS SPEED, never an authored constant** — a boosted Dolphin's
unreachable bubble is 372u against 83u at cruise, off the same authored 110°/s, so it is derived
live in `VesselTransformer.MinTurnRadius`; (3) reason from **`Course`, not the nose** — the radius
applies to the direction of TRAVEL, and the two differ during a drift (which is also why the
break-off is suppressed there: a locked course reads as an infinite radius, i.e. an unbreakable
orbit); (4) a geometric test cannot catch an orbit it does not describe, so `OrbitDetector` is the
empirical backstop — swept angle with no progress — and its progress gate must compare against the
range at the START of the window, because **a running minimum tracks a steady approach downward and
can then never register progress at all**, silently degrading the detector to "constant range only";
and (5) **how far the break-off flies out is a TIME, not a distance** — `2R/v = 2/ω` is constant for
a given turn rate, so `speed × approachRunSeconds` buys the same straight run at 60 u/s and at 357,
and it is simultaneously how long the pilot spends leaving and how long the return leg lasts. The
fleet runs 1.5s; **the Dolphin is authored at 2.5s** because it is the one vessel that AIMS on the
way in (it locks course on the crystal then swings its nose onto a rival — 180° at 110°/s is 1.64s,
so the run has to cover it). Measured: 373/400 randomized objectives reached → **400/400**, for
+0.18s of mean time.
  **A reported "the AI dodges the crystal at the last second" was NOT the break-off** — two
  plausible fixes were measured and rejected first (a commit range trades the dodge for orbiting,
  because the turning-circle test is structurally a sub-1s test; a look-ahead factor moves the
  median but not the earliest and costs 9× the break-offs). The cause was
  `AIPilot.UpdateCellContent` comparing a squared distance against `MinDistance * MinDistance`
  while `MinDistance` already held a squared distance — a `d⁴` threshold that **every** later
  candidate passed, so the pilot took the LAST eligible crystal rather than the nearest, and
  re-picked arbitrarily on every `OnCellItemsUpdated` (which every respawn raises). General rule:
  **a comparison that mixes a squared quantity with a linear one is invisible to review and to
  every static check, and its symptom is a behaviour nobody attributes to arithmetic.** Selection
  now lives in `AIObjectiveScoring.Select` — pure, list-based, so the shipped path IS the tested
  one — with commitment hysteresis (`objectiveSwitchImprovement` 0.75) so a crystal event can no
  longer re-point a pilot that is a second from arriving. Two more from the same report, both
  about the Dolphin never aiming at the player: **a provider that NAMES an aim point must not
  inherit the fallback's "would this drift actually turn the vessel" test** (the mass cluster's
  only job is to find somewhere interesting to point, so it defers when the objective is already
  ahead; an explicitly named rival being ahead is the BEST case, and rejecting it turned the nose
  away from exactly the pilot it was lining up on), and **an AI's engagement range must track its
  weapon's** (Bends capped `aiAimMaxRange` at 900 against `AOEConicExplosion.prefab`'s authored
  `height: 2400`).
