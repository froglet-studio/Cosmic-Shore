# AI Training Consolidation

Audit of the three unmerged AI-training branches against `bleeding-edge`
(`def29f5e1`, 2026-09-29), plus the Prompt 1 port status below.

Working branch: `feat/ai-genetic-training` (local, from `origin/bleeding-edge`
at `def29f5e1`). `AI-Genetic-algorithm` is the earlier local branch at the same
commit and was not deleted.

## Prompt 1 status

The `Assets/_Scripts/Utility/AITraining/` tree was copied from
`origin/claude/ai-training-tool-S3xw3` (`5d82eb0de`). The rest of that branch
was not merged. Compile fixes against current APIs:

- `GameModes.Freestyle` is retired. The launcher scene arm and the runner's
  fallback fitness case are gone. `MultiplayerFreestyle` (28) was retired
  2026-10, and `ApplyFreestyleDefaults()` with it.
- `Domains.None` and `Domains.Unassigned` are gone. Sensors treat `Domains.Blue`
  as the no-team sentinel.
- `TrainingPilot.BindVessel` still writes only `IInputStatus` and calls
  `PerformShipControllerActions` / `StopShipControllerActions`. It also disables
  `AIPilot` after `StopAIPilot` so the ability coroutines stop pressing buttons.
- `FrogletTools/AI Training` and `Quick Setup` carry `[FrogletTool]`. Quick
  Setup records created assets on `FrogletToolChangeLedger` and the window
  draws `FrogletToolShipPanel`.
- Tests stay in `AITraining/Tests/Editor/`. No gameplay asmdef was added.

## Prompt 2 status

HexRace intensity 4 is the one mode the loop is wired for. A completed
rollout persists the session asset, then calls
`MiniGameControllerBase.RequestReplay`. HexRace reloads the scene
(`UseSceneReloadForReplay`). The dying runner sets a handoff flag so
`OnDisable` does not `StopSession` and does not clear `IsTraining`. The
DontDestroyOnLoad launcher waits for the new roster, flips the host onto
autopilot, and starts a new runner, which checks out the next genomes.

`GameDataSO.IsTraining` stays true for the whole play session so
`TrainingDeploymentService` and a prefab `TrainingAIDeploymentBridge` do
not install a second pilot. A user halt calls `AbandonCheckout` and sets
`IsTraining` false. `TrainingPlayModeHook` also clears the serialized
flag on `EnteredEditMode`. Pressing Learn again resumes the same
`TrainingSessionStateSO` (generation and completed evaluations). A plain
Play does not auto-launch, because Stop clears `AutoStartOnPlay`.

HexRace golf `Score` is negated in `FitnessProfileSO.SignedRaw` when the
component is `ScoreFromRoundStats`. Crystal collection is not negated.
The host seat is paused so the keyboard does not poll, and `TrainingPilot`
still writes sticks while paused. `LateUpdate` re-disables `AIPilot` if
something turns it back on.

The overnight multi-mode loop is not claimed. `PrismSensor` still uses
`Physics.OverlapSphereNonAlloc` (checklist item 6). A dedicated GA flag
separate from `IsTraining` (item 3) is still deferred; this pass uses the
flag the deployment service already reads. Launcher and runner traces are
still `Debug.Log` (item 9). Play-mode proof of three live HexRace matches
was not run in this session: no editor was attached to `unity command`.
The edit-mode stand-in is `Loop_ThreeMatchesAtPopulationSize_IncrementsGeneration`.
The in-editor steps are `Assets/_Scripts/Utility/AITraining/README.md`
§ "Loop verification".

Prompt 1's first edit-mode run was 17 passed. After the loop tests,
`unity test --mode EditMode --filter AITrainingCoreTests` (2026-09-29
11:25:22Z) is **20 passed, 0 failed**, including
`Loop_ThreeMatchesAtPopulationSize_IncrementsGeneration`,
`Population_AbandonCheckout_RewindsWithoutRecording`,
`HexRace_ScoreFromRoundStats_IsNegated`, and
`EditorWindow_OpensWithoutMissingTypes`. Unity 6000.3.17f1, batch mode.
No editor was attached, so the three live HexRace matches in the README
were not executed here.

## Prompt 3 status

Actuation stays on the stick path. `TrainingPilot`, every
`IDecisionPolicy`, the sensors, `IntensityDitherer`, and both deployment
installers were scanned for Course, pose, teleport, and rigidbody writes.
The only transform uses are reads (`position` / `forward` /
`InverseTransformDirection`) that fill `DecisionContext`. Deployment calls
`LoadGenome` + `BeginEpisode` on `TrainingPilot` and does not write sticks
itself. The allowed surface is `README.md` § "Input-only contract".

`unity test --mode EditMode --filter AITrainingCoreTests|InputOnlyContractTests`
(2026-09-29 11:30:31Z) is **23 passed, 0 failed**, including
`InputOnly_PilotPoliciesAndSensors_DoNotCallBannedApis` and
`InputOnly_TrainingAndDeploy_ShareTheStickPath`. No editor was attached, so
a deployed `TrainingPilot` was not flown in a live match. The in-editor
check is the last paragraph of that README section.

