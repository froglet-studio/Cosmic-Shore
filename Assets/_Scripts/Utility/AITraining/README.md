# AI Training Framework

An overnight-friendly evolutionary AI trainer for Cosmic Shore. Designed to be
run on any computer, against any minigame, by anyone, with no babysitting.

The trained pilots are deployed back into the game via a single ScriptableObject
asset (`TrainingArchiveSO`) and become the AI opponents at all four difficulty
intensities — intensity 4 is the trained ceiling, intensities 1-3 are
runtime-dithered versions of the same pilot.

## Why this framework exists

Earlier work on `claude/ai-pilot-intensity-levels-XK6ST` and
`claude/extend-ai-training-duration-vfMGG` established that:
- A genetic-algorithm search over a fixed set of AI parameters (throttle,
  steering aggressiveness, prism standoff, etc.) does converge on competent
  pilots given enough episodes.
- A domain reload used to drop the in-memory generation. Finished episodes
  now live on the session asset; an open match is abandoned, not scored.
- Camera handling, race timeouts, and scene resets all need defensive
  safeguards before a system can be trusted to run unattended.

This framework rebuilds on top of those lessons with a few additions that
matter for the longer haul:

1. **Extensible search space.** Parameters are registered by behavior modules
   into a process-wide `GeneRegistry`. Adding a new behavior — say, a "shield
   when threatened" policy — adds new genes without touching any central type.
2. **Structural mutation.** The genome stores both numeric values and a
   set of enabled module bits. Crossover and mutation can flip module bits at
   a low rate, so the search learns *which behaviors to use* in addition to
   how to tune them.
3. **Novelty-augmented selection.** Each genome's behavior fingerprint
   is hashed and compared against a rolling archive; rare genomes get a
   selection bonus. This stops long overnight runs from collapsing onto a
   single local optimum.
4. **Per-game fitness recipes.** A `FitnessProfileSO` picks and weights
   `IFitnessComponent` entries (crystal collection, joust collisions, volume
   created, ability use, etc.) so the same trainer can target HexRace,
   CrystalCapture, or Joust without code changes.
5. **One pilot for training and deployment.** Both modes use the same
   `TrainingPilot` MonoBehaviour. What you train is exactly what ships.
6. **Hard input-only constraint.** The pilot is allowed to write to
   `IInputStatus` and call `vessel.PerformShipControllerActions`. It is
   *not* allowed to touch the transform or set physics state. This is what
   keeps trained behavior transferable: the AI plays the game the same way
   a human would.

## Input-only contract

A genome transfers because the pilot that flies a normal match is the same
component that flew training, and that component can only do what a stick
can do. `TrainingSessionRunner`, `TrainingDeploymentService`, and
`TrainingAIDeploymentBridge` all end at `TrainingPilot.BeginEpisode`. None
of them has a second decision path.

Each frame, while an episode is active:

1. `TrainingPilot.BuildContext` copies pose, speed, and vessel flags into
   `DecisionContext`. That read is sensing.
2. Each `ITrainingSensor.Sample` fills the same context (target, nearby
   prisms, threats). Sensors may query the world. They do not move the ship.
3. Each enabled `IDecisionPolicy.Decide` returns a `DecisionOutput` vote:
   local steer, throttle, roll, and ability start/stop events.
4. The pilot blends the votes by weight, then `IntensityDitherer.Apply`
   degrades the blend for intensities 1–3. Intensity 4 is the vote unchanged.
5. `ApplyToInputStatus` writes `IInputStatus`. `ApplyActionRequests` calls
   `IVessel.PerformShipControllerActions` and `StopShipControllerActions`.

Allowed writes, and only these:

| Surface | What is written |
|---|---|
| Dual-stick `IInputStatus` | `XSum`, `YSum` (yaw/pitch), `YDiff` (yaw), `XDiff` (throttle, or `1` when a policy votes ram) |
| Single-stick `IInputStatus` | `EasedLeftJoystickPosition` |
| Abilities | `PerformShipControllerActions` / `StopShipControllerActions` for the `InputEvents` a policy requested |
| Episode end | those stick fields back to zero |

`InverseTransformDirection` and reading `transform.position` / `forward`
are aim math. They are not actuation.

