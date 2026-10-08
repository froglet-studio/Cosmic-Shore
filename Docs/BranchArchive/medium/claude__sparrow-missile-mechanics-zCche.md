# Branch archive: `claude/sparrow-missile-mechanics-zCche`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-03-07 by Braden Hamilton
- **Unmerged commits:** 10
- **Forked from:** `dbfae0bc7` (2026-02-26, Give Squirrel New Jet Effects)
- **Tip:** `59a8b0df2`
- **Files touched (66):**
  - `Assets/Buttons.meta`
  - `Assets/Buttons/Button_Flat_Gray.png`
  - `Assets/Buttons/Button_Flat_Gray.png.meta`
  - `Assets/Buttons/Button_Flat_Green.png`
  - `Assets/Buttons/Button_Flat_Green.png.meta`
  - `Assets/Buttons/Button_Flat_Green_Flipped.png`
  - `Assets/Buttons/Button_Flat_Green_Flipped.png.meta`
  - `Assets/Buttons/Button_Flat_Orange.png`
  - `Assets/Buttons/Button_Flat_Orange.png.meta`
  - `Assets/Buttons/Button_Flat_Orange_Flipped.png`
  - `Assets/Buttons/Button_Flat_Orange_Flipped.png.meta`
  - `Assets/Buttons/Button_Flat_Purple.png`
  - `Assets/Buttons/Button_Flat_Purple.png.meta`
  - `Assets/Buttons/Button_Flat_White.png`
  - `Assets/Buttons/Button_Flat_White.png.meta`
  - `Assets/Buttons/Button_Flat_White_Flipped.png`
  - `Assets/Buttons/Button_Flat_White_Flipped.png.meta`
  - `Assets/Buttons/Button_Gradient_Blue.png`
  - `Assets/Buttons/Button_Gradient_Blue.png.meta`
  - `Assets/Buttons/Button_Gradient_Gray.png`
  - `Assets/Buttons/Button_Gradient_Gray.png.meta`
  - `Assets/Buttons/Button_Gradient_Green.png`
  - `Assets/Buttons/Button_Gradient_Green.png.meta`
  - `Assets/Buttons/Button_Gradient_Green_Flipped.png`
  - `Assets/Buttons/Button_Gradient_Green_Flipped.png.meta`
  - `Assets/Buttons/Button_Gradient_Orange.png`
  - `Assets/Buttons/Button_Gradient_Orange.png.meta`
  - `Assets/Buttons/Button_Outline_White.png`
  - `Assets/Buttons/Button_Outline_White.png.meta`
  - `Assets/Buttons/Controller.meta`
  - `Assets/Buttons/Controller/ButtonA.png`
  - `Assets/Buttons/Controller/ButtonA.png.meta`
  - `Assets/Buttons/Controller/ButtonB.png`
  - `Assets/Buttons/Controller/ButtonB.png.meta`
  - `Assets/Buttons/Controller/ButtonCircle.png`
  - `Assets/Buttons/Controller/ButtonCircle.png.meta`
  - `Assets/Buttons/Controller/ButtonCross.png`
  - `Assets/Buttons/Controller/ButtonCross.png.meta`
  - `Assets/Buttons/Controller/ButtonSquare.png`
  - `Assets/Buttons/Controller/ButtonSquare.png.meta`
  - … and 26 more

### `30be56d4b` — Play missile launch animation based on available ammo count

_Claude, 2026-03-03 23:13:57 +0000_

```text
When the Sparrow fires a SkyBurst Rocket, play "Missle Launch 1" if 2
missiles were available, or "Missle Launch 2" if only 1 was available.

- FireGunActionExecutor: add OnMissileFired event (ammoBeforeFire, ammoCost)
  fired just before ammo is consumed
- SparrowAnimationController: wire missileExecutor field, subscribe to
  OnMissileFired, and call animator.Play() with the correct state name
```

