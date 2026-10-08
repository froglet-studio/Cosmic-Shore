# Branch archive: `claude/align-brittlestar-falcon-ZY8cd`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-15 by Claude
- **Unmerged commits:** 9
- **Forked from:** `65e7a0a71` (2026-05-07, prism tweaks in hexrace)
- **Tip:** `f677230f1`
- **Files touched (36):**
  - `Assets/DefaultNetworkPrefabs.asset`
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_Prefabs/Environment/CapsuleMembrane.prefab`
  - `Assets/_Prefabs/Spacevessels/BrittleStar.prefab`
  - `Assets/_Prefabs/Spacevessels/BrittleStar.prefab.meta`
  - `Assets/_Prefabs/Spacevessels/Falcon.prefab`
  - `Assets/_SO_Assets/Vessel Prefab Container.asset`
  - `Assets/_SO_Assets/VesselActions/BrittleStar.meta`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset`
  - `Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset.meta`
  - `Assets/_Scenes/Bootstrap.unity`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/BrittleStarBoostSO.cs`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Data Containers/BrittleStarBoostSO.cs.meta`
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
  - `Packages/manifest.json`
  - `Packages/packages-lock.json`
  - `ProjectSettings/ProjectSettings.asset`
  - `ProjectSettings/ProjectVersion.txt`
  - `ProjectSettings/VirtualProjectsConfig.json`

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

<details><summary>Patch (code/doc/text files, first 150 of 428 lines)</summary>

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
+        "com.unity.modules.androidjni": "1.0.0"
       },
       "url": "https://packages.unity.com"
     },
     "com.unity.ai.navigation": {
-      "version": "2.0.9",
+      "version": "2.0.12",
       "depth": 0,
       "source": "registry",
       "dependencies": {
@@ -60,26 +61,27 @@
       "url": "https://packages.unity.com"
     },
     "com.unity.animation.rigging": {
-      "version": "1.3.0",
+      "version": "1.4.1",
       "depth": 0,
       "source": "registry",
       "dependencies": {
         "com.unity.burst": "1.4.1",
-        "com.unity.test-framework": "1.1.24"
+        "com.unity.test-framework": "1.1.24",
+        "com.unity.modules.animation": "1.0.0"
       },
       "url": "https://packages.unity.com"
     },
     "com.unity.bindings.openimageio": {
-      "version": "1.0.0",
+      "version": "1.0.2",
       "depth": 1,
       "source": "registry",
       "dependencies": {
-        "com.unity.collections": "1.0.0"
+        "com.unity.collections": "1.2.4"
       },
       "url": "https://packages.unity.com"
     },
     "com.unity.burst": {
-      "version": "1.8.25",
+      "version": "1.8.29",
       "depth": 1,
       "source": "registry",
       "dependencies": {
@@ -98,21 +100,21 @@
       "url": "https://packages.unity.com"
     },
     "com.unity.collab-proxy": {
-      "version": "2.10.0",
+      "version": "2.12.4",
       "depth": 0,
       "source": "registry",
       "dependencies": {},
       "url": "https://packages.unity.com"
     },
     "com.unity.collections": {
-      "version": "2.6.2",
+      "version": "2.6.5",
       "depth": 1,
       "source": "registry",
       "dependencies": {
-        "com.unity.burst": "1.8.23",
+        "com.unity.burst": "1.8.27",
         "com.unity.mathematics": "1.3.2",
         "com.unity.test-framework": "1.4.6",
-        "com.unity.nuget.mono-cecil": "1.11.5",
+        "com.unity.nuget.mono-cecil": "1.11.6",
         "com.unity.test-framework.performance": "3.0.3"
       },
       "url": "https://packages.unity.com"
@@ -125,33 +127,33 @@
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

<details><summary>Patch (code/doc/text files, first 150 of 232 lines)</summary>

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
+            }
+
+        }
+    }
+
+}
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
new file mode 100644
index 000000000..267053497
--- /dev/null
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
@@ -0,0 +1,44 @@
+using Obvious.Soap;
+using UnityEngine;
+
+namespace CosmicShore.Gameplay
+{
+    [CreateAssetMenu(fileName = "BrittleStarModeSwitchingFire",
+        menuName = "ScriptableObjects/Vessel Actions/BrittleStar Mode Switching Fire")]
+    public class BrittleStarModeSwitchingFireSO : ShipActionSO
+    {
+        [Header("Actions")]
+        [SerializeField] private ShipActionSO speedMode;
+        [SerializeField] private ShipActionSO ringFire;
+        [SerializeField] private ShipActionSO creationMode;
+
+        private ActionExecutorRegistry _registry;
+        private bool _isHeld;
+        private int _selector = 1;
+        private ShipActionSO _active;
+
+        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
+        {
+            base.Initialize(vs);
+            _isHeld = true;
+            _registry = execs;
+
+            switch (_selector)
+            {
+                case 1: _active = ringFire; _selector = 2; break;
+                case 2: _active = creationMode; _selector = 3; break;
+                case 3: _active = speedMode; _selector = 1; break;
+            }
+
+            _active?.StartAction(execs, vesselStatus);
+        }
+
+        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vs)
+        {
+            _isHeld = false;
+            _active?.StopAction(execs, vesselStatus);
+            _active = null;
+            _registry = null;
+        }
+    }
+}
\ No newline at end of file
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index ac3997e47..72c2e4e22 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -213,7 +213,13 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
             CSDebug.LogError($"[FullAutoActionExecutor] Loop error: {e}");
         }
     }
-    #endregion
+        #endregion
+
+        //modfiy guns transforms via script
+        public void SetGuns(Transform[] newMuzzle)
```

</details>

### `f677230f1` — fix(brittlestar): repair BrittleStarBoostActionExecutor initialization and boost lifecycle

_Claude, 2026-05-15 20:26:09 +0000_

```text
- _status was never assigned in Initialize (passed null to base instead of shipStatus)
- _cts was never created so End() always no-oped; replaced with a _boosting bool guard
- Begin/End now own boost state; OnEnable/OnDisable only manage event subscription
- Fixed namespace (CosmicShore → CosmicShore.Gameplay), removed unused imports
- Renamed field to camelCase (fullAutoActionExecutor) for consistency
```

```text
 .../R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs       | 69 +++++++++++++--------------------
 1 file changed, 28 insertions(+), 41 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarBoostActionExecutor.cs
index 08a65eba8..b765b470b 100644
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
             ChooseGuns();
-            _cts.Cancel();
-            _cts.Dispose();
-            _cts = null;
-
         }
 
-        void OnTurnEndOfMiniGame()
-        {
-            End();
-        }
+        void OnTurnEndOfMiniGame() => End();
 
         void ChooseGuns()
         {
+            if (fullAutoActionExecutor == null) return;
 
-            if (_status.IsBoosting)
-            {
-                FullAutoActionExecutor.SetGuns(muzzlesSecondary);
-            }
+            if (_boosting)
+                fullAutoActionExecutor.SetGuns(muzzlesSecondary);
             else
-            {
-                FullAutoActionExecutor.SetGuns(muzzlesMain);
-            }
-
+                fullAutoActionExecutor.SetGuns(muzzlesMain);
         }
     }
-
 }
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
