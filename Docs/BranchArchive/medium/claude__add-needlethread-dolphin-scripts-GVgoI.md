# Branch archive: `claude/add-needlethread-dolphin-scripts-GVgoI`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-24 by Claude
- **Unmerged commits:** 10
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/427
- **Forked from:** `fac70f182` (2026-03-24, Merge pull request #431 from froglet-studio/claude/fix-arcade-vessel-selection)
- **Tip:** `39f93e427`
- **Files touched (34):**
  - `Assets/_Prefabs/Environment/MiniGames/DartBoard.prefab`
  - `Assets/_Prefabs/Trails/GreenDartBlock.prefab`
  - `Assets/_SO_Assets/Effects/Vessel Crystal Effects/DolphinVesselChangeResourceByCrystalEffect.asset`
  - `Assets/_SO_Assets/Effects/Vessel Crystal Effects/DolphinVesselExplosionByCrystalEffect.asset`
  - `Assets/_SO_Assets/Games/ArcadeGameNeedleThreader.asset`
  - `Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameNeedleThreader.unity`
  - `Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs`
  - `Assets/_Scripts/Game/Arcade/NeedleThreadController.cs`
  - `Assets/_Scripts/Game/Arcade/NeedleThreadController.cs.meta`
  - `Assets/_Scripts/Game/Arcade/NeedleThreadScoreTracker.cs`
  - `Assets/_Scripts/Game/Arcade/NeedleThreadScoreTracker.cs.meta`
  - `Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs`
  - `Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs.meta`
  - `Assets/_Scripts/Game/Arcade/TurnMonitors/VolumeDestructionTurnMonitor.cs`
  - `Assets/_Scripts/Game/Arcade/TurnMonitors/VolumeDestructionTurnMonitor.cs.meta`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs`
  - `Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs`
  - `Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/PrismEffectHelper.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselChangeResourceByPrismEffectSO.cs`
  - `Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs`
  - `Assets/_Scripts/Game/Ship/DolphinVesselTelemetry.cs`
  - `Assets/_Scripts/Game/Ship/VesselTelemetryBootstrapper.cs`
  - `Assets/_Scripts/Game/UI/NeedleThreadHUD.cs`
  - `Assets/_Scripts/Game/UI/NeedleThreadHUD.cs.meta`
  - `Assets/_Scripts/Game/UI/NeedleThreadHUDView.cs`
  - `Assets/_Scripts/Game/UI/NeedleThreadHUDView.cs.meta`
  - `Assets/_Scripts/Game/UI/NeedleThreadScoreboard.cs`
  - `Assets/_Scripts/Game/UI/NeedleThreadScoreboard.cs.meta`
  - `Assets/_Scripts/Game/UI/UGSStatsManager.cs`
  - `Assets/_Scripts/Models/Enums/GameModes.cs`
  - `Assets/_Scripts/Utility/DataContainers/NeedleThreadEndGameController.cs`
  - `Assets/_Scripts/Utility/DataContainers/NeedleThreadEndGameController.cs.meta`

### `acac65182` — Add NeedleThread game mode scripts for Dolphin vessel

_Claude, 2026-03-20 00:18:20 +0000_

```text
New multiplayer game mode where Dolphin players race along a track,
collect crystals to charge explosions, and destroy dartboard structures.
Winner is first to reach hostile volume destruction threshold.

New scripts:
- NeedleThreadController: multiplayer race controller with volume-based win
- NeedleThreadScoreTracker: tracks elapsed time and volume destroyed
- VolumeDestructionTurnMonitor: ends turn when volume threshold reached
- NeedleThreadEndGameController: victory/defeat with time or echo hits left
- NeedleThreadScoreboard: formats winner time and loser echo hits
- NeedleThreadHUD/HUDView: live hostile volume destroyed display
- NeedleThreadStatsReporter: UGS telemetry reporting

Modified:
- GameModes enum: added NeedleThread = 39
- CallToActionTargetType: added PlayGameNeedleThread = 435
- UGSStatsManager: added ReportNeedleThreadStats method
- ArcadeGameNeedleThreader.asset: updated Mode, DisplayName, Description,
  SceneName, Vessels (Dolphin), and CTA target
```

```text
 Assets/_SO_Assets/Games/ArcadeGameNeedleThreader.asset                |  14 +-
 Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs    |   1 +
 Assets/_Scripts/Game/Arcade/NeedleThreadController.cs                 | 252 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Arcade/NeedleThreadScoreTracker.cs               | 144 ++++++++++++++++++
 Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs              |  60 ++++++++
 .../_Scripts/Game/Arcade/TurnMonitors/VolumeDestructionTurnMonitor.cs |  69 +++++++++
 Assets/_Scripts/Game/UI/NeedleThreadHUD.cs                            |  34 +++++
 Assets/_Scripts/Game/UI/NeedleThreadHUDView.cs                        |   6 +
 Assets/_Scripts/Game/UI/NeedleThreadScoreboard.cs                     |  76 ++++++++++
 Assets/_Scripts/Game/UI/UGSStatsManager.cs                            |  14 ++
 Assets/_Scripts/Models/Enums/GameModes.cs                             |   1 +
 .../_Scripts/Utility/DataContainers/NeedleThreadEndGameController.cs  |  78 ++++++++++
 12 files changed, 742 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 815 lines)</summary>

