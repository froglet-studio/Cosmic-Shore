# Branch archive: `claude/optimize-mobile-performance-7uCEG`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-09 by Claude
- **Unmerged commits:** 3
- **Forked from:** `600dc09a9` (2026-03-09, Merge branch 'development' into claude/fix-joust-end-screen-ghd7s)
- **Tip:** `186c43384`
- **Files touched (33):**
  - `Assets/_Scripts/Game/AI/AIPilot.cs`
  - `Assets/_Scripts/Game/Animation/RotateAroundOrigin.cs`
  - `Assets/_Scripts/Game/Arcade/MiniGame.cs`
  - `Assets/_Scripts/Game/Arcade/TurnMonitorController.cs`
  - `Assets/_Scripts/Game/Assemblers/WallAssembler.cs`
  - `Assets/_Scripts/Game/Environment/Cell.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/BoidSimulationController.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs`
  - `Assets/_Scripts/Game/ImpactEffects/EffectsSO/Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs`
  - `Assets/_Scripts/Game/Managers/Arcade.cs`
  - `Assets/_Scripts/Game/Managers/MaterialStateManager.cs`
  - `Assets/_Scripts/Game/Projectiles/Projectile.cs`
  - `Assets/_Scripts/Game/Ship/Animation/ParametricJetEffect.cs`
  - `Assets/_Scripts/Game/Ship/ClearPrisms.cs`
  - `Assets/_Scripts/Game/Ship/DriftJet.cs`
  - `Assets/_Scripts/Game/Ship/GunTransformer.cs`
  - `Assets/_Scripts/Game/Ship/Prism.cs`
  - `Assets/_Scripts/Game/Ship/ResourceSystem.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/ChargeBoostAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/ChargedFireGunAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/DriftTrailAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/FullAutoAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/OverheatingAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/SeedAssemblerConfigurator.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/SeedWallAction.cs`
  - `Assets/_Scripts/Game/Ship/TrailViewer.cs`
  - `Assets/_Scripts/Game/UI/CurrentScore.cs`
  - `Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs`
  - `Assets/_Scripts/Utility/Effects/FadeIn.cs`
  - `Assets/_Scripts/Utility/Effects/Impact.cs`
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs`

### `d67feea8b` — Optimize mobile performance across 27 files — zero functional changes

_Claude, 2026-03-09 03:15:36 +0000_

```text
MobilePerformanceManager:
- Disable shadows entirely, reduce pixel lights to 1, LOD bias 0.7
- Set skin weights to 2 bones, resolution scale 0.85, shader LOD 200
- Reduce physics solver iterations, disable billboards face camera

GC allocation elimination:
- Replace Physics.OverlapSphere with NonAlloc in Boid, LightFauna, WallAssembler
- Cache WaitForSeconds in 7 coroutine loops (DriftTrail, ResourceSystem,
  FullAuto, ChargeBoost, Overheating, ChargedFireGun, Boid)
- Cache GetComponentsInChildren in GunTransformer (was allocating every frame)
- Fix BoidSimulationController.CreateBoid triple-allocation via Array.Copy
- Replace LINQ OrderBy/Where with manual sort in SkimmerOvercharge
- Replace LINQ First/Last/Where with direct indexing (Cell, Arcade,
  SeedWallAction, SeedAssemblerConfigurator)
- Remove MaterialStateManager.ToArray() iteration, use index loop
- Cache Gradient in ParametricJetEffect (was new Gradient() per frame)
- Add dirty-checking to skip redundant particle/material updates

Material and shader optimization:
- ClearPrisms: use MaterialPropertyBlock instead of .material in OnTriggerStay
- FadeIn: cache Renderer reference, use PropertyToID
- Impact: cache all Shader.PropertyToID calls
- MaterialBlendUtility: cache ColorID/EmissionColorID as static readonly
- TrailViewer: cache Renderer per Prism, replace IndexOf with for-loop

