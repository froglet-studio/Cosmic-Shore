# Branch archive: `Reintroduce-Falcon`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

## What this branch is

**Reintroduce Falcon vessel with gun ring**

Brought the Falcon vessel back into the game: fixed its shooting, added a ring of spinning guns, switchable fire modes, a Falcon boost ability, a Falcon trail, camera settings and a Falcon HUD. It was worked on from Nov 2025 to Apr 2026 against the old folder layout (Game/Ship) and also started a BrittleStar prefab. Many commits are merges from development.

- **Status:** Partly landed
- **Areas:** Falcon vessel, vessel abilities, weapons, vessel HUD
- **Already in bleeding-edge:** bleeding-edge has Assets/_Prefabs/Spacevessels/Falcon.prefab and FalconProjectile.prefab, but no FalconModeSwitchingFireSO, FalconBoostAction, FalconHUDVessel or GunRingTransformer; that work moved to the Bleeding-Edge-Reintroduce-Falcon / BrittleStar branches (claude/intelligent-cannon-2uqj0), also unmerged.
- **Risk if deleted:** medium
- **Suggestion (2026-10-08):** can be deleted after archiving — Written against a pre-restructure layout and continued in the BrittleStar branches, which carry the same gun-ring and mode-switch ideas in current paths.

## Evidence