Banned on `TrainingPilot`, every `IDecisionPolicy`, every sensor, the
ditherer, and both deployment installers: `Course`, `SetPose`, `Teleport`,
`SetInitialSpeed`, rigidbody forces and velocities, `Translate`, `LookAt`,
`Warp`, and any assignment to `transform.position` / `rotation` /
`localScale`. The edit-mode scan is
`InputOnly_PilotPoliciesAndSensors_DoNotCallBannedApis`.

Disabling `AIPilot` is part of the contract. `BindVessel` calls
`StopAIPilot` and sets `enabled = false`. `LateUpdate` repeats that if
something turns `AIPilot` back on, so the two pilots never write sticks
on the same frame. `AIPilot` itself still writes `Course`; `TrainingPilot`
must not grow that write.

In-editor check, once an archive has a genome and
`TrainingControlSO.DeployArchiveInNormalPlay` is on: start a normal
HexRace (not Learn). The deployed ship should turn and change speed the
way a stick pilot does. It should not snap to a crystal or a rival. If it
does, the scan above is the first place to look.

## Folder layout

```
_Scripts/Utility/AITraining/
├── Core/                            Genome, population, decision context, ditherer
├── Policies/                        Built-in behavior modules
├── Sensors/                         World-state samplers
├── Fitness/                         Built-in fitness components + profile SO
├── Pilot/                           TrainingPilot MonoBehaviour + deployment bridge
├── Runner/                          Scenario SO, session state SO, runner MonoBehaviour
├── Persistence/                     Archive SO, JSON sidecars
├── Telemetry/                       SOAP telemetry data container
├── Editor/                          FrogletTools / AI Training window
└── Tests/Editor/                    Edit-mode tests for the search-side primitives
```

## Operator

Open **FrogletTools → AI Training**. Four tabs sit under Learn and Stop.
Quick Setup creates `Assets/_SO_Assets/AI Training/` the first time anything
is missing and does not overwrite a scenario that already exists.

1. **Configure.** Pick a mode. That loads its scenario. Set population,
   elites, vessel (locked modes keep theirs), and intensity. Target episodes
   of **−1** runs until you press Stop. The watchdog force-ends a match that
   never reaches its objective; it is separate from the episode cap. Save
   settings, or reset the session if you changed population and want the
   new size now.
2. **Learn.** Enters Play, races every seat on autopilot, and keeps going
   across scene reloads. **Stop** saves every finished match, drops the one
   still in the air, and leaves Play. A plain Play button does not resume.
   Press Learn again to continue the same session.
3. **Inspect.** Generation, best fitness, the hall-of-fame genome, novelty
   archive size, finished episode count, and an ETA. Overnight runs say they
   stop when you press Stop.
4. **Archive.** Browse by vessel, mode, and intensity. Export or import a
   genome as JSON.
5. **Deploy.** Push the best genome into the archive. Leave **Use archive in
   normal play** on. **Play against trained AI** launches the same scenario
   as an ordinary match: you fly the host seat, and the AI seats fly
   `TrainingPilot` from the intensity-4 archive. That launch is not a
   training run.

## Overnight queue

The Schedule tab edits one `TrainingScheduleSO`. A slot is a scenario plus
an episode cap and a wall-clock budget in hours. Either cap advances to the
next slot. Both at 0 means that slot does not end on its own.

The cursor (`ScheduleIndex`, evaluations spent, unscaled seconds spent,
failures spent) lives on `TrainingSessionStateSO`. Halt and a domain reload
save that asset, so the next **Learn the queue** starts the same slot.
Resetting the session, or Restart queue, is what sends the cursor back to
slot 1. A single-scenario Learn does not attach the schedule.

Each slot files its best genome under that scenario's own archive key
(`vessel_mode_I4`). Advancing does not copy or clear another mode's bucket.

A missing controller, or a slot with no scenario, is logged and then retried
or skipped from that slot's attempt cap. The rest of the queue still runs.
The runner does not load the next scene itself. It asks
`TrainingAutoLauncher.Relaunch`, which is the same `InvokeGameLaunch` path
the first mode used. While a schedule is attached, the single-scenario
episode target does not end the night.

This queue has not been flown overnight. The edit-mode tests cover two
modes, the wall clock, a halt that keeps the cursor, a retry-then-skip, and
archive keys that stay distinct.

## One-click training: press Learn and walk away

