# Branch archive: `claude/fix-gun-focus-vvYxp`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-05-29 by Claude
- **Unmerged commits:** 20
- **Forked from:** `ce09522dd` (2026-05-19, Merge pull request #520 from froglet-studio/claude/optimize-objectiveindicator)
- **Tip:** `cf29c8001`
- **Files touched (45):**
  - `Assets/DefaultNetworkPrefabs.asset`
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_Prefabs/Environment/CapsuleMembrane.prefab`
  - `Assets/_Prefabs/Spacevessels/BrittleStar.prefab`
  - `Assets/_Prefabs/Spacevessels/BrittleStar.prefab.meta`
  - `Assets/_Prefabs/Spacevessels/Falcon.prefab`
  - `Assets/_SO_Assets/Vessel Prefab Container.asset`
  - `Assets/_SO_Assets/VesselActions/BrittleStar.meta`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarBoostSO.asset`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarBoostSO.asset.meta`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarFullAutoAction.asset`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarFullAutoAction.asset.meta`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset.meta`
  - `Assets/_Scenes/Bootstrap.unity`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity`
  - `Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs`
  - `Assets/_Scripts/Controller/IO/TouchInputStrategy.cs`
  - `Assets/_Scripts/Controller/Projectiles/Gun.cs`
  - `Assets/_Scripts/Controller/Projectiles/ProjectilePoolManager.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/BrittleStarBoostSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/BrittleStarBoostSO.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FullAutoActionSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs.meta`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs.meta`
  - `Assets/_Scripts/Data/Enums/VesselClassType.cs`
  - `Assets/_Scripts/Game/Environment/CapsuleMembrane.cs`
  - `Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs`
  - `Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs.meta`
  - `Assets/_Scripts/Game/Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs`
  - `Assets/_Scripts/System/AppManager.cs`
  - `Assets/_Scripts/System/MainMenuController.cs`
  - `Assets/_Scripts/UI/View/FalconHUDVessel.cs`
  - `Assets/_Scripts/UI/View/FalconHUDVessel.cs.meta`
  - … and 5 more

### `ca1861bb4` — Commit 4/1/26

_Philip Appoh, 2026-04-29 12:31:04 -0500_

```text
 Assets/_Prefabs/Spacevessels/Falcon.prefab                            | 1665 +++++++++++++++++++------------
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |   65 ++
 Assets/_Scripts/UI/View/FalconHUDVessel.cs                            |   15 +
 Assets/_Scripts/UI/View/FalconHUDVessel.cs.meta                       |    2 +
 4 files changed, 1108 insertions(+), 639 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/FalconHUDVessel.cs b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
new file mode 100644
index 000000000..a2e30a48c
--- /dev/null
+++ b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
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

### `c3a7844ad` — Revert "update capsule membrane and camera configuration"

_Philip Appoh, 2026-04-29 13:37:03 -0500_

```text
This reverts commit bf0484610d001bbef18927450ec621aa4df82a8b.
```

```text
 Assets/_Prefabs/Environment/CapsuleMembrane.prefab  |   8 +---
 Assets/_Scenes/Bootstrap.unity                      |   2 +-
 Assets/_Scenes/Menu_Main.unity                      | 107 +++++++++++++++++++++++++++++++++++++++++++++-----
 Assets/_Scripts/Game/Environment/CapsuleMembrane.cs |  14 +------
 4 files changed, 101 insertions(+), 30 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
index 98e1b7a9e..44cc9730d 100644
--- a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
+++ b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
@@ -143,23 +143,11 @@ namespace CosmicShore.Game
                 Vector3 nc = noiseCoords[i];
                 // Sample Perlin noise at the capsule's sphere-surface coordinate + animated time offset
                 // Unity's PerlinNoise is 2D, so we take two samples for a pseudo-3D effect
-                // then we convert this to a vector3 with three different noise samples for more interesting variation between capsules
                 float noise = Mathf.PerlinNoise(nc.x + time, nc.y + time * 0.7f) * 2f - 1f;
                 noise += (Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f) * 0.5f;
                 noise *= 0.667f; // normalize back to roughly -1..1
-                Vector3 noiseVec = new Vector3(
-                                       Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f,
-                                                          Mathf.PerlinNoise(nc.z + time * 0.8f, nc.x + time * 0.2f) * 2f - 1f,
-                                                                             Mathf.PerlinNoise(nc.x + time * 0.6f, nc.y + time * 0.4f) * 2f - 1f
-                                                                                            );
 
-
-
-
-
-                float r = baseRadii[i];// + noise * noiseAmplitude;
-                Quaternion eulerNoise = Quaternion.Euler(noiseAmplitude * noiseVec);
-                rotations[i] = Quaternion.LookRotation(eulerNoise * baseDirections[i], eulerNoise * Vector3.Cross(baseDirections[i], Vector3.up).normalized);
+                float r = baseRadii[i] + noise * noiseAmplitude;
                 Vector3 position = center + baseDirections[i] * r;
                 matrices[i] = Matrix4x4.TRS(position, rotations[i], capsuleScale);
             }