## Prompt 4 status

Every start-list mode has a scene in Editor Build Settings and a
ScoringRule asset, so none of them was skipped. Each has
`Assets/_SO_Assets/AI Training/Scenarios/Scenario_*.asset` and
`Profiles/FitnessProfile_*.asset`: HexRace, Crystal Capture, Joust,
Rampage, Ribcage, Wildlife Liberation, Dog Fight, Bends, Scarab
Scramble, Salvo, Nucleus Rush, Astro League. Recipes go through
`FitnessProfileSO.ApplyFor` and the four new RoundStats readers
(`HostilePrismsDestroyed`, `LifeformsKilled`, `CombatPoints`,
`GoalsScored`). Golf `Score` is negated for every golf mode, including
Joust. Personal-goal modes are not negated and do not also weight
`Score`. HexRace stays Squirrel, intensity 4, three players; the other
scenarios are four players. Modes outside that list, the Nucleus Rush
representative stamp, and the missing fauna/cage seek target are in
`README.md` § BACKLOG.

`unity test --mode EditMode --filter AITrainingCoreTests|InputOnlyContractTests`
(2026-09-29 11:40:30Z) is **25 passed, 0 failed**, including
`Catalog_EveryLiveMode_HasScenarioAndProfileMatchingApplyFor`. Unity
6000.3.17f1, batch mode. No editor was attached, so none of these
scenarios was flown.

## Prompt 5 status

`FrogletTools → AI Training` is the operator surface. The tabs are
Configure, Inspect, Archive, and Deploy. Learn and Stop sit above every
tab. Configure edits the selected scenario (population, elites, vessel,
mode, intensity) and the control asset (target episodes, watchdog). −1
and 0 both mean overnight. A non-positive watchdog keeps the 180s default
so an older control asset cannot force-end every match. Learn enters Play
through `TrainingAutoLauncher`. Stop calls `StopSession` (finished episodes
stay, the in-flight checkout is abandoned) and leaves Play; the hook then
clears `AutoStartOnPlay` and `HumanPlaysThisLaunch`. Inspect reads
generation, hall-of-fame fitness, novelty-archive size, episode count, and
an ETA. Archive browses `(vessel × mode × intensity)` and import/export
goes through `GenomeJson`. Deploy pushes the best genome in edit mode and
toggles `DeployArchiveInNormalPlay`. Play against trained AI launches the
same scenario with `IsTraining` off, the host left human, and no runner;
`TrainingDeploymentService` installs `TrainingPilot` on the AI seats.
Quick Setup still creates `Assets/_SO_Assets/AI Training/` only when those
assets are missing. The window is a keeper and draws the ship panel
because it writes assets.

Edit-mode: `AITrainingCoreTests` + `InputOnlyContractTests` — 29 passed,
0 failed (2026-09-29 11:54:04Z), Unity 6000.3.17f1, batch mode. No editor
was attached for this pass, so Learn → Stop → play-against was not flown.
The operator steps are `README.md` § Operator.

## Prompt 6 status

Overnight halt-safety is in the population and the session asset.
`TrainingPopulation` keeps an open checkout off the serialized cursor:
fitness and `nextCheckoutIndex` move only in `ReturnFitness`.
`AbandonCheckout` drops the in-flight count and does not rewrite a
recorded score. `PersistState` returns while `HasOpenCheckout` is true,
so a mid-match flush cannot save a half-scored generation.
`EndEpisodeInternal` force-saves after every pilot has returned, then
calls `RequestReplay`. The runner does not call `SceneManager.LoadScene`
or `ResetForReplay`. The platform reload (`ExecuteSceneReloadReplay`)
already despawns AI players spawned with `destroyWithScene: false`.

A domain reload or script recompile hits
`AssemblyReloadEvents.beforeAssemblyReload` and abandons the open match
unless a replay handoff is already in progress. The launcher's
`_hasLaunched` survives that reload. If Play is still inside the training
scene it asks the mode for `RequestReplay` rather than scoring the
interrupted `RoundStats`. Resume is Learn again. Stop Play and the
Training window Stop both abandon the in-flight checkout and flush what
was already committed. A kill mid-match loses that match only. A kill
after an in-memory evolution but before the next finished match re-rolls
the unscored children from the last saved population. Recorded fitness is
not rewritten either way.

The stuck-match watchdog is wall-clock (`Time.unscaledTime`) and records
the episode as a timeout. Bring-up traces are `CSLogChannel.AITraining`
(off until FrogletTools → Toolbox → Logging). Warnings and errors stay
loud. There is no per-frame `Debug.Log` under `AITraining`.

Optional batch flags on `TrainingControlSO` (time scale, mute audio,
disable `Camera.main`) apply only to a non-human training launch. A
missing or 0/1 time scale leaves `Time.timeScale` alone; the cap is 8;
the scale stays 1 while another client is connected. None of them writes
sticks, `Course`, or transforms, and none culls, decays, or despawns
mass. The chaos checklist is `README.md` § Chaos checklist.