```diff
diff --git a/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs b/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs
index 1a33b8306..5742e128a 100644
--- a/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs
+++ b/Assets/_Scripts/App/Systems/CallToAction/CallToActionTargetType.cs
@@ -58,6 +58,7 @@ namespace CosmicShore.App.Systems.CTA
 
         PlayGameMultiplayerDogFight = 433,
         PlayGameMultiplayerMissileDogFight = 434,
+        PlayGameNeedleThread = 435,
 
         /*********** ADDED BY WILL *************/
 
diff --git a/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs b/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
new file mode 100644
index 000000000..0af722b41
--- /dev/null
+++ b/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
@@ -0,0 +1,252 @@
+using System.Linq;
+using Cysharp.Threading.Tasks;
+using Unity.Collections;
+using Unity.Netcode;
+using UnityEngine;
+using CosmicShore.Utility;
+
+namespace CosmicShore.Game.Arcade
+{
+    public class NeedleThreadController : MultiplayerDomainGamesController
+    {
+        [Header("Course")]
+        [SerializeField] SegmentSpawner segmentSpawner;
+        [SerializeField] int baseNumberOfSegments = 10;
+        [SerializeField] int baseStraightLineLength = 400;
+        [SerializeField] bool scaleNumberOfSegmentsWithIntensity = true;
+        [SerializeField] bool scaleLengthWithIntensity = true;
+
+        [Header("Helix")]
+        [SerializeField] SpawnableHelix helix;
+        [SerializeField] float helixIntensityScaling = 1.3f;
+
+        [Header("Seed")]
+        [SerializeField] int seed = 0;
+
+        [Header("Race Rules")]
+        [Tooltip("Volume of hostile prisms that must be destroyed to win. If 0, uses networked value.")]
+        [SerializeField] float volumeToFinishOverride = 0f;
+
+        int Intensity => Mathf.Max(1, gameData.SelectedIntensity.Value);
+
+        private bool _raceEnded;
+        private bool _trackSpawned;
+        private readonly NetworkVariable<int> _netTrackSeed = new(0);
+        private readonly NetworkVariable<float> _netVolumeToFinish = new(0f);
+
+        public string WinnerName { get; private set; } = "";
+        public bool RaceResultsReady { get; private set; } = false;
+
+        protected override bool UseGolfRules => true;
+
+        public override void OnNetworkSpawn()
+        {
+            base.OnNetworkSpawn();
+            numberOfRounds = 1;
+            numberOfTurnsPerRound = 1;
+
+            _netTrackSeed.OnValueChanged += OnTrackSeedChanged;
+
+            if (IsServer)
+            {
+                SpawnTrackEarly().Forget();
+            }
+            else if (_netTrackSeed.Value != 0)
+            {
+                SpawnTrackLocally(_netTrackSeed.Value);
+            }
+        }
+
+        public override void OnNetworkDespawn()
+        {
+            _netTrackSeed.OnValueChanged -= OnTrackSeedChanged;
+            base.OnNetworkDespawn();
+        }
+
+        private void OnTrackSeedChanged(int previousValue, int newValue)
+        {
+            if (newValue != 0)
+                SpawnTrackLocally(newValue);
+        }
+
+        private async UniTaskVoid SpawnTrackEarly()
+        {
+            await UniTask.Delay(1500, DelayType.UnscaledDeltaTime);
+            if (!IsServer || _trackSpawned) return;
+
+            int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
+            _netTrackSeed.Value = generatedSeed;
+        }
+
+        protected override void OnCountdownTimerEnded()
+        {
+            if (!IsServer) return;
+
+            if (_netTrackSeed.Value == 0)
+            {
+                int generatedSeed = (seed != 0) ? seed : Random.Range(int.MinValue, int.MaxValue);
+                _netTrackSeed.Value = generatedSeed;
+            }
+
+            base.OnCountdownTimerEnded();
+        }
+
+        private void SpawnTrackLocally(int trackSeed)
+        {
+            if (_trackSpawned || !segmentSpawner) return;
+            segmentSpawner.Seed = trackSeed;
+            segmentSpawner.NumberOfSegments = scaleNumberOfSegmentsWithIntensity
+                ? baseNumberOfSegments * Intensity
+                : baseNumberOfSegments;
+            segmentSpawner.StraightLineLength = scaleLengthWithIntensity
+                ? baseStraightLineLength / Intensity
+                : baseStraightLineLength;
+            ApplyHelixIntensity();
+            segmentSpawner.Initialize();
+            _trackSpawned = true;
+        }
+
+        void ApplyHelixIntensity()
+        {
+            if (!helix) return;
+            var radius = Intensity / helixIntensityScaling;
+            helix.firstOrderRadius = radius;
+            helix.secondOrderRadius = radius;
+        }
+
+        public void ReportLocalPlayerFinished(float finishTimeSeconds)
+        {
+            string myName = gameData.LocalPlayer.Name;
+            ReportPlayerFinished_ServerRpc(finishTimeSeconds, myName);
+        }
+
+        [ServerRpc(RequireOwnership = false)]
+        void ReportPlayerFinished_ServerRpc(float finishTimeSeconds, string playerName)
+        {
+            if (_raceEnded) return;
+            _raceEnded = true;
+
+            var winnerStats = gameData.RoundStatsList.FirstOrDefault(s => s.Name == playerName);
+            if (winnerStats == null)
+            {
+                CSDebug.LogError($"[NeedleThread] Could not find RoundStats for winner '{playerName}'. " +
```