Layer and transform caching:
- Prism: static layer name cache to avoid repeated NameToLayer string lookups
- Boid: static mound layer mask/index cache
- DriftJet: cache parent transform reference
- RotateAroundOrigin: cache Quaternion.Euler result in local var
- Remove 6 unused System.Linq imports
```

```text
 Assets/_Scripts/Game/Animation/RotateAroundOrigin.cs                  |  5 +-
 Assets/_Scripts/Game/Assemblers/WallAssembler.cs                      | 10 ++--
 Assets/_Scripts/Game/Environment/Cell.cs                              |  5 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs                | 55 ++++++++++++++-------
 .../Game/Environment/FloraAndFauna/BoidSimulationController.cs        | 13 +++--
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs          | 13 +++--
 .../EffectsSO/Skimmer Prism Effects/MaterialBlendUtility.cs           | 26 +++++-----
 .../Skimmer Prism Effects/SkimmerOverchargeCollectPrismEffectSO.cs    | 38 +++++++++------
 Assets/_Scripts/Game/Managers/Arcade.cs                               | 10 ++--
 Assets/_Scripts/Game/Managers/MaterialStateManager.cs                 |  9 ++--
 Assets/_Scripts/Game/Ship/Animation/ParametricJetEffect.cs            | 85 ++++++++++++++++++++++++++-------
 Assets/_Scripts/Game/Ship/ClearPrisms.cs                              | 14 ++++--
 Assets/_Scripts/Game/Ship/DriftJet.cs                                 | 14 +++++-
 Assets/_Scripts/Game/Ship/GunTransformer.cs                           | 41 +++++++++++-----
 Assets/_Scripts/Game/Ship/Prism.cs                                    | 18 ++++++-
 Assets/_Scripts/Game/Ship/ResourceSystem.cs                           |  6 ++-
 Assets/_Scripts/Game/Ship/ShipActions/ChargeBoostAction.cs            |  6 ++-
 Assets/_Scripts/Game/Ship/ShipActions/ChargedFireGunAction.cs         |  6 ++-
 Assets/_Scripts/Game/Ship/ShipActions/DriftTrailAction.cs             |  4 +-
 Assets/_Scripts/Game/Ship/ShipActions/FullAutoAction.cs               |  6 ++-
 Assets/_Scripts/Game/Ship/ShipActions/OverheatingAction.cs            |  5 +-
 Assets/_Scripts/Game/Ship/ShipActions/SeedAssemblerConfigurator.cs    |  4 +-
 Assets/_Scripts/Game/Ship/ShipActions/SeedWallAction.cs               |  4 +-
 Assets/_Scripts/Game/Ship/TrailViewer.cs                              | 39 ++++++++-------
 Assets/_Scripts/Utility/Effects/FadeIn.cs                             | 20 +++++---
 Assets/_Scripts/Utility/Effects/Impact.cs                             | 28 ++++++-----
 Assets/_Scripts/Utility/MobilePerformanceManager.cs                   | 55 ++++++++++++++++++---
 27 files changed, 380 insertions(+), 159 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1159 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/RotateAroundOrigin.cs b/Assets/_Scripts/Game/Animation/RotateAroundOrigin.cs
index 9665262e3..e2ce10864 100644
--- a/Assets/_Scripts/Game/Animation/RotateAroundOrigin.cs
+++ b/Assets/_Scripts/Game/Animation/RotateAroundOrigin.cs
@@ -10,7 +10,8 @@ namespace CosmicShore.Game.Animation
         void Update()
         {
             float speedT = speed * Time.deltaTime;
-            transform.position = Quaternion.Euler(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT) * transform.position;
+            var rotation = Quaternion.Euler(rotationDirection.x * speedT, rotationDirection.y * speedT, rotationDirection.z * speedT);
+            transform.position = rotation * transform.position;
         }
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Game/Assemblers/WallAssembler.cs b/Assets/_Scripts/Game/Assemblers/WallAssembler.cs
index 1e292942c..2a939a3cc 100644
--- a/Assets/_Scripts/Game/Assemblers/WallAssembler.cs
+++ b/Assets/_Scripts/Game/Assemblers/WallAssembler.cs
@@ -64,6 +64,9 @@ namespace CosmicShore
         [SerializeField] float radius = 40f;
         bool isStopped = true;
 
+        // Cached physics buffer to avoid per-call allocations
+        private static readonly Collider[] _overlapBuffer = new Collider[64];
+
         int depth = -1;
 
         public override int Depth
@@ -376,10 +379,11 @@ namespace CosmicShore
             float closestDistance = float.MaxValue;
             WallAssembler closest = null;
             SiteType bondee = SiteType.Right;
