# Branch archive: `claude/review-optimization-branches-WDr9T`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-05 by Claude
- **Unmerged commits:** 2
- **Forked from:** `3f0ba6e1a` (2026-05-06, Merge pull request #511 from froglet-studio/claude/fix-multiplayer-domain-sele)
- **Tip:** `b0f04f6e0`
- **Files touched (7):**
  - `Assets/_Scripts/Controller/AI/AIPilot.cs`
  - `Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs`
  - `Assets/_Scripts/Controller/Managers/MaterialStateManager.cs`
  - `Assets/_Scripts/Controller/Vessel/ClearPrisms.cs`
  - `Assets/_Scripts/Controller/Vessel/GunTransformer.cs`
  - `Assets/_Scripts/UI/CurrentScore.cs`
  - `Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs`

### `b7f2f8814` — Port no-brainer perf fixes from optimization branches — zero functional changes

_Claude, 2026-05-05 18:32:36 +0000_

```text
GenericPoolManager: cap synchronous prewarm to maxSyncPrewarm=8 so scene loads
don't stall instantiating the full buffer; clear _activeObjects on OnDisable/
OnDestroy to prevent stale-ref leaks.

ClearPrisms: replace renderer.material.SetFloat (clones a material instance every
OnTriggerStay call, which fires 100s of times/frame) with MaterialPropertyBlock +
Shader.PropertyToID; cache Renderer lookups in a Dictionary per Collider.

TurnMonitorController: replace monitors.Any() LINQ delegate allocation in Update
with an indexed for-loop; drop unused System.Linq import.

CurrentScore: replace per-frame GetSortedListInDecendingOrderBasedOnVolumeRemaining
(OrderByDescending().ToList() every frame) + FirstOrDefault with a single direct
loop over RoundStatsList; drop System.Linq import.

GunTransformer: cache GetComponentsInChildren<Transform>() result in Start instead
of re-allocating the array every Update frame.

MaterialStateManager: replace activeAnimators.ToArray() (new array each call) with
a reusable _animatorsToRemove List; drop unused System.Linq import.

AIPilot: cache WaitForSeconds(playerSeekUpdateInterval) after Initialize() so the
UpdatePlayerTarget coroutine doesn't allocate a new object every tick; add static
WaitAbilityStart for the fixed 3-second initial delay in UseAbilityCoroutine.

Source branches: claude/add-prism-activation-queue-CEoJM,
claude/optimize-pool-manager-6tOOr, claude/optimize-mobile-performance-7uCEG.
```

```text
 Assets/_Scripts/Controller/AI/AIPilot.cs                      | 10 +++++++---
 Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs    |  8 ++++++--
 Assets/_Scripts/Controller/Managers/MaterialStateManager.cs   | 12 +++++++-----
 Assets/_Scripts/Controller/Vessel/ClearPrisms.cs              | 35 +++++++++++++++++++++++++++--------
 Assets/_Scripts/Controller/Vessel/GunTransformer.cs           |  8 +++++---
 Assets/_Scripts/UI/CurrentScore.cs                            | 30 ++++++++++--------------------
 Assets/_Scripts/Utility/PoolsAndBuffers/GenericPoolManager.cs | 23 ++++++++++++++++++-----
 7 files changed, 80 insertions(+), 46 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 320 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/AI/AIPilot.cs b/Assets/_Scripts/Controller/AI/AIPilot.cs
index 06b686a0c..baa250af4 100644
--- a/Assets/_Scripts/Controller/AI/AIPilot.cs
+++ b/Assets/_Scripts/Controller/AI/AIPilot.cs
@@ -92,6 +92,9 @@ namespace CosmicShore.Gameplay
         IVesselStatus VesselStatus => vessel.VesselStatus;
         IInputStatus _inputStatus => VesselStatus.InputStatus;
 
+        WaitForSeconds _waitPlayerSeek;
+        static readonly WaitForSeconds WaitAbilityStart = new WaitForSeconds(3);
+
         float _lastPitchTarget;
         float _lastYawTarget;
         float _lastRollTarget;
@@ -212,7 +215,7 @@ namespace CosmicShore.Gameplay
                     _targetPosition = bestPos;
                 }
 
-                yield return new WaitForSeconds(playerSeekUpdateInterval);
+                yield return _waitPlayerSeek;
             }
         }
 
@@ -231,6 +234,7 @@ namespace CosmicShore.Gameplay
                 ability.Ability = inst;
             }
 
+            _waitPlayerSeek = new WaitForSeconds(playerSeekUpdateInterval);
             _maxDistanceSquared = _maxDistance * _maxDistance;
             aggressiveness = defaultAggressiveness;
             throttle = defaultThrottle;
@@ -323,9 +327,9 @@ namespace CosmicShore.Gameplay
             throttle += throttleIncrease * Time.deltaTime;
         }
         
-        IEnumerator UseAbilityCoroutine(AIAbility action) 
+        IEnumerator UseAbilityCoroutine(AIAbility action)
         {
-            yield return new WaitForSeconds(3);
+            yield return WaitAbilityStart;
             while (AutoPilotEnabled)
             {
                 action.Ability.StartAction(actionExecutorRegistry, VesselStatus);
diff --git a/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs b/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs
index d9925eaae..ff1ebb4f5 100644
--- a/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs
+++ b/Assets/_Scripts/Controller/Arcade/TurnMonitorController.cs
@@ -1,5 +1,4 @@
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Utility;
 using Unity.Netcode;
 using UnityEngine;
@@ -70,7 +69,12 @@ namespace CosmicShore.Gameplay
             if (!_isRunning)
                 return;
 
-            if (!monitors.Any(m => m.CheckForEndOfTurn()))
+            bool anyTriggered = false;
+            for (int i = 0; i < monitors.Count; i++)
+            {
+                if (monitors[i].CheckForEndOfTurn()) { anyTriggered = true; break; }
+            }
+            if (!anyTriggered)
                 return;
 
             _isRunning = false;
diff --git a/Assets/_Scripts/Controller/Managers/MaterialStateManager.cs b/Assets/_Scripts/Controller/Managers/MaterialStateManager.cs
index d5b1871a4..a5ec3d7b7 100644
--- a/Assets/_Scripts/Controller/Managers/MaterialStateManager.cs
+++ b/Assets/_Scripts/Controller/Managers/MaterialStateManager.cs
@@ -3,7 +3,6 @@ using Unity.Collections;
 using Unity.Jobs;
 using Unity.Mathematics;
 using System.Collections.Generic;
-using System.Linq;
 using CosmicShore.Gameplay;
 using CosmicShore.Utility;
 using System;
@@ -15,6 +14,8 @@ namespace CosmicShore.Gameplay
         private readonly List<(MaterialPropertyAnimator animator, float4 brightColor, float4 darkColor, float3 spread)> propertyUpdateQueue =
             new List<(MaterialPropertyAnimator, float4, float4, float3)>(32);
 
+        private readonly List<MaterialPropertyAnimator> _animatorsToRemove = new List<MaterialPropertyAnimator>(8);
+
         private MaterialPropertyBlock sharedPropertyBlock;
 
         private const string BRIGHT_COLOR_PROP = "_BrightColor";
@@ -123,13 +124,14 @@ namespace CosmicShore.Gameplay
 
 
             // Validate all remaining active animators are actually animating
-            foreach (var animator in activeAnimators.ToArray())
+            _animatorsToRemove.Clear();
+            foreach (var animator in activeAnimators)
             {
                 if (!animator.IsAnimating)
-                {
-                    activeAnimators.Remove(animator);
-                }
+                    _animatorsToRemove.Add(animator);
             }
+            for (int i = 0; i < _animatorsToRemove.Count; i++)
+                activeAnimators.Remove(_animatorsToRemove[i]);
 
             // Batch apply property updates
             if (propertyUpdateQueue.Count > 0)
diff --git a/Assets/_Scripts/Controller/Vessel/ClearPrisms.cs b/Assets/_Scripts/Controller/Vessel/ClearPrisms.cs
index af19fd519..4c887081a 100644
--- a/Assets/_Scripts/Controller/Vessel/ClearPrisms.cs
+++ b/Assets/_Scripts/Controller/Vessel/ClearPrisms.cs
@@ -1,3 +1,4 @@
+using System.Collections.Generic;
 using CosmicShore.Gameplay;
 using UnityEngine;
 using CosmicShore.Utility;
@@ -6,6 +7,8 @@ namespace CosmicShore.Gameplay
 {
     public class ClearPrisms : MonoBehaviour
     {
+        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
+
         Transform mainCamera;
 
         [SerializeField, RequireInterface(typeof(IVessel))]
@@ -23,7 +26,10 @@ namespace CosmicShore.Gameplay
 
         CameraManager cameraManager;
         GeometryUtils.LineData lineData;
-        
+
+        readonly Dictionary<Collider, Renderer> _renderers = new Dictionary<Collider, Renderer>();
+        MaterialPropertyBlock _block;
+
         bool isInitialized;
 
 
@@ -65,6 +71,7 @@ namespace CosmicShore.Gameplay
             visibilityCapsule.isTrigger = true;
             visibilityCapsule.radius = capsuleRadius;
 
+            _block = new MaterialPropertyBlock();
             isInitialized = true;
         }
 
@@ -93,22 +100,34 @@ namespace CosmicShore.Gameplay
 
         void OnTriggerEnter(Collider other)
         {
-            Prism prism = other.GetComponent<Prism>();
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
