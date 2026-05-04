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
- Editor domain reloads can lose generation state mid-run.
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

## Quick start

1. **Create assets.** Right-click in Project, then:
   - `Create → ScriptableObjects → AI Training → Scenario` — defines what to train
   - `Create → ScriptableObjects → AI Training → Session State` — survives crashes
   - `Create → ScriptableObjects → AI Training → Archive` — deployable trained pilots
   - `Create → ScriptableObjects → AI Training → Telemetry` — runtime status surface
   - `Create → ScriptableObjects → AI Training → Fitness Profile` — per-game scoring
2. **Configure the scenario.** Set the vessel, game mode, intensity (start
   at 4), max episode seconds, and the fitness profile.
3. **Open a game scene** that already spawns AI players (e.g. `MinigameHexRace`).
4. **FrogletTools → AI Training.** Assign the four assets in the Run tab.
5. **Press Play in Unity.**
6. **Press Start Session in the window.** The runner installs a
   `TrainingPilot` on each AI vessel, hands them genomes, watches for the
   game to end, scores them, and loops.
7. **Walk away.** The session writes session state every generation and
   auto-deploys the best genome to the archive every 5 minutes (configurable).

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

The trained genome at intensity 4 is "flawless". Lower intensities are
produced by `IntensityDitherer` at runtime — it injects:
- input dropout (probability per frame to skip the new decision),
- gaussian steering noise,
- reaction delay (samples the input ring buffer N ms ago),
- ability-use skipping,
- throttle scaling.

The dithering lives entirely in `TrainingPilot.Apply` and never modifies
the genome itself. If a particular intensity needs its own trained pilot
rather than a dither, set `useDitheringForLowerIntensities = false` on
the deployment bridge and store explicit intensity-1/2/3 genomes in the
archive.

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
- The runner does not pause `Time.timeScale` between episodes — that
  would freeze the rest of the scene. It uses `gameData.ResetForReplay()`
  the same way the singleplayer flows do.
- For overnight runs, lower vsync and target framerate in
  `BootstrapConfigSO` to match the wall clock you actually want — most of
  the time signal is in episode count, not real-time.