Edit-mode: `AITrainingCoreTests` + `InputOnlyContractTests` — **33 passed,
0 failed** (2026-09-29 12:45:52Z), Unity 6000.3.17f1, batch mode.
`python3 Tools/Build/check_conditional_compilation.py` is OK (1800 files).
The resume proof is the JSON round-trip
(`CommitBarrier_OpenCheckout_DoesNotChangeSerializedPopulation`,
`Resume_MatchingScenario_DoesNotResetCompletedEpisodes`). No editor was
attached, so a machine restart and a live Play-mode resume were not flown.

## Prompt 7 status

Normal play ships the intensity-4 archive genome. Intensities 1–3 run
that same clone through `IntensityDitherer` (dropout, steering noise,
reaction delay, ability skip, throttle scale). The ditherer never calls
`TrainingGenome.Set`. `ArchiveDeployment.ResolveGenome` returns a clone,
or null when the archive has no entry, so a lower tier cannot rewrite
the stored god-tier vector and an empty archive does not invent a pilot.
`UseStoredGenomeForLowerIntensity` (default off; a missing YAML bool stays
off) is the opt-out: that seat looks up the exact intensity and flies it
raw. A missing exact entry leaves `AIPilot` in control.

`TrainingDeploymentService` and `TrainingAIDeploymentBridge` both install
through `ArchiveDeployment`. A vessel-locked mode keys the archive on the
catalog hull. An unlocked mode keys it on the seat's `VesselClassType`.
Install stops `AIPilot` and disables it before `BeginEpisode`. Both
callers return if a `TrainingPilot` is already on the vessel, and
`TrainingPilot.LateUpdate` stops a late `ToggleAIPilot(true)`. Push and
auto-deploy file under intensity 4. The service stays quiet while
`IsTraining` is set, so it does not fight the runner.

The playtest matrix is `README.md` § Playtest verification matrix (mode ×
intensity 1..4, human vs AI). It was not flown. No editor was attached.

Edit-mode: `AITrainingCoreTests` + `InputOnlyContractTests` — **37 passed,
0 failed** (2026-09-29 12:56:16Z), Unity 6000.3.17f1, batch mode.
`python3 Tools/Build/check_conditional_compilation.py` is OK (1801 files).
Checklist items 3 and 6 stay open.

## Prompt 8 status

One `TrainingScheduleSO` queues scenarios. A slot advances when its
evaluation cap or its unscaled wall-clock budget is hit. The cursor
(index, evaluations, elapsed seconds, failures) is on
`TrainingSessionStateSO`. `ResetForScenario` keeps that cursor, so a halt
and the next **Learn the queue** resume the same slot. Restart queue and
Reset session are what send it back to slot 1. The top Learn button still
runs one scenario and clears the schedule reference on the control asset.

Each scenario files its best genome under its own archive key. Advancing
does not clear another mode's bucket. A missing controller or a slot with
no scenario is logged, retried up to that slot's attempt cap, then skipped.
The runner does not load the next scene. `TrainingAutoLauncher.Relaunch`
uses the same `InvokeGameLaunch` path as the first mode. While a schedule
is attached, the single-scenario episode target does not end the night.

The overnight queue was not flown. No editor was attached.

Edit-mode: `AITrainingCoreTests` + `InputOnlyContractTests` — **43 passed,
0 failed** (2026-09-29 13:06:59Z), Unity 6000.3.17f1, batch mode.
`python3 Tools/Build/check_conditional_compilation.py` is OK (1802 files).
Checklist items 3 and 6 stay open.

### Deferred from the older branches

Not ported. Both live under `Assets/_Scripts/Game/AI/` on
`origin/claude/extend-ai-training-duration-vfMGG` and
`origin/claude/ai-pilot-intensity-levels-XK6ST`, and both fight the S3xw3
architecture:

- Spectator camera and play-along (`AITrainingController.SetupSpectatorCamera`,
  `PlayAlongTrainingController`) restart via `gameData.ResetForReplay()` and
  assume the old `Game/AI` controllers. A spectator is a later slice, after
  one mode completes a generation on the scene-reload loop.
- Collision-prediction tuning (`ComputeCollisionAvoidance`,
  `PilotGenome.collisionAvoidanceDistance`) writes `VesselStatus.Course` and
  lerps from inspector defaults toward the genome as intensity rises, which is
  the opposite of the locked intensity law. The idea belongs later as an
  `ObstacleAvoidancePolicy` gene, not as a second `AIPilot`.

Also deferred (not compile breaks): `PrismSensor` still uses
`Physics.OverlapSphereNonAlloc`. Prompt 2 closed the other three items in
that list: `IsTraining` stands the deployment service down for the session,
HexRace golf score is negated at harvest, and the host is flipped onto
autopilot before each rollout starts (the pilot writes sticks even when
`InputStatus.Paused` is set).

## Locked product

- Overnight GA: race a population, record completed episodes, cull, mutate and
  cross over, start the next generation. Halt mid-run keeps every finished
  episode and drops the one in flight.
- The trained pilot writes `IInputStatus` and calls
  `PerformShipControllerActions` / `StopShipControllerActions`. It does not
  teleport, write transforms, write physics, or write `VesselStatus.Course`.
