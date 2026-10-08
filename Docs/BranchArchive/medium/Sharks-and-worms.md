# Branch archive: `Sharks-and-worms`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2025-03-05 by Garrett Milliron
- **Unmerged commits:** 4
- **Forked from:** `3fcdfd55e` (2025-02-06, tuned freestyle and squirrel)
- **Tip:** `e9f80a1d1`
- **Files touched (55):**
  - `Assets/Plugins/Android/mainTemplate.gradle`
  - `Assets/Plugins/Android/mainTemplate.gradle.backup4`
  - `Assets/Plugins/Android/mainTemplate.gradle.backup4.meta`
  - `Assets/Resources/PerformanceTestRunInfo.json`
  - `Assets/Resources/PerformanceTestRunInfo.json.meta`
  - `Assets/Resources/PerformanceTestRunSettings.json`
  - `Assets/Resources/PerformanceTestRunSettings.json.meta`
  - `Assets/_Graphics/Materials/BadMaterial.mat`
  - `Assets/_Graphics/Materials/BadMaterial.mat.meta`
  - `Assets/_Graphics/Materials/GoodlMaterial.mat`
  - `Assets/_Graphics/Materials/GoodlMaterial.mat.meta`
  - `Assets/_Models/Fauna/MassSharkFauna.prefab`
  - `Assets/_Prefabs/Environment/Cytoplasm/NudgeShards.prefab`
  - `Assets/_Prefabs/FloraAndFauna/ChargeGyroidFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/MassGyroidFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/Populations/LoneMassSharkPopulation .prefab`
  - `Assets/_Prefabs/FloraAndFauna/Populations/LoneMassSharkPopulation .prefab.meta`
  - `Assets/_Prefabs/FloraAndFauna/Populations/MassSharkPopulation.prefab`
  - `Assets/_Prefabs/FloraAndFauna/Populations/WormPopulation.prefab`
  - `Assets/_Prefabs/FloraAndFauna/Populations/WormPopulation.prefab.meta`
  - `Assets/_Prefabs/FloraAndFauna/SpaceGyroidFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/TimeGyroidFlora.prefab`
  - `Assets/_Prefabs/FloraAndFauna/worm.prefab`
  - `Assets/_SO_Assets/CellTypes/BlobCell.asset`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scenes/MinigameFreestyle.unity`
  - `Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors.meta`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs.meta`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs.meta`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs.meta`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/FaunaBehavior.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/FaunaBehavior.cs.meta`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/FaunaBehaviorOption.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/FaunaBehaviorOption.cs.meta`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/LightFaunaBoidBehavior.cs`
  - `Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/LightFaunaBoidBehavior.cs.meta`
  - … and 15 more

### `5742f0e76` — working bigger blocks

_Garrett Milliron, 2025-02-17 13:40:01 -0500_

```text
 .../FloraAndFauna/Populations/LoneMassSharkPopulation .prefab         |  55 ++++++++++++++
 .../FloraAndFauna/Populations/LoneMassSharkPopulation .prefab.meta    |   7 ++
 .../FloraAndFauna/Populations/{Worms.prefab => WormPopulation.prefab} |   8 +-
 .../Populations/{Worms.prefab.meta => WormPopulation.prefab.meta}     |   0
 Assets/_Prefabs/FloraAndFauna/worm.prefab                             | 131 +++++++++++++++++++++++++++++---
 Assets/_SO_Assets/CellTypes/BlobCell.asset                            |   2 +-
 Assets/_Scenes/Menu_Main.unity                                        |   4 +-
 Assets/_Scenes/MinigameFreestyle.unity                                |  12 ++-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Worm.cs                |   2 -
 Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs         |   2 +-
 Assets/_Scripts/Game/Environment/Node.cs                              |   4 +-
 Assets/_Scripts/Game/Managers/BlockScaleManager.cs                    |  14 ++--
 Assets/_Scripts/Game/Ship/TrailBlock.cs                               |   5 +-
 13 files changed, 213 insertions(+), 33 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Worm.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Worm.cs
index 409a8704f..16023dbe9 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Worm.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Worm.cs
@@ -1,7 +1,5 @@
 using UnityEngine;
 using System.Collections.Generic;