```

</details>

### `54b5638ba` — Reapply "update capsule membrane and camera configuration"

_Philip Appoh, 2026-04-29 13:37:30 -0500_

```text
This reverts commit c3a7844adc68daeee3d88ecb5d6d265249cac914.
```

```text
 Assets/_Prefabs/Environment/CapsuleMembrane.prefab  |   8 +++-
 Assets/_Scenes/Bootstrap.unity                      |   2 +-
 Assets/_Scenes/Menu_Main.unity                      | 107 +++++---------------------------------------------
 Assets/_Scripts/Game/Environment/CapsuleMembrane.cs |  14 ++++++-
 4 files changed, 30 insertions(+), 101 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
index 44cc9730d..98e1b7a9e 100644
--- a/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
+++ b/Assets/_Scripts/Game/Environment/CapsuleMembrane.cs
@@ -143,11 +143,23 @@ namespace CosmicShore.Game
                 Vector3 nc = noiseCoords[i];
                 // Sample Perlin noise at the capsule's sphere-surface coordinate + animated time offset
                 // Unity's PerlinNoise is 2D, so we take two samples for a pseudo-3D effect
+                // then we convert this to a vector3 with three different noise samples for more interesting variation between capsules
                 float noise = Mathf.PerlinNoise(nc.x + time, nc.y + time * 0.7f) * 2f - 1f;
                 noise += (Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f) * 0.5f;
                 noise *= 0.667f; // normalize back to roughly -1..1
+                Vector3 noiseVec = new Vector3(
+                                       Mathf.PerlinNoise(nc.y + time * 0.3f, nc.z + time * 0.5f) * 2f - 1f,
+                                                          Mathf.PerlinNoise(nc.z + time * 0.8f, nc.x + time * 0.2f) * 2f - 1f,
+                                                                             Mathf.PerlinNoise(nc.x + time * 0.6f, nc.y + time * 0.4f) * 2f - 1f
+                                                                                            );
 
-                float r = baseRadii[i] + noise * noiseAmplitude;
+
+
+
+
+                float r = baseRadii[i];// + noise * noiseAmplitude;
+                Quaternion eulerNoise = Quaternion.Euler(noiseAmplitude * noiseVec);
+                rotations[i] = Quaternion.LookRotation(eulerNoise * baseDirections[i], eulerNoise * Vector3.Cross(baseDirections[i], Vector3.up).normalized);
                 Vector3 position = center + baseDirections[i] * r;
                 matrices[i] = Matrix4x4.TRS(position, rotations[i], capsuleScale);
             }
```

</details>

### `f0963f770` — Revert "Commit 4/1/26"

_Philip Appoh, 2026-04-29 13:37:45 -0500_

```text
This reverts commit ca1861bb473e856a49b5a3e475f3714cab49feed.
```

```text
 Assets/_Prefabs/Spacevessels/Falcon.prefab                            | 1715 ++++++++++++-------------------
 .../Ship/R_ShipActions/Data Containers/FalconModeSwitchingFireSO.cs   |   65 --
 Assets/_Scripts/UI/View/FalconHUDVessel.cs                            |   15 -
 Assets/_Scripts/UI/View/FalconHUDVessel.cs.meta                       |    2 -
 4 files changed, 664 insertions(+), 1133 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/View/FalconHUDVessel.cs b/Assets/_Scripts/UI/View/FalconHUDVessel.cs
deleted file mode 100644
index a2e30a48c..000000000
--- a/Assets/_Scripts/UI/View/FalconHUDVessel.cs
+++ /dev/null
@@ -1,15 +0,0 @@
-using CosmicShore.Game;
-using System.Linq;
-using UnityEngine;
-
-namespace CosmicShore
-{
-    public class FalconHudView : VesselHUDView
-    {
-        // Start is called once before the first execution of Update after the MonoBehaviour is created
-        public override void Initialize()
-        {
-          
-        }
-    }
-}
```

</details>

### `dc65a4197` — Commit 5/8/26

_Philip Appoh, 2026-05-08 13:15:22 -0500_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 289 +------------
 Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs           | 746 ++++++++++++++++++++++++++++++++
 Assets/_Scripts/Game/IO/_Input Mapping/InputActionsAsset.cs.meta      |   2 +
 Packages/manifest.json                                                |  38 +-
 Packages/packages-lock.json                                           | 100 ++---
 ProjectSettings/ProjectSettings.asset                                 |   7 +
 ProjectSettings/ProjectVersion.txt                                    |   4 +-
 ProjectSettings/VirtualProjectsConfig.json                            |   2 +-
 8 files changed, 838 insertions(+), 350 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 428 lines)</summary>

```diff
diff --git a/Packages/manifest.json b/Packages/manifest.json
index a76719e7e..6fd6725aa 100644
--- a/Packages/manifest.json
+++ b/Packages/manifest.json
@@ -5,41 +5,41 @@
     "com.unity.2d.sprite": "1.0.0",
     "com.unity.adaptiveperformance": "5.1.6",
     "com.unity.adaptiveperformance.samsung.android": "5.1.0",