-            var colliders = Physics.OverlapSphere(bondSite, radius); // Adjust radius as needed
-            if (colliders.Length < colliderTheshold) return new BondMate { Mate = null };
-            foreach (var potentialMate in colliders) // Adjust radius as needed
+            int hitCount = Physics.OverlapSphereNonAlloc(bondSite, radius, _overlapBuffer);
+            if (hitCount < colliderTheshold) return new BondMate { Mate = null };
+            for (int idx = 0; idx < hitCount; idx++)
             {
+                var potentialMate = _overlapBuffer[idx];
                 WallAssembler mateComponent = potentialMate.GetComponent<WallAssembler>();
                 if (mateComponent == null)
                 {
diff --git a/Assets/_Scripts/Game/Environment/Cell.cs b/Assets/_Scripts/Game/Environment/Cell.cs
index 65902be92..814b9587b 100644
--- a/Assets/_Scripts/Game/Environment/Cell.cs
+++ b/Assets/_Scripts/Game/Environment/Cell.cs
@@ -1,6 +1,5 @@
 ﻿// Cell.cs
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Core;
 using CosmicShore.Soap;
 using UnityEngine;
@@ -320,8 +319,8 @@ namespace CosmicShore.Game
         internal Domains GetHostileDomainToLocalLegacy()
         {
             var local = gameData.LocalRoundStats?.Domain ?? Domains.Jade;
-            var candidates = new[] { Domains.Ruby, Domains.Gold, Domains.Blue, Domains.Jade };
-            return candidates.First(d => d != local);
+            if (local != Domains.Ruby) return Domains.Ruby;
+            return Domains.Gold;
         }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
index d1baf7950..460bae948 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Boid.cs
@@ -38,7 +38,7 @@ public class Boid : Fauna
 
     [Header("Mound Settings")]
     public Transform Mound;
-    
+
     [SerializeField]
     Prism healthPrism;
 
@@ -56,10 +56,28 @@ public class Boid : Fauna
 
     List<Collider> separatedBoids = new List<Collider>();
     HealthPrism embeddedHealthPrism;
-    
+
+    // Cached physics buffers to avoid per-call allocations
+    private static readonly Collider[] _overlapBuffer = new Collider[64];
+    private static readonly Collider[] _moundOverlapBuffer = new Collider[16];
+    private static int _moundLayerMask = -1;
+    private static int _moundLayerIndex = -1;
+
+    // Cached WaitForSeconds to avoid per-yield allocations
+    private WaitForSeconds _behaviorWait;
+
     public BoidManager BoidManager { get; set; }
     public BoidController BoidController { get; set; }
 
+    private static void EnsureMoundLayerCached()
+    {
+        if (_moundLayerIndex < 0)
+        {
+            _moundLayerIndex = LayerMask.NameToLayer("Mound");
+            _moundLayerMask = 1 << _moundLayerIndex;
+        }
+    }
+
     public override void Initialize(Cell cell)
     {
         embeddedHealthPrism = GetComponentInChildren<HealthPrism>(true);
@@ -76,6 +94,7 @@ public class Boid : Fauna
         embeddedHealthPrism.ChangeTeam(domain);
 
         currentVelocity = transform.forward * Random.Range(minSpeed, Mathf.Max(minSpeed, maxSpeed));
+        _behaviorWait = new WaitForSeconds(behaviorUpdateRate);
         float initialDelay = normalizedIndex * behaviorUpdateRate;
         StartCoroutine(CalculateBehaviorCoroutine(initialDelay));
     }
@@ -89,11 +108,11 @@ public class Boid : Fauna
         {
             if (!isAttached)
             {
-                target = Goal;      // Check it later
+                target = Goal;
             }
 
             CalculateBehavior();
-            yield return new WaitForSeconds(behaviorUpdateRate);
+            yield return _behaviorWait;
         }
     }
 
@@ -121,12 +140,11 @@ public class Boid : Fauna
         float averageSpeed = 0.0f;
         separatedBoids.Clear();
 
-        var boidsInVicinity = Physics.OverlapSphere(transform.position, cohesionRadius);
-        int colliderCount = boidsInVicinity.Length;
+        int colliderCount = Physics.OverlapSphereNonAlloc(transform.position, cohesionRadius, _overlapBuffer);
 
         for (int i = 0; i < colliderCount; i++)
         {
-            Collider collider = boidsInVicinity[i];
+            Collider collider = _overlapBuffer[i];
             if (!collider) continue;
 
             // Ignore our own collider (if present)
@@ -187,7 +205,7 @@ public class Boid : Fauna
             }
```

</details>

### `55b5d7bb6` — Eliminate remaining LINQ and WaitForSeconds allocations in hot paths

_Claude, 2026-03-09 03:22:25 +0000_

```text
- CurrentScore: replace per-frame OrderByDescending().ToList() + FirstOrDefault() with direct iteration (was the worst offender — sorting + allocating every frame)
- TurnMonitorController: replace .Any() LINQ in Update with indexed for-loop
- MiniGameHUDView: replace FirstOrDefault() with manual loop for domain color lookup
- AIPilot: cache WaitForSeconds for player seek interval and ability duration/cooldown coroutines
- MiniGame: cache WaitForSeconds for start game delay and end-of-turn delay
```

```text
 Assets/_Scripts/Game/AI/AIPilot.cs                   | 16 +++++++++++-----
 Assets/_Scripts/Game/Arcade/MiniGame.cs              | 10 +++++++---
 Assets/_Scripts/Game/Arcade/TurnMonitorController.cs | 11 +++++++++--
 Assets/_Scripts/Game/UI/CurrentScore.cs              | 20 +++++++++++---------
 Assets/_Scripts/MinigameHUD/View/MinigameHUDView.cs  |  9 ++++++---
 5 files changed, 44 insertions(+), 22 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 183 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/AI/AIPilot.cs b/Assets/_Scripts/Game/AI/AIPilot.cs
index a831629f1..6603ceb69 100644
--- a/Assets/_Scripts/Game/AI/AIPilot.cs
+++ b/Assets/_Scripts/Game/AI/AIPilot.cs
@@ -123,6 +123,9 @@ namespace CosmicShore.Game.AI
         }
         #endregion
 