-using CosmicShore;
-using CosmicShore.Core;
 using CosmicShore.Utility;
 
 public class Worm : MonoBehaviour
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs
index 38756b126..8a29737f0 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/WormManager.cs
@@ -98,7 +98,7 @@ public class WormManager : Population
         newWorm.InitializeWorm();
 
         return newWorm;
-    }
+    } 
 
     public void RemoveWorm(Worm worm)
     {
diff --git a/Assets/_Scripts/Game/Environment/Node.cs b/Assets/_Scripts/Game/Environment/Node.cs
index 135ec76cb..1c40f91f3 100644
--- a/Assets/_Scripts/Game/Environment/Node.cs
+++ b/Assets/_Scripts/Game/Environment/Node.cs
@@ -86,7 +86,7 @@ public class Node : MonoBehaviour
             {
                 modifier.Apply(this);
             }
-            SpawnLife();
+            //SpawnLife();
             Crystal.gameObject.SetActive(true);
         }   
     }
@@ -363,7 +363,7 @@ public class Node : MonoBehaviour
         while (true)
         {
             var controllingVolume = GetTeamVolume(ControllingTeam);
-            var period = baseFaunaSpawnTime * faunaSpawnVolumeThreshold / controllingVolume; //TODO: use this to adjust spawn rate
+            //var period = baseFaunaSpawnTime * faunaSpawnVolumeThreshold / controllingVolume; //TODO: use this to adjust spawn rate
             if (controllingVolume > faunaSpawnVolumeThreshold)
             {
                 
diff --git a/Assets/_Scripts/Game/Managers/BlockScaleManager.cs b/Assets/_Scripts/Game/Managers/BlockScaleManager.cs
index c6c78fea7..714e6cb73 100644
--- a/Assets/_Scripts/Game/Managers/BlockScaleManager.cs
+++ b/Assets/_Scripts/Game/Managers/BlockScaleManager.cs
@@ -32,18 +32,18 @@ namespace CosmicShore.Core
             activeAnimatorsList.AddRange(activeAnimators);
 
             int scalingCount = 0;
-            foreach (var block in activeAnimatorsList)
+            foreach (var blockAnimator in activeAnimatorsList)
             {
-                if (block == null || !block.enabled || !block.IsScaling) continue;
+                if (blockAnimator == null || !blockAnimator.enabled || !blockAnimator.IsScaling) continue;
 
-                var targetScale = Vector3.Min(Vector3.Max(block.TargetScale, block.MinScale), block.MaxScale);
+                var targetScale = Vector3.Min(Vector3.Max(blockAnimator.TargetScale, blockAnimator.MinScale), blockAnimator.MaxScale);
                 animationData[scalingCount] = new ScaleAnimationData
                 {
-                    currentScale = block.transform.localScale,
+                    currentScale = blockAnimator.transform.localScale,
                     targetScale = targetScale,
-                    growthRate = block.GrowthRate,
-                    minScale = block.MinScale,
-                    maxScale = block.MaxScale,
+                    growthRate = blockAnimator.GrowthRate,
+                    minScale = blockAnimator.MinScale,
+                    maxScale = blockAnimator.MaxScale,
                     blockIndex = scalingCount
                 };
                 scalingCount++;
diff --git a/Assets/_Scripts/Game/Ship/TrailBlock.cs b/Assets/_Scripts/Game/Ship/TrailBlock.cs
index 9cbb7c27f..011256374 100644
--- a/Assets/_Scripts/Game/Ship/TrailBlock.cs
+++ b/Assets/_Scripts/Game/Ship/TrailBlock.cs
@@ -120,6 +120,7 @@ namespace CosmicShore.Core
 
         private void InitializeTrailBlockProperties()
         {
+            if (TargetScale == Vector3.zero) TargetScale = transform.localScale;
             if (TrailBlockProperties == null) return;
 
             TrailBlockProperties.position = transform.position;
@@ -141,9 +142,9 @@ namespace CosmicShore.Core
             // Set initial target scale before beginning growth animation
             if (scaleAnimator.TargetScale == Vector3.zero)
             {
-                scaleAnimator.SetTargetScale(Vector3.one);
+                scaleAnimator.SetTargetScale(transform.localScale);
             }
-            
+
             // Update volume before growth animation starts
             TrailBlockProperties.volume = scaleAnimator.GetCurrentVolume();
             
```

</details>

### `328178030` — a new behaviour system

_Garrett Milliron, 2025-02-24 15:24:14 -0500_

```text
 Assets/Plugins/Android/mainTemplate.gradle.backup4                    |  63 +++++++
 Assets/Plugins/Android/mainTemplate.gradle.backup4.meta               |   7 +
 Assets/Resources/PerformanceTestRunInfo.json                          |   1 +
 Assets/Resources/PerformanceTestRunInfo.json.meta                     |   7 +
 Assets/Resources/PerformanceTestRunSettings.json                      |   1 +
 Assets/Resources/PerformanceTestRunSettings.json.meta                 |   7 +
 Assets/_Models/Fauna/MassSharkFauna.prefab                            | 255 +++++++++++++++++++++++++---
 Assets/_Prefabs/FloraAndFauna/Populations/MassSharkPopulation.prefab  |   2 +-
 Assets/_SO_Assets/CellTypes/BlobCell.asset                            |  10 +-
 Assets/_Scenes/Menu_Main.unity                                        |   4 +-
 Assets/_Scenes/MinigameFreestyle.unity                                |   2 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors.meta         |   8 +
 .../Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs   | 204 ++++++++++++++++++++++
 .../Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs.meta   |   2 +
 .../Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs      |  93 ++++++++++
 .../Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs.meta |   2 +
 .../Game/Environment/FloraAndFauna/Behaviors/FaunaBehavior.cs         |  33 ++++
 .../Game/Environment/FloraAndFauna/Behaviors/FaunaBehavior.cs.meta    |   2 +
 .../Game/Environment/FloraAndFauna/Behaviors/FaunaBehaviorOption.cs   |  16 ++
 .../Environment/FloraAndFauna/Behaviors/FaunaBehaviorOption.cs.meta   |   2 +
 .../Environment/FloraAndFauna/Behaviors/LightFaunaBoidBehavior.cs     | 291 ++++++++++++++++++++++++++++++++
 .../FloraAndFauna/Behaviors/LightFaunaBoidBehavior.cs.meta            |   2 +
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs          | 172 ++++++++-----------
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs.meta     |  11 +-
 Assets/_Scripts/Game/Environment/FloraAndFauna/Spindle.cs             |   2 +-
 Assets/_Scripts/Game/Environment/HealthBlock.cs                       |   1 +
 Assets/_Scripts/Scripts.meta                                          |   8 +
 Assets/_Scripts/Scripts/Behaviors.meta                                |   8 +
 ProjectSettings/ProjectSettings.asset                                 |   2 +-
 30 files changed, 1088 insertions(+), 151 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 915 lines)</summary>

```diff
diff --git a/Assets/Resources/PerformanceTestRunInfo.json b/Assets/Resources/PerformanceTestRunInfo.json
new file mode 100644
index 000000000..f90fd8b53
--- /dev/null
+++ b/Assets/Resources/PerformanceTestRunInfo.json
@@ -0,0 +1 @@
+{"TestSuite":"","Date":0,"Player":{"Development":false,"ScreenWidth":0,"ScreenHeight":0,"ScreenRefreshRate":0,"Fullscreen":false,"Vsync":0,"AntiAliasing":0,"Batchmode":false,"RenderThreadingMode":"MultiThreaded","GpuSkinning":true,"Platform":"","ColorSpace":"","AnisotropicFiltering":"","BlendWeights":"","GraphicsApi":"","ScriptingBackend":"IL2CPP","AndroidTargetSdkVersion":"AndroidApiLevel33","AndroidBuildSystem":"Gradle","BuildTarget":"Android","StereoRenderingPath":"MultiPass"},"Hardware":{"OperatingSystem":"","DeviceModel":"","DeviceName":"","ProcessorType":"","ProcessorCount":0,"GraphicsDeviceName":"","SystemMemorySizeMB":0},"Editor":{"Version":"6000.0.32f1","Branch":"6000.0/staging","Changeset":"b2e806cf271c","Date":1733851156},"Dependencies":["com.unity.2d.sprite@1.0.0","com.unity.adaptiveperformance@5.1.0","com.unity.adaptiveperformance.samsung.android@5.0.0","com.unity.ads@4.12.0","com.unity.ai.navigation@2.0.5","com.unity.animation.rigging@1.3.0","com.unity.cinemachine@3.1.2","com.unity.collab-proxy@2.6.0","com.unity.device-simulator.devices@1.0.0","com.unity.feature.mobile@1.0.0","com.unity.ide.rider@3.0.34","com.unity.ide.visualstudio@2.0.22","com.unity.inputsystem@1.11.2","com.unity.mobile.android-logcat@1.4.4","com.unity.mobile.notifications@2.4.0","com.unity.multiplayer.center@1.0.0","com.unity.multiplayer.playmode@1.3.3","com.unity.multiplayer.tools@2.2.3","com.unity.netcode.gameobjects@2.1.1","com.unity.nuget.newtonsoft-json@3.2.1","com.unity.performance.profile-analyzer@1.2.3","com.unity.purchasing@4.12.2","com.unity.recorder@5.1.2","com.unity.render-pipelines.universal@17.0.3","com.unity.services.analytics@6.0.2","com.unity.services.multiplayer@1.1.0","com.unity.test-framework@1.4.5","com.unity.timeline@1.8.7","com.unity.toolchain.win-x86_64-linux-x86_64@2.0.10","com.unity.ugui@2.0.0","com.unity.visualeffectgraph@17.0.3","jp.hadashikick.vcontainer@1.6.3","com.unity.modules.accessibility@1.0.0","com.unity.modules.ai@1.0.0","com.unity.modules.androidjni@1.0.0","com.unity.modules.animation@1.0.0","com.unity.modules.assetbundle@1.0.0","com.unity.modules.audio@1.0.0","com.unity.modules.cloth@1.0.0","com.unity.modules.director@1.0.0","com.unity.modules.imageconversion@1.0.0","com.unity.modules.imgui@1.0.0","com.unity.modules.jsonserialize@1.0.0","com.unity.modules.particlesystem@1.0.0","com.unity.modules.physics@1.0.0","com.unity.modules.physics2d@1.0.0","com.unity.modules.screencapture@1.0.0","com.unity.modules.terrain@1.0.0","com.unity.modules.terrainphysics@1.0.0","com.unity.modules.tilemap@1.0.0","com.unity.modules.ui@1.0.0","com.unity.modules.uielements@1.0.0","com.unity.modules.umbra@1.0.0","com.unity.modules.unityanalytics@1.0.0","com.unity.modules.unitywebrequest@1.0.0","com.unity.modules.unitywebrequestassetbundle@1.0.0","com.unity.modules.unitywebrequestaudio@1.0.0","com.unity.modules.unitywebrequesttexture@1.0.0","com.unity.modules.unitywebrequestwww@1.0.0","com.unity.modules.vehicles@1.0.0","com.unity.modules.video@1.0.0","com.unity.modules.vr@1.0.0","com.unity.modules.wind@1.0.0","com.unity.modules.xr@1.0.0","com.unity.modules.subsystems@1.0.0","com.unity.modules.hierarchycore@1.0.0","com.unity.shadergraph@17.0.3","com.unity.render-pipelines.core@17.0.3","com.unity.sysroot@2.0.10","com.unity.sysroot.linux-x86_64@2.0.9","com.unity.ext.nunit@2.0.5","com.unity.transport@2.4.0","com.unity.collections@2.5.1","com.unity.services.qos@1.3.0","com.unity.services.core@1.14.0","com.unity.services.wire@1.2.7","com.unity.services.deployment@1.3.0","com.unity.services.authentication@3.4.0","com.unity.render-pipelines.universal-config@17.0.3","com.unity.bindings.openimageio@1.0.0","com.unity.nuget.mono-cecil@1.11.4","com.unity.burst@1.8.18","com.unity.mathematics@1.3.2","com.unity.profiling.core@1.0.2","com.unity.splines@2.7.2","com.unity.rendering.light-transport@1.0.1","com.unity.searcher@4.9.2","com.unity.services.deployment.api@1.0.0","com.unity.test-framework.performance@3.0.3","com.unity.settings-manager@2.0.1"],"Results":[]}
\ No newline at end of file
diff --git a/Assets/Resources/PerformanceTestRunSettings.json b/Assets/Resources/PerformanceTestRunSettings.json
new file mode 100644
index 000000000..49438ae14
--- /dev/null
+++ b/Assets/Resources/PerformanceTestRunSettings.json
@@ -0,0 +1 @@
+{"MeasurementCount":-1}
\ No newline at end of file
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs
new file mode 100644
index 000000000..719ad18d3
--- /dev/null
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs
@@ -0,0 +1,204 @@
+using UnityEngine;
+using System.Collections;
+
+namespace CosmicShore
+{
+    /// <summary>
+    /// Demonstrates an "ambush predator" style behavior,
+    /// waiting near a point for unsuspecting targets, then 
+    /// quickly rushing them for a short burst.
+    /// </summary>
+    public class AmbushPredatorBehavior : FaunaBehavior
+    {
+        [SerializeField] float ambushWaitTime = 5f;
+        [SerializeField] float rushSpeed = 20f;
+        [SerializeField] float rushDuration = 2f;
+
+        private bool isAmbushing = false;
+
+        public override bool CanPerform(Fauna fauna)
+        {
+            return !isAmbushing;
+        }
+
+        public override IEnumerator Perform(Fauna fauna)
+        {
+            isAmbushing = true;
+
+            // Step 1: Hide or remain still for a few seconds
+            yield return new WaitForSeconds(ambushWaitTime);
+
+            // Step 2: Rush forward
+            float timer = 0f;
+            Vector3 direction = fauna.transform.forward;
+            while (timer < rushDuration)
+            {
+                fauna.transform.position += direction * rushSpeed * Time.deltaTime;
+                timer += Time.deltaTime;
+                yield return null;
+            }
+            yield return null;
+        }
+
+        public override void OnBehaviorEnd(Fauna fauna)
+        {
+            isAmbushing = false;
+        }
+    }
+
+    /// <summary>
+    /// A short-distance teleport or 'blink' style behavior.
+    /// Useful for surprising movement or passing obstacles.
+    /// </summary>
+    public class BlinkTeleportBehavior : FaunaBehavior
+    {
+        [SerializeField] float blinkDistance = 10f;
+        [SerializeField] float blinkCooldown = 3f;
+
+        private bool onCooldown = false;
+
+        public override bool CanPerform(Fauna fauna)
+        {
+            return !onCooldown;
+        }
+
+        public override IEnumerator Perform(Fauna fauna)
+        {
+            onCooldown = true;
+
+            // Teleport in the facing direction
+            Vector3 forward = fauna.transform.forward;
+            fauna.transform.position += forward * blinkDistance;
+
+            // Maybe spawn a VFX or sound effect
+            // e.g.:
+            // Instantiate(teleportEffect, fauna.transform.position, fauna.transform.rotation);
+
+            yield return new WaitForSeconds(blinkCooldown);
+            onCooldown = false;
+        }
+    }
+
+    /// <summary>
+    /// A simple 'Gather Resource' style behavior
+    /// that looks for the nearest block of a certain type
+    /// and tries to pull it in or attach it.
+    /// </summary>
+    public class GatherResourceBehavior : FaunaBehavior
+    {
+        [SerializeField] float scanRadius = 20f;
+        [SerializeField] float pullSpeed = 3f;
+        [SerializeField] LayerMask resourceLayers;
+
+        private bool isGathering = false;
+
+        public override bool CanPerform(Fauna fauna)
+        {
+            return !isGathering;
+        }
+
+        public override IEnumerator Perform(Fauna fauna)
+        {
+            isGathering = true;
+
+            Collider[] resources = Physics.OverlapSphere(fauna.transform.position, scanRadius, resourceLayers);
+            if (resources.Length > 0)
+            {
+                // Just pick the first or find the closest
+                Collider closest = resources[0];
+                float minDist = (closest.transform.position - fauna.transform.position).sqrMagnitude;
+                foreach (var r in resources)
+                {
+                    float dist = (r.transform.position - fauna.transform.position).sqrMagnitude;
+                    if (dist < minDist)
+                    {
+                        minDist = dist;
+                        closest = r;
+                    }
+                }
+
+                // Pull it in or move to it
+                float time = 0f;
+                float maxTime = 5f; // fail-safe if it can't be gathered quickly
+                Vector3 pullDir = (fauna.transform.position - closest.transform.position).normalized;
+                while (time < maxTime && closest != null)
+                {
+                    // Move resource or the fauna
+                    closest.transform.position = Vector3.MoveTowards(
+                        closest.transform.position, 
```

</details>

### `0d31284cf` — updates

_Garrett Milliron, 2025-03-04 12:50:46 -0500_

```text
 Assets/_Models/Fauna/MassSharkFauna.prefab                            | 24 +++++++----------
 Assets/_Prefabs/FloraAndFauna/Populations/MassSharkPopulation.prefab  |  2 +-
 .../Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs   | 22 +++++++--------
 .../Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs      | 48 +++++++++++++++++++++++++++++++++
 .../Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs.meta |  2 ++
 .../Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs      | 45 ++-----------------------------
 .../Game/Environment/FloraAndFauna/Behaviors/FaunaBehavior.cs         | 14 +++++++---
 .../Environment/FloraAndFauna/Behaviors/LightFaunaBoidBehavior.cs     |  6 ++---
 Assets/_Scripts/Game/Environment/FloraAndFauna/Fauna.cs               |  5 ++++
 Assets/_Scripts/Game/Environment/FloraAndFauna/LightFauna.cs          |  8 +++---
 ProjectSettings/UnityConnectSettings.asset                            |  2 +-
 11 files changed, 97 insertions(+), 81 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 330 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs
index 719ad18d3..ea08acd39 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/AdditionalBehaviors.cs
@@ -16,12 +16,12 @@ namespace CosmicShore
 
         private bool isAmbushing = false;
 
-        public override bool CanPerform(Fauna fauna)
+        public override bool CanPerform()
         {
             return !isAmbushing;
         }
 
-        public override IEnumerator Perform(Fauna fauna)
+        public override IEnumerator Perform()
         {
             isAmbushing = true;
 
@@ -40,7 +40,7 @@ namespace CosmicShore
             yield return null;
         }
 
-        public override void OnBehaviorEnd(Fauna fauna)
+        public override void OnBehaviorEnd()
         {
             isAmbushing = false;
         }
@@ -57,12 +57,12 @@ namespace CosmicShore
 
         private bool onCooldown = false;
 
-        public override bool CanPerform(Fauna fauna)
+        public override bool CanPerform()
         {
             return !onCooldown;
         }
 
-        public override IEnumerator Perform(Fauna fauna)
+        public override IEnumerator Perform()
         {
             onCooldown = true;
 
@@ -92,12 +92,12 @@ namespace CosmicShore
 
         private bool isGathering = false;
 
-        public override bool CanPerform(Fauna fauna)
+        public override bool CanPerform()
         {
             return !isGathering;
         }
 
-        public override IEnumerator Perform(Fauna fauna)
+        public override IEnumerator Perform()
         {
             isGathering = true;
 
@@ -140,7 +140,7 @@ namespace CosmicShore
             isGathering = false;
         }
 
-        public override void OnBehaviorEnd(Fauna fauna)
+        public override void OnBehaviorEnd()
         {
             isGathering = false;
         }
@@ -158,13 +158,13 @@ namespace CosmicShore
         
         private bool isMorphing = false;
 
-        public override bool CanPerform(Fauna fauna)
+        public override bool CanPerform()
         {
             // Only do it if aggression is at least X
             return !isMorphing && fauna.aggression >= requiredAggression;
         }
 
-        public override IEnumerator Perform(Fauna fauna)
+        public override IEnumerator Perform()
         {
             isMorphing = true;
 
@@ -195,7 +195,7 @@ namespace CosmicShore
             isMorphing = false;
         }
 
-        public override void OnBehaviorEnd(Fauna fauna)
+        public override void OnBehaviorEnd()
         {
             // If forcibly ended, revert if needed
             isMorphing = false;
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs
new file mode 100644
index 000000000..2fdc838fe
--- /dev/null
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/BullRushBehavior.cs
@@ -0,0 +1,48 @@
+﻿using UnityEngine;
+using System.Collections;
+
+namespace CosmicShore
+{
+    /// <summary>
+    /// A simple example behavior that makes the Fauna "rush forward" if it meets
+    /// certain conditions. Demonstrates referencing extra components or
+    /// species-specific data from the Fauna.
+    /// </summary>
+    public class BullRushBehavior : FaunaBehavior
+    {
+        [SerializeField] private float rushSpeed = 15f;
+        [SerializeField] private float rushDuration = 2f;
+        [SerializeField] private float cooldownTime = 5f;
+
+        private bool isOnCooldown = false;
+
+
+        public override bool CanPerform()
+        {
+            // Example condition: Must not be on cooldown and must have a certain aggression level
+            if (isOnCooldown) return false;
+            return (fauna.aggression >= 5);
+        }
+
+        public override IEnumerator Perform()
+        {
+            // Example logic: Move forward at high speed for a short time
+            var startRotation = fauna.transform.rotation;
+            var startVelocity = fauna.transform.forward * rushSpeed;
+
+            float timer = 0f;
+            while (timer < rushDuration)
+            {
+                timer += Time.deltaTime;
+                fauna.transform.position += startVelocity * Time.deltaTime;
+                // Optionally add more logic (damage, visual effects, etc.)
+                yield return null;
+            }
+
+            // Once done, set a cooldown so we can't do it again immediately.
+            isOnCooldown = true;
+            yield return new WaitForSeconds(cooldownTime);
+            isOnCooldown = false;
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs
index 50be9abdc..739a36abc 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/Behaviors/ExampleBehaviors.cs
```

</details>

### `e9f80a1d1` — snow changer changes

_Garrett Milliron, 2025-03-05 15:52:08 -0500_

```text
 Assets/_Graphics/Materials/BadMaterial.mat                 | 209 +++++++++++++++++++++++++++++++++++++++++++
 Assets/_Graphics/Materials/BadMaterial.mat.meta            |   8 ++
 Assets/_Graphics/Materials/GoodlMaterial.mat               | 209 +++++++++++++++++++++++++++++++++++++++++++
 Assets/_Graphics/Materials/GoodlMaterial.mat.meta          |   8 ++
 Assets/_Prefabs/Environment/Cytoplasm/NudgeShards.prefab   |   3 +
 Assets/_Prefabs/FloraAndFauna/ChargeGyroidFlora.prefab     |   1 +
 Assets/_Prefabs/FloraAndFauna/MassGyroidFlora.prefab       |   1 +
 Assets/_Prefabs/FloraAndFauna/SpaceGyroidFlora.prefab      |   1 +
 Assets/_Prefabs/FloraAndFauna/TimeGyroidFlora.prefab       |   1 +
 Assets/_Scenes/Menu_Main.unity                             |  78 +---------------
 Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs  |  40 +++++++--
 Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs |   5 ++
 Assets/_Scripts/Game/Environment/Node.cs                   |   6 +-
 13 files changed, 485 insertions(+), 85 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs b/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs
index ee48edadd..7568f6a68 100644
--- a/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs
+++ b/Assets/_Scripts/Game/Environment/Cytoplasm/SnowChanger.cs
@@ -1,9 +1,14 @@
+using CosmicShore;
 using CosmicShore.Environment.FlowField;
+using System.Collections.Generic;
 using UnityEngine;
 
+
 public class SnowChanger : MonoBehaviour
 {
     public GameObject Crystal;
+    public List<LifeForm> Threats;
+
     [SerializeField] GameObject snow;
     [SerializeField] Vector3 crystalSize = new Vector3(500, 500, 500);
     [SerializeField] int shardDistance = 100;
@@ -12,6 +17,9 @@ public class SnowChanger : MonoBehaviour
     [SerializeField] bool lookAt;
     [SerializeField] Vector3 targetAxis;
     [SerializeField] Vector3 newOrigin;
+
+    [SerializeField] Material GoodMaterial;
+    [SerializeField] Material BadMaterial;
     
 
     GameObject[,,] crystalLattice;
@@ -21,7 +29,7 @@ public class SnowChanger : MonoBehaviour
     int shardsX;
     int shardsY;
     int shardsZ;
-    float sphereDiameter;
+    float sqrSphereDiameter;
     Vector3 origin = Vector3.zero;
 
     void OnEnable()
@@ -43,7 +51,9 @@ public class SnowChanger : MonoBehaviour
         shardsY = (int)(crystalSize.y / shardDistance);
         shardsZ = (int)(crystalSize.z / shardDistance);
 
-        if (Crystal != null) sphereDiameter = sphereScaler * Crystal.GetComponent<Crystal>().sphereRadius;
+        if (Crystal != null) sqrSphereDiameter = sphereScaler * Crystal.GetComponent<Crystal>().sphereRadius;
+        sqrSphereDiameter *= sqrSphereDiameter;
+
 
         crystalLattice = new GameObject[shardsX * 2 + 1, shardsY * 2 + 1, shardsZ * 2 + 1]; // both sides of each axis plus the midplane
 
@@ -79,12 +89,25 @@ public class SnowChanger : MonoBehaviour
                     var shard = crystalLattice[x, y, z];
                     float normalizedDistance;
                     if (Crystal != null)
-                    { 
-                        float clampedDistance = Mathf.Clamp(
-                        (shard.transform.position - Crystal.transform.position).magnitude, 0, sphereDiameter);
-                        normalizedDistance = clampedDistance / sphereDiameter;
-
-                        shard.transform.LookAt(Crystal.transform);
+                    {
+                        float threatSqrDistance = float.MaxValue;
+                        float crystalSqrDistance = (shard.transform.position - Crystal.transform.position).sqrMagnitude;
+                        if (Threats.Count > 0)
+                        {
+                            threatSqrDistance = (shard.transform.position - Threats[0].transform.position).sqrMagnitude;
+                        }
+                        if (threatSqrDistance < crystalSqrDistance)
+                        {
+                            normalizedDistance = threatSqrDistance / sqrSphereDiameter;
+                            shard.transform.LookAt(Threats[0].transform);
+                            shard.GetComponentInChildren<MeshRenderer>().material = BadMaterial;
+                        }
+                        else
+                        {
+                            normalizedDistance = crystalSqrDistance / sqrSphereDiameter;
+                            shard.transform.LookAt(Crystal.transform);
+                            shard.GetComponentInChildren<MeshRenderer>().material = GoodMaterial;
+                        }
                     }
                     else
                     {
@@ -100,6 +123,7 @@ public class SnowChanger : MonoBehaviour
                         Vector3.forward * (normalizedDistance * nodeScaler + nodeSize) +
                         Vector3.one * (normalizedDistance * nodeScalerOverThree + nodeSize);
 
+
                 }       
             }
         }
diff --git a/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs b/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
index e123081a6..a8add9a3e 100644
--- a/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
+++ b/Assets/_Scripts/Game/Environment/FloraAndFauna/LifeForm.cs
@@ -13,6 +13,7 @@ namespace CosmicShore
         [SerializeField] int healthBlocksForMaturity = 1;
         [SerializeField] int minHealthBlocks = 0;
         [SerializeField] float shieldPeriod = 0;
+        [SerializeField] public bool Threatening = false;
 
         public Teams Team;
         protected HashSet<Spindle> spindles = new HashSet<Spindle>();
@@ -80,6 +81,10 @@ namespace CosmicShore
 
         protected virtual void Die()
         {
+            if (Threatening)
+            {
+                node.SnowChanger.Threats.Remove(this);
+            }
             crystal.ActivateCrystal();
             StatsManager.Instance.LifeformDestroyed(node.ID);
             foreach (HealthBlock healthBlock in healthBlocks.ToArray())
diff --git a/Assets/_Scripts/Game/Environment/Node.cs b/Assets/_Scripts/Game/Environment/Node.cs
index 1c40f91f3..6a5727417 100644
--- a/Assets/_Scripts/Game/Environment/Node.cs
+++ b/Assets/_Scripts/Game/Environment/Node.cs
@@ -25,7 +25,7 @@ public class Node : MonoBehaviour
         } 
     }
 
-    SnowChanger SnowChanger;
+    public SnowChanger SnowChanger;
     GameObject membrane;
     GameObject nucleus; // TODO: Use radius to spawn/move crystal
 
@@ -116,6 +116,10 @@ public class Node : MonoBehaviour
             for (int i = 0; i < FloraTypeCount; i++)
             {
                 var floraConfiguration = SpawnRandomFlora();
+                if ( floraConfiguration.Flora.Threatening)
+                {
+                    SnowChanger.Threats.Add(floraConfiguration.Flora);
+                }
                 StartCoroutine(SpawnFlora(floraConfiguration, spawnJade));
             }
         }
```

</details>