-    "com.unity.ads": "4.12.0",
-    "com.unity.ai.navigation": "2.0.9",
-    "com.unity.animation.rigging": "1.3.0",
+    "com.unity.ads": "4.16.4",
+    "com.unity.ai.navigation": "2.0.12",
+    "com.unity.animation.rigging": "1.4.1",
     "com.unity.cinemachine": "3.1.2",
-    "com.unity.collab-proxy": "2.10.0",
+    "com.unity.collab-proxy": "2.12.4",
     "com.unity.device-simulator.devices": "1.0.0",
-    "com.unity.entities": "1.4.2",
-    "com.unity.entities.graphics": "1.4.15",
+    "com.unity.entities": "1.4.5",
+    "com.unity.entities.graphics": "1.4.18",
     "com.unity.feature.mobile": "1.0.0",
-    "com.unity.ide.rider": "3.0.38",
-    "com.unity.ide.visualstudio": "2.0.25",
-    "com.unity.inputsystem": "1.14.2",
-    "com.unity.mobile.android-logcat": "1.4.6",
-    "com.unity.mobile.notifications": "2.4.2",
+    "com.unity.ide.rider": "3.0.39",
+    "com.unity.ide.visualstudio": "2.0.27",
+    "com.unity.inputsystem": "1.19.0",
+    "com.unity.mobile.android-logcat": "1.4.7",
+    "com.unity.mobile.notifications": "2.4.3",
     "com.unity.multiplayer.center": "1.0.0",
     "com.unity.multiplayer.center.quickstart": "1.0.1",
-    "com.unity.multiplayer.playmode": "1.6.1",
-    "com.unity.multiplayer.tools": "2.2.6",
+    "com.unity.multiplayer.playmode": "1.6.3",
+    "com.unity.multiplayer.tools": "2.2.8",
     "com.unity.multiplayer.widgets": "1.0.1",
     "com.unity.netcode.gameobjects": "2.5.0",
     "com.unity.nuget.newtonsoft-json": "3.2.1",
-    "com.unity.performance.profile-analyzer": "1.2.3",
-    "com.unity.purchasing": "4.12.2",
-    "com.unity.recorder": "5.1.2",
+    "com.unity.performance.profile-analyzer": "1.3.3",
+    "com.unity.purchasing": "4.14.2",
+    "com.unity.recorder": "5.1.6",
     "com.unity.render-pipelines.universal": "17.0.4",
-    "com.unity.services.analytics": "6.2.1",
+    "com.unity.services.analytics": "6.3.0",
     "com.unity.services.cloudsave": "3.4.0",
     "com.unity.services.core": "1.16.0",
     "com.unity.services.friends": "1.1.1",
     "com.unity.services.leaderboards": "2.3.3",
     "com.unity.services.multiplayer": "1.1.8",
     "com.unity.test-framework": "1.6.0",
-    "com.unity.timeline": "1.8.9",
+    "com.unity.timeline": "1.8.12",
     "com.unity.toolchain.win-x86_64-linux-x86_64": "2.0.10",
-    "com.unity.transport": "2.6.0",
+    "com.unity.transport": "2.7.2",
     "com.unity.ugui": "2.0.0",
     "com.unity.visualeffectgraph": "17.0.4",
     "com.veriorpies.parrelsync": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync",
diff --git a/Packages/packages-lock.json b/Packages/packages-lock.json
index e52a4ae69..4cf845be5 100644
--- a/Packages/packages-lock.json
+++ b/Packages/packages-lock.json
@@ -42,16 +42,17 @@
       "url": "https://packages.unity.com"
     },
     "com.unity.ads": {
-      "version": "4.12.0",
+      "version": "4.16.4",
       "depth": 0,
       "source": "registry",
       "dependencies": {
-        "com.unity.ugui": "1.0.0"
+        "com.unity.ugui": "1.0.0",
```

</details>

### `506ae8352` — Added BrittleStar

_Philip Appoh, 2026-05-13 11:42:20 -0500_

```text
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab      | 3338 ++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab.meta |    7 +
 Assets/_Scripts/Data/Enums/VesselClassType.cs        |    1 +
 3 files changed, 3346 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Data/Enums/VesselClassType.cs b/Assets/_Scripts/Data/Enums/VesselClassType.cs
index 5e8db1a31..5cc714a70 100644
--- a/Assets/_Scripts/Data/Enums/VesselClassType.cs
+++ b/Assets/_Scripts/Data/Enums/VesselClassType.cs
@@ -21,5 +21,6 @@ namespace CosmicShore.Data
         Falcon = 9,
         Shrike = 10,
         Sparrow = 11,
+        BrittleStar = 12,
     }
 }