- **Last commit:** 2026-04-22 by Philip Appoh
- **Unmerged commits:** 19
- **Forked from:** `8a3d6d0d3` (2026-04-16, Merge pull request #483 from froglet-studio/claude/update-skimmer-prism-effect)
- **Tip:** `45afa8ad6`
- **Files touched (47):**
  - `Assets/DefaultNetworkPrefabs.asset`
  - `Assets/FalconBoostActionExecutor.cs`
  - `Assets/FalconBoostActionExecutor.cs.meta`
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_DLLs.meta`
  - `Assets/_DLLs/MatchMaking.meta`
  - `Assets/_Prefabs/Projectile/FalconProjectile.prefab`
  - `Assets/_Prefabs/Spaceships/BrittleStar.prefab`
  - `Assets/_Prefabs/Spaceships/BrittleStar.prefab.meta`
  - `Assets/_Prefabs/Spaceships/Falcon.prefab`
  - `Assets/_SO_Assets/Camera/FalconCameraSettingsSO.asset`
  - `Assets/_SO_Assets/Camera/FalconCameraSettingsSO.asset.meta`
  - `Assets/_SO_Assets/SOAP/Configuration Data/Ship Prefab Container.asset`
  - `Assets/_SO_Assets/ShipActions/Falcon.meta`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconBoostAction.asset`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconBoostAction.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconBoostSO.cs`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconBoostSO.cs.meta`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.asset`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.asset.meta`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs`
  - `Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs.meta`
  - `Assets/_Scenes/VolumeTestScene.unity`
  - `Assets/_Scripts/Game/Ship/GunRingTransformer.cs`
  - `Assets/_Scripts/Game/Ship/GunRingTransformer.cs.meta`
  - `Assets/_Scripts/Game/Ship/GunTransformer.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs.meta`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs.meta`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconTrailSO.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconTrailSO.cs.meta`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconsTrailExecutor.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconsTrailExecutor.cs.meta`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs.meta`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs`
  - `Assets/_Scripts/Game/Ship/TrailPassives.meta`
  - … and 7 more

### `196ec061b` — Reintroduce Falcon init comit

_Philip Appoh, 2025-11-03 14:18:27 -0600_

```text
 Assets/_DLLs.meta                                                     |    8 +
 Assets/_DLLs/MatchMaking.meta                                         |    8 +
 Assets/_Prefabs/Spaceships/Falcon.prefab                              | 6067 +++++++++++++++++++++++--------
 Assets/_SO_Assets/Camera/FalconCameraSettingsSO.asset                 |   26 +
 Assets/_SO_Assets/Camera/FalconCameraSettingsSO.asset.meta            |    8 +
 Assets/_SO_Assets/SOAP/Configuration Data/Ship Prefab Container.asset |    1 +
 Assets/_Scenes/VolumeTestScene.unity                                  |  106 +-
 Assets/_Scripts/Utility/SerializeInterface.meta                       |    8 +
 8 files changed, 4663 insertions(+), 1569 deletions(-)
```

### `3e0f4733e` — Fixed Falcon Shooting

_Philip Appoh, 2025-11-04 12:05:41 -0600_

```text
 Assets/DefaultNetworkPrefabs.asset                                    |    6 +
 Assets/_Prefabs/Spaceships/BrittleStar.prefab                         | 4075 +++++++++++++++++++++++++++++++
 Assets/_Prefabs/Spaceships/BrittleStar.prefab.meta                    |    7 +
 Assets/_Prefabs/Spaceships/Falcon.prefab                              |   34 +-
 Assets/_SO_Assets/SOAP/Configuration Data/Ship Prefab Container.asset |    1 +
 Assets/_Scripts/Models/Enums/VesselClassType.cs                       |    1 +
 6 files changed, 4112 insertions(+), 12 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Models/Enums/VesselClassType.cs b/Assets/_Scripts/Models/Enums/VesselClassType.cs
index 35ec17ad5..1e8fcf9aa 100644
--- a/Assets/_Scripts/Models/Enums/VesselClassType.cs
+++ b/Assets/_Scripts/Models/Enums/VesselClassType.cs
@@ -18,4 +18,5 @@ public enum VesselClassType
     Falcon = 9,
     Shrike = 10,
     Sparrow = 11,
+    BrittleStar = 12,
 }
\ No newline at end of file
```

</details>

### `edee4f184` — Added Gun Spin Transforms

_Philip Appoh, 2025-11-06 11:25:15 -0600_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 72 ++++---------------------
 Assets/_Prefabs/Projectile/FalconProjectile.prefab                    | 78 +++++++++++++++++++++++----
 Assets/_Prefabs/Spaceships/Falcon.prefab                              | 40 +++++++++++++-
 Assets/_SO_Assets/Camera/FalconCameraSettingsSO.asset                 |  2 +-
 Assets/_Scenes/VolumeTestScene.unity                                  | 94 ---------------------------------
 Assets/_Scripts/Game/Ship/GunRingTransformer.cs                       | 68 ++++++++++++++++++++++++
 Assets/_Scripts/Game/Ship/GunRingTransformer.cs.meta                  |  2 +
 Assets/_Scripts/Game/Ship/GunTransformer.cs                           |  8 +--
 8 files changed, 190 insertions(+), 174 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 107 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/GunRingTransformer.cs b/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
new file mode 100644
index 000000000..74a69af05
--- /dev/null
+++ b/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
@@ -0,0 +1,68 @@
+using CosmicShore.Core;
+using CosmicShore.Game;
+using UnityEngine;
+using UnityEngine.UIElements;
+
+namespace CosmicShore
+{
+    public class GunRingTransformer : MonoBehaviour
+    {
+        [SerializeField] float radius = 20f;
+        float constant;
+
+        [RequireInterface(typeof(IVesselStatus))]
+        [SerializeField] MonoBehaviour shipInstance;
+        [SerializeField] Transform gunFocus;
+        [SerializeField] float UnitsPerSec = 3;
+
+        IInputStatus InputStatus => (shipInstance as IVesselStatus).InputStatus;
+
+
+
+
+        void Start()
+        {
+            var children = GetComponentsInChildren<Transform>();
+            constant = 2 * Mathf.PI / (children.Length - 1); //Finds the radian so all point are equally spaced | Subtract one to remove the parent
+            InputStatus.RightClampedPosition.SqrMagnitude();
+        }
+
+        // Update is called once per frame
+        void Update()
+        {
+            var i = 0;
+
+            foreach (var child in GetComponentsInChildren<Transform>())
+            {
+
+                if (child == transform)
+                {
+                    continue;
+                }
+
+                //Compute an angle offset from the joystick direction, rotated 90�, and spaced apart by a multiple of constant
+                var RotatedAngle = i * constant + (Mathf.PI) / 2 - Mathf.Atan2(InputStatus.RightNormalizedJoystickPosition.y,InputStatus.RightNormalizedJoystickPosition.x); 
+                i++;
+
+
+
+                /* 
+                  Smoothly move the child toward its target position on a circular formation that rotates based on the right joystick�s direction. 
+                  The target point is determined by angle 'RotatedAngle' and radius, while Slerp ensures smooth, frame-rate independent motion along the circle 
+                */
+                child.transform.localPosition = Vector3.Slerp(child.transform.localPosition, radius * new Vector3(Mathf.Sin(RotatedAngle), Mathf.Cos(RotatedAngle), 0), Time.deltaTime * UnitsPerSec);
+
+
+                /* 
+                   Smoothly adjust the gun's focus position forward or backward based on how far the right joystick is pushed. 
+                   The farther the stick is tilted, the greater the Z offset (up to 300 units), with a base offset of 70.
+                */
+                gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, new Vector3(0, 0, 300 * InputStatus.RightNormalizedJoystickPosition.SqrMagnitude() + 70), Time.deltaTime);
+
+                // Orient child to face the gun focus.
+                child.LookAt(gunFocus);
+
+            }
+        }
+    }
+}
diff --git a/Assets/_Scripts/Game/Ship/GunTransformer.cs b/Assets/_Scripts/Game/Ship/GunTransformer.cs
index 8380d451b..2eba963b2 100644
--- a/Assets/_Scripts/Game/Ship/GunTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/GunTransformer.cs
@@ -12,7 +12,7 @@ namespace CosmicShore
         [RequireInterface(typeof(IVesselStatus))]