- Train the strongest genome per arcade mode at intensity 4. Intensities 1–3
  are a runtime nerf of that genome (dropout, steering noise, reaction delay,
  skipped abilities, throttle scale). The genome itself is not rewritten to
  make a weaker pilot.
- One editor surface under `FrogletTools/`: configure a run, inspect results,
  deploy an archive, play against the trained pilot.
- Do not invent a second framework. The baseline already exists on
  `origin/claude/ai-training-tool-S3xw3`.

## Branch inventory

| Branch | Tip | Tip date | Merge-base with bleeding-edge | Behind / ahead |
|---|---|---|---|---|
| `origin/claude/ai-training-tool-S3xw3` | `5d82eb0de` | 2026-05-05 | `46fd37675` (PR #508) | 2576 / 5 |
| `origin/claude/extend-ai-training-duration-vfMGG` | `5a87f0adb` | 2026-02-25 | `65ea946e7` | 4284 / 21 |
| `origin/claude/ai-pilot-intensity-levels-XK6ST` | `d18d1e89b` | 2026-02-27 | `65ea946e7` | 4284 / 21 |

S3xw3's five commits, oldest first:

1. `bcac0f52c` — the `Assets/_Scripts/Utility/AITraining/` tree
2. `bb53c5089` — `DefaultEnabledModules` lookup fix
3. `0900665c9` — zero-config defaults and Quick Setup
4. `c8cd762d8` — one-click Learn (Bootstrap → game → loop)
5. `5d82eb0de` — launch on `OnClientReady`, not `ApplicationState.MainMenu`

The two older branches share a merge-base and differ by one commit each.
Extend's unique tip is "run indefinitely." Intensity's unique tip is "harden
for unattended operation." Neither is a superset of the other. Both edit only
`Assets/_Scripts/Game/AI/`. That directory does not exist on bleeding-edge.
Current AI lives at `Assets/_Scripts/Controller/AI/AIPilot.cs`.

S3xw3's README treats the older stack as lessons (session state that survives
a domain reload, don't record a partial episode, don't busy-poll). It does
not vendor their code. That reading is still right.

## Recommended baseline

**Port `Assets/_Scripts/Utility/AITraining/**` from `5d82eb0de`.** Then fix
the compile breaks and the episode-restart break listed below. Do not merge
the branch. Do not port `Assets/_Scripts/Game/AI/`.

Discard from the older branches, as implementation:

- `PilotGenome` intensity lerp. It moves from inspector defaults toward the
  genome as intensity goes 1 → 4, and intensity ≤ 1 falls through to a
  separate update that writes `Course`. That is the opposite of "train at 4,
  dither down."
- `AIPilot` `Course` writes (four on the intensity tip).
- `AITrainingController` and `PlayAlongTrainingController` calling
  `gameData.ResetForReplay()` from `Update`. Same restart bug as S3xw3, on
  a deleted path.
- `Physics.OverlapSphere` prey scans in that `AIPilot`.

Salvage only as requirements already met by S3xw3: indefinite run until Stop,
halt without recording the in-flight episode, population stored on a
ScriptableObject so a domain reload does not wipe a generation. Play-along
and a spectator camera are not in the S3xw3 tree. They are a later product
slice, not part of this port.

## What S3xw3 actually contains

All of these files are **added** versus bleeding-edge. None are on the
working tree. Sizes are blob bytes.