```text
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs               | 20 ++++++++++++++++++++
 Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs |  5 ++++-
 2 files changed, 24 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index bf5f0427a..cac12180f 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -5,6 +5,7 @@ namespace CosmicShore.Game.Animation
     class SparrowAnimationController : VesselAnimation
     {
         [SerializeField] Animator animator;
+        [SerializeField] FireGunActionExecutor missileExecutor;
 
         float currentPitch = 0;
         float currentYaw = 0;
@@ -12,6 +13,25 @@ namespace CosmicShore.Game.Animation
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
+            var animName = ammoBeforeFire >= 2f * ammoCost ? "Missle Launch 1" : "Missle Launch 2";
+            animator.Play(animName);
+        }
+
         protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
         {
 
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
index 69d6c535f..46eda3253 100644
--- a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FireGunActionExecutor.cs
@@ -10,6 +10,7 @@ public class FireGunActionExecutor : ShipActionExecutorBase
 {
     public event Action OnGunFired;
     public event Action<float> OnAmmoChanged;
+    public event Action<float, float> OnMissileFired; // (ammoBeforeFire, ammoCost)
 
     [Header("Scene Refs")]
     [SerializeField] Gun gun;
@@ -100,10 +101,12 @@ public class FireGunActionExecutor : ShipActionExecutorBase
 
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
```

</details>

### `f9653c831` — Add Missile launch animations to Sparrow ship

_Braden Hamilton, 2026-03-03 17:28:45 -0600_

```text
 Assets/SparrowModel1.fbx                                              | Bin 0 -> 6021116 bytes
 Assets/SparrowModel1.fbx.meta                                         | 107 ++++++++++++
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 289 ++------------------------------
 Assets/_Animations/SparrowAnimatorController.controller               |  91 ++++++++++
 Assets/_Models/Ship Models/SparrowModel1.fbx.meta                     |   6 +-
 Assets/_Models/Ship Models/SparrowModel2.fbx                          | Bin 0 -> 4571932 bytes
 Assets/_Models/Ship Models/SparrowModel2.fbx.meta                     | 107 ++++++++++++
 Assets/_Models/Ship Models/SparrowModel3.fbx                          | Bin 0 -> 6021116 bytes
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta                     | 107 ++++++++++++
 ProjectSettings/ProjectSettings.asset                                 |  50 ++++++
 10 files changed, 474 insertions(+), 283 deletions(-)
```

### `1949a676a` — Add missile trail effects Add missile launch animations

_Braden Hamilton, 2026-03-03 18:32:09 -0600_

```text
 Assets/_Graphics/Design Assests/FX/fx_arclightning.mat  |   1 +
 Assets/_Graphics/Materials/DangerProjectileMaterial.mat |   1 +
 Assets/_Graphics/Materials/FireProjectileMaterial.mat   |   1 +
 Assets/_Models/Materials/CreatureMaterial.mat           |   2 +-
 Assets/_Models/Sparrow Missile.fbx                      | Bin 0 -> 57916 bytes
 Assets/_Models/Sparrow Missile.fbx.meta                 | 107 ++++++++++++
 Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab    | 418 ++++++++++++++++++++++++++++++++--------------
 7 files changed, 406 insertions(+), 124 deletions(-)
```

### `5929d0e4e` — Switch Sparrow Model Rig up shooting animation

_Braden Hamilton, 2026-03-06 19:44:29 -0600_

```text
 Assets/_Models/Ship Models/SparrowModel2.fbx      | Bin 4571932 -> 0 bytes
 Assets/_Models/Ship Models/SparrowModel2.fbx.meta | 107 ----------
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta | 410 +++++++++++++++++++++++++++++++++++++-
 Assets/_Prefabs/Spaceships/Sparrow.prefab         | 552 +++++++---------------------------------------------
 4 files changed, 482 insertions(+), 587 deletions(-)
```

### `63c1c07a4` — Fix Sparrow

_Braden Hamilton, 2026-03-06 19:53:15 -0600_

```text
 Assets/_Prefabs/Spaceships/Sparrow.prefab | 6 +++---
 1 file changed, 3 insertions(+), 3 deletions(-)
```

### `397ee6857` — Fix Sparrow missile animations: wrong state names, missing layer weight, broken YawRight clip

_Claude, 2026-03-07 02:01:47 +0000_

```text
Three bugs introduced in the animation branch merge:

1. SparrowAnimationController called animator.Play("Missle Launch 1/2") — wrong
   word order and missing 'i'. Correct state names are "Launch Missile 1/2".
   Also missing the layer index (1) so Unity couldn't find the states at all.

2. Missile Launch animator layer has m_DefaultWeight: 0, so even a correct Play()
   call was invisible. Now SetLayerWeight(1, 1f) before playing and a coroutine
   resets it to 0 after the clip finishes.

3. SparrowModel1.fbx.meta swapped the YawRight animation slot (internalID
   4977852477300310412) to point at a_Sparrow_Mesh|Idle_SparrowAnim.006 instead
   of Sparrow_Armature+Mesh|YawRight_SparrowAnim, breaking the yaw-right blend
   in the movement tree. Reverted to the correct take name.
```