```

</details>

### `189dc718c` — Added Falcon Gun Ring Prototype

_Philip Appoh, 2025-11-07 11:12:02 -0600_

```text
 Assets/_Prefabs/Projectile/FalconProjectile.prefab |  18 ++++----
 Assets/_Prefabs/Spaceships/Falcon.prefab           | 113 ++++++++++++++++++++++++++++++++++++++++++---------
 Assets/_Scripts/Game/Ship/GunRingTransformer.cs    |  77 +++++++++++++++++++----------------
 ProjectSettings/TagManager.asset                   |   1 +
 4 files changed, 149 insertions(+), 60 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 116 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/GunRingTransformer.cs b/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
index 74a69af05..2acf744d0 100644
--- a/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
@@ -1,68 +1,77 @@
 using CosmicShore.Core;
 using CosmicShore.Game;
+using System.Collections.Generic;
+using System.Linq;
 using UnityEngine;
-using UnityEngine.UIElements;
+using UnityEngine.InputSystem;
 
 namespace CosmicShore
 {
     public class GunRingTransformer : MonoBehaviour
     {
-        [SerializeField] float radius = 20f;
-        float constant;
-
-        [RequireInterface(typeof(IVesselStatus))]
         [SerializeField] MonoBehaviour shipInstance;
         [SerializeField] Transform gunFocus;
-        [SerializeField] float UnitsPerSec = 3;
+        [SerializeField] GameObject pivotObject;
+
+        [SerializeField] private float radius = 20.0f;
+        [SerializeField] private float rotationSpeed = 20.0f;
+        [SerializeField] private float speed = 10.0f;
+        
+
+
+       void Start()
+        {
+            foreach (var child in GetComponentsInChildren<Transform>())
+            {
+
+                if (child == transform) continue; // skip the parent itself
+
+                // Get direction from origin to child
+                Vector3 direction = (child.position - shipInstance.transform.position).normalized;
+
+                // Move outward equally along that direction
+                child.position = shipInstance.transform.position + direction * radius;
+
+
+            }
+
 
-        IInputStatus InputStatus => (shipInstance as IVesselStatus).InputStatus;
 
 
 
 
-        void Start()
-        {
-            var children = GetComponentsInChildren<Transform>();
-            constant = 2 * Mathf.PI / (children.Length - 1); //Finds the radian so all point are equally spaced | Subtract one to remove the parent
-            InputStatus.RightClampedPosition.SqrMagnitude();
         }
 
-        // Update is called once per frame
+        
         void Update()
         {
-            var i = 0;
+            Vector2 rightStick = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
+
+
+
+
 
             foreach (var child in GetComponentsInChildren<Transform>())
             {
 
-                if (child == transform)
-                {
-                    continue;
-                }
+                child.transform.RotateAround(pivotObject.transform.position, new Vector3(0, 0, 1), rotationSpeed * Time.deltaTime);
```

</details>

### `48129e893` — Added Falcon Mode Switching Fire

_Philip Appoh, 2025-11-10 10:53:01 -0600_

```text
 Assets/_Prefabs/Spaceships/Falcon.prefab                              |  3 ++-
 Assets/_SO_Assets/ShipActions/Falcon.meta                             |  8 ++++++++
 Assets/_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset    | 17 ++++++++++++++++
 .../_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset.meta  |  8 ++++++++
 Assets/_Scripts/Game/Ship/GunRingTransformer.cs                       | 28 ++-----------------------
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   | 27 +++++++++++++++++++++++++
 .../R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs.meta   | 14 +++++++++++++
 ProjectSettings/QualitySettings.asset                                 | 36 +++++++++++++++++++++++----------
 8 files changed, 103 insertions(+), 38 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/GunRingTransformer.cs b/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
index 2acf744d0..6c6a48d0f 100644
--- a/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
+++ b/Assets/_Scripts/Game/Ship/GunRingTransformer.cs
@@ -17,8 +17,6 @@ namespace CosmicShore
         [SerializeField] private float rotationSpeed = 20.0f;
         [SerializeField] private float speed = 10.0f;
         
-
-
        void Start()
         {
             foreach (var child in GetComponentsInChildren<Transform>())
@@ -34,44 +32,22 @@ namespace CosmicShore
 
 
             }
-
-
-
-
-
-
         }
 
         
         void Update()
         {
+            //This very hacky and probally will get removed when brittlestar become more finialized
             Vector2 rightStick = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
 
-
-
-
-
             foreach (var child in GetComponentsInChildren<Transform>())
             {
-
-                child.transform.RotateAround(pivotObject.transform.position, new Vector3(0, 0, 1), rotationSpeed * Time.deltaTime);
-
-
+                child.transform.RotateAround(pivotObject.transform.position, pivotObject.transform.forward, rotationSpeed * Time.deltaTime);
                 Vector3 targetFocus = new Vector3(0, 0, 300f * rightStick.sqrMagnitude + 70f);
                 gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, targetFocus, Time.deltaTime * speed);
-
-
-
                 child.LookAt(gunFocus);
-
-
-
             }
 
-
-
-
-
             }
     }
 }
```

</details>

### `d5ee00954` — Stared Work On Falcon Boost

_Philip Appoh, 2025-11-13 11:26:27 -0600_

```text
 Assets/_Prefabs/Spaceships/Falcon.prefab                               |  3 ++-
 Assets/_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset     |  4 ++--
 .../Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs       | 32 ++++++++++++++++++++++++++++++++
 .../Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs.meta  |  2 ++
 .../_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs | 17 +++++++++++++++++
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs.meta     |  2 ++
 6 files changed, 57 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

### `5dff15735` — Continuing work on falcon switch weapon

_Philip Appoh, 2025-11-21 11:08:45 -0600_

```text
 Assets/_Prefabs/Spaceships/Falcon.prefab                              | 47 ++++++++++++++++++++++++++++++++-
 Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.asset              | 14 ++++++++++
 Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.asset.meta         |  8 ++++++
 .../Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs      |  2 +-
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |  2 +-
 .../_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconTrailSO.cs | 22 +++++++++++++++
 .../Game/Ship/R_ShipActions/Data Containers/FalconTrailSO.cs.meta     |  2 ++
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs         | 16 ++++++++---
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs.meta    |  2 +-
 9 files changed, 107 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff

```

</details>

### `cddcb9121` — Add Proto typed Falcon Switching

_Philip Appoh, 2025-12-01 14:04:05 -0600_

```text
 Assets/DefaultNetworkPrefabs.asset                                    |   6 ++
 Assets/_Prefabs/Spaceships/Falcon.prefab                              | 132 ++++++++++++++------------------
 Assets/_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset    |   4 +-
 Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.asset              |   2 +-
 Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs                 |  15 ++++
 Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs.meta            |   2 +
 Assets/_Scenes/VolumeTestScene.unity                                  |   2 +-
 .../Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs      |  20 ++---
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |  19 +++--
 .../_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconTrailSO.cs |  22 ------
 .../Game/Ship/R_ShipActions/Data Containers/FalconTrailSO.cs.meta     |   2 -
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailExecutor.cs   |  60 +++++++++++++++
 .../Ship/R_ShipActions/Data Containers/FalconsTrailExecutor.cs.meta   |   2 +
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs         |  67 ++++++++++++----
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailSO.cs.meta    |   2 +-
 15 files changed, 223 insertions(+), 134 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs b/Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs
new file mode 100644
index 000000000..e9fbe8d24
--- /dev/null
+++ b/Assets/_SO_Assets/ShipActions/Falcon/FalconTrailSO.cs
@@ -0,0 +1,15 @@
+using CosmicShore.Game;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    [CreateAssetMenu(fileName = "FalconTrailSO", menuName = "ScriptableObjects/Vessel Actions/Falcon Trail SO")]
+    public class FalconTrailSO : ShipActionSO
+    {
+        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
+           => execs?.Get<FalconsTrailExecutor>()?.Begin(this);
+
+        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
+            => execs?.Get<FalconsTrailExecutor>()?.End();
+    }
+}
```

</details>

### `7899dc1ae` — Contioned work on falcon

_Philip Appoh, 2025-12-19 13:17:35 -0600_

```text
 Assets/FalconBoostActionExecutor.cs                                   |  64 ++++++++
 Assets/FalconBoostActionExecutor.cs.meta                              |   2 +
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 279 ++------------------------------
 Assets/_Prefabs/Spaceships/Falcon.prefab                              |  54 ++++++-
 Assets/_SO_Assets/ShipActions/Falcon/FalconBoostAction.asset          |  14 ++
 Assets/_SO_Assets/ShipActions/Falcon/FalconBoostAction.asset.meta     |   8 +
 Assets/_SO_Assets/ShipActions/Falcon/FalconBoostSO.cs                 |  21 +++
 .../ShipActions/Falcon/FalconBoostSO.cs.meta}                         |   0
 Assets/_SO_Assets/ShipActions/Falcon/FalconModeSwitchingFire.asset    |   4 +-
 .../Game/Ship/R_ShipActions/Data Containers/FalconBoostAction.cs      |  32 ----
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |  49 ++++--
 .../Game/Ship/R_ShipActions/Data Containers/FalconsTrailExecutor.cs   |  11 +-
 Assets/_Scripts/Game/Ship/TrailPassives.meta                          |   8 +
 Assets/_Scripts/VesselHUD/Controller/VesselHUDController.cs           |  13 ++
 14 files changed, 241 insertions(+), 318 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 121 lines)</summary>

```diff
diff --git a/Assets/FalconBoostActionExecutor.cs b/Assets/FalconBoostActionExecutor.cs
new file mode 100644
index 000000000..4ba3153cc
--- /dev/null
+++ b/Assets/FalconBoostActionExecutor.cs
@@ -0,0 +1,64 @@
+using CosmicShore.Game;
+using Obvious.Soap;
+using System.Diagnostics;
+using System.Threading;
+using UnityEngine;
+
+
+namespace CosmicShore
+{
+    public class FalconBoostActionExecutor : ShipActionExecutorBase
+    {
+        private IVesselStatus _status;
+        public ScriptableEventNoParam OnMiniGameTurnEnd;
+        private CancellationTokenSource _cts;
+
+
+
+        void OnEnable()
+        {
+            if (_status == null) return;
+            _status.IsBoosting = true;
+            _status.VesselTransformer?.ModifyVelocity(_status.Course, 100f);
+            _status.IsStationary = false;
+
+
+            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
+        }
+
+        void OnDisable()
+        {
+            if (_status == null) return;
+            _status.IsBoosting = false;
+            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
+        }
+
+        public override void Initialize(IVesselStatus shipStatus)
+        {
+
+            base.Initialize(_status);
+
+
+        }
+
+
+        public void Begin(FalconBoostSO so)
+        {
+            //Debug.LogError("Boost Started");
+        }
+
+        public void End()
+        {
+            if (_cts == null) return;
+
+            _cts.Cancel();
+            _cts.Dispose();
+            _cts = null;
+        }
+
+        void OnTurnEndOfMiniGame()
+        {
+            End();
+        }
+    }
+}
diff --git a/Assets/_SO_Assets/ShipActions/Falcon/FalconBoostSO.cs b/Assets/_SO_Assets/ShipActions/Falcon/FalconBoostSO.cs
new file mode 100644
index 000000000..ed954f2b9
--- /dev/null
+++ b/Assets/_SO_Assets/ShipActions/Falcon/FalconBoostSO.cs
@@ -0,0 +1,21 @@
+using CosmicShore.Game;
+using System.Net.NetworkInformation;
+using UnityEngine;
+
```

</details>

### `a748981dc` — Added different fire mode to falcon

_Philip Appoh, 2026-01-12 11:33:48 -0600_

```text
 Assets/FalconBoostActionExecutor.cs                                   |   23 +-
 Assets/_Prefabs/Spaceships/Falcon.prefab                              | 3381 +++----------------------------
 Assets/_SO_Assets/SOAP/Configuration Data/Ship Prefab Container.asset |    1 -
 .../Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs       |    8 +
 4 files changed, 258 insertions(+), 3155 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/FalconBoostActionExecutor.cs b/Assets/FalconBoostActionExecutor.cs
index 4ba3153cc..b241b91f6 100644
--- a/Assets/FalconBoostActionExecutor.cs
+++ b/Assets/FalconBoostActionExecutor.cs
@@ -13,15 +13,18 @@ namespace CosmicShore
         public ScriptableEventNoParam OnMiniGameTurnEnd;
         private CancellationTokenSource _cts;
 
+        [SerializeField] private FullAutoActionExecutor FullAutoActionExecutor;
+        [SerializeField] private Transform[] muzzlesMain;
+        [SerializeField] private Transform[] muzzlesSecondary;
 
 
         void OnEnable()
         {
             if (_status == null) return;
             _status.IsBoosting = true;
-            _status.VesselTransformer?.ModifyVelocity(_status.Course, 100f);
+            _status.VesselTransformer?.ModifyVelocity(_status.Course * 100.0f, 1000);
             _status.IsStationary = false;
-
+            ChooseGuns();
 
             OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
         }
@@ -51,14 +54,30 @@ namespace CosmicShore
         {
             if (_cts == null) return;
 
+            ChooseGuns();
             _cts.Cancel();
             _cts.Dispose();
             _cts = null;
+
         }
 
         void OnTurnEndOfMiniGame()
         {
             End();
         }
+
+        void ChooseGuns()
+        {
+         
+            if (_status.IsBoosting)
+            {
+                FullAutoActionExecutor.setGuns(muzzlesSecondary);
+            }
+            else
+            {
+                FullAutoActionExecutor.setGuns(muzzlesMain);
+            }
+            
+        }
     }
 }