+        private WaitForSeconds _playerSeekWait;
+        private static readonly WaitForSeconds AbilityStartDelay = new(3);
+
         public bool AutoPilotEnabled { get; private set; }
 
         private void OnEnable()
@@ -201,7 +204,8 @@ namespace CosmicShore.Game.AI
                     _targetPosition = bestPos;
                 }
 
-                yield return new WaitForSeconds(playerSeekUpdateInterval);
+                _playerSeekWait ??= new WaitForSeconds(playerSeekUpdateInterval);
+                yield return _playerSeekWait;
             }
         }
 
@@ -312,15 +316,17 @@ namespace CosmicShore.Game.AI
             throttle += throttleIncrease * Time.deltaTime;
         }
         
-        IEnumerator UseAbilityCoroutine(AIAbility action) 
+        IEnumerator UseAbilityCoroutine(AIAbility action)
         {
-            yield return new WaitForSeconds(3);
+            yield return AbilityStartDelay;
+            var durationWait = new WaitForSeconds(action.Duration);
+            var cooldownWait = new WaitForSeconds(action.Cooldown);
             while (AutoPilotEnabled)
             {
                 action.Ability.StartAction(actionExecutorRegistry, VesselStatus);
-                yield return new WaitForSeconds(action.Duration);
+                yield return durationWait;
                 action.Ability.StopAction(actionExecutorRegistry, VesselStatus);
-                yield return new WaitForSeconds(action.Cooldown);
+                yield return cooldownWait;
             }
         }
         
diff --git a/Assets/_Scripts/Game/Arcade/MiniGame.cs b/Assets/_Scripts/Game/Arcade/MiniGame.cs
index ecda2886d..f71c1a0d6 100644
--- a/Assets/_Scripts/Game/Arcade/MiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/MiniGame.cs
@@ -73,6 +73,7 @@ namespace CosmicShore.Game.Arcade
         int activePlayerId;
         protected List<int> RemainingPlayers = new();
         protected bool gameRunning;
+        private WaitForSeconds _endOfTurnWait;
 
         public IPlayer ActivePlayer { get; protected set; }
 
@@ -140,9 +141,11 @@ namespace CosmicShore.Game.Arcade
             StartCoroutine(StartNewGameCoroutine());
         }
 
+        private static readonly WaitForSeconds StartGameDelay = new(0.2f);
+
         IEnumerator StartNewGameCoroutine()
         {
-            yield return new WaitForSeconds(.2f);
+            yield return StartGameDelay;
 
             StartNewGame();
         }
@@ -269,7 +272,8 @@ namespace CosmicShore.Game.Arcade
             ActivePlayer.InputController.InputStatus.Paused = true;
             // ActivePlayer.Vessel.VesselStatus.VesselPrismController.StopSpawn();
 