```

</details>

### `c57e264c6` — First pass at BrittleSar

_Philip Appoh, 2026-05-15 15:09:41 -0500_

```text
 Assets/DefaultNetworkPrefabs.asset                                    |   6 +
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab                       | 870 +++++++++++++++++++++++++-------
 Assets/_SO_Assets/Vessel Prefab Container.asset                       |   1 +
 Assets/_SO_Assets/VesselActions/BrittleStar.meta                      |   8 +
 .../VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset      |  17 +
 .../VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset.meta |   8 +
 Assets/_Scenes/Menu_Main.unity                                        | 241 ++++++++-
 Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity            |  58 ++-
 .../Vessel/R_VesselActions/Data Containers/BrittleStarBoostSO.cs      |  15 +
 .../Vessel/R_VesselActions/Data Containers/BrittleStarBoostSO.cs.meta |   2 +
 .../R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs       |  80 +++
 .../R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs.meta  |   2 +
 .../R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs       |  44 ++
 .../R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs.meta  |   2 +
 .../Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs        |   8 +-
 .../Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs |  43 ++
 .../Vessel/R_VesselActions/Executors/GunRingTransformer.cs.meta       |   2 +
 Assets/_Scripts/System/AppManager.cs                                  |   2 +-
 Assets/_Scripts/System/MainMenuController.cs                          |   2 +-
 19 files changed, 1199 insertions(+), 212 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 232 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs
new file mode 100644
index 000000000..08a65eba8
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs
@@ -0,0 +1,80 @@
+using CosmicShore.Game;
+using CosmicShore.Gameplay;
+using Obvious.Soap;
+using System.Diagnostics;
+using System.Threading;
+using UnityEngine;
+namespace CosmicShore
+{
+    public class BrittleStarBoostActionExecutor : ShipActionExecutorBase
+    {
+        private IVesselStatus _status; public ScriptableEventNoParam OnMiniGameTurnEnd; private CancellationTokenSource _cts;
+        [SerializeField] private FullAutoActionExecutor FullAutoActionExecutor;
+        [SerializeField] private Transform[] muzzlesMain;
+        [SerializeField] private Transform[] muzzlesSecondary;
+
+
+        void OnEnable()
+        {
+            if (_status == null) return;
+            _status.IsBoosting = true;
+            _status.VesselTransformer?.ModifyVelocity(_status.Course * 100.0f, 1000);
+            _status.IsStationary = false;
+            ChooseGuns();
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
+        public void Begin(BrittleStarBoostSO so)
+        {
+            //Debug.LogError("Boost Started");
+        }
+
+        public void End()
+        {
+            if (_cts == null) return;
+
+            ChooseGuns();
+            _cts.Cancel();
+            _cts.Dispose();
+            _cts = null;
+
+        }
+
+        void OnTurnEndOfMiniGame()
+        {
+            End();
+        }
+
+        void ChooseGuns()
+        {
+
+            if (_status.IsBoosting)
+            {
+                FullAutoActionExecutor.SetGuns(muzzlesSecondary);
+            }
+            else
+            {
+                FullAutoActionExecutor.SetGuns(muzzlesMain);
```

</details>

### `d212749cb` — Itrated on BrittleStar

_Philip Appoh, 2026-05-20 11:11:07 -0500_

```text
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab                       | 388 +-------------------------------
 Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarBoostSO.asset  |  14 ++
 .../VesselActions/BrittleStar/BrittleStarBoostSO.asset.meta           |   8 +
 .../VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset      |   4 +-
 .../R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs       |  71 +++---
 5 files changed, 57 insertions(+), 428 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 115 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs
index 08a65eba8..6371daff0 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs
@@ -1,80 +1,67 @@
-using CosmicShore.Game;
-using CosmicShore.Gameplay;
 using Obvious.Soap;
-using System.Diagnostics;
-using System.Threading;
 using UnityEngine;
-namespace CosmicShore
+
+namespace CosmicShore.Gameplay
 {
     public class BrittleStarBoostActionExecutor : ShipActionExecutorBase
     {
-        private IVesselStatus _status; public ScriptableEventNoParam OnMiniGameTurnEnd; private CancellationTokenSource _cts;
-        [SerializeField] private FullAutoActionExecutor FullAutoActionExecutor;
+        [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;
+        [SerializeField] private FullAutoActionExecutor fullAutoActionExecutor;
         [SerializeField] private Transform[] muzzlesMain;
         [SerializeField] private Transform[] muzzlesSecondary;
 
+        private IVesselStatus _status;
+        private bool _boosting;
 
         void OnEnable()
         {
-            if (_status == null) return;
-            _status.IsBoosting = true;
-            _status.VesselTransformer?.ModifyVelocity(_status.Course * 100.0f, 1000);
-            _status.IsStationary = false;
-            ChooseGuns();
-
-            OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
+            if (OnMiniGameTurnEnd != null)
+                OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
         }
 
         void OnDisable()
         {
-            if (_status == null) return;
-            _status.IsBoosting = false;
-            OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
+            End();
+            if (OnMiniGameTurnEnd != null)
+                OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
         }
 
         public override void Initialize(IVesselStatus shipStatus)
         {
-
-            base.Initialize(_status);
-
-
+            _status = shipStatus;
         }
 
-
         public void Begin(BrittleStarBoostSO so)
         {
-            //Debug.LogError("Boost Started");
+            if (_status == null) return;
+
+            _boosting = true;
+            _status.IsBoosting = true;
+            _status.VesselTransformer?.ModifyVelocity(_status.Course * 100.0f, 1000);
+            _status.IsStationary = false;
+            ChooseGuns();
         }
 
         public void End()
         {
-            if (_cts == null) return;
+            if (!_boosting) return;
 
+            _boosting = false;
+            if (_status != null)
+                _status.IsBoosting = false;
```

</details>

### `676a8f994` — fix(input): populate RightNormalizedJoystickPosition from gamepad and touch strategies

_Claude, 2026-05-20 17:33:08 +0000_

```text
GunTransformer (BrittleStar) reads InputStatus.RightNormalizedJoystickPosition to
position the gun focus point and orient the gun ring, but neither GamepadInputStrategy
nor TouchInputStrategy ever wrote to that field — it stayed Vector2.zero every frame,
so the focus was stuck at Z=70 and guns never tracked the stick direction.

Also resolves the TODO in GunRingTransformer: replaced the direct Gamepad.current read
with the proper IVesselStatus.InputStatus abstraction and switched from concrete
VesselStatus to the IVesselStatus interface.
```

```text
 Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs                             |  3 +++
 Assets/_Scripts/Controller/IO/TouchInputStrategy.cs                               |  3 +++
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs | 12 +++++++-----
 3 files changed, 13 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
index f55b8ba69..6787a080e 100644
--- a/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/GamepadInputStrategy.cs
@@ -175,6 +175,9 @@ namespace CosmicShore.Gameplay
                 Ease(2 * rightStickRaw.y)
             );
 
+            inputStatus.RightNormalizedJoystickPosition = rightStickRaw;
+            inputStatus.LeftNormalizedJoystickPosition = leftStickRaw;
+
             // Calculate sums and differences exactly as touch input does
             inputStatus.XSum = Ease(rightStickRaw.x + leftStickRaw.x);
             inputStatus.YSum = -Ease(rightStickRaw.y + leftStickRaw.y);
diff --git a/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs b/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
index af23ca5ad..178b05ca0 100644
--- a/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
+++ b/Assets/_Scripts/Controller/IO/TouchInputStrategy.cs
@@ -294,6 +294,9 @@ namespace CosmicShore.Gameplay
                 Ease(2 * leftNormalizedJoystickPosition.y)
             );
 
+            inputStatus.RightNormalizedJoystickPosition = rightNormalizedJoystickPosition;
+            inputStatus.LeftNormalizedJoystickPosition = leftNormalizedJoystickPosition;
+
             inputStatus.XSum = Ease(rightNormalizedJoystickPosition.x + leftNormalizedJoystickPosition.x);
             inputStatus.YSum = -Ease(rightNormalizedJoystickPosition.y + leftNormalizedJoystickPosition.y);
             inputStatus.XDiff = (rightNormalizedJoystickPosition.x - leftNormalizedJoystickPosition.x + 2) / 4;
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
index b18cbace7..4806dbaaa 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
@@ -1,10 +1,12 @@
+using CosmicShore.Utility;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
 {
     public class GunRingTransformer : MonoBehaviour
     {
-        [SerializeField] VesselStatus shipInstance;
+        [RequireInterface(typeof(IVesselStatus))]
+        [SerializeField] MonoBehaviour shipInstance;
         [SerializeField] Transform gunFocus;
         [SerializeField] GameObject pivotObject;
 
@@ -18,15 +20,15 @@ namespace CosmicShore.Gameplay
             {
                 if (child == transform) continue;
 
-                Vector3 direction = (child.position - shipInstance.transform.position).normalized;
-                child.position = shipInstance.transform.position + direction * radius;
+                var vessel = shipInstance as IVesselStatus;
+                Vector3 direction = (child.position - vessel.Transform.position).normalized;
+                child.position = vessel.Transform.position + direction * radius;
             }
         }
 
         void Update()
         {
-            // TODO: replace with InputController read once BrittleStar input is wired
-            Vector2 rightStick = UnityEngine.InputSystem.Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
+            Vector2 rightStick = (shipInstance as IVesselStatus).InputStatus.RightNormalizedJoystickPosition;
 
             Vector3 targetFocus = new Vector3(0, 0, 300f * rightStick.sqrMagnitude + 70f);
             gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, targetFocus, Time.deltaTime * speed);
```

</details>

### `dad9f2ffc` — fix(projectile): fire along muzzle forward instead of Gun component forward

_Claude, 2026-05-20 17:39:49 +0000_

```text
Gun.FireSingle() was using transform.forward (the Gun component's own transform)
for projectile direction, ignoring the containerTransform passed in by each muzzle.
GunTransformer calls child.LookAt(gunFocus) so each muzzle's forward already points
at the focus point — switching to containerTransform.forward makes shots travel where
the guns are actually aimed.

FireGunActionExecutor is unaffected: _worldMuzzleAnchor is snapped to gun.transform
position+rotation, so its forward was already equivalent to gun.transform.forward.
```

```text
 Assets/_Scripts/Controller/Projectiles/Gun.cs | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/Gun.cs b/Assets/_Scripts/Controller/Projectiles/Gun.cs
index b5479f99a..15494a645 100644
--- a/Assets/_Scripts/Controller/Projectiles/Gun.cs
+++ b/Assets/_Scripts/Controller/Projectiles/Gun.cs
@@ -153,7 +153,7 @@ namespace CosmicShore.Gameplay
                 return;
             }
 
-            Vector3 direction = customDirection ?? transform.forward;
+            Vector3 direction = customDirection ?? containerTransform.forward;
             Vector3 spawnPos  = containerTransform.position;   // using container for spawn point
 
             SafeLookRotation.TryGet(direction, out var rotation, this);
```

</details>

### `c8b244243` — feat(brittlestar): give BrittleStar its own FullAutoAction SO, decouple from Sparrow

_Claude, 2026-05-29 16:13:53 +0000_

```text
BrittleStarModeSwitchingFire and the direct Button1 input mapping were both pointing
at Sparrow's FullAutoAction.asset, meaning any fire-rate change would affect both
vessels. Created BrittleStarFullAutoAction.asset (same defaults, firingRate: 30) and
rewired both references to use it. Tune firingRate in that asset independently.
```

```text
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab                            |  2 +-
 .../_SO_Assets/VesselActions/BrittleStar/BrittleStarFullAutoAction.asset   | 28 ++++++++++++++++++++++++++++
 .../VesselActions/BrittleStar/BrittleStarFullAutoAction.asset.meta         |  8 ++++++++
 .../VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset           |  2 +-
 4 files changed, 38 insertions(+), 2 deletions(-)
```

### `f1f278ab4` — fix(projectile): inject DI into pool-replenished projectiles

_Claude, 2026-05-29 16:57:10 +0000_

```text
GenericPoolManager.CreateFunc() calls bare Instantiate with no DI injection.
Prewarmed projectiles (defaultCapacity:25) were covered by InjectRecursive at
vessel spawn time, but replenishment objects created later got audioSystem=null
and crashed on LaunchProjectile(). BrittleStar fires 5 guns at 30Hz so the
25-object pool exhausts in ~170ms, making this reproduce immediately.

ProjectilePoolManager now overrides CreateFunc to call InjectRecursive when a
container is available. Prewarm objects (created before DI injection sets the
container) are still handled by the existing vessel-spawn InjectRecursive call.
```

```text
 Assets/_Scripts/Controller/Projectiles/ProjectilePoolManager.cs | 16 ++++++++++++++--
 1 file changed, 14 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Projectiles/ProjectilePoolManager.cs b/Assets/_Scripts/Controller/Projectiles/ProjectilePoolManager.cs
index 6bb39fc4d..660880511 100644
--- a/Assets/_Scripts/Controller/Projectiles/ProjectilePoolManager.cs
+++ b/Assets/_Scripts/Controller/Projectiles/ProjectilePoolManager.cs
@@ -1,10 +1,15 @@
 using CosmicShore.Utility;
+using Reflex.Attributes;
+using Reflex.Core;
+using Reflex.Injectors;
 using UnityEngine;
 
 namespace CosmicShore.Gameplay
 {
     public class ProjectilePoolManager : GenericPoolManager<Projectile>
     {
+        [Inject] private Container _container;
+
         public override Projectile Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays) =>
             Get_(position, rotation, parent);
 
@@ -15,9 +20,16 @@ namespace CosmicShore.Gameplay
                 CSDebug.LogError("Projectile already released! Should not call twice!");
                 return;
             }
-            
+
             Release_(instance);
         }
-            
+
+        protected override Projectile CreateFunc()
+        {
+            var obj = base.CreateFunc();
+            if (_container != null)
+                GameObjectInjector.InjectRecursive(obj.gameObject, _container);
+            return obj;
+        }
     }
 }
\ No newline at end of file
```

</details>

### `253fb26fb` — feat(gun): add per-muzzle inter-shot delay to spread volleys over time

_Claude, 2026-05-29 17:49:35 +0000_

```text
FullAutoActionSO gains interMuzzleDelayMs (default 0, backwards-compat).
FireLoopAsync awaits that delay between each muzzle so bursts are visually
spread rather than firing all muzzles in the same frame.

BrittleStarFullAutoAction: firingRate=1 (one volley/sec),
interMuzzleDelayMs=100 (5 muzzles over 0.4 s then 0.6 s gap).
```

```text
 Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarFullAutoAction.asset           |  3 ++-
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FullAutoActionSO.cs |  3 +++
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs | 14 ++++++++++++--
 3 files changed, 17 insertions(+), 3 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index 72c2e4e22..5821473df 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -138,8 +138,9 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
         var projectileScale = so.ProjectileScale;
         var projectileTime  = so.ProjectileTime;
         var   firingPattern   = so.FiringPattern;
-        var energy          = so.Energy;
-        var speedValue      = so.SpeedValue.Value;
+        var energy              = so.Energy;
+        var speedValue          = so.SpeedValue.Value;
+        var interMuzzleDelayMs  = so.InterMuzzleDelayMs;
 
         try
         {
@@ -191,6 +192,15 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
                             firingPattern,
                             energy
                         );
+
+                        if (interMuzzleDelayMs > 0 && i < count - 1)
+                        {
+                            await UniTask.Delay(
+                                interMuzzleDelayMs,
+                                DelayType.DeltaTime,
+                                PlayerLoopTiming.PreLateUpdate,
+                                token);
+                        }
                     }
 
                     _resources.ChangeResourceAmount(ammoIndex, -ammoCost);
```

</details>

### `bf330d542` — revert(gun): remove inter-muzzle delay, not needed

_Claude, 2026-05-29 17:51:38 +0000_

```text
 Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarFullAutoAction.asset           |  1 -
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/FullAutoActionSO.cs |  3 ---
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs | 14 ++------------
 3 files changed, 2 insertions(+), 16 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index 5821473df..72c2e4e22 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -138,9 +138,8 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
         var projectileScale = so.ProjectileScale;
         var projectileTime  = so.ProjectileTime;
         var   firingPattern   = so.FiringPattern;
-        var energy              = so.Energy;
-        var speedValue          = so.SpeedValue.Value;
-        var interMuzzleDelayMs  = so.InterMuzzleDelayMs;
+        var energy          = so.Energy;
+        var speedValue      = so.SpeedValue.Value;
 
         try
         {
@@ -192,15 +191,6 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
                             firingPattern,
                             energy
                         );
-
-                        if (interMuzzleDelayMs > 0 && i < count - 1)
-                        {
-                            await UniTask.Delay(
-                                interMuzzleDelayMs,
-                                DelayType.DeltaTime,
-                                PlayerLoopTiming.PreLateUpdate,
-                                token);
-                        }
                     }
 
                     _resources.ChangeResourceAmount(ammoIndex, -ammoCost);
```

</details>

### `1e0a08243` — fix(gun): respect fire rate on rapid taps, not just during held fire

_Claude, 2026-05-29 18:15:21 +0000_

```text
Track _lastFireTime across Begin() calls. When a new Begin() starts,
compute how much cooldown remains from the last shot and wait it out
before the first volley. This makes firingRate a true rate limit instead
of only affecting repeat-fire while the button is held.
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs | 13 +++++++++++++
 1 file changed, 13 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index 72c2e4e22..13bfa3d3e 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -26,6 +26,7 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
 
     private CancellationTokenSource _cts;
     private CancellationToken _lifetimeToken;
+    private float _lastFireTime = float.MinValue;
 
     #region Unity Lifecycle
     private void Awake()
@@ -143,6 +144,17 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
 
         try
         {
+            // Respect rate limit even on a fresh Begin() — wait out the remaining cooldown from the last shot.
+            float waitBeforeFirst = (_lastFireTime + interval) - Time.time;
+            if (waitBeforeFirst > 0f)
+            {
+                await UniTask.Delay(
+                    TimeSpan.FromSeconds(waitBeforeFirst),
+                    DelayType.DeltaTime,
+                    PlayerLoopTiming.PreLateUpdate,
+                    token);
+            }
+
             while (!token.IsCancellationRequested)
             {
                 if (_resources == null || _resources.Resources == null || ammoIndex < 0 || ammoIndex >= _resources.Resources.Count)
@@ -194,6 +206,7 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
                     }
 
                     _resources.ChangeResourceAmount(ammoIndex, -ammoCost);
+                    _lastFireTime = Time.time;
                     OnVolleyFired?.Invoke(_status?.PlayerName);
                 }
 
```

</details>

### `1e5557bea` — Revert "fix(gun): respect fire rate on rapid taps, not just during held fire"

_Claude, 2026-05-29 18:54:42 +0000_

```text
This reverts commit 1e0a082433de1a3849594e4fe071d1ca9e8cef1c.
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs | 13 -------------
 1 file changed, 13 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index 13bfa3d3e..72c2e4e22 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -26,7 +26,6 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
 
     private CancellationTokenSource _cts;
     private CancellationToken _lifetimeToken;
-    private float _lastFireTime = float.MinValue;
 
     #region Unity Lifecycle
     private void Awake()
@@ -144,17 +143,6 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
 
         try
         {
-            // Respect rate limit even on a fresh Begin() — wait out the remaining cooldown from the last shot.
-            float waitBeforeFirst = (_lastFireTime + interval) - Time.time;
-            if (waitBeforeFirst > 0f)
-            {
-                await UniTask.Delay(
-                    TimeSpan.FromSeconds(waitBeforeFirst),
-                    DelayType.DeltaTime,
-                    PlayerLoopTiming.PreLateUpdate,
-                    token);
-            }
-
             while (!token.IsCancellationRequested)
             {
                 if (_resources == null || _resources.Resources == null || ammoIndex < 0 || ammoIndex >= _resources.Resources.Count)
@@ -206,7 +194,6 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
                     }
 
                     _resources.ChangeResourceAmount(ammoIndex, -ammoCost);
-                    _lastFireTime = Time.time;
                     OnVolleyFired?.Invoke(_status?.PlayerName);
                 }
 
```

</details>

### `206150a3a` — fix(brittlestar): gate ring fire bursts by interval in mode-switching SO

_Claude, 2026-05-29 18:57:49 +0000_

```text
Rapid trigger taps bypassed the executor's firingRate because each
StartAction() call fired a burst immediately. Adding ringFireInterval
(default 1s) directly in BrittleStarModeSwitchingFireSO gates ring fire
bursts so the rate is respected without touching shared executor code.
```

```text
 .../_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs | 12 ++++++++++++
 1 file changed, 12 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
index 267053497..216179d20 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
@@ -12,10 +12,15 @@ namespace CosmicShore.Gameplay
         [SerializeField] private ShipActionSO ringFire;
         [SerializeField] private ShipActionSO creationMode;
 
+        [Header("Ring Fire Rate")]
+        [Tooltip("Minimum seconds between ring fire bursts when mode-switching triggers it.")]
+        [SerializeField] private float ringFireInterval = 1f;
+
         private ActionExecutorRegistry _registry;
         private bool _isHeld;
         private int _selector = 1;
         private ShipActionSO _active;
+        private float _lastRingFireTime = float.MinValue;
 
         public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
         {
@@ -30,6 +35,13 @@ namespace CosmicShore.Gameplay
                 case 3: _active = speedMode; _selector = 1; break;
             }
 
+            if (_active == ringFire)
+            {
+                if (Time.time - _lastRingFireTime < ringFireInterval)
+                    return;
+                _lastRingFireTime = Time.time;
+            }
+
             _active?.StartAction(execs, vesselStatus);
         }
 
```

</details>

### `cf29c8001` — fix(brittlestar): don't advance mode selector when ring fire is on cooldown

_Claude, 2026-05-29 19:58:26 +0000_

```text
Previous logic advanced _selector before the cooldown check, locking the
user out of ring fire and forcing them to cycle through creation/speed to
get back. Now the selector only advances when the action actually fires.
```

```text
 .../Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs   | 19 ++++++++++++-------
 1 file changed, 12 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
index 216179d20..9381a5e99 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
@@ -28,19 +28,24 @@ namespace CosmicShore.Gameplay
             _isHeld = true;
             _registry = execs;
 
+            ShipActionSO next;
+            int nextSelector;
             switch (_selector)
             {
-                case 1: _active = ringFire; _selector = 2; break;
-                case 2: _active = creationMode; _selector = 3; break;
-                case 3: _active = speedMode; _selector = 1; break;
+                case 1: next = ringFire;      nextSelector = 2; break;
+                case 2: next = creationMode;  nextSelector = 3; break;
+                case 3: next = speedMode;     nextSelector = 1; break;
+                default: return;
             }
 
+            if (next == ringFire && Time.time - _lastRingFireTime < ringFireInterval)
+                return;
+
+            _active = next;
+            _selector = nextSelector;
+
             if (_active == ringFire)
-            {
-                if (Time.time - _lastRingFireTime < ringFireInterval)
-                    return;
                 _lastRingFireTime = Time.time;
-            }
 
             _active?.StartAction(execs, vesselStatus);
         }
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