</details>

### `e624f8647` — Fix missing CosmicShore.Soap using in NeedleThreadStatsReporter

_Claude, 2026-03-20 00:54:01 +0000_

```text
GameDataSO lives in CosmicShore.Soap namespace. The stats reporter
references it directly as a serialized field, unlike the controller
and score tracker which inherit it from base classes.
```

```text
 Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs b/Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs
index bc708ba24..b568caeb4 100644
--- a/Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs
+++ b/Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs
@@ -1,6 +1,7 @@
 using System.Linq;
 using CosmicShore.Core;
 using CosmicShore.Game.Analytics;
+using CosmicShore.Soap;
 using UnityEngine;
 using CosmicShore.Utility;
 
```

</details>

### `8f3c4996f` — Modify Needle Thread Configuration

_Shombith03, 2026-03-24 23:38:28 +0530_

```text
 Assets/_SO_Assets/Games/GameLists/OrganicRematchGames.asset           |   5 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameNeedleThreader.unity        | 272 +++++++++++++++++---------------
 Assets/_Scripts/Game/Arcade/NeedleThreadController.cs.meta            |   2 +
 Assets/_Scripts/Game/Arcade/NeedleThreadScoreTracker.cs.meta          |   2 +
 Assets/_Scripts/Game/Arcade/NeedleThreadStatsReporter.cs.meta         |   2 +
 .../Game/Arcade/TurnMonitors/VolumeDestructionTurnMonitor.cs.meta     |   2 +
 Assets/_Scripts/Game/UI/NeedleThreadHUD.cs.meta                       |   2 +
 Assets/_Scripts/Game/UI/NeedleThreadHUDView.cs.meta                   |   2 +
 Assets/_Scripts/Game/UI/NeedleThreadScoreboard.cs.meta                |   2 +
 .../Utility/DataContainers/NeedleThreadEndGameController.cs.meta      |   2 +
 10 files changed, 160 insertions(+), 133 deletions(-)
```