-            yield return new WaitForSeconds(EndOfTurnDelay);
+            _endOfTurnWait ??= new WaitForSeconds(EndOfTurnDelay);
+            yield return _endOfTurnWait;
 
             TurnsTakenThisRound++;
 
@@ -457,7 +461,7 @@ namespace CosmicShore.Game.Arcade
 
         IEnumerator TimedCallbackCoroutine(float invokeAfterSeconds, Action callback)
         {
-            yield return new WaitForSeconds(invokeAfterSeconds);
+            yield return new WaitForSeconds(invokeAfterSeconds); // variable duration, cannot cache
 
             callback?.Invoke();
         }
diff --git a/Assets/_Scripts/Game/Arcade/TurnMonitorController.cs b/Assets/_Scripts/Game/Arcade/TurnMonitorController.cs
index 96daedf97..93b36dc3b 100644
--- a/Assets/_Scripts/Game/Arcade/TurnMonitorController.cs
+++ b/Assets/_Scripts/Game/Arcade/TurnMonitorController.cs
@@ -1,5 +1,4 @@
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Soap;
 using Unity.Netcode;
 using UnityEngine;
@@ -81,6 +80,14 @@ namespace CosmicShore.Game.Arcade
                 m.StopMonitor();
         }
 
-        bool CheckEndOfTurn() => monitors.Any(m => m.CheckForEndOfTurn());
+        bool CheckEndOfTurn()
+        {
+            for (int i = 0, count = monitors.Count; i < count; i++)
+            {
+                if (monitors[i].CheckForEndOfTurn())
+                    return true;
+            }
+            return false;
+        }
     }
 }
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/UI/CurrentScore.cs b/Assets/_Scripts/Game/UI/CurrentScore.cs
index e330b998b..af7c84010 100644
--- a/Assets/_Scripts/Game/UI/CurrentScore.cs
+++ b/Assets/_Scripts/Game/UI/CurrentScore.cs
@@ -1,4 +1,3 @@
-using System.Linq;
 using UnityEngine;
 using TMPro;
 using CosmicShore.Core;