| Path | Responsibility | Verdict |
|---|---|---|
| `Core/TrainingGenome.cs` | Genome, module bits, gene values | Port. Pure data. |
| `Core/TrainingPopulation.cs` | Population, elite, crossover, mutation, novelty, checkout | Port. Pure data. |
| `Core/GeneRegistry.cs`, `GeneSpec.cs` | Policies register genes; missing genes fall back to defaults | Port. |
| `Core/IDecisionPolicy.cs`, `DecisionContext.cs`, `DecisionOutput.cs` | One decision step: sense → policy → stick/ability output | Port. |
| `Core/IntensityDitherer.cs` | Runtime nerf for intensities 1–3. Does not edit the genome. | Port. This is the locked intensity law. |
| `Core/TrainingFitness.cs` | Episode fitness value | Port. |
| `Policies/*.cs` (9 + `PolicyBootstrap`) | Seek, throttle, drift, boost, skim, obstacles, threats, ability schedule | Port, then retune against current vessels. |
| `Sensors/TargetSensor.cs`, `ThreatSensor.cs` | Objective and threat reads | Port. Verify the queries still exist. |
| `Sensors/PrismSensor.cs` | Nearby mass via `Physics.OverlapSphereNonAlloc` | Port the interface. Replace the query. Colliders are off for 0.6 s after spawn, and `PrismSpatialIndex` is the spatial API. |
| `Fitness/FitnessProfileSO.cs`, `FitnessComponents.cs`, `IFitnessComponent.cs` | Per-mode recipes over `IRoundStats` | Port. Recipes are stale (below). The `IRoundStats` members they already read still exist: `Score`, `CrystalsCollected`, `SkimmerShipCollisions`, `JoustCollisions`, `VolumeCreated`, `VolumeRestored`, `HostileVolumeDestroyed`. |
| `Pilot/TrainingPilot.cs` | The pilot. Writes `XSum` / `YSum` / `XDiff` / `YDiff` / `EasedLeftJoystickPosition`. Calls `PerformShipControllerActions`. Calls `StopAIPilot` on the legacy pilot. No `Course` write. | Port. This is the input-only constraint, and it is real in the source. |
| `Pilot/TrainingDeploymentService.cs` | DontDestroyOnLoad. On `OnPlayerPairInitialized(ulong)`, skips when `gameData.IsTraining`, else `AddComponent<TrainingPilot>` from the archive. | Port, then change the flag it reads. `IsTraining` is already taken (below). `AddComponent` does not replicate. |
| `Pilot/TrainingAIDeploymentBridge.cs` | Prefab alternative: `RequireComponent(AIPilot)`, stop the legacy pilot. | Port as the explicit opt-in. Same replication limit. |
| `Runner/TrainingSessionRunner.cs` | Episode machine: checkout genome, watch turn end, 180 s watchdog, harvest, persist, `ResetForReplay`. `StopSession` abandons the in-flight episode. | Port the machine. Replace the restart. |
| `Runner/TrainingSessionStateSO.cs` | Population + novelty archive on an asset | Port. This is how a halt and a domain reload keep completed work. |
| `Runner/TrainingScenarioSO.cs` | Mode, intensity, `UseResetForReplay` (default true), early-exit thresholds | Port. Default `UseResetForReplay` is now the wrong default. |
| `Runner/TrainingAutoLauncher.cs` | Sets `IsTraining`, writes `GameDataSO`, `InvokeGameLaunch`, flips the host via `ToggleAIPilot`. Scene-name `switch` includes deleted `MinigameFreestyle`. | Port the idea. Replace the scene switch and the host-seat trick. |
| `Persistence/TrainingArchiveSO.cs` | Deployable best genome keyed by vessel × mode × intensity | Port. Its comment says `AIPilot` reads it. `AIPilot` does not. Deployment replaces `AIPilot`. |
| `Persistence/TrainingControlSO.cs` | `DeployArchiveInNormalPlay` | Port. |
| `Persistence/GenomeJson.cs` | Sidecar export/import | Port. |
| `Telemetry/TrainingTelemetrySO.cs` | Editor-facing session mirrors | Port. |
| `Editor/TrainingEditorWindow.cs` | `FrogletTools/AI Training` and Quick Setup | Port. Add `[FrogletTool]` and the tool-output ledger. Learn writes assets under `Assets/_SO_Assets/AI Training/`. |
| `Editor/TrainingPlayModeHook.cs` | Auto-start when entering Play after Learn | Port. |
| `Tests/Editor/AITrainingCoreTests.cs` | Edit-mode tests, already under `Editor/` | Port. Keep them under `Editor/`. Do not add a test asmdef. |
| `README.md` (in that folder) | Operator notes | Do not treat as current. It disagrees with its own tip commit (below). |

## README claims versus this tree

The folder README was not updated by `5d82eb0de`. The code waits for
`gameData.OnClientReady` in `Menu_Main` and keeps the app-state path only as
a 12 s timeout. The README still says the launcher fires when
`ApplicationState` reaches MainMenu.