### `0ea83940d` — Fix Dolphin vessel: null-safe SkimmerImpactor and DolphinVesselTelemetry

_Claude, 2026-03-24 18:21:26 +0000_

```text
- SkimmerImpactor.OwnDomain and isInitialized now null-check the skimmer
  reference before accessing Domain/IsInitialized, preventing NullRef when
  collisions happen before VesselController.Initialize() completes

- Create DolphinVesselTelemetry with ExplosionsTriggered and VolumeDestroyed
  tracking (Dolphin previously fell through to DefaultVesselTelemetry)

- Register DolphinVesselTelemetry in VesselTelemetryBootstrapper switch
```

```text
 Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs |  4 ++--
 Assets/_Scripts/Game/Ship/DolphinVesselTelemetry.cs             | 34 ++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/Ship/VesselTelemetryBootstrapper.cs        |  1 +
 3 files changed, 37 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs b/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs
index 84d432720..1770d6717 100644
--- a/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/Impactors/SkimmerImpactor.cs
@@ -21,8 +21,8 @@ namespace CosmicShore.Game
 
         [Header("Refs")] [SerializeField] private Skimmer skimmer;
         public Skimmer Skimmer => skimmer;
-        public override Domains OwnDomain => Skimmer.Domain;
-        protected override bool isInitialized => Skimmer.IsInitialized;
+        public override Domains OwnDomain => skimmer != null && skimmer.IsInitialized ? skimmer.Domain : Domains.Unassigned;
+        protected override bool isInitialized => skimmer != null && skimmer.IsInitialized;
 
         // runtime state (moved from Skimmer)
         readonly Dictionary<string, float> _skimStartTimes = new();
diff --git a/Assets/_Scripts/Game/Ship/DolphinVesselTelemetry.cs b/Assets/_Scripts/Game/Ship/DolphinVesselTelemetry.cs
new file mode 100644
index 000000000..809453086
--- /dev/null
+++ b/Assets/_Scripts/Game/Ship/DolphinVesselTelemetry.cs
@@ -0,0 +1,34 @@
+namespace CosmicShore.Game
+{
+    /// <summary>
+    /// Vessel-specific telemetry for the Dolphin.
+    /// Adds on top of VesselTelemetry base (drift, boost, prisms damaged):
+    ///   - Explosions triggered (crystal collisions that spawned AOE)
+    ///   - Total hostile volume destroyed
+    ///
+    /// Dolphin's core loop: thread gaps to build charge → drift into crystal → AOE explosion → destroy structures.
+    /// </summary>
+    public class DolphinVesselTelemetry : VesselTelemetry
+    {
+        public int ExplosionsTriggered   { get; private set; }
+        public float VolumeDestroyed     { get; private set; }
+
+        protected override void ResetExtended()
+        {
+            ExplosionsTriggered = 0;
+            VolumeDestroyed     = 0f;
+        }
+
+        public void RecordExplosion()
+        {
+            if (!IsTracking) return;
+            ExplosionsTriggered++;
+        }
+
+        public void RecordVolumeDestroyed(float volume)
+        {
+            if (!IsTracking) return;
+            VolumeDestroyed += volume;
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Ship/VesselTelemetryBootstrapper.cs b/Assets/_Scripts/Game/Ship/VesselTelemetryBootstrapper.cs
index 333a9dcea..5811bd0fa 100644
--- a/Assets/_Scripts/Game/Ship/VesselTelemetryBootstrapper.cs
+++ b/Assets/_Scripts/Game/Ship/VesselTelemetryBootstrapper.cs
@@ -34,6 +34,7 @@ namespace CosmicShore.Game
             {
                 VesselClassType.Sparrow  => gameObject.AddComponent<SparrowVesselTelemetry>(),
                 VesselClassType.Squirrel => gameObject.AddComponent<SquirrelVesselTelemetry>(),
+                VesselClassType.Dolphin  => gameObject.AddComponent<DolphinVesselTelemetry>(),
                 _ => gameObject.AddComponent<DefaultVesselTelemetry>()
             };
 
```