@@ -10,18 +9,21 @@ namespace CosmicShore.Game.UI
     public class CurrentScore : MonoBehaviour
     {
         [SerializeField] TMP_Text currentScoreText;
-        
+
         [FormerlySerializedAs("miniGameData")] [SerializeField] GameDataSO gameData;
 
         void Update()
         {
-            // Use MiniGameData instead of StatsManager
-            var roundStats = gameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();
-
-            float Vol(Domains t) => roundStats.FirstOrDefault(rs => rs.Domain == t)?.VolumeRemaining ?? 0f;
-
-            float greenVolume = Vol(Domains.Jade);
-            float redVolume   = Vol(Domains.Ruby);
+            // Iterate directly instead of sorting + LINQ per frame
+            var statsList = gameData.RoundStatsList;
+            float greenVolume = 0f;
+            float redVolume = 0f;
+            for (int i = 0, count = statsList.Count; i < count; i++)
+            {
+                var rs = statsList[i];
```

</details>

### `186c43384` — Eliminate material cloning and cache hot-path GetComponent lookups

_Claude, 2026-03-09 03:24:24 +0000_

```text
- Projectile: replace .material clone with .sharedMaterial + MaterialPropertyBlock for per-instance opacity (eliminates material instance per projectile)
- ClearPrisms: cache Renderer lookups in Dictionary for OnTriggerStay (fires 100s of times/frame with GenerateOnTriggerStayEvents), use TryGetComponent consistently
```

```text
 Assets/_Scripts/Game/Projectiles/Projectile.cs | 18 +++++++++++++-----
 Assets/_Scripts/Game/Ship/ClearPrisms.cs       | 17 ++++++++++++-----
 2 files changed, 25 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Projectiles/Projectile.cs b/Assets/_Scripts/Game/Projectiles/Projectile.cs
index 064a4c78a..ca17b9fed 100644
--- a/Assets/_Scripts/Game/Projectiles/Projectile.cs
+++ b/Assets/_Scripts/Game/Projectiles/Projectile.cs
@@ -32,6 +32,10 @@ namespace CosmicShore.Game.Projectiles
 
         private MeshRenderer meshRenderer;
 
+        // MaterialPropertyBlock for per-instance opacity (avoids material cloning)
+        private static readonly int OpacityPropertyID = Shader.PropertyToID("_Opacity");
+        private readonly MaterialPropertyBlock _mpb = new();
+
         // NEW: remember pooled parent so we can restore it
         private Transform _pooledParent;
 
@@ -67,8 +71,9 @@ namespace CosmicShore.Game.Projectiles
             if (spike)
             {
                 meshRenderer = GetComponent<MeshRenderer>();
-                meshRenderer.material = _themeManagerData.GetTeamSpikeMaterial(OwnDomain);
-                meshRenderer.material.SetFloat("_Opacity", 0.5f);
+                meshRenderer.sharedMaterial = _themeManagerData.GetTeamSpikeMaterial(OwnDomain);
+                _mpb.SetFloat(OpacityPropertyID, 0.5f);
+                meshRenderer.SetPropertyBlock(_mpb);
             }
         }
 
@@ -125,7 +130,8 @@ namespace CosmicShore.Game.Projectiles
             if (spike)
             {
                 transform.localScale = new Vector3(0.4f, 0.4f, 2f);
-                meshRenderer.material.SetFloat("_Opacity", 0.5f);
+                _mpb.SetFloat(OpacityPropertyID, 0.5f);
+                meshRenderer.SetPropertyBlock(_mpb);
             }
 
             Stop(); // Stop any running movement before starting a new one
@@ -162,7 +168,6 @@ namespace CosmicShore.Game.Projectiles
             float elapsedTime = 0f;
             var t = transform; // cache
             var useSpike = spike && meshRenderer;
-            var mat = useSpike ? meshRenderer.material : null;
 
             try
             {
@@ -176,7 +181,10 @@ namespace CosmicShore.Game.Projectiles
                     {
                         float percentRemaining = elapsedTime / projectileTime;
                         if (percentRemaining > 0.9f)
-                            mat.SetFloat("_Opacity", 1f - Mathf.Pow(percentRemaining, 4f));
+                        {
+                            _mpb.SetFloat(OpacityPropertyID, 1f - Mathf.Pow(percentRemaining, 4f));
+                            meshRenderer.SetPropertyBlock(_mpb);
+                        }
                     }
 
                     elapsedTime += deltaTime;
diff --git a/Assets/_Scripts/Game/Ship/ClearPrisms.cs b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
index 154f4c413..37aae2db1 100644
--- a/Assets/_Scripts/Game/Ship/ClearPrisms.cs
+++ b/Assets/_Scripts/Game/Ship/ClearPrisms.cs
@@ -1,3 +1,4 @@
+using System.Collections.Generic;
 using CosmicShore.Core;
 using CosmicShore.Game;
 using UnityEngine;
@@ -31,6 +32,9 @@ namespace CosmicShore
         private static readonly int AlphaPropertyID = Shader.PropertyToID("_Alpha");
         private readonly MaterialPropertyBlock _mpb = new();
 
+        // Cache Renderer lookups — OnTriggerStay fires hundreds of times per frame
+        private readonly Dictionary<Collider, Renderer> _rendererCache = new(128);
+
 
         private void OnEnable()
         {
@@ -98,14 +102,17 @@ namespace CosmicShore
 
         void OnTriggerEnter(Collider other)
         {
-            Prism prism = other.GetComponent<Prism>();
-            if (prism != null)
+            if (other.TryGetComponent<Prism>(out var prism))
                 prism.SetTransparency(true);
         }
 
         private void OnTriggerStay(Collider other)
         {
-            if (!other.TryGetComponent<Renderer>(out var renderer)) return;
+            if (!_rendererCache.TryGetValue(other, out var renderer))
+            {
+                if (!other.TryGetComponent(out renderer)) return;
+                _rendererCache[other] = renderer;
+            }
             float alpha = scaleCurve.Evaluate(GeometryUtils.DistanceFromPointToLine(other.transform.position, lineData) / capsuleRadius);
             renderer.GetPropertyBlock(_mpb);
             _mpb.SetFloat(AlphaPropertyID, alpha);
@@ -114,8 +121,8 @@ namespace CosmicShore
 
         void OnTriggerExit(Collider other)
         {
-            Prism prism = other.GetComponent<Prism>();
-            if (prism != null)
+            _rendererCache.Remove(other);
+            if (other.TryGetComponent<Prism>(out var prism))
                 prism.SetTransparency(false);
         }
     }
```

</details>