| Claim | Status on bleeding-edge |
|---|---|
| Press Learn and the session runs Bootstrap → menu → match → generations | **Partial.** `OnClientReady`, `InvokeGameLaunch`, `SelectedIntensity`, and `SceneLoader` still exist. The launcher has never compiled against the current menu. `MainMenuController` writes vessel / player count / intensity on its own start; the launcher's comment says it overwrites those after `HandleMenuReady`. That order is plausible and unproven. |
| `ResetForReplay()` starts the next match, "the same way singleplayer does" | **Broken.** `GameDataSO.ResetForReplay` clears stats, players, and runtime data, then raises `OnResetForReplay`. It does not reload the scene and it does not start a turn. Every current domain mode sets `UseSceneReloadForReplay => true` (HexRace, Joust, Crystal Capture, Rampage, Ribcage, Nucleus Rush, Wildlife Liberation, Dog Fight, Bends, Astro League, Scarab Scramble, Salvo). HexRace removed `OnResetForReplayCustom` and sets `SegmentSpawner.ExternalResetControl`, so the event does not rebuild the track. The real replay is `MultiplayerMiniGameControllerBase.ExecuteSceneReloadReplay`, which despawns AI (`destroyWithScene: false`) before the Netcode reload. |
| The runner does not spawn vessels | **Still valid.** Spawning stays on `ServerPlayerVesselInitializerWithAI`. The runner only attaches pilots to `gameData.Players`. |
| AI touches only the input system | **True of `TrainingPilot`. False of live `AIPilot`.** Current `AIPilot.Update` assigns `VesselStatus.Course` on drift commit and calls `PerformShipControllerActions`. The aim telegraph uses `PerformShipControllerActionsReplicated` because an AI pilot is server-only. `TrainingPilot` calls the non-replicated method. |
| Stop keeps completed episodes and drops the in-flight one | **Still valid as source.** `StopSession` clears pilots without harvesting, then `PersistState(forceSave: true)`. It depends on the runner and the session asset both surviving the stop. A scene reload destroys a scene-placed runner unless something DontDestroyOnLoad owns the session. |
| Session SO survives a domain reload | **Still the right shape** for script recompiles. It does not, by itself, re-attach pilots after a scene reload. |
| Host seat trains because the launcher calls `ToggleAIPilot(true)` | **Partial / likely wrong.** `IVesselStatus.AIPilot` and `ToggleAIPilot` still exist. The host human is `IsLocalPilot`. `InputController` returns immediately when `!IsLocalPilot`, so device polling and the AI's stick writes are different paths. Flipping autopilot on the human can double-drive that vessel. Backfill already fills `RequestedAIBackfillCount`. A 3-genome race should be 3 AI bodies, not "host plus 2 AI." |
| `IsTraining` means "this is a GA session" | **Conflict.** The field exists (`GameDataSO` line 87) and predates S3xw3. `Arcade.LaunchTrainingGame` sets `gameData.IsTraining = !isDailyChallenge` for the legacy practice-mode launch, and `Arcade.LaunchArcadeGame` forces it false. `TrainingDeploymentService` skips install when the flag is true. Reusing it makes practice games and GA sessions the same bit. |
| Archive is what `AIPilot` reads at deployment | **Obsolete comment.** Nothing on this tree reads `TrainingArchiveSO`. |
| One scenario covers the modes you can train | **Missing modes.** The launcher's scene switch and the fallback fitness `switch` know HexRace, Joust, Crystal Capture, Cellular Duel, the two freestyle values, and 2v2. `GameModes.Freestyle` (7) is retired and `MinigameFreestyle` is gone, so that `switch` does not compile. Modes added after May 2026 are absent: Rampage-as-multiplayer, Ribcage, Nucleus Rush, Wildlife Liberation, Dog Fight, Bends, Astro League, Scarab Scramble, Salvo. |
| Fitness from `RoundStats` ranks the pilot | **API valid, meaning drifted.** Per-player stats still exist and still live on the persistent `Player` NetworkObject, so they survive a scene load unless something resets them. Win conditions for domain modes are `ScoringMetrics.SumByDomain`, not the individual counter. HexRace golf stores elapsed time in `Score` (lower is better). `ScoreFromRoundStats` adds `Score` with a positive weight, which rewards a slower racer. `LifeformsKilled` and `CombatPoints` have no fitness component. |
| Prism queries are an ordinary physics overlap | **Broken by current rules.** Fresh prisms have colliders disabled. New queries go through `PrismSpatialIndex`. |

`InvokeGameTurnConditionsMet` still exists and still raises `OnMiniGameTurnEnd`
after stopping the flight clock. The runner calls it to force-end a hung
episode. On a scene-reload mode that raise ends the turn and then the
controller reloads the scene. It is not an in-place "run it again."

## End-to-end loop

| Step | Status |
|---|---|
| Register genes, hold a population, crossover, mutate, novelty archive | **Exists** on S3xw3. Absent here. Pure C#; port as-is. |
| Persist the population on a ScriptableObject after each completed episode | **Exists** on S3xw3. |
| Halt without recording the in-flight episode | **Exists** in `StopSession`. |
| Launch a match from the editor (Learn) | **Partial.** Menu init, `OnClientReady`, and `GameConfigSynced` can overwrite or race the launcher's `GameDataSO` writes. `GameConfigSynced` is cleared inside `ResetRuntimeData`. An intensity-wise cell that boots before the config RPC sticks on intensity 1. |
| Spawn the bodies | **Exists** on the platform (`ServerPlayerVesselInitializerWithAI`). The training code correctly does not spawn. |
| Attach a genome to each body | **Exists** in `StartNextEpisode` (`Checkout`, `AddComponent<TrainingPilot>`, `LoadGenome`). |
| Race through the input system only | **Partial.** `TrainingPilot` writes `InputStatus` fields that `VesselTransformer` still reads (`XSum`, `YSum`, `XDiff`, `YDiff`, throttle). `InputController` does not poll devices for a non-local pilot, which is what you want: the pilot writes the status directly. AI vessels are server-owned, and `VesselController.Update` runs on the owner, so a host-only overnight sim can move. This is unproven until one vessel flies with `AIPilot` disabled. Do not "fix" a stationary ship by copying `AIPilot`'s `Course` write. |
| Keep `AIPilot` from also driving | **Partial.** `StopAIPilot` exists. `AIPilot.Update` returns when autopilot is off. The component must stay disabled for the episode. Two enabled pilots fight. |
| Abilities during the race | **Partial.** `PerformShipControllerActions` runs locally. Peers see the aim telegraph only through `PerformShipControllerActionsReplicated`. Host-only training can live without the replicated call. "Play against this AI" in a party cannot. |
| Harvest fitness | **Partial.** Stats properties exist. Domain sum, golf sign, and the post-May metrics are not in the recipes. Stats on a persistent `Player` can carry into the next episode if the reset does not run before the next sample. |
| Advance to the next episode in the same scene | **Broken.** Default `UseResetForReplay` calls `ResetForReplay`, which clears `Players` and does not restart a domain mode. |
| Advance via scene reload and re-attach | **Missing.** Reload despawns AI, destroys scene objects (the runner, if it lives in the scene, and every `AddComponent` pilot), then spawns a new roster. Nothing waits for the new `OnPlayerPairInitialized` and checks out the next genomes. The session SO can hold the population across that gap. The in-memory pilot map cannot. |
| Dither intensities 1–3 from a frozen intensity-4 genome | **Exists** in `IntensityDitherer`. Do not replace it with the older lerp. |
| Deploy the archive into a normal match | **Partial.** The service's event signature matches (`OnPlayerPairInitialized` is still `ulong`, and `ClientPlayerVesselInitializer` raises `player.PlayerNetId`). Install is host-local. Clients do not get the component. The `IsTraining` guard collides with practice-mode. |
| Train every mode at intensity 4, then nerf 1–3 | **Missing.** No scheduler. No fitness recipe for combat, ecology-kill, demolition, or ball modes. |