</details>

### `e9192d2c5` — Fix Dolphin Artillery Cannon chain and add DartBoard to NeedleThread

_Claude, 2026-03-24 18:28:04 +0000_

```text
The Dolphin's charge→explosion ability was completely broken:

1. VesselChangeResourceByPrismEffectSO: Script used old API (halved
   resource instead of adding charge). Now uses ResourceChangeSpec
   matching the asset's _change field (adds 0.5 to Energy per prism hit)

2. DolphinVesselChangeResourceByCrystalEffect: Wrong resource index (1=Boost
   instead of 0=Energy). Fixed to consume Energy when hitting crystal.

3. DolphinVesselExplosionByCrystalEffect: Wrong resource index (1=Boost
   instead of 0=Energy). Fixed so explosion scales off accumulated charge.

4. NeedleThread scene: Added DartBoard prefab to SegmentSpawner's
   guaranteedSpawnables so dartboard structures spawn alongside the track.
```

```text
 .../Effects/Vessel Crystal Effects/DolphinVesselChangeResourceByCrystalEffect.asset          |  2 +-
 Assets/_SO_Assets/Effects/Vessel Crystal Effects/DolphinVesselExplosionByCrystalEffect.asset |  2 +-
 Assets/_Scenes/Multiplayer Scenes/MinigameNeedleThreader.unity                               |  3 ++-
 .../Game/ImpactEffects/EffectsSO/Vessel Prism Effects/VesselChangeResourceByPrismEffectSO.cs | 10 +++-------
 4 files changed, 7 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

### `6c09895ae` — Fix DartBoard prefab: wire greenPrism and redPrism references

_Claude, 2026-03-24 18:41:48 +0000_

```text
The DartBoard prefab had a stale 'trailBlock' field name that didn't match
the renamed 'greenPrism' (via FormerlySerializedAs("greenTrailBlock")),
causing both greenPrism and redPrism to be null at runtime. This produced
"The Object you want to instantiate is null" in SpawnLeafObjects.

Now correctly references GreenDartBlock and RedDartBlock prefabs.
```

```text
 Assets/_Prefabs/Environment/MiniGames/DartBoard.prefab | 4 +++-
 1 file changed, 3 insertions(+), 1 deletion(-)
```

### `937732688` — Fix DartBoard: distribute along track, hostile domains, NullRef fixes

_Claude, 2026-03-24 18:57:53 +0000_

```text
- GreenDartBlock: fix null onPrismVolumeModified and onPrismStolen SO refs
  that caused NullReferenceException in PrismScaleAnimator.ExecuteOnScaleComplete
- PrismScaleAnimator: add null-safety for onPrismVolumeModified.Raise()
- SpawnableDartBoard: add hostileToAll flag (Domains.None) so all prisms
  are hostile to every player's explosions
- DartBoard.prefab: enable hostileToAll by default
- SegmentSpawner: add ConfigureGuaranteedDistribution() for distributing
  guaranteed shapes along the track at each segment position
- NeedleThreadController: distribute DartBoards along track at 0.2x scale
  instead of one large cluster 420 units from origin
```

```text
 Assets/_Prefabs/Environment/MiniGames/DartBoard.prefab                |  1 +
 Assets/_Prefabs/Trails/GreenDartBlock.prefab                          |  5 +--
 Assets/_Scripts/Game/Arcade/NeedleThreadController.cs                 | 11 +++++++
 Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs    | 57 +++++++++++++++++++++++++++++++++
 .../_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs   | 10 ++++--
 Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs         | 11 ++++---
 6 files changed, 87 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 171 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs b/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