1. **`FrogletTools → AI Training`** to open the window.
2. **Press the big "Learn" button.**

That's it. The button:

- Creates a default asset set under `Assets/_SO_Assets/AI Training/`
  (`Scenario_HexRace`, `SessionState`, `Archive`, `Telemetry`,
  `FitnessProfile_HexRace`, `TrainingControl`, `Schedule`) if they're not
  there yet. An existing schedule is left as it is. The top Learn button
  runs one scenario. **Learn the queue** on the Schedule tab runs the list.
- Flips the control asset's `AutoStartOnPlay` flag.
- Enters Play mode.

From that point on, an editor play-mode hook spawns a
`TrainingAutoLauncher` GameObject. The launcher:

- Lets `AppManager` boot (Bootstrap → Auth → Menu_Main) exactly as it
  would for a normal launch.
- When `OnClientReady` fires in a menu scene, configures `GameDataSO`
  for an all-AI HexRace match (3 racers, intensity 4), sets
  `IsTraining`, and calls `gameData.InvokeGameLaunch()` — the same
  entry point the arcade configure modal uses, so the rest of the
  pipeline runs unchanged (host start, AI backfill, scene load,
  controller spawn). `ApplicationState.MainMenu` is only a 12 s
  safety net.
- After the game scene loads, waits until the vessel count matches
  the selected player count, flips the host human onto autopilot,
  then starts a `TrainingSessionRunner`. The host's input stays
  paused so the keyboard does not fight the pilot; the pilot still
  writes sticks.

Each completed match:

- Writes each pilot's fitness back to the population. HexRace `Score`
  is golf: that component is negated before the weight is applied.
- Updates the hall-of-fame best.
- Saves the session-state asset and the archive to disk
  (`AssetDatabase.SaveAssets`) before the replay is requested. A kill
  after that point keeps the finished match.
- Calls `MiniGameControllerBase.RequestReplay()`. HexRace reloads
  `MinigameHexRace`. The scene load destroys the runner. The launcher
  survives and attaches the next genomes to the new roster. The
  platform despawns AI players (`destroyWithScene: false`) before that
  load. The runner does not call `SceneManager.LoadScene` or
  `ResetForReplay`.

