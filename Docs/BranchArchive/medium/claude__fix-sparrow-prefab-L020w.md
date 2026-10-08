# Branch archive: `claude/fix-sparrow-prefab-L020w`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-19 by Braden Hamilton
- **Unmerged commits:** 8
- **Forked from:** `14a193123` (2026-03-07, Merge pull request #414 from froglet-studio/claude/migrate-debug-logs-fNeXt)
- **Tip:** `8e13aad81`
- **Files touched (23):**
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_Animations/SparrowAnimatorController.controller`
  - `Assets/_Graphics/Design Assests/FX/fx_arclightning.mat`
  - `Assets/_Graphics/Materials/Graphs/ShipGraph.shadergraph`
  - `Assets/_Models/Ship Models/SparrowModel3.fbx`
  - `Assets/_Models/Ship Models/SparrowModel3.fbx.meta`
  - `Assets/_Models/Sparrow Missile.fbx`
  - `Assets/_Models/Sparrow Missile.fbx.meta`
  - `Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab`
  - `Assets/_Prefabs/Spaceships/Manta.prefab`
  - `Assets/_Prefabs/Spaceships/Sparrow.prefab`
  - `Assets/_SO_Assets/Camera/SparrowCameraSettingsSO.asset`
  - `Assets/_Scripts/Game/Animation/SparrowAnimationController.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs`
  - `Assets/_Scripts/Utility/MobilePerformanceManager.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/DeterministicBenchmarkController.cs.meta`
  - `Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs.meta`
  - `ProjectSettings/ProjectSettings.asset`

### `d6656eaa2` — Fix Sparrow prefab: pull missile mechanics from sparrow-missile-mechanics-zCche

_Claude, 2026-03-07 23:09:12 +0000_

```text
Brings over all Sparrow missile animation work from the user's feature branch:

- FireGunActionExecutor: add OnMissileFired(ammoBeforeFire, ammoCost) event fired
  before ammo is consumed so SparrowAnimationController can pick the correct clip,
  plus OnShotFired static event for telemetry
- SparrowAnimationController: wire Initialize/OnDestroy lifecycle, subscribe to
  OnMissileFired, play "Launch Missile 1" or "Launch Missile 2" on layer 1 with
  proper SetLayerWeight calls and a coroutine to reset the layer after the clip ends
- SparrowAnimatorController.controller: restore the Missile Launch layer (layer 1)
  with "Launch Missile 1" and "Launch Missile 2" states and correct animation GUIDs
  pointing at SparrowModel3
- Sparrow.prefab: bring in position reset (0,0,0), updated model instance to
  SparrowModel3, SkinnedMeshRenderer and animator references wired up
- SparrowModel3.fbx / SparrowModel3.fbx.meta: add the model asset that the prefab
  and animator controller target
```

```text
 Assets/_Animations/SparrowAnimatorController.controller               |  91 ++++++
 Assets/_Models/Ship Models/SparrowModel3.fbx                          | Bin 0 -> 6021116 bytes
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta                     | 528 +++++++++++++++++++++++++++++++
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 534 +++++---------------------------
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs          |  37 ++-
 .../Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs        |   9 +-
 6 files changed, 737 insertions(+), 462 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index bf5f0427a..0734d2896 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -1,3 +1,4 @@
+using System.Collections;
 using UnityEngine;
 
 namespace CosmicShore.Game.Animation
@@ -5,6 +6,9 @@ namespace CosmicShore.Game.Animation
     class SparrowAnimationController : VesselAnimation
     {
         [SerializeField] Animator animator;
+        [SerializeField] FireGunActionExecutor missileExecutor;
+
+        const int MissileLaunchLayer = 1;
 
         float currentPitch = 0;
         float currentYaw = 0;
@@ -12,6 +16,35 @@ namespace CosmicShore.Game.Animation
         float currentThrottle = 0;
         float animationSpeed = 3.25f;
 
+        public override void Initialize(IVesselStatus vesselStatus)
+        {
+            base.Initialize(vesselStatus);
+            if (missileExecutor != null)
+                missileExecutor.OnMissileFired += HandleMissileFired;
+        }
+
+        void OnDestroy()
+        {
+            if (missileExecutor != null)
+                missileExecutor.OnMissileFired -= HandleMissileFired;
+        }
+
+        void HandleMissileFired(float ammoBeforeFire, float ammoCost)
+        {
+            var animName = ammoBeforeFire >= 2f * ammoCost ? "Launch Missile 1" : "Launch Missile 2";
+            animator.SetLayerWeight(MissileLaunchLayer, 1f);
+            animator.Play(animName, MissileLaunchLayer);
+            StartCoroutine(ResetMissileLaunchLayer());
+        }
+
+        IEnumerator ResetMissileLaunchLayer()
+        {
+            yield return null; // wait one frame for the animator to enter the new state
+            while (animator.GetCurrentAnimatorStateInfo(MissileLaunchLayer).normalizedTime < 1f)
+                yield return null;
+            animator.SetLayerWeight(MissileLaunchLayer, 0f);
+        }
+
         protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
         {
 
@@ -25,7 +58,7 @@ namespace CosmicShore.Game.Animation
             animator.SetFloat("Yaw", currentYaw);
             animator.SetFloat("Roll", currentRoll);
             animator.SetFloat("Throttle", currentThrottle);
-            
+
         }
 
         protected override void Idle()
@@ -46,4 +79,4 @@ namespace CosmicShore.Game.Animation
 
         protected override void AssignTransforms() { /* NOOP Abstract Implementation */ }
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
index 69d6c535f..d78a0e815 100644
--- a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
@@ -8,8 +8,12 @@ using UnityEngine;
 
 public class FireGunActionExecutor : ShipActionExecutorBase
 {
+    /// <summary>Static event: each time a gun fires a single shot. Param = player name.</summary>
+    public static event Action<string> OnShotFired;
+
     public event Action OnGunFired;
     public event Action<float> OnAmmoChanged;
+    public event Action<float, float> OnMissileFired; // (ammoBeforeFire, ammoCost)
 
     [Header("Scene Refs")]
     [SerializeField] Gun gun;
@@ -100,10 +104,12 @@ public class FireGunActionExecutor : ShipActionExecutorBase
 
     public void Fire(FireGunActionSO so, IVesselStatus status)
     {
-        if (_resources.Resources[so.AmmoIndex].CurrentAmount < so.AmmoCost)
+        var currentAmmo = _resources.Resources[so.AmmoIndex].CurrentAmount;
+        if (currentAmmo < so.AmmoCost)
             return;
 
         _soRef = so;
+        OnMissileFired?.Invoke(currentAmmo, so.AmmoCost);
         _resources.ChangeResourceAmount(so.AmmoIndex, -so.AmmoCost);
 
         OnAmmoChanged?.Invoke(Ammo01);
@@ -115,6 +121,7 @@ public class FireGunActionExecutor : ShipActionExecutorBase
 
         AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.GunFire);
         OnGunFired?.Invoke();
+        OnShotFired?.Invoke(_status?.PlayerName);
 
         gun.FireGun(
             _worldMuzzleAnchor,
```

</details>

### `a0f0ce308` — Fix Sparrow prefab: replace MantaAnimationController with SparrowAnimationController

_Claude, 2026-03-08 00:02:38 +0000_

```text
- Swap MantaAnimationController (wrong vessel) for SparrowAnimationController
- Add stripped Animator entry for the new SparrowModel3 FBX (guid 1b0725c5c79aad145835d47b9ecfa510)
- Wire animator field to stripped Animator (fileID 8796843700321160693)
- Wire missileExecutor field to FireGunActionExecutor (fileID 4658886838346608747)
- Restore _shipGeometries[0] to SparrowModel3 root GameObject (fileID 2117239821978650842)

The d6656eaa commit swapped the FBX but left MantaAnimationController in place with
null references, breaking missile animations and geometry caching.
```

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab | 18 ++++++++++++------
 1 file changed, 12 insertions(+), 6 deletions(-)
```

### `5267f456c` — Fix SparrowAnimationController: add public modifier, fix prefab YAML whitespace

_Claude, 2026-03-08 00:27:03 +0000_

```text
- Add `public` to SparrowAnimationController class (Unity requires public MonoBehaviours
  for reliable serialization)
- Restore trailing spaces on m_Name and m_EditorClassIdentifier in prefab YAML
  (null vs empty string difference in Unity's YAML parser)
```

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab                    | 4 ++--
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs | 2 +-
 2 files changed, 3 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index 0734d2896..45504c6e5 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -3,7 +3,7 @@ using UnityEngine;
 
 namespace CosmicShore.Game.Animation
 {
-    class SparrowAnimationController : VesselAnimation
+    public class SparrowAnimationController : VesselAnimation
     {
         [SerializeField] Animator animator;
         [SerializeField] FireGunActionExecutor missileExecutor;
```

</details>

### `c78b93dbb` — Import Sparrow prefab from claude/sparrow-missile-mechanics-zCche

_Claude, 2026-03-08 00:31:54 +0000_

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab | 92 ++++---------------------------------------------------------
 1 file changed, 6 insertions(+), 86 deletions(-)
```

### `c778dc86b` — Make Sparrow visible in game Set up missile shooting animations

_Braden Hamilton, 2026-03-14 21:22:53 -0500_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 72 ++++++++++++++++++++++++++++-----
 Assets/_Graphics/Materials/Graphs/ShipGraph.shadergraph               |  4 +-
 Assets/_Prefabs/Spaceships/Manta.prefab                               | 41 +++++++++++--------
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 49 ++++++++++++++++++----
 Assets/_Scripts/Utility/MobilePerformanceManager.cs.meta              |  2 +
 Assets/_Scripts/Utility/Tools/Benchmarking.meta                       |  8 ++++
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkReport.cs.meta    |  2 +
 .../Utility/Tools/Benchmarking/BenchmarkSessionConfig.cs.meta         |  2 +
 .../Utility/Tools/Benchmarking/BenchmarkSessionSummary.cs.meta        |  2 +
 Assets/_Scripts/Utility/Tools/Benchmarking/BenchmarkWindow.cs.meta    |  2 +
 .../Tools/Benchmarking/DeterministicBenchmarkController.cs.meta       |  2 +
 Assets/_Scripts/Utility/Tools/Benchmarking/PerformanceSampler.cs.meta |  2 +
 ProjectSettings/ProjectSettings.asset                                 | 44 ++++++++++++++++++++
 13 files changed, 196 insertions(+), 36 deletions(-)
```

### `0ab2c33c4` — Set up Missile shooting animations Remake missle shape

_Braden Hamilton, 2026-03-19 16:46:22 -0500_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 72 +++++----------------------------
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta                     |  2 +-
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             | 35 ++++++++++++----
 Assets/_SO_Assets/Camera/SparrowCameraSettingsSO.asset                | 13 +++---
 4 files changed, 44 insertions(+), 78 deletions(-)
```

### `8e13aad81` — Set Up Missile shooting animations

_Braden Hamilton, 2026-03-19 17:39:25 -0500_

```text
 Assets/_Animations/SparrowAnimatorController.controller |  158 +-
 Assets/_Graphics/Design Assests/FX/fx_arclightning.mat  |    1 +
 Assets/_Models/Sparrow Missile.fbx                      |  Bin 0 -> 57916 bytes
 Assets/_Models/Sparrow Missile.fbx.meta                 |  107 +
 Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab    | 4984 ++++++++++++++++++++++++++++++++++++++++++++-
 5 files changed, 5167 insertions(+), 83 deletions(-)
```

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