index 0af722b41..28a6681e6 100644
--- a/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
+++ b/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
@@ -23,6 +23,10 @@ namespace CosmicShore.Game.Arcade
         [Header("Seed")]
         [SerializeField] int seed = 0;
 
+        [Header("DartBoard Distribution")]
+        [Tooltip("Scale factor for DartBoards spawned along the track (1 = full size).")]
+        [SerializeField] float dartBoardScaleFactor = 0.2f;
+
         [Header("Race Rules")]
         [Tooltip("Volume of hostile prisms that must be destroyed to win. If 0, uses networked value.")]
         [SerializeField] float volumeToFinishOverride = 0f;
@@ -101,6 +105,13 @@ namespace CosmicShore.Game.Arcade
             segmentSpawner.StraightLineLength = scaleLengthWithIntensity
                 ? baseStraightLineLength / Intensity
                 : baseStraightLineLength;
+
+            // Distribute DartBoards along the track at each segment, scaled down
+            segmentSpawner.ConfigureGuaranteedDistribution(
+                alongTrack: true,
+                repeatCount: 1,
+                scaleFactor: dartBoardScaleFactor);
+
             ApplyHelixIntensity();
             segmentSpawner.Initialize();
             _trackSpawned = true;
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs
index 7f72afdc7..c1445fa75 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs
@@ -52,11 +52,27 @@ public class SegmentSpawner : MonoBehaviour
     [Tooltip("Radius of the ring that the guaranteed shapes are arranged in at the cluster point.")]
     [SerializeField] float guaranteedShapeClusterRadius = 80f;
 
+    // Runtime — distribution overrides (set via ConfigureGuaranteedDistribution before Initialize)
+    private bool _distributeAlongTrack;
+    private int _guaranteedRepeatCount = 1;
+    private float _guaranteedScaleFactor = 1f;
+
     // Runtime state
     private GameObject SpawnedSegmentContainer;
     private List<Trail> trails = new();
     private float[] _normalizedWeights;
 
+    /// <summary>
+    /// Configure guaranteed shape distribution along the track instead of in a single cluster.
+    /// Call before Initialize().
+    /// </summary>
+    public void ConfigureGuaranteedDistribution(bool alongTrack, int repeatCount = 1, float scaleFactor = 1f)
+    {
+        _distributeAlongTrack = alongTrack;
+        _guaranteedRepeatCount = Mathf.Max(1, repeatCount);
+        _guaranteedScaleFactor = scaleFactor;
+    }
+
     void Start()
     {
         MigrateLegacyFields();
@@ -199,6 +215,12 @@ public class SegmentSpawner : MonoBehaviour
     {
         if (guaranteedSpawnables == null || guaranteedSpawnables.Count == 0) return;
 
+        if (_distributeAlongTrack && StraightLineLength > 0)
+        {
+            SpawnGuaranteedAlongTrack(intensity);
+            return;
+        }
+
         var worldOrigin = origin + transform.position;
 
         // Pick a random direction from center for the cluster
@@ -239,6 +261,41 @@ public class SegmentSpawner : MonoBehaviour
         }
     }
 
