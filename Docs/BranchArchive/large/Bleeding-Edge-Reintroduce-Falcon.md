# Branch archive: `Bleeding-Edge-Reintroduce-Falcon`

_Snapshot 2026-10-08. Index: [README](../README.md). **Large branch — protected from automatic deletion.**_

<!-- SUMMARY -->

- **Last commit:** 2026-07-15 by Philip Appoh
- **Unmerged commits:** 19
- **Forked from:** `621353988` (2026-07-14, Merge pull request #591 from froglet-studio/claude/connect-dots-3d-constructs-)
- **Tip:** `4ae86b979`
- **Files touched (45):**
  - `Assets/BrittleStarModeSwitchingActionExecutor.cs`
  - `Assets/BrittleStarModeSwitchingActionExecutor.cs.meta`
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
  - `Assets/_Scripts/Controller/Multiplayer/Tests/ServerPlayerVesselInitializerWithAITests.cs.meta`
  - `Assets/_Scripts/Controller/Projectiles/Gun.cs`
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

### `35d1edea6` — Adjusted Stuff on BrittleStar

_Philip Appoh, 2026-05-29 12:46:04 -0500_

```text
 Assets/BrittleStarModeSwitchingActionExecutor.cs                      |  14 ++++
 Assets/BrittleStarModeSwitchingActionExecutor.cs.meta                 |   2 +
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab                       | 143 +++++++++++++++++++++++++++-----
 Assets/_Scenes/Menu_Main.unity                                        |  15 +++-
 .../Tests/ServerPlayerVesselInitializerWithAITests.cs.meta            |   2 +
 Assets/_Scripts/Controller/Projectiles/Gun.cs                         |   2 +-
 .../R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs       |  18 +++-
 .../Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs |  12 +--
 8 files changed, 175 insertions(+), 33 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 80 of 100 lines)</summary>

```diff
diff --git a/Assets/BrittleStarModeSwitchingActionExecutor.cs b/Assets/BrittleStarModeSwitchingActionExecutor.cs
new file mode 100644
index 000000000..94a3a0b52
--- /dev/null
+++ b/Assets/BrittleStarModeSwitchingActionExecutor.cs
@@ -0,0 +1,14 @@
+using CosmicShore.Gameplay;
+using CosmicShore.Utility;
+using UnityEngine;
+
+namespace CosmicShore
+{
+    public class BrittleStarModeSwitchingActionExecutor : ShipActionExecutorBase
+    {
+        public override void Initialize(IVesselStatus shipStatus)
+        {
+           
+        }
+    }
+}
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
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
index 267053497..671274826 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
@@ -25,9 +25,21 @@ namespace CosmicShore.Gameplay
 
             switch (_selector)
             {
-                case 1: _active = ringFire; _selector = 2; break;
-                case 2: _active = creationMode; _selector = 3; break;
-                case 3: _active = speedMode; _selector = 1; break;
+                case 1: 
+                    _active = ringFire; 
+                    _selector = 2;
+                    Debug.Log("Ring Fire");
+                    break;
+                case 2: 
+                    _active = creationMode; 
+                    _selector = 3;
+                    Debug.Log("Creation Mode");
+                    break;
+                case 3: 
+                    _active = speedMode; 
+                    _selector = 1;
+                    Debug.Log("Speed Mode");
+                    break;
             }
 
             _active?.StartAction(execs, vesselStatus);
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
index b18cbace7..cb4915444 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
@@ -1,10 +1,12 @@
 using UnityEngine;
+using CosmicShore.Utility;
 
 namespace CosmicShore.Gameplay
 {
     public class GunRingTransformer : MonoBehaviour
     {
-        [SerializeField] VesselStatus shipInstance;
+        [RequireInterface(typeof(IVesselStatus))]
+        [SerializeField] MonoBehaviour shipInstance;
         [SerializeField] Transform gunFocus;
         [SerializeField] GameObject pivotObject;
 
```

</details>

### `c24493623` — Trying to get GunRingTransformer Guns To Work

_Philip Appoh, 2026-05-29 15:04:36 -0500_

```text
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab                       | 36 ++++++++++++-------------
 .../VesselActions/BrittleStar/BrittleStarFullAutoAction.asset         | 28 ++++++++++++++++++++
 .../VesselActions/BrittleStar/BrittleStarFullAutoAction.asset.meta    |  8 ++++++
 .../VesselActions/BrittleStar/BrittleStarModeSwitchingFire.asset      |  3 ++-
 .../R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs       | 47 ++++++++++++++++++++++-----------
 5 files changed, 88 insertions(+), 34 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
index 671274826..5a43eeef2 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/BrittleStarModeSwitchingFireSO.cs
@@ -12,36 +12,53 @@ namespace CosmicShore.Gameplay
         [SerializeField] private ShipActionSO ringFire;
         [SerializeField] private ShipActionSO creationMode;
 
+
+        [Header("Ring Fire Rate")]
+        [Tooltip("Minimum seconds between ring fire bursts when mode-switching triggers it.")]
+        [SerializeField] private float ringFireInterval = 1f;
+
         private ActionExecutorRegistry _registry;
         private bool _isHeld;
         private int _selector = 1;
         private ShipActionSO _active;
 
+        private float _lastRingFireTime = float.MinValue;
+
+
         public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
         {
             base.Initialize(vs);
             _isHeld = true;
             _registry = execs;
+            ShipActionSO next;
+            int nextSelector;
+
 
             switch (_selector)
             {
-                case 1: 
-                    _active = ringFire; 
-                    _selector = 2;
-                    Debug.Log("Ring Fire");
-                    break;
-                case 2: 
-                    _active = creationMode; 
-                    _selector = 3;
-                    Debug.Log("Creation Mode");
-                    break;
-                case 3: 
-                    _active = speedMode; 
-                    _selector = 1;
-                    Debug.Log("Speed Mode");
-                    break;
+                case 1: next = ringFire; nextSelector = 2; break;
+                case 2: next = creationMode; nextSelector = 3; break;
+                case 3: next = speedMode; nextSelector = 1; break;
+                default: return;
+            }
+
+            if (_active == ringFire)
+            {
+                if (Time.time - _lastRingFireTime < ringFireInterval)
+                    return;
+                _lastRingFireTime = Time.time;
             }
 
+
+            if (next == ringFire && Time.time - _lastRingFireTime < ringFireInterval)
+                return;
+
+            _active = next;
+            _selector = nextSelector;
+
+            if (_active == ringFire)
+                _lastRingFireTime = Time.time;
+
             _active?.StartAction(execs, vesselStatus);
         }
 
```

</details>

### `08682d842` — BuildSetts

_Philip Appoh, 2026-07-03 12:23:59 -0500_

```text
 ProjectSettings/EditorBuildSettings.asset  | 2 ++
 ProjectSettings/VirtualProjectsConfig.json | 2 +-
 2 files changed, 3 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/ProjectSettings/VirtualProjectsConfig.json b/ProjectSettings/VirtualProjectsConfig.json
index 602c3b1ec..4ea16eb5a 100644
--- a/ProjectSettings/VirtualProjectsConfig.json
+++ b/ProjectSettings/VirtualProjectsConfig.json
@@ -1,4 +1,4 @@
 {
   "PlayerTags": [],
-  "version": "1.6.3"
+  "version": "1.6.1"
 }
\ No newline at end of file
```

</details>

### `76e9aa413` — Tried fix guns

_Philip Appoh, 2026-07-15 11:15:11 -0500_

```text
 .../Resources/Fonts & Materials/Electronic Highway Sign SDF.asset     | 289 ++------------------------------
 Assets/_Prefabs/Spacevessels/BrittleStar.prefab                       |  41 ++++-
 .../Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs |  13 +-
 3 files changed, 49 insertions(+), 294 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
index cb4915444..c70940a3a 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/GunRingTransformer.cs
@@ -16,11 +16,11 @@ namespace CosmicShore.Gameplay
 
         void Start()
         {
-            foreach (var child in GetComponentsInChildren<Transform>())
-            {
-                if (child == transform) continue;
+            var vessel = shipInstance as IVesselStatus;
 
-                var vessel = shipInstance as IVesselStatus;
+            for (int i = 0; i < transform.childCount; i++)
+            {
+                var child = transform.GetChild(i);
                 Vector3 direction = (child.position - vessel.Transform.position).normalized;
                 child.position = vessel.Transform.position + direction * radius;
             }
@@ -33,10 +33,9 @@ namespace CosmicShore.Gameplay
             Vector3 targetFocus = new Vector3(0, 0, 300f * rightStick.sqrMagnitude + 70f);
             gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, targetFocus, Time.deltaTime * speed);
 
-            foreach (var child in GetComponentsInChildren<Transform>())
+            for (int i = 0; i < transform.childCount; i++)
             {
-                if (child == transform) continue;
-
+                var child = transform.GetChild(i);
                 child.RotateAround(pivotObject.transform.position, pivotObject.transform.forward, rotationSpeed * Time.deltaTime);
                 child.LookAt(gunFocus);
             }
```

</details>

_Also contains 7 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