diff --git a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs
index 90db71e79..2b27658a5 100644
--- a/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Game/Ship/R_ShipActions/Executors/FullAutoActionExecutor.cs
@@ -208,4 +208,12 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
         }
     }
     #endregion
+
+
+    //modfiy guns transforms via script
+    public void setGuns(Transform[] newMuzzle)
+    {
+        muzzles = newMuzzle;
+    }
+   
 }
```

</details>

### `d52b703ee` — Commit 4/1/26

_Philip Appoh, 2026-04-01 10:41:44 -0500_

```text
 Assets/_Prefabs/Spaceships/Falcon.prefab                              | 188 +++++++++++++++++++++++++++++---
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |   4 +-
 Assets/_Scripts/VesselHUD/View/FalconHUDVessel.cs                     |  15 +++
 Assets/_Scripts/VesselHUD/View/FalconHUDVessel.cs.meta                |   2 +
 4 files changed, 189 insertions(+), 20 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/VesselHUD/View/FalconHUDVessel.cs b/Assets/_Scripts/VesselHUD/View/FalconHUDVessel.cs
new file mode 100644
index 000000000..a2e30a48c
--- /dev/null
+++ b/Assets/_Scripts/VesselHUD/View/FalconHUDVessel.cs
@@ -0,0 +1,15 @@
+using CosmicShore.Game;
+using System.Linq;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    public class FalconHudView : VesselHUDView
+    {
+        // Start is called once before the first execution of Update after the MonoBehaviour is created
+        public override void Initialize()
+        {
+          
+        }
+    }
+}
```

</details>

_Also contains 8 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