+    void SpawnGuaranteedAlongTrack(int intensity)
+    {
+        var worldOrigin = origin + transform.position;
+        int totalSlots = NumberOfSegments * _guaranteedRepeatCount;
+
+        for (int slot = 0; slot < totalSlots; slot++)
+        {
+            var spawnable = guaranteedSpawnables[slot % guaranteedSpawnables.Count];
+            if (spawnable == null) continue;
+
+            if (Seed != 0) spawnable.SetSeed(Seed + 2000 + slot);
+
+            spawnable.InvalidateCache();
+            var spawned = spawnable.Spawn(intensity);
+            if (!spawned) continue;
+
+            spawned.transform.SetParent(SpawnedSegmentContainer.transform);
+
+            // Position at each segment along the track with a lateral offset so it doesn't overlap crystals
+            float z = slot * StraightLineLength + StraightLineLength * 0.5f;
+            float lateralOffset = guaranteedShapeClusterRadius;
+            // Alternate sides of the track
+            float side = (slot % 2 == 0) ? 1f : -1f;
+            spawned.transform.position = worldOrigin + new Vector3(side * lateralOffset, 0f, z);
+
+            // Face along the track
+            spawned.transform.rotation = Quaternion.LookRotation(Vector3.forward);
+
+            if (_guaranteedScaleFactor != 1f)
+                spawned.transform.localScale *= _guaranteedScaleFactor;
+
+            trails.AddRange(spawnable.GetTrails());
+        }
+    }
+
     public void NukeTheTrails()
     {
         trails.Clear();
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs
index 9ed253ff4..be4eb1465 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SpawnableDartBoard.cs
@@ -12,6 +12,12 @@ public class SpawnableDartBoard : SpawnableBase
     [SerializeField] float ringThickness = 5f;
     [SerializeField] float gap = 6;
 
+    [Header("Domain")]
+    [Tooltip("When true, all blocks use Domains.None so they are hostile to every player's explosions.")]
+    [SerializeField] bool hostileToAll;
+
+    public bool HostileToAll { get => hostileToAll; set => hostileToAll = value; }
+
     protected override SpawnTrailData[] GenerateTrailData()
     {
         var trailDataList = new SpawnTrailData[ringCount];
@@ -69,12 +75,12 @@ public class SpawnableDartBoard : SpawnableBase
                 if ((block / ring + ring / 3) % 2 == 0)
                 {
                     prism = greenPrism;
-                    blockDomain = Domains.Jade;
+                    blockDomain = hostileToAll ? Domains.None : Domains.Jade;
                 }
                 else
                 {
                     prism = redPrism;
-                    blockDomain = Domains.Ruby;
+                    blockDomain = hostileToAll ? Domains.None : Domains.Ruby;
                 }
 
                 var point = td.Points[block];
diff --git a/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs b/Assets/_Scripts/Game/Environment/Prisms/PrismScaleAnimator.cs
index 86ee2f4d5..925cb2b57 100644
```

</details>

### `1898dde5b` — Fix DartBoard scale to 0.5x and suppress no-player error during init

_Claude, 2026-03-24 19:06:14 +0000_

```text
- NeedleThreadController: change dartBoardScaleFactor from 0.2 to 0.5
  (half of original size, not 5x smaller which made them invisible)
- PrismEffectHelper: silently skip damage when Player is null instead of
  spamming LogError — this happens when vessel collides with prisms
  before player assignment completes during game start
```

```text
 Assets/_Scripts/Game/Arcade/NeedleThreadController.cs                     |  2 +-
 Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/PrismEffectHelper.cs | 10 ++--------
 2 files changed, 3 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs b/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
index 28a6681e6..e207ef878 100644
--- a/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
+++ b/Assets/_Scripts/Game/Arcade/NeedleThreadController.cs
@@ -25,7 +25,7 @@ namespace CosmicShore.Game.Arcade
 
         [Header("DartBoard Distribution")]
         [Tooltip("Scale factor for DartBoards spawned along the track (1 = full size).")]
-        [SerializeField] float dartBoardScaleFactor = 0.2f;
+        [SerializeField] float dartBoardScaleFactor = 0.5f;
 
         [Header("Race Rules")]
         [Tooltip("Volume of hostile prisms that must be destroyed to win. If 0, uses networked value.")]
diff --git a/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/PrismEffectHelper.cs b/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/PrismEffectHelper.cs
index a36d69b4e..bc2b0d160 100644
--- a/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/PrismEffectHelper.cs
+++ b/Assets/_Scripts/Game/ImpactEffects/EffectsSO/Helpers/PrismEffectHelper.cs
@@ -8,12 +8,9 @@ namespace CosmicShore.Game
         /// Override if you want a different damage formula.
         public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 course, float speed)
         {
-            // Default: Course * Speed * inertia
+            // Skip silently when player hasn't been assigned yet (e.g. during initialization)
             if (status.Player == null)
-            {
-                CSDebug.LogError("No player found to deal damage to prism!");
                 return;
-            }
             
             var damage= course * speed * inertia;
             prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
@@ -21,12 +18,9 @@ namespace CosmicShore.Game
         
         public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 Velocity)
         {
-            // Default: Course * Speed * inertia
+            // Skip silently when player hasn't been assigned yet (e.g. during initialization)
             if (status.Player == null)
-            {
-                CSDebug.LogError("No player found to deal damage to prism!");
                 return;
-            }
             
             var damage= Velocity * inertia;
             prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
```

</details>

### `39f93e427` — Fix DartBoard distribution: instantiate independent spawnable copies

_Claude, 2026-03-24 19:21:53 +0000_

```text
The previous approach called Spawn() repeatedly on the same prefab-based
SpawnableBase reference, which caused cache/state issues resulting in only
one DartBoard appearing. Now instantiates an independent copy of the
SpawnableDartBoard for each segment position, calls Spawn() on the fresh
instance, then destroys the temporary spawnable. Each Spawn() call has
clean state and produces a fully initialized DartBoard.
```

```text
 Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs | 44 ++++++++++++++++++++----------------
 1 file changed, 24 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs b/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs
index c1445fa75..3cf398813 100644
--- a/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs
+++ b/Assets/_Scripts/Game/Environment/MiniGameObjects/SegmentSpawner.cs
@@ -264,35 +264,39 @@ public class SegmentSpawner : MonoBehaviour
     void SpawnGuaranteedAlongTrack(int intensity)
     {
         var worldOrigin = origin + transform.position;
-        int totalSlots = NumberOfSegments * _guaranteedRepeatCount;
 
-        for (int slot = 0; slot < totalSlots; slot++)
+        foreach (var spawnablePrefab in guaranteedSpawnables)
         {
-            var spawnable = guaranteedSpawnables[slot % guaranteedSpawnables.Count];
-            if (spawnable == null) continue;
+            if (spawnablePrefab == null) continue;
 
-            if (Seed != 0) spawnable.SetSeed(Seed + 2000 + slot);
+            for (int seg = 0; seg < NumberOfSegments; seg++)
+            {
+                // Instantiate an independent copy of the spawnable so each
+                // Spawn() call has its own cache/state — avoids issues with
+                // repeated Spawn() on the same prefab reference.
+                var spawnableInstance = Instantiate(spawnablePrefab);
+                spawnableInstance.gameObject.SetActive(false);
 
-            spawnable.InvalidateCache();
-            var spawned = spawnable.Spawn(intensity);
-            if (!spawned) continue;
+                if (Seed != 0) spawnableInstance.SetSeed(Seed + 2000 + seg);
 
-            spawned.transform.SetParent(SpawnedSegmentContainer.transform);
+                var spawned = spawnableInstance.Spawn(intensity);
 
-            // Position at each segment along the track with a lateral offset so it doesn't overlap crystals
-            float z = slot * StraightLineLength + StraightLineLength * 0.5f;
-            float lateralOffset = guaranteedShapeClusterRadius;
-            // Alternate sides of the track
-            float side = (slot % 2 == 0) ? 1f : -1f;
-            spawned.transform.position = worldOrigin + new Vector3(side * lateralOffset, 0f, z);
+                // Grab trails before destroying the temporary spawnable
+                trails.AddRange(spawnableInstance.GetTrails());
+                Destroy(spawnableInstance.gameObject);
 
-            // Face along the track
-            spawned.transform.rotation = Quaternion.LookRotation(Vector3.forward);
+                if (!spawned) continue;
 
-            if (_guaranteedScaleFactor != 1f)
-                spawned.transform.localScale *= _guaranteedScaleFactor;
+                spawned.transform.SetParent(SpawnedSegmentContainer.transform);
 
-            trails.AddRange(spawnable.GetTrails());
+                float z = seg * StraightLineLength + StraightLineLength * 0.5f;
+                float side = (seg % 2 == 0) ? 1f : -1f;
+                spawned.transform.position = worldOrigin + new Vector3(side * guaranteedShapeClusterRadius, 0f, z);
+                spawned.transform.rotation = Quaternion.LookRotation(Vector3.forward);
+
+                if (_guaranteedScaleFactor != 1f)
+                    spawned.transform.localScale = Vector3.one * _guaranteedScaleFactor;
+            }
         }
     }
 
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