## Port checklist

Do these in order. Stop at the first compile failure and fix that, rather
than rewriting the design.

Prompt 1 did 1 (onto `feat/ai-genetic-training`), the delete half of 2,
5, 8, and 11. Prompt 2 did 4 and the HexRace half of 7 (score sign at
harvest, not a full per-mode recipe). Prompt 6 did 9 (bring-up traces are
`CSLogChannel.AITraining`; warnings and errors stay loud). Scene
resolution from the arcade card (the rest of 2), plus 3 and 6, are still
open. Item 3 stays a dedicated flag; the runner uses the existing
`IsTraining` stand-down. Item 10's edit-mode run is 20 passed, 0 failed
(2026-09-29 11:25:22Z).

**Item 3 closed 2026-10-10.** `GameDataSO.IsGeneticTrainingSession` is the
GA session's own serialized flag: the runner and the auto-launcher set it,
Stop, a halt, a failed handoff and `TrainingPlayModeHook` clear it, and the
three installers that stood down on `IsTraining` (`TrainingDeploymentService`,
`TrainingAIDeploymentBridge`, `SkimRaceAIDeployment.Claims`) read it instead.
`IsTraining` goes back to meaning practice mode only (`Arcade.LaunchTrainingGame`,
reached from `HangarTrainingModal` and the daily challenge), which until now
suppressed every one of those installers in every Hangar practice game by
accident (risk register row 11). `SkimRaceAITests.Deployment_ClaimsOnlyNormalSkimRaceAndRegatta`
carries the negative control (practice mode still claims). A GA launch also
clears a stale practice flag. Proved by `unity_refcompile`; the edit-mode
suite was not re-run here (no editor).

**Item 6 closed 2026-10-10.** `PrismSensor.Sample` reads
`PrismSpatialIndex.Instance.QuerySphere` (the same call
`SkimRacePilot.GatherLaidMass` makes), so the genome pilot sees a prism the
moment it registers rather than 0.6 s later when its collider wakes, the
NonAlloc truncation of dense cells is gone, and the per-hit
`GetComponentInParent` with it. `PrismSensor.PrismLayerMask` and
`TrainingPilot.prismLayerMask` were removed (nothing serialized them: no
prefab or scene carries either script). A null index returns an empty
neighbourhood. Proved by `Tools/Build/unity_refcompile` (player config,
0 project errors); no editor was attached, so the edit-mode suites were
not re-run here.

1. Copy `Assets/_Scripts/Utility/AITraining/` from `5d82eb0de` onto
   `AI-Genetic-algorithm`. Do not copy `Assets/_Scripts/Game/AI/`.
2. Delete the `GameModes.Freestyle` arm. Resolve scenes from the arcade
   card / `SceneNameListSO`, not a hardcoded scene-name switch.
3. Give the GA session its own flag. Leave `GameDataSO.IsTraining` to
   `Arcade.LaunchTrainingGame`.
4. Change the episode advance. `UseResetForReplay` must not be the path
   that starts the next match. The next match is a scene reload (or a
   mode that truly resets in place — none of the current domain modes do).
   A DontDestroyOnLoad session owner reloads state from
   `TrainingSessionStateSO` after the new roster exists, then checks out
   genomes. It does not keep `TrainingPilot` instances across the reload.
5. Disable `AIPilot` for the episode (`StopAIPilot` and disable the
   component). Do not add `Course` writes to `TrainingPilot`.
6. Point `PrismSensor` at `PrismSpatialIndex`.
7. Fix the default racing fitness so HexRace `Score` is not a reward.
   Read individual `RoundStats` only after confirming the controller reset
   them for this episode. Domain-sum modes need a note in the recipe: a
   teammate's contribution is in the same win.
8. Add `[FrogletTool]` on the window. Any asset Learn creates goes through
   `FrogletToolChangeLedger` and the ship panel (`Docs/TOOLING.md`).