```text
 Assets/_Models/Ship Models/SparrowModel1.fbx.meta            |  4 ++--
 Assets/_Scripts/Game/Animation/SparrowAnimationController.cs | 17 +++++++++++++++--
 2 files changed, 17 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
index cac12180f..fd7189496 100644
--- a/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
+++ b/Assets/_Scripts/Game/Animation/SparrowAnimationController.cs
@@ -1,3 +1,4 @@
+using System.Collections;
 using UnityEngine;
 
 namespace CosmicShore.Game.Animation
@@ -7,6 +8,8 @@ namespace CosmicShore.Game.Animation
         [SerializeField] Animator animator;
         [SerializeField] FireGunActionExecutor missileExecutor;
 
+        const int MissileLaunchLayer = 1;
+
         float currentPitch = 0;
         float currentYaw = 0;
         float currentRoll = 0;
@@ -28,8 +31,18 @@ namespace CosmicShore.Game.Animation
 
         void HandleMissileFired(float ammoBeforeFire, float ammoCost)
         {
-            var animName = ammoBeforeFire >= 2f * ammoCost ? "Missle Launch 1" : "Missle Launch 2";
-            animator.Play(animName);
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
         }
 
         protected override void PerformShipPuppetry(float pitch, float yaw, float roll, float throttle)
```

</details>

### `94b5442ee` — Fix SparrowAnimatorController: update missile animation GUIDs to SparrowModel3

_Claude, 2026-03-07 02:03:36 +0000_

```text
SparrowModel2.fbx was deleted (commit 5929d0e4e) but the controller still
referenced its GUID for the "Launch Missile 2" motion. SparrowModel3.fbx has
both missile clips at the same internal fileIDs, so update both motion GUIDs
to SparrowModel3 (1b0725c5c79aad145835d47b9ecfa510):

- Launch Missile 1: was pointing at Assets/SparrowModel1.fbx (root copy, GUID
  3818ba29) — now correctly points to SparrowModel3
- Launch Missile 2: was pointing at deleted SparrowModel2.fbx (GUID 829ae623)
  — now correctly points to SparrowModel3
```

```text
 Assets/_Animations/SparrowAnimatorController.controller | 4 ++--
 1 file changed, 2 insertions(+), 2 deletions(-)
```

### `59a8b0df2` — Fix Sparrow Fix animaitons

_Braden Hamilton, 2026-03-07 16:55:59 -0600_

```text
 Assets/{_Graphics => }/Buttons/Button_Gradient_Gray.png               | Bin
 Assets/{_Graphics => }/Buttons/Button_Gradient_Gray.png.meta          |   0
 Assets/{_Graphics => }/Buttons/Button_Gradient_Green.png              | Bin
 Assets/{_Graphics => }/Buttons/Button_Gradient_Green.png.meta         |   0
 Assets/{_Graphics => }/Buttons/Button_Gradient_Green_Flipped.png      | Bin
 Assets/{_Graphics => }/Buttons/Button_Gradient_Green_Flipped.png.meta |   0
 Assets/{_Graphics => }/Buttons/Button_Gradient_Orange.png             | Bin
 Assets/{_Graphics => }/Buttons/Button_Gradient_Orange.png.meta        |   0
 Assets/{_Graphics => }/Buttons/Button_Outline_White.png               | Bin
 Assets/{_Graphics => }/Buttons/Button_Outline_White.png.meta          |   0
 Assets/{_Graphics => }/Buttons/Controller.meta                        |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonA.png                 | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonA.png.meta            |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonB.png                 | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonB.png.meta            |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonCircle.png            | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonCircle.png.meta       |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonCross.png             | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonCross.png.meta        |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonSquare.png            | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonSquare.png.meta       |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonTriangle.png          | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonTriangle.png.meta     |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonX.png                 | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonX.png.meta            |   0
 Assets/{_Graphics => }/Buttons/Controller/ButtonY.png                 | Bin
 Assets/{_Graphics => }/Buttons/Controller/ButtonY.png.meta            |   0
 Assets/_Models/Ship Models/SparrowModel3.fbx.meta                     |  17 ++++++++++++++++-
 Assets/_Prefabs/Spaceships/Sparrow.prefab                             |   6 +++---
 48 files changed, 19 insertions(+), 4 deletions(-)
```

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