When the user presses **Stop** (in the window or Unity's Play button),
the runner drops the in-flight match without recording its incomplete
fitness, then flushes session state and archive to disk. The committed
cursor never moved for that match, so the same genomes are served
again and a finished score is not rewritten. Press Stop after dozens
of matches and you keep the dozens.
`AutoStartOnPlay` is cleared, so the next plain Play is a normal
match. Press **Learn** again to resume the same session asset.

## Loop verification

This is the HexRace intensity-4 proof. Do it in the editor. A batch-mode
edit-mode test covers the generation math
(`Loop_ThreeMatchesAtPopulationSize_IncrementsGeneration`) and does not
substitute for these steps.

1. Open **FrogletTools → AI Training → Quick Setup** once, so
   `Assets/_SO_Assets/AI Training/` exists. If a previous run already
   keyed `SessionState` to `Squirrel_HexRace_I4`, delete that asset (or
   clear its population) before changing size. `EnsureStateInitialized`
   keeps an existing population when the scenario key matches, so a
   later size edit does not resize it.
2. On `Scenario_HexRace`, set **Population Size** to **3** and
   **Max Episode Seconds** to **20**. Leave intensity at 4 and the
   mode at HexRace. Population size 3 is only for this proof. The
   overnight default stays 24, and three pilots against a size-24
   population do not evolve inside three matches.
3. Press **Learn**. Those traces are off until you enable
   **FrogletTools → Toolbox → Logging → AITraining**. With the channel
   on, the console shows, in order, for each match:
   - `[Training] Rollout start. generation=…`
   - `[Training] Rollout recorded.` once per pilot (domain, crystals,
     lineage).
   - `[Training] Requesting replay via HexRaceController.RequestReplay`
   - `[MultiplayerController] Scene reload replay - loading MinigameHexRace`
4. After three completed matches the session asset's **Generation** is
   **2** (match 2's start log says `generation=1`, match 3's says
   `generation=2`). **Episodes Completed** counts one evaluation per
   pilot, so three pilots × three matches is **9**, not 3.
5. Press **Stop** during a fourth match. With the channel on, the log is
   `[Training] Halt abandoned the in-flight rollout.` Generation and
   Episodes Completed stay at the post-match-3 values. The in-flight
   pilots are not recorded.
6. Exit Play. `AutoStartOnPlay` is off. `GameDataSO.IsTraining` is
   false again (the play-mode hook clears it on entering edit mode).
   Press **Learn**, not a bare Play. The next `Rollout start` log
   shows the same generation, not 0.

A 20 s cap ends the match through `MaxEpisodeSeconds` and still counts as a
completed rollout (harvest, persist, `RequestReplay`). Fitness will be near
zero. The 180 s watchdog is the separate wedge timer, measured on
`Time.unscaledTime`, so a frozen time scale still ends a stuck match and
records it as a timeout. At time scale 1 the episode cap still fires first.
The loop is what this proves: harvest, persist, scene reload, next
genomes, halt without rewriting completed evaluations. Let a match run
to the crystal objective by raising Max Episode Seconds when you want
real scores. The runner does not spawn vessels and does not write
`Course` or transforms.

## Chaos checklist

Finished episodes are on disk. An open match is not a fitness row.
`CommitBarrier_OpenCheckout_DoesNotChangeSerializedPopulation` and
`Resume_MatchingScenario_DoesNotResetCompletedEpisodes` are the edit-mode
proof. A machine restart was not flown in batch mode; the same asset
round-trip is what a restart reads.

1. **Stop mid-episode.** Press Stop during a match. Episodes Completed
   and the hall of fame stay at the last finished match. The open
   genomes are served again on the next Learn.
2. **Reload domain.** Recompile a script while Play is running. The
   scene runner is destroyed. Its reload callback abandons the open
   checkout and saves the last commit. The launcher requests a clean
   replay so the interrupted match's RoundStats are not harvested.
   Press Learn after you return to edit mode if Play itself stopped.
3. **Restart the machine.** The session asset and the archive are
   ordinary ScriptableObjects under `Assets/_SO_Assets/AI Training/`.
   After reboot, open the project and press Learn. Do not press a bare
   Play: Stop cleared `AutoStartOnPlay`.
4. **Population and archive intact.** Generation, Episodes Completed,
   hall-of-fame fitness, and archive entries match the last finished
   match. A kill during a match loses that match only. A kill after
   evolution has started in memory but before the next finished match
   re-rolls the unscored children from the last saved generation.
   Recorded fitness is not rewritten.

## Overnight batch flags

On the Configure tab, all off unless you set them. They apply only to a
host-only training launch. Play against trained AI leaves the clock,
the listener, and the camera alone. A value of 0 or 1 for time scale
leaves `Time.timeScale` alone, so an older control asset cannot pause
the match. Above 1 speeds the simulation, capped at 8. Mute pauses
`AudioListener` and restores the previous pause state when the launcher
is destroyed. Disable camera turns `Camera.main` off and back on. None
of the three writes sticks, `Course`, or a transform, and none culls,
decays, or despawns mass. Time scale stays at 1 while another client is
connected, because a local time scale would freeze those peers.

### Player-vs-trained-AI deployment

When the user later plays a mode normally (not in training mode),
`TrainingDeploymentService` installs the archive on AI seats:

- Listens to `gameData.OnPlayerPairInitialized`.
- Skips when `gameData.IsTraining` is true (the runner owns those vessels).
- Skips the human seat.
- Installs only when the archive has an entry. A missing entry leaves
  `AIPilot` in control. A registry-default genome is not an entry.
- Vessel-locked modes key the archive on the mode's hull (HexRace is
  Squirrel even if the live class differs). Unlocked modes (Crystal
  Capture, Nucleus Rush, Astro League) key it on the seat's
  `VesselClassType`.
- Stops `AIPilot` and disables it before `TrainingPilot.BeginEpisode`.
  A seat that already has a `TrainingPilot` is left alone, so the two
  pilots never share the sticks.
- Intensity 4 flies the stored genome raw. Intensities 1–3 run that
  same genome through `IntensityDitherer`. The archive is not written.

`TrainingControlSO.UseStoredGenomeForLowerIntensity` and the bridge's
`useDitheringForLowerIntensities = false` are the opt-out: the seat
flies a genome stored for that exact intensity, with no extra dither.
`DeployArchiveInNormalPlay` turns the service off.