9. Move the launcher and runner `Debug.Log` traces onto a `CSLogChannel`.
   Leave warnings and errors loud. Done in Prompt 6
   (`CSLogChannel.AITraining`).
10. Keep tests under `Tests/Editor/`. Run them. Then compile in the editor
    before any Play Mode claim.
11. Do not port intensity lerp, play-along, or the old `AIPilot` as a
    second driver. A spectator / play-along mode is a separate prompt
    after one mode completes a generation.

## Hard-loop risk register

Ordered by how likely they are to make generation 2 never start, or to
record a lie.

1. **Restart is a scene reload.** Addressed for HexRace in Prompt 2: the
   runner calls `RequestReplay` after persist, the handoff skips
   `StopSession`, and the launcher starts the next rollout only after the
   replacement roster exists. `ResetForReplay` is no longer the advance.
   The historical failure mode remains true of any code that still calls
   it: it clears the roster and raises an event the mode ignores.
2. **New bodies are new objects.** Checkout indices from the previous
   roster do not apply to the next spawn. Re-checkout after
   `OnPlayerPairInitialized` for the new count. If the spawn count changes,
   the population still has the right genomes; the assignment is what
   changes.
3. **Stale `RoundStats`.** `RoundStats` lives on the persistent `Player`.
   AI players are despawned on reload, which is safer than reuse, but a
   human host who is left in the roster can carry last episode's crystals
   into the next sample. Sample only after the mode's reset, and never
   sample the abandoned in-flight episode on Stop.
4. **`GameConfigSynced` and intensity.** Runtime reset clears the flag.
   Intensity-wise cells that bootstrap before the config RPC build
   intensity 1 for the whole match. HexRace's track seed also depends on
   intensity arriving before `SegmentSpawner` runs. Re-apply scenario
   intensity on the server before the cell and the track init, every
   episode, not only on the first Learn click.
5. **Menu versus launcher.** `MainMenuController` writes its own vessel,
   player count, and intensity, then raises `OnClientReady`. The launcher
   must be the last writer before `InvokeGameLaunch`, and it must ignore
   `OnClientReady` from the game scene (the tip commit already does that).
   Confirm the menu does not write again after the launcher.
6. **Host seat.** Training N genomes needs N AI vessels. Turning the local
   human into an autopilot shares a body with `IsLocalPilot` input and the
   camera laws. Prefer backfill count = population slot count, and leave
   the human paused out of the fitness sample.
7. **Two pilots.** If `AIPilot` stays enabled it keeps writing `Course`
   and pressing buttons. `TrainingPilot` then trains against a pilot that
   is not the genome.
8. **Input writes that nothing integrates.** `VesselTransformer` reads
   `InputStatus` on the owner. That is the path to prove with one AI
   vessel and `AIPilot` disabled. If the ship does not move, the bug is
   in that path. The fix is still input, not a transform write.
9. **Abilities that only exist on the owner.** Non-replicated
   `PerformShipControllerActions` is enough for a host-only night.
   Deploying the same pilot into a party match needs the replicated
   action path or peers never see the button.
10. **`AddComponent` is local.** A client in a normal match will not have
    `TrainingPilot`. Deployment is a host install, which matches how AI
    already runs server-side. Do not expect the component to replicate.
11. **`IsTraining` means practice mode.** Guarding deployment or the
    runner on that bool will skip or double-fire for reasons that have
    nothing to do with the GA.
12. **Golf and domain fitness.** A positive `Score` weight ranks slow
    HexRace pilots as fitter. A domain win credits every teammate. Either
    one poisons the population while the loop "works."
13. **OverlapSphere.** The sensor misses new mass and violates the spatial
    index rule. Fitness then rewards pilots who ignore the trail they
    cannot see.
14. **`GameModes.Freestyle`.** Fixed in the port. The retired scene arm
    and the runner fallback case are gone.
15. **Modes with no recipe.** A scheduler that launches Dog Fight, Bends,
    Salvo, Wildlife Liberation, Scarab Scramble, Rampage, or Ribcage on
    the racing fallback optimizes crystal-racing fitness in a mode that
    does not pay for crystals.
16. **Watchdog versus scene reload.** A 180 s force-end that calls
    `InvokeGameTurnConditionsMet` will reload the scene. That is a legal
    way to abandon a hung episode only if the session asset was not given
    a fitness row for it. `StopSession` already has the right rule;
    the watchdog must use that rule.
17. **Tool output.** Learn creates ScriptableObjects. Without the ledger,
    those assets stay in the working tree and never travel with the
    branch that contains the tool.
18. **Do not merge the intensity branch to "get intensities."** Its lerp
    trains the weak pilot and plays the strong one. `IntensityDitherer`
    is the implementation of the locked law.

## What this audit did not do

The "partial" rows above are the source comparison from Prompt 0. Prompt 1
copied the tree and fixed the compile breaks named in the port checklist.
It did not run Play Mode and does not claim a finished episode, a generation
boundary, or an overnight run. The next proof is one HexRace intensity-4
episode on the host with `AIPilot` disabled and `TrainingPilot` writing
sticks. Generation 2 is the hard loop above, not a follow-on feature.
