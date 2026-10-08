# Branch archive: `claude/ai-training-tool-S3xw3`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-05 by Claude
- **Unmerged commits:** 5
- **Forked from:** `46fd37675` (2026-05-04, Merge pull request #508 from froglet-studio/claude/fix-client-permissions-JIrk)
- **Tip:** `5d82eb0de`
- **Files touched (92):**
  - `Assets/_Scripts/Utility/AITraining.meta`
  - `Assets/_Scripts/Utility/AITraining/Core.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/DecisionContext.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/DecisionContext.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/DecisionOutput.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/DecisionOutput.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/GeneSpec.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/GeneSpec.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/IDecisionPolicy.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/IDecisionPolicy.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/IntensityDitherer.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/IntensityDitherer.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/TrainingFitness.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/TrainingFitness.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Core/TrainingPopulation.cs`
  - `Assets/_Scripts/Utility/AITraining/Core/TrainingPopulation.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Editor.meta`
  - `Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs`
  - `Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Editor/TrainingPlayModeHook.cs`
  - `Assets/_Scripts/Utility/AITraining/Editor/TrainingPlayModeHook.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Fitness.meta`
  - `Assets/_Scripts/Utility/AITraining/Fitness/FitnessComponents.cs`
  - `Assets/_Scripts/Utility/AITraining/Fitness/FitnessComponents.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Fitness/FitnessProfileSO.cs`
  - `Assets/_Scripts/Utility/AITraining/Fitness/FitnessProfileSO.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Fitness/IFitnessComponent.cs`
  - `Assets/_Scripts/Utility/AITraining/Fitness/IFitnessComponent.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Persistence.meta`
  - `Assets/_Scripts/Utility/AITraining/Persistence/GenomeJson.cs`
  - `Assets/_Scripts/Utility/AITraining/Persistence/GenomeJson.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Persistence/TrainingArchiveSO.cs`
  - `Assets/_Scripts/Utility/AITraining/Persistence/TrainingArchiveSO.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Persistence/TrainingControlSO.cs`
  - `Assets/_Scripts/Utility/AITraining/Persistence/TrainingControlSO.cs.meta`
  - `Assets/_Scripts/Utility/AITraining/Pilot.meta`
  - … and 52 more

### `bcac0f52c` — feat(ai-training): general overnight evolutionary AI training framework

_Claude, 2026-05-04 23:15:59 +0000_

```text
Adds Assets/_Scripts/Utility/AITraining/, a self-contained tool that lets
anyone configure and run unattended evolutionary AI training in any
Cosmic Shore minigame, then deploy the trained pilot back into the game
via a single ScriptableObject asset.

Key design choices:

- Extensible search space. Behavior modules register their genes into a
  process-wide GeneRegistry. Adding a new behavior is one file in
  Policies/ plus a line in PolicyBootstrap; no central edit needed.

- Structural mutation. Genome stores numeric values AND a set of enabled
  module bits. Crossover and mutation flip module bits at a low rate so
  the search learns *which* behaviors to use, not just how to tune them.

- Novelty-augmented selection. Behavior fingerprints are hashed and
  compared against a rolling archive; rare genomes get a selection bonus
  so long overnight runs don't collapse onto a single local optimum.

- Per-game fitness recipes via FitnessProfileSO. Same trainer targets
  HexRace, CrystalCapture, Joust, etc. without code changes.

- One pilot for training and deployment. Both modes use TrainingPilot;
  what you train is exactly what ships.

- Hard input-only constraint. The pilot is allowed to write IInputStatus
  and call PerformShipControllerActions/StopShipControllerActions on the
  vessel. It is NOT allowed to touch the transform or set physics state.
  This keeps trained behavior transferable.

- Intensity dithering. Trained genome at intensity 4 is "flawless".
  IntensityDitherer injects per-frame dropout, gaussian steering noise,
  reaction delay, ability-skip, and throttle scaling for intensities 1-3.
  Dithering never modifies the genome.

- Persistence. TrainingSessionStateSO serializes the whole population +
  novelty archive, so editor domain reloads and machine restarts resume
  cleanly. TrainingArchiveSO is the deployable end product, indexed by
  (vessel, game mode, intensity).

Includes:
- Core: GeneRegistry, GeneSpec, TrainingGenome, TrainingPopulation,
  TrainingFitness, IDecisionPolicy, DecisionContext/Output, IntensityDitherer
- Policies: TargetSeeking, ObstacleAvoidance, ThrottleControl, Drift,
  Skim, BoostManagement, AbilityScheduler, ThreatEngagement
- Sensors: TargetSensor (crystals/enemies), PrismSensor (OverlapSphere),
  ThreatSensor (other vessels via gameData.Players)
- Fitness: 16 built-in components covering objective progress, survival,
  efficiency, ability use, distance, and per-stat scoring
- Pilot: TrainingPilot MonoBehaviour, TrainingAIDeploymentBridge
- Runner: TrainingScenarioSO, TrainingSessionStateSO,
  TrainingSessionRunner with watchdog, auto-deploy, and reset-for-replay
- Persistence: TrainingArchiveSO, GenomeJson sidecar export/import
- Telemetry: TrainingTelemetrySO with SOAP events
- Editor: FrogletTools/AI Training window with Run / Search Space /
  Archive / Schedule tabs
- Tests: Edit-mode tests for genome round-trip, evolution convergence,
  archive lookup, intensity dithering invariants

Builds on the lessons from claude/ai-pilot-intensity-levels-XK6ST and
claude/extend-ai-training-duration-vfMGG (extensible search space,
structural mutation, novelty bonus, modular fitness, deployment bridge,
editor orchestration).
```

```text
 Assets/_Scripts/Utility/AITraining/Policies/ThreatEngagementPolicy.cs |  73 +++++
 .../Utility/AITraining/Policies/ThreatEngagementPolicy.cs.meta        |   2 +
 Assets/_Scripts/Utility/AITraining/Policies/ThrottleControlPolicy.cs  |  79 ++++++
 .../Utility/AITraining/Policies/ThrottleControlPolicy.cs.meta         |   2 +
 Assets/_Scripts/Utility/AITraining/README.md                          | 171 ++++++++++++
 Assets/_Scripts/Utility/AITraining/README.md.meta                     |   7 +
 Assets/_Scripts/Utility/AITraining/Runner.meta                        |   8 +
 Assets/_Scripts/Utility/AITraining/Runner/TrainingScenarioSO.cs       |  80 ++++++
 Assets/_Scripts/Utility/AITraining/Runner/TrainingScenarioSO.cs.meta  |   2 +
 Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs    | 456 ++++++++++++++++++++++++++++++++
 .../_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs.meta  |   2 +
 Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionStateSO.cs   | 102 +++++++
 .../_Scripts/Utility/AITraining/Runner/TrainingSessionStateSO.cs.meta |   2 +
 Assets/_Scripts/Utility/AITraining/Sensors.meta                       |   8 +
 Assets/_Scripts/Utility/AITraining/Sensors/ITrainingSensor.cs         |  29 ++
 Assets/_Scripts/Utility/AITraining/Sensors/ITrainingSensor.cs.meta    |   2 +
 Assets/_Scripts/Utility/AITraining/Sensors/PrismSensor.cs             |  69 +++++
 Assets/_Scripts/Utility/AITraining/Sensors/PrismSensor.cs.meta        |   2 +
 Assets/_Scripts/Utility/AITraining/Sensors/TargetSensor.cs            | 139 ++++++++++
 Assets/_Scripts/Utility/AITraining/Sensors/TargetSensor.cs.meta       |   2 +
 Assets/_Scripts/Utility/AITraining/Sensors/ThreatSensor.cs            |  60 +++++
 Assets/_Scripts/Utility/AITraining/Sensors/ThreatSensor.cs.meta       |   2 +
 Assets/_Scripts/Utility/AITraining/Telemetry.meta                     |   8 +
 Assets/_Scripts/Utility/AITraining/Telemetry/TrainingTelemetrySO.cs   |  40 +++
 .../_Scripts/Utility/AITraining/Telemetry/TrainingTelemetrySO.cs.meta |   2 +
 Assets/_Scripts/Utility/AITraining/Tests.meta                         |   8 +
 Assets/_Scripts/Utility/AITraining/Tests/Editor.meta                  |   8 +
 .../_Scripts/Utility/AITraining/Tests/Editor/AITrainingCoreTests.cs   | 246 +++++++++++++++++
 .../Utility/AITraining/Tests/Editor/AITrainingCoreTests.cs.meta       |   2 +
 84 files changed, 4468 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 4511 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/AITraining/Core/DecisionContext.cs b/Assets/_Scripts/Utility/AITraining/Core/DecisionContext.cs
new file mode 100644
index 000000000..6ecf802f6
--- /dev/null
+++ b/Assets/_Scripts/Utility/AITraining/Core/DecisionContext.cs
@@ -0,0 +1,100 @@
+using System.Collections.Generic;
+using CosmicShore.Data;
+using CosmicShore.Gameplay;
+using UnityEngine;
+
+namespace CosmicShore.Utility.AITraining
+{
+    /// <summary>
+    /// Per-frame snapshot of everything a behavior policy is allowed to read.
+    /// Built once per Update by TrainingPilot, then handed to every active policy.
+    /// Keeping this immutable-after-build (we just clear-and-refill per frame) prevents
+    /// policies from interfering with each other through shared state.
+    ///
+    /// Note: only WORLD INFORMATION is exposed here. Policies do not get to mutate
+    /// the vessel — they emit a DecisionOutput, which the pilot writes to InputStatus.
+    /// </summary>
+    public class DecisionContext
+    {
+        // Identity
+        public IVessel Vessel;
+        public IVesselStatus VesselStatus;
+        public Domains MyDomain;
+        public string PlayerName;
+
+        // Self pose & motion
+        public Vector3 Position;
+        public Vector3 Forward;
+        public Vector3 Up;
+        public Vector3 Right;
+        public Vector3 Velocity;
+        public float Speed;
+        public bool IsBoosting;
+        public bool IsDrifting;
+        public bool IsStationary;
+        public bool IsAttached;
+        public bool GunsActive;
+        public bool HasLiveProjectiles;
+
+        // Resources
+        public float ChargedBoostCharge;
+        public bool IsChargedBoostDischarging;
+
+        // Targeting
+        public bool HasTarget;
+        public Vector3 TargetPosition;
+        public Vector3 TargetVelocity;
+        public TargetKind TargetKind;
+        public float TargetRange;
+
+        // Threats (other vessels, prisms, mines)
+        public readonly List<ThreatInfo> Threats = new(16);
+        public readonly List<PrismInfo> NearbyPrisms = new(32);
+
+        // Navigation
+        public Vector3 ObjectiveDirection;   // 0 if HasTarget == false
+        public float DotForwardObjective;    // -1..1
+        public float TimeSinceLastDamage;
+        public float TimeSinceLastObjectiveProgress;
+
+        // Episode bookkeeping
+        public float EpisodeTime;
+        public int EpisodeFrame;
+        public TrainingGenome Genome;        // Read-only handle for the current rollout
+
+        public void Clear()
+        {
+            Threats.Clear();
+            NearbyPrisms.Clear();
+            HasTarget = false;
+        }
+    }
+
+    public enum TargetKind
+    {
+        None = 0,
+        Crystal = 1,        // Crystal/objective collection
+        EnemyVessel = 2,    // Joust / shooter
+        EnemyTerritory = 3, // Cellular / capture
+        Waypoint = 4,       // Race
+        Friendly = 5        // Co-op partner
+    }
+
+    public struct ThreatInfo
+    {
+        public Vector3 Position;
+        public Vector3 Velocity;
+        public float Range;
+        public float Severity;   // 0..1, fitness-component-defined
+        public Domains Domain;
+    }
+
+    public struct PrismInfo
+    {
+        public Vector3 Position;
+        public Vector3 Forward;
+        public float Range;
+        public Domains Domain;
+        public bool IsHostile;
+    }
+}
diff --git a/Assets/_Scripts/Utility/AITraining/Core/DecisionOutput.cs b/Assets/_Scripts/Utility/AITraining/Core/DecisionOutput.cs
new file mode 100644
index 000000000..3b519e34f
--- /dev/null
+++ b/Assets/_Scripts/Utility/AITraining/Core/DecisionOutput.cs
@@ -0,0 +1,53 @@
+using System.Collections.Generic;
+using CosmicShore.Data;
+using UnityEngine;
+
+namespace CosmicShore.Utility.AITraining
+{
+    /// <summary>
+    /// What a single behavior policy contributes for one frame. Multiple policies
+    /// can run in parallel and their outputs are blended by the pilot before being
+    /// written to InputStatus.
+    ///
+    /// All steering values are in the vessel's local frame. Throttle is 0..1.
+    /// Booleans are sticky-per-frame requests: any policy voting true triggers the action.
+    /// </summary>
+    public struct DecisionOutput
+    {
+        public Vector2 SteerLocal;      // (yaw, pitch) in -1..1
+        public float SteerWeight;       // 0..1 — how much this contribution should count
+        public float Throttle;          // 0..1, additive
+        public float ThrottleWeight;
+        public float Roll;              // -1..1
+        public float RollWeight;
+        public bool RequestDrift;
+        public bool RequestRam;
+        public bool RequestFire;
+        public List<InputEvents> RequestActionsStart;
+        public List<InputEvents> RequestActionsStop;
+
+        public static DecisionOutput Zero => new()
+        {
+            SteerLocal = Vector2.zero,
+            SteerWeight = 0f,
+            Throttle = 0f,
+            ThrottleWeight = 0f,
+            Roll = 0f,
+            RollWeight = 0f
+        };
+
```

</details>

### `bb53c5089` — fix(ai-training): replace DefaultEnabledModules.Contains with IsDefaultEnabled method

_Claude, 2026-05-05 00:48:11 +0000_

```text
IReadOnlyCollection<string>.Contains(string) is not declared on the
interface, so the compiler falls back to extension method resolution.
Without LINQ in scope, MemoryExtensions.Contains(ReadOnlySpan<char>,
ReadOnlySpan<char>, StringComparison) wins, which doesn't match the
two-argument call and produces:

  CS7036: There is no argument given that corresponds to the required
  formal parameter 'comparisonType' of MemoryExtensions.Contains(...).

Fix: expose GeneRegistry.IsDefaultEnabled(string) as an explicit method
that runs against the underlying HashSet<string> directly. The public
DefaultEnabledModules property changes from IReadOnlyCollection<string>
to IEnumerable<string> since membership checks now go through the method.
```

```text
 Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs           | 9 ++++++++-
 Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs         | 4 ++--
 Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs | 2 +-
 3 files changed, 11 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs b/Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs
index d2f5458e6..813d75b43 100644
--- a/Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs
+++ b/Assets/_Scripts/Utility/AITraining/Core/GeneRegistry.cs
@@ -21,7 +21,14 @@ namespace CosmicShore.Utility.AITraining
 
         public static IReadOnlyDictionary<string, GeneSpec> Specs => s_Specs;
         public static IReadOnlyDictionary<string, List<string>> Modules => s_Modules;
-        public static IReadOnlyCollection<string> DefaultEnabledModules => s_DefaultEnabledModules;
+        public static IEnumerable<string> DefaultEnabledModules => s_DefaultEnabledModules;
+
+        /// <summary>
+        /// O(1) membership check. Exposed as a method rather than a property because
+        /// IReadOnlyCollection.Contains routes through MemoryExtensions for strings,
+        /// which requires an explicit StringComparison and fails to type-check.
+        /// </summary>
+        public static bool IsDefaultEnabled(string moduleName) => s_DefaultEnabledModules.Contains(moduleName);
 
         public static void Register(string moduleName, GeneSpec spec, bool defaultEnabled = true)
         {
diff --git a/Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs b/Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs
index f529d9263..5b84b7eff 100644
--- a/Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs
+++ b/Assets/_Scripts/Utility/AITraining/Core/TrainingGenome.cs
@@ -106,7 +106,7 @@ namespace CosmicShore.Utility.AITraining
                 g.Set(kv.Key, kv.Value.RandomValue());
             foreach (var moduleName in GeneRegistry.Modules.Keys)
             {
-                bool enabled = GeneRegistry.DefaultEnabledModules.Contains(moduleName)
+                bool enabled = GeneRegistry.IsDefaultEnabled(moduleName)
                     || UnityEngine.Random.value < moduleEnableProbability;
                 g.SetModuleEnabled(moduleName, enabled);
             }
@@ -174,7 +174,7 @@ namespace CosmicShore.Utility.AITraining
             foreach (var moduleName in GeneRegistry.Modules.Keys)
             {
                 if (UnityEngine.Random.value > structuralRate) continue;
-                if (GeneRegistry.DefaultEnabledModules.Contains(moduleName)) continue;
+                if (GeneRegistry.IsDefaultEnabled(moduleName)) continue;
                 SetModuleEnabled(moduleName, !IsModuleEnabled(moduleName));
             }
         }
diff --git a/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs b/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
index 049320411..b5abe7c90 100644
--- a/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
+++ b/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
@@ -243,7 +243,7 @@ namespace CosmicShore.Utility.AITraining.Editor
                         && !kv.Value.Any(g => g.ToLower().Contains(_moduleFilter.ToLower())))
                         continue;
 
-                    bool defaultOn = GeneRegistry.DefaultEnabledModules.Contains(kv.Key);
+                    bool defaultOn = GeneRegistry.IsDefaultEnabled(kv.Key);
                     EditorGUILayout.LabelField(
                         $"Module: {kv.Key} {(defaultOn ? "[default-on]" : "[default-off]")}",
                         EditorStyles.boldLabel);
```

</details>

### `0900665c9` — feat(ai-training): zero-config defaults and Quick Setup wizard

_Claude, 2026-05-05 15:05:31 +0000_

```text
Reduce first-time-user friction from 11 manual steps to 4 clicks. Anyone
can now: FrogletTools → AI Training → Quick Setup, open a game scene,
press Play, press Start Session.

Defaults populated:

- FitnessProfileSO.Reset() pre-fills with a racing/collection recipe
  (crystals, score, boost-time, time penalty, friendly-fire penalty)
  so a freshly-created profile trains usefully out of the box.
- Four named recipe presets are exposed as methods:
  ApplyRacingDefaults, ApplyJoustDefaults, ApplyCellularCaptureDefaults,
  ApplyFreestyleDefaults. Reset uses Racing.
- TrainingScenarioSO.Reset() picks HexRace + Manta + Intensity 4 and
  adds a CrystalsAtLeast=39 early-exit so winning rollouts close cleanly
  instead of waiting for the watchdog.
- TrainingSessionStateSO gets a parameterless Reset() (Unity callback)
  alongside the runtime ResetForScenario helper. The runtime caller in
  the runner is renamed accordingly.
- Runner falls back to an in-memory FitnessProfile picked by game mode
  when the scenario has none assigned. Racing for HexRace/Freestyle,
  Joust for joust modes, Cellular for capture/duel.

Editor window improvements:

- AutoDiscoverAssets at OnEnable + Re-Discover button — fills empty
  reference slots from the project so the user doesn't have to drag
  four assets in by hand.
- Quick Setup button + FrogletTools/AI Training/Quick Setup menu item.
  Creates Assets/_SO_Assets/AI Training/{Scenario_HexRace_Manta,
  SessionState, Archive, Telemetry, FitnessProfile_Default}, wires the
  fitness profile to the scenario, and snaps everything into the
  window's slots. Idempotent.
- HelpBox text updated to point at Quick Setup as the easy path.

Runner now AutoResolveReferences in OnEnable so a runner GameObject
dropped into a scene works without four manual serialized-field
assignments. Editor-only — uses AssetDatabase.FindAssets behind a
UNITY_EDITOR guard.

README's Quick Start section rewritten to reflect the new 4-click flow.
```

```text
 Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs   | 127 +++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Utility/AITraining/Fitness/FitnessProfileSO.cs      |  67 ++++++++++++++++++
 Assets/_Scripts/Utility/AITraining/README.md                        |  45 +++++++-----
 Assets/_Scripts/Utility/AITraining/Runner/TrainingScenarioSO.cs     |  34 +++++++++
 Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs  |  74 ++++++++++++++++++--
 Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionStateSO.cs |  16 ++++-
 6 files changed, 338 insertions(+), 25 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 477 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs b/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
index b5abe7c90..6698466b1 100644
--- a/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
+++ b/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
@@ -59,6 +59,27 @@ namespace CosmicShore.Utility.AITraining.Editor
         void OnEnable()
         {
             PolicyBootstrap.EnsureInitialized();
+            AutoDiscoverAssets();
+        }
+
+        /// <summary>
+        /// Looks for matching SO assets anywhere in the project so the user doesn't
+        /// have to drag four references in by hand on first open. Runs at every
+        /// OnEnable but only assigns slots that are currently empty.
+        /// </summary>
+        void AutoDiscoverAssets()
+        {
+            if (_scenario == null) _scenario = FirstAssetOfType<TrainingScenarioSO>();
+            if (_state == null) _state = FirstAssetOfType<TrainingSessionStateSO>();
+            if (_archive == null) _archive = FirstAssetOfType<TrainingArchiveSO>();
+            if (_telemetry == null) _telemetry = FirstAssetOfType<TrainingTelemetrySO>();
+        }
+
+        static T FirstAssetOfType<T>() where T : ScriptableObject
+        {
+            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
+            if (guids.Length == 0) return null;
+            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
         }
 
         void Update()
@@ -112,9 +133,29 @@ namespace CosmicShore.Utility.AITraining.Editor
                 _archive = (TrainingArchiveSO)EditorGUILayout.ObjectField("Archive", _archive, typeof(TrainingArchiveSO), false);
                 _telemetry = (TrainingTelemetrySO)EditorGUILayout.ObjectField("Telemetry", _telemetry, typeof(TrainingTelemetrySO), false);
 
+                EditorGUILayout.Space(4);
+                using (new EditorGUILayout.HorizontalScope())
+                {
+                    if (GUILayout.Button(new GUIContent("Quick Setup",
+                        "Creates a complete set of default training assets at " +
+                        "Assets/_SO_Assets/AI Training and wires them in. Press Play, " +
+                        "then Start Session, and walk away."), GUILayout.Height(24)))
+                    {
+                        QuickSetup();
+                    }
+                    if (GUILayout.Button(new GUIContent("Re-Discover Assets",
+                        "Searches the project for the four assets and assigns the first match in each empty slot."), GUILayout.Height(24)))
+                    {
+                        AutoDiscoverAssets();
+                    }
+                }
+
                 if (_scenario == null)
                 {
-                    EditorGUILayout.HelpBox("Assign a TrainingScenarioSO. Right-click in Project: Create → ScriptableObjects → AI Training → Scenario.", MessageType.Info);
+                    EditorGUILayout.HelpBox(
+                        "No scenario assigned. Press Quick Setup to create a default set of assets, " +
+                        "or right-click in Project: Create → ScriptableObjects → AI Training → Scenario.",
+                        MessageType.Info);
                     return;
                 }
 
@@ -206,6 +247,90 @@ namespace CosmicShore.Utility.AITraining.Editor
             _activeRunner = runner;
         }
 
+        // ─────────────────────────────────────────────
+        //  Quick Setup
+        // ─────────────────────────────────────────────
+
+        const string QuickSetupRoot = "Assets/_SO_Assets/AI Training";
+
+        [MenuItem("FrogletTools/AI Training/Quick Setup", false, 22)]
+        public static void QuickSetupMenuItem() => RunQuickSetup(focusWindow: true);
+
+        void QuickSetup()
+        {
+            RunQuickSetup(focusWindow: false);
+            // After setup, snap the just-created assets into our wiring slots.
+            _scenario = FirstAssetOfType<TrainingScenarioSO>();
+            _state = FirstAssetOfType<TrainingSessionStateSO>();
+            _archive = FirstAssetOfType<TrainingArchiveSO>();
+            _telemetry = FirstAssetOfType<TrainingTelemetrySO>();
+            Repaint();
+        }
+
+        /// <summary>
+        /// Creates (or loads) the standard default asset set under
+        /// <see cref="QuickSetupRoot"/>: a Scenario, a Session State, an Archive, a
+        /// Telemetry container, and a Fitness Profile. Wires them together so the
+        /// Run tab is one click away from training.
+        ///
+        /// Idempotent — running it twice does not overwrite existing assets, just
+        /// re-fills any empty cross-references.
+        /// </summary>
+        public static void RunQuickSetup(bool focusWindow)
+        {
+            EnsureFolder(QuickSetupRoot);
+
+            var fitness = LoadOrCreateAsset<FitnessProfileSO>(QuickSetupRoot + "/FitnessProfile_Default.asset",
+                so => so.ApplyRacingDefaults());
+
+            var scenario = LoadOrCreateAsset<TrainingScenarioSO>(QuickSetupRoot + "/Scenario_HexRace_Manta.asset",
+                _ => { /* TrainingScenarioSO.Reset already populates defaults */ });
+            if (scenario.FitnessProfile == null)
+            {
+                scenario.FitnessProfile = fitness;
+                EditorUtility.SetDirty(scenario);
+            }
+
+            var state = LoadOrCreateAsset<TrainingSessionStateSO>(QuickSetupRoot + "/SessionState.asset",
+                so => so.ResetForScenario(scenario.Key, scenario));
+
+            LoadOrCreateAsset<TrainingArchiveSO>(QuickSetupRoot + "/Archive.asset", _ => { });
+            LoadOrCreateAsset<TrainingTelemetrySO>(QuickSetupRoot + "/Telemetry.asset", _ => { });
+
+            AssetDatabase.SaveAssets();
+            AssetDatabase.Refresh();
+
+            if (focusWindow)
+            {
+                var w = GetWindow<TrainingEditorWindow>("AI Training");
+                w.AutoDiscoverAssets();
+                w.Repaint();
+            }
+
+            Debug.Log($"[AI Training] Quick Setup complete. Assets at: {QuickSetupRoot}\n" +
+                      $"Scenario: {scenario.name}, State: {state.name}, Fitness: {fitness.name}.");
+        }
+
+        static T LoadOrCreateAsset<T>(string path, System.Action<T> initialize) where T : ScriptableObject
+        {
+            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
+            if (existing != null) return existing;
+
+            var so = ScriptableObject.CreateInstance<T>();
+            initialize?.Invoke(so);
+            AssetDatabase.CreateAsset(so, path);
+            return so;
+        }
+
+        static void EnsureFolder(string assetPath)
+        {
+            if (AssetDatabase.IsValidFolder(assetPath)) return;
+            var parent = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
+            var leaf = System.IO.Path.GetFileName(assetPath);
+            EnsureFolder(parent);
+            AssetDatabase.CreateFolder(parent, leaf);
+        }
```

</details>

### `c8cd762d8` — feat(ai-training): one-click Learn flow drives full Bootstrap→Game→loop

_Claude, 2026-05-05 16:01:25 +0000_

```text
Adds a single Learn button in FrogletTools/AI Training that:

  1. Creates default assets if they don't exist (Quick Setup behaviour).
  2. Marks the active scenario for auto-launch via TrainingControlSO.
  3. Enters Play mode.

From there the entire flow runs without human input. AppManager boots,
auth signs in anonymously, Menu_Main loads. As soon as
ApplicationStateMachine reports MainMenu, a TrainingAutoLauncher
GameObject — installed by an InitializeOnLoad editor hook — overrides
GameDataSO with all-AI settings (3 racers, intensity 4) and calls
gameData.InvokeGameLaunch(). The same pipeline the arcade configure
modal uses then loads the game scene, starts the host, spawns AI
backfill via ServerPlayerVesselInitializerWithAI, and hands control to
the controller.

Once in the game scene, the auto-launcher:

  - Spawns or finds a TrainingSessionRunner and starts it.
  - Polls gameData.Players for the host's human player and flips its
    vessel onto autopilot via ToggleAIPilot(true), so all 3 racers are
    AI and the runner can train them.

The runner now:

  - Treats any vessel where AIPilot.AutoPilotEnabled is true as a
    training subject (in addition to IsInitializedAsAI), so the host's
    flipped vessel trains alongside the backfill.
  - Persists state.SetDirty + DeployBestToArchive after EVERY completed
    episode, not on a 5-minute timer. Interrupting the runner — by the
    Stop button or Unity's Play toggle — keeps everything that
    completed and discards only the in-flight match. The previous
    "EndEpisodeInternal force=true on Stop" path was removed: it was
    crediting incomplete matches and skewing the rolling-mean fitness.

Adds TrainingDeploymentService, a DontDestroyOnLoad MonoBehaviour
auto-installed via RuntimeInitializeOnLoadMethod. It listens to
gameData.OnPlayerPairInitialized in NORMAL play (gameData.IsTraining ==
false) and replaces each AI vessel's legacy AIPilot with a
TrainingPilot loaded from the archive. Lower intensities are produced
by the runtime ditherer. Toggle off via
TrainingControlSO.DeployArchiveInNormalPlay. This is what gives the
user "play HexRace yourself, opponents use the latest training" with
zero manual prefab edits.

New files:
  - Persistence/TrainingControlSO.cs — single editor↔runtime handoff
  - Runner/TrainingAutoLauncher.cs — drives Bootstrap→Auth→Menu→Game
  - Pilot/TrainingDeploymentService.cs — global deployment of trained
    archive in normal play
  - Editor/TrainingPlayModeHook.cs — InitializeOnLoad listener that
    spawns the auto-launcher on EnteredPlayMode

Modified:
  - Editor/TrainingEditorWindow.cs — adds DrawLearnHero with big Learn
    + Stop buttons, StartLearn / StopLearn, Quick Setup creates the
    control asset
  - Runner/TrainingSessionRunner.cs — interrupt-safe StopSession,
    per-episode persistence, relaxed ShouldTrainPlayer
  - README.md — rewritten Quick Start around the Learn flow
```

```text
 Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs     | 116 +++++++++++--
 Assets/_Scripts/Utility/AITraining/Editor/TrainingPlayModeHook.cs     |  79 +++++++++
 .../_Scripts/Utility/AITraining/Editor/TrainingPlayModeHook.cs.meta   |   2 +
 Assets/_Scripts/Utility/AITraining/Persistence/TrainingControlSO.cs   |  39 +++++
 .../_Scripts/Utility/AITraining/Persistence/TrainingControlSO.cs.meta |   2 +
 Assets/_Scripts/Utility/AITraining/Pilot/TrainingDeploymentService.cs | 161 ++++++++++++++++++
 .../Utility/AITraining/Pilot/TrainingDeploymentService.cs.meta        |   2 +
 Assets/_Scripts/Utility/AITraining/README.md                          |  86 +++++++---
 Assets/_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs     | 285 ++++++++++++++++++++++++++++++++
 .../_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs.meta   |   2 +
 Assets/_Scripts/Utility/AITraining/Runner/TrainingSessionRunner.cs    |  61 ++++++-
 11 files changed, 796 insertions(+), 39 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 948 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs b/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
index 6698466b1..e0204ace9 100644
--- a/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
+++ b/Assets/_Scripts/Utility/AITraining/Editor/TrainingEditorWindow.cs
@@ -126,7 +126,9 @@ namespace CosmicShore.Utility.AITraining.Editor
             {
                 _runScroll = scope.scrollPosition;
 
-                EditorGUILayout.Space(6);
+                DrawLearnHero();
+
+                EditorGUILayout.Space(8);
                 EditorGUILayout.LabelField("Asset Wiring", EditorStyles.boldLabel);
                 _scenario = (TrainingScenarioSO)EditorGUILayout.ObjectField("Scenario", _scenario, typeof(TrainingScenarioSO), false);
                 _state = (TrainingSessionStateSO)EditorGUILayout.ObjectField("Session State", _state, typeof(TrainingSessionStateSO), false);
@@ -136,15 +138,14 @@ namespace CosmicShore.Utility.AITraining.Editor
                 EditorGUILayout.Space(4);
                 using (new EditorGUILayout.HorizontalScope())
                 {
-                    if (GUILayout.Button(new GUIContent("Quick Setup",
-                        "Creates a complete set of default training assets at " +
-                        "Assets/_SO_Assets/AI Training and wires them in. Press Play, " +
-                        "then Start Session, and walk away."), GUILayout.Height(24)))
+                    if (GUILayout.Button(new GUIContent("Quick Setup (advanced)",
+                        "Creates the default asset set without entering Play mode. Use Learn for the one-click flow."),
+                        GUILayout.Height(22)))
                     {
                         QuickSetup();
                     }
                     if (GUILayout.Button(new GUIContent("Re-Discover Assets",
-                        "Searches the project for the four assets and assigns the first match in each empty slot."), GUILayout.Height(24)))
+                        "Searches the project for the assets and assigns the first match in each empty slot."), GUILayout.Height(22)))
                     {
                         AutoDiscoverAssets();
                     }
@@ -153,8 +154,7 @@ namespace CosmicShore.Utility.AITraining.Editor
                 if (_scenario == null)
                 {
                     EditorGUILayout.HelpBox(
-                        "No scenario assigned. Press Quick Setup to create a default set of assets, " +
-                        "or right-click in Project: Create → ScriptableObjects → AI Training → Scenario.",
+                        "No scenario assigned. Press Learn for the one-click flow, or Quick Setup to create the assets without entering play mode.",
                         MessageType.Info);
                     return;
                 }
@@ -247,6 +247,93 @@ namespace CosmicShore.Utility.AITraining.Editor
             _activeRunner = runner;
         }
 
+        // ─────────────────────────────────────────────
+        //  Learn (one-click)
+        // ─────────────────────────────────────────────
+        void DrawLearnHero()
+        {
+            EditorGUILayout.Space(6);
+            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
+            {
+                EditorGUILayout.LabelField("One-Click Training", EditorStyles.boldLabel);
+
+                bool isPlaying = Application.isPlaying;
+                bool hasSession = isPlaying && _activeRunner != null && _activeRunner.IsRunning;
+
+                using (new EditorGUILayout.HorizontalScope())
+                {
+                    using (new EditorGUI.DisabledScope(hasSession))
+                    {
+                        var label = isPlaying
+                            ? new GUIContent("Learn (running)", "Training is already running.")
+                            : new GUIContent("Learn",
+                                "Creates default assets if needed, marks the active scenario for auto-launch, " +
+                                "enters Play mode, and runs AI-vs-AI matches forever. Press Stop or Unity's " +
+                                "Stop button to interrupt — everything except the in-progress match is preserved.");
+                        if (GUILayout.Button(label, GUILayout.Height(40)))
+                            StartLearn();
+                    }
+
+                    using (new EditorGUI.DisabledScope(!isPlaying))
+                    {
+                        if (GUILayout.Button(new GUIContent("Stop",
+                            "Stops the running session and exits Play mode. The in-progress match is discarded; " +
+                            "all completed matches are already saved."), GUILayout.Height(40), GUILayout.Width(120)))
+                            StopLearn();
+                    }
+                }
+
+                if (!isPlaying)
+                {
+                    EditorGUILayout.HelpBox(
+                        "Press Learn. The tool will create defaults, enter Play, drive Bootstrap → Auth → Menu → " +
+                        "the scenario's game scene, and run AI-vs-AI matches in a loop. Walk away. Come back. " +
+                        "Press Stop. Your archive holds the best of every completed match.",
+                        MessageType.None);
+                }
+                else if (hasSession)
+                {
+                    EditorGUILayout.LabelField($"Generation {_telemetry?.Generation ?? 0}, " +
+                        $"Episode {_telemetry?.EpisodesCompleted ?? 0}, " +
+                        $"Best fitness {_telemetry?.CurrentBestFitness ?? 0:F1}");
+                }
+            }
+        }
+
+        void StartLearn()
+        {
+            // Step 1: ensure default assets exist.
+            RunQuickSetup(focusWindow: false);
+            AutoDiscoverAssets();
+
+            // Step 2: ensure a TrainingControlSO exists, point it at the active scenario,
+            // and flip AutoStartOnPlay. The play-mode hook (TrainingPlayModeHook) reads
+            // this when EnteredPlayMode fires and creates the auto-launcher.
+            var control = TrainingPlayModeHook.FindControlAsset();
+            if (control == null)
+                control = LoadOrCreateAsset<TrainingControlSO>(QuickSetupRoot + "/TrainingControl.asset", _ => { });
+            control.Scenario = _scenario;
+            control.State = _state;
+            control.Archive = _archive;
+            control.Telemetry = _telemetry;
+            control.AutoStartOnPlay = true;
+            EditorUtility.SetDirty(control);
+            AssetDatabase.SaveAssets();
+
+            // Step 3: enter Play mode. Unity loads the first scene in the build settings,
+            // which is Bootstrap; from there AppManager → Auth → Menu → game scene runs
+            // exactly as a normal launch would, just without ever waiting on user input.
+            EditorApplication.isPlaying = true;
+        }
+
+        void StopLearn()
+        {
+            if (_activeRunner != null && _activeRunner.IsRunning)
+                _activeRunner.StopSession();
+            // Exiting play mode triggers the hook's HandleExiting which clears AutoStartOnPlay.
+            EditorApplication.isPlaying = false;
+        }
+
         // ─────────────────────────────────────────────
         //  Quick Setup
         // ─────────────────────────────────────────────
@@ -294,8 +381,17 @@ namespace CosmicShore.Utility.AITraining.Editor
             var state = LoadOrCreateAsset<TrainingSessionStateSO>(QuickSetupRoot + "/SessionState.asset",
                 so => so.ResetForScenario(scenario.Key, scenario));
 
-            LoadOrCreateAsset<TrainingArchiveSO>(QuickSetupRoot + "/Archive.asset", _ => { });
-            LoadOrCreateAsset<TrainingTelemetrySO>(QuickSetupRoot + "/Telemetry.asset", _ => { });
+            var archive = LoadOrCreateAsset<TrainingArchiveSO>(QuickSetupRoot + "/Archive.asset", _ => { });
+            var telemetry = LoadOrCreateAsset<TrainingTelemetrySO>(QuickSetupRoot + "/Telemetry.asset", _ => { });
+
+            // Wire up the control asset so the Learn button + play-mode hook know
+            // which scenario to drive. AutoStartOnPlay stays false until Learn is pressed.
```

</details>

### `5d82eb0de` — fix(ai-training): launch on OnClientReady, not AppState.MainMenu

_Claude, 2026-05-05 17:33:01 +0000_

```text
Symptom: pressing Learn entered Play mode, the app reached Menu_Main, and
then sat there forever — the auto-launcher never advanced to HexRace.

Root cause: ApplicationStateMachine transitions to MainMenu BEFORE the
Menu_Main scene finishes loading and BEFORE MainMenuController.Start
runs. The previous handler:

  - configured GameDataSO with the scenario's vessel/intensity/player count
  - called gameData.InvokeGameLaunch() two frames later

Both raced MainMenuController.Start, which writes its own menu defaults
(Squirrel, intensity 1) into GameDataSO during ConfigureMenuGameData,
overwriting our scenario values. Worse, InvokeGameLaunch fired before
SceneLoader's OnLaunchGame listener had been set up for the new scene
in some Unity-version timings, leading to a silent no-op.

Fix: trigger off gameData.OnClientReady instead. That event is raised
exclusively by ClientPlayerVesselInitializer.InitializePair AFTER the
local user's player+vessel pair is initialised in the active scene —
i.e. AFTER Menu_Main is loaded, AFTER MainMenuController.Start has
finished writing its menu defaults, AFTER MainMenuController has
transitioned menu state to Ready. Configuring + launching from this
point sees a stable GameDataSO and a SceneLoader that's listening.

Also:

- IsMenuScene gate on the OnClientReady handler so subsequent
  OnClientReady invocations in game scenes (every match) don't try to
  re-launch.
- AppState.MainMenu handler is preserved as a 12s SAFETY TIMEOUT — if
  OnClientReady never fires (misconfigured menu prefab, no local player
  spawn), we launch anyway rather than hanging forever.
- Diagnostic Debug.Log on Start, on each event, on launch, and on every
  failure path so the next time something doesn't move past Menu_Main
  the Console says exactly why.
- Null-safe logging on launch summary (selectedVesselClass, intensity).
```

```text
 Assets/_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs | 106 ++++++++++++++++++++++++++++++++----
 1 file changed, 94 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 160 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs b/Assets/_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs
index 837b2ed62..82ff8708f 100644
--- a/Assets/_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs
+++ b/Assets/_Scripts/Utility/AITraining/Runner/TrainingAutoLauncher.cs
@@ -55,6 +55,7 @@ namespace CosmicShore.Utility.AITraining
 
         Coroutine _launchCo;
         Coroutine _hostAutopilotCo;
+        Coroutine _safetyCo;
 
         public TrainingScenarioSO Scenario => Control != null ? Control.Scenario : null;
 
@@ -68,12 +69,18 @@ namespace CosmicShore.Utility.AITraining
         {
             SceneManager.sceneLoaded -= HandleSceneLoaded;
             UnhookAppState();
+            UnhookGameDataEvents();
         }
 
         void Start()
         {
             ResolveProjectAssets();
+            HookGameDataEvents();
             HookAppState();
+            Debug.Log($"[TrainingAutoLauncher] Started. " +
+                      $"GameData={(_gameData != null ? _gameData.name : "null")}, " +
+                      $"AppState={(_appState != null ? _appState.name : "null")}, " +
+                      $"Scenario={(Scenario != null ? Scenario.Key : "null")}");
         }
 
         // ── Reference resolution ───────────────────────
@@ -96,14 +103,23 @@ namespace CosmicShore.Utility.AITraining
         }
 
         // ── App state events ───────────────────────────
+        // AppState.MainMenu transitions BEFORE Menu_Main scene loads and BEFORE
+        // MainMenuController.Start runs. Configuring + launching from here races
+        // MainMenuController.ConfigureMenuGameData (which writes its own values
+        // to GameDataSO and stomps ours) and InvokeGameLaunch fires before
+        // SceneLoader has its OnLaunchGame listener live for the new scene.
+        //
+        // The right primary trigger is gameData.OnClientReady — see HookGameDataEvents.
+        // The AppState handler stays as a SAFETY NET: if OnClientReady never fires
+        // within 12s of MainMenu (e.g. misconfigured menu prefab), we launch anyway.
         void HookAppState()
         {
-            if (_appState == null || _appState.Value == null || _appState.Value.OnStateChanged == null) return;
+            if (_appState == null || _appState.Value == null || _appState.Value.OnStateChanged == null)
+            {
+                Debug.LogWarning("[TrainingAutoLauncher] ApplicationStateData.OnStateChanged not wired; will rely on OnClientReady only.");
+                return;
+            }
             _appState.Value.OnStateChanged.OnRaised += HandleAppStateChanged;
-
-            // If we joined late (state already MainMenu by the time we listen),
-            // dispatch ourselves so the launch can proceed without waiting for
-            // a state machine that's already settled.
             if (_appState.Value.State == ApplicationState.MainMenu)
                 HandleAppStateChanged(ApplicationState.MainMenu);
         }
@@ -124,26 +140,92 @@ namespace CosmicShore.Utility.AITraining
                 return;
             }
 
+            Debug.Log("[TrainingAutoLauncher] AppState=MainMenu — waiting for OnClientReady (12s safety timeout).");
+            if (_safetyCo == null) _safetyCo = StartCoroutine(SafetyTimeout());
+        }
+
+        IEnumerator SafetyTimeout()
+        {
+            yield return new WaitForSeconds(12f);
+            if (_hasLaunched) yield break;
+            Debug.LogWarning("[TrainingAutoLauncher] OnClientReady didn't fire within 12s; launching anyway.");
+            _hasLaunched = true;
+            yield return ConfigureAndLaunch();
+        }
+
+        // ── Game data events ───────────────────────────
+        void HookGameDataEvents()
+        {
+            if (_gameData == null)
+            {
+                Debug.LogError("[TrainingAutoLauncher] GameDataSO not found in project; auto-launch will not work.");
+                return;
+            }
+            if (_gameData.OnClientReady == null)
+            {
+                Debug.LogWarning("[TrainingAutoLauncher] GameDataSO.OnClientReady is null; falling back to AppState-only trigger.");
+                return;
+            }
+            _gameData.OnClientReady.OnRaised += HandleClientReady;
+        }
+
+        void UnhookGameDataEvents()
+        {
+            if (_gameData == null || _gameData.OnClientReady == null) return;
+            _gameData.OnClientReady.OnRaised -= HandleClientReady;
+        }
+
+        void HandleClientReady()
+        {
+            if (_hasLaunched) return;
+            // OnClientReady fires every match (game scenes raise it after the local
+            // player vessel spawns). Only treat the menu invocation as the launch
+            // trigger; in-game invocations are a no-op for us.
+            string activeScene = SceneManager.GetActiveScene().name;
+            if (!IsMenuScene(activeScene))
+            {
+                Debug.Log($"[TrainingAutoLauncher] OnClientReady in '{activeScene}' (not menu) — ignoring.");
+                return;
+            }
+            if (Scenario == null)
+            {
+                Debug.LogError("[TrainingAutoLauncher] No scenario assigned on TrainingControlSO; cannot launch.");
+                return;
+            }
+
             _hasLaunched = true;
+            Debug.Log($"[TrainingAutoLauncher] OnClientReady in '{activeScene}' — configuring + launching {Scenario.Key}.");
             _launchCo = StartCoroutine(ConfigureAndLaunch());
         }
 
+        static bool IsMenuScene(string sceneName)
+        {
+            return !string.IsNullOrEmpty(sceneName)
+                && sceneName.IndexOf("menu", System.StringComparison.OrdinalIgnoreCase) >= 0;
+        }
+
         // ── Launch ─────────────────────────────────────
         IEnumerator ConfigureAndLaunch()
         {
-            // Give MainMenuController a frame to finish its own initialisation.
-            // It writes default vessel/intensity into GameDataSO during Initialising,
-            // and we want to overwrite those AFTER it's done so our values stick.
+            // OnClientReady fires after MainMenuController.HandleMenuReady finishes
+            // its own work, so by here MainMenuController has already written its
+            // menu defaults into GameDataSO. Two yields gives any same-frame listeners
+            // time to settle before we overwrite.
             yield return null;
             yield return null;
 
             ConfigureGameData();
-
-            // One more frame so listeners see the configured values before launch.
             yield return null;
+
+            if (_gameData.OnLaunchGame == null)
+            {
+                Debug.LogError("[TrainingAutoLauncher] GameDataSO.OnLaunchGame is null; cannot launch. Check the GameDataSO inspector.");
+                yield break;
```

</details>