### Playtest verification matrix

Not flown. No editor was attached for this pass. The edit-mode tests
cover the lookup, the clone (dither does not rewrite stored genes),
and the stop-before-start order. Each cell below is what a human
playtest should confirm.

Rows are the twelve catalog modes. Columns are match intensity.
**Human** is the local seat: it always keeps player input, at every
intensity. **AI** is every `IsInitializedAsAI` seat.

| Mode | Hull key | I4 AI | I1–I3 AI |
|---|---|---|---|
| HexRace | Squirrel (locked) | raw genome | dither only |
| Crystal Capture | the seat's class | raw genome | dither only |
| Joust | Squirrel (locked) | raw genome | dither only |
| Rampage | Dolphin (locked) | raw genome | dither only |
| Peel the Cage | Rhino (locked) | raw genome | dither only |
| Wildlife Liberation | Sparrow (locked) | raw genome | dither only |
| Dog Fight | Sparrow (locked) | raw genome | dither only |
| The Bends | Dolphin (locked) | raw genome | dither only |
| Scarab Scramble | Scarab (locked) | raw genome | dither only |
| Salvo | Sparrow (locked) | raw genome | dither only |
| Brood Rush | the seat's class | raw genome | dither only |
| Astro League | the seat's class | raw genome | dither only |

For each intensity 1, 2, 3, and 4, play one match as the human against
AI. Intensity 4 should match the trained pilot. Intensities 1–3 should
drop inputs, add steering noise, delay reactions, skip abilities, and
scale throttle down (0.75 / 0.85 / 0.95), and the intensity-4 archive
entry's gene values should be unchanged afterward. With no archive
entry, the AI seat should still be `AIPilot`. With the opt-out on, an
explicit intensity-1/2/3 entry should fly raw and an intensity that has
no entry should stay on `AIPilot`.

### Manual / advanced flow

- **Quick Setup** button creates the assets without entering Play.
- **Re-Discover Assets** scans the project and refills empty slots in
  the window.
- The window auto-discovers existing AI Training assets when opened.
- A scenario without a `FitnessProfile` falls back to an in-memory
  recipe picked by game mode — racing recipe for HexRace / Freestyle,
  Joust recipe for joust modes, Cellular recipe for capture/duel
  modes. Assigning a custom profile always overrides the fallback.
- `FitnessProfileSO` ships with four named presets accessible from
  code (`ApplyRacingDefaults`, `ApplyJoustDefaults`,
  `ApplyCellularCaptureDefaults`, `ApplyFreestyleDefaults`) and via
  the asset's `Reset` context-menu item.

To deploy at runtime, drop a `TrainingAIDeploymentBridge` on the AI vessel
prefab next to its `AIPilot`, point it at your `TrainingArchiveSO`, and the
trained genome takes over from inspector defaults.

## Adding a new behavior

```csharp
public class MyAggressiveRamPolicy : IDecisionPolicy
{
    public string ModuleName => "AggressiveRam";

    const string GeneAggression = "ram.aggression";

    float _aggression;

    public void RegisterGenes()
    {
        GeneRegistry.Register(ModuleName, new GeneSpec(GeneAggression, 0f, 1f, 0.4f),
                              defaultEnabled: false);
    }

    public void OnEpisodeStart(TrainingGenome g) => _aggression = g.Get(GeneAggression);

    public DecisionOutput Decide(DecisionContext ctx)
    {
        if (ctx.Threats.Count == 0) return DecisionOutput.Zero;
        // ... return a steering vote ...
    }
}
```

Then register it in `PolicyBootstrap.EnsureInitialized`. New genes appear in
the editor window's Search Space tab immediately. Existing trained genomes
remain valid — missing genes fall back to their registered defaults.

## Adding a new fitness component

1. Implement `IFitnessComponent` in `Fitness/FitnessComponents.cs`.
2. Add an enum entry to `FitnessProfileSO.ComponentKind`.
3. Add the case to `FitnessComponentFactory.Create`.

Then any scenario that wants the new component just adds an entry in its
fitness profile inspector.

## Intensity dithering

The trained genome at intensity 4 is the god-tier pilot. Lower
intensities do not get their own mutated copy. `IntensityDitherer`
runs after the policy vote, on the decision only, and injects:

- input dropout (probability per frame to skip the new decision),
- gaussian steering noise,
- reaction delay (samples the input ring buffer N ms ago),
- ability-use skipping,
- throttle scaling.

Intensity 4 leaves the decision unchanged (throttle scale 1, every
probability 0). Intensity 1 is the heaviest degrade (dropout 0.30,
noise 0.40, delay 0.20 s, ability skip 0.50, throttle 0.75).
Intensities 2 and 3 sit between those.

The dithering lives in `TrainingPilot` and never calls `TrainingGenome.Set`.
Deployment clones the archive entry before `LoadGenome`, so a match
cannot write the stored genes. Push and auto-deploy always file the
vector under intensity 4.

Opt out on the Deploy tab (`Store a genome per intensity`) or on a
prefab bridge (`useDitheringForLowerIntensities = false`). That seat
then flies the genome stored for the requested intensity, raw. Without
that entry, `AIPilot` stays in control.

## Fitness profiles

Each live mode under `Assets/_SO_Assets/AI Training/` has one
`FitnessProfile_*` and one `Scenario_*`. The heavy row is the
`ScoringMetric` that mode already writes on this pilot's `RoundStats`.
Golf modes (HexRace, Crystal Capture, Joust, Rampage, Peel the Cage,
Wildlife Liberation, Dog Fight, The Bends, Salvo) also weight the
shared `Score` at 0.1; harvest negates it, because the winning domain's
score is finish time and every teammate on a domain shares that number.
Nucleus Rush, Astro League, and Scarab Scramble copy personal
`GoalsScored` into `Score`, so the recipe weights goals only.

`OpponentCount` on a scenario is the match size. HexRace stays at 3.
Every other catalog mode is 4 players at intensity 4. Vessel-locked
cards pin the hull (Squirrel, Dolphin, Rhino, Sparrow, or Scarab).
Crystal Capture defaults to Squirrel (the card also allows Manta and
Sparrow). Nucleus Rush defaults to Squirrel. Astro League defaults to
Rhino (the card also allows Scarab).

## BACKLOG

These modes have no scenario yet. They are outside the start list, or
the existing stats cannot express a per-pilot signal the mode does not
already write.

- Multiplayer Freestyle, Cellular Duel, Wildlife Blitz, and 2v2 Co-op
  vs AI. Freestyle has a distance/speed fallback recipe in code and no
  scenario asset.
- Tournament / Maelstrom. It chains other modes; it is not its own
  scoring surface.
- Single-player arcade cards whose scenes are gone.
- Nucleus Rush attribution. `GoalsScored` increments on one
  representative player of the controlling domain, then the mode sums
  by domain. The scenario ships anyway. A teammate who held the nucleus
  and did not receive the stamp scores zero brood. Do not add a second
  counter.
- Ribcage and Wildlife Liberation have no seek target for cage mass or
  fauna. Fitness still reads `HostilePrismsDestroyed` and
  `LifeformsKilled`. Do not add a sensor until one is asked for.

## What this framework does NOT do

- It doesn't spawn vessels. Whatever spawning the game already does
  (`ServerPlayerVesselInitializerWithAI`, single-player adapters, etc.)
  is what produces the vessels the runner trains on.
- It doesn't override transforms or physics. If you need an AI that can
  cheat through walls, you've outgrown this framework — and you've also
  trained an AI that won't transfer to multiplayer because the input it
  emits no longer corresponds to what it does.
- It doesn't ship machine-learning models. The "growing sophistication"
  hook is structural mutation over a registry of hand-written behavior
  modules; the search learns which to use and how to tune them. If you
  want neural-network policies, the existing scaffolding gives you an
  `IDecisionPolicy` with weights — wire a tiny MLP into it as a single
  module and add its weights to the registry.

## Performance notes

- The pilot allocates only its sensor scratch buffers up-front; per-frame
  work is bounded.
- `PrismSensor.OverlapSphereNonAlloc` is the dominant cost. If you train
  in scenes with many prism colliders, lower `prismScanRange` or set
  `prismLayerMask` to a tighter mask.
- The runner does not pause `Time.timeScale` between episodes. HexRace
  advances by `RequestReplay`, which reloads the scene.
- For overnight runs, lower vsync and target framerate in
  `BootstrapConfigSO` to match the wall clock you actually want — most of
  the time signal is in episode count, not real-time.
