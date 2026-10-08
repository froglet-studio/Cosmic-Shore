# Branch archive: `claude/new-vessel-creation-f55WE`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-15 by Claude
- **Unmerged commits:** 9
- **Forked from:** `65e7a0a71` (2026-05-07, prism tweaks in hexrace)
- **Tip:** `022ce2699`
- **Files touched (19):**
  - `Assets/Unity Assests/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Electronic Highway Sign SDF.asset`
  - `Assets/_Prefabs/Environment/CapsuleMembrane.prefab`
  - `Assets/_Prefabs/Spacevessels/Falcon.prefab`
  - `Assets/_Scenes/Bootstrap.unity`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs`
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

### `64d580c21` — chore(vessel): set default menu vessel to Sparrow for BrittleStar dev

_Claude, 2026-05-15 17:24:07 +0000_

```text
Swap hardcoded Squirrel references in AppManager.ConfigureGameData()
and MainMenuController.menuVesselClass to Sparrow. Will be updated to
BrittleStar once the enum value and prefab are registered.
```

```text
 Assets/_Scripts/System/AppManager.cs         | 2 +-
 Assets/_Scripts/System/MainMenuController.cs | 2 +-
 2 files changed, 2 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/System/AppManager.cs b/Assets/_Scripts/System/AppManager.cs
index bd73ad510..ab597aa37 100644
--- a/Assets/_Scripts/System/AppManager.cs
+++ b/Assets/_Scripts/System/AppManager.cs
@@ -447,7 +447,7 @@ namespace CosmicShore.Core
 
             // Set sane defaults for the menu scene.
             gameData.SelectedPlayerCount.Value = 1;
-            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
+            gameData.selectedVesselClass.Value = VesselClassType.Sparrow;
             gameData.SelectedIntensity.Value = 1;
         }
 
diff --git a/Assets/_Scripts/System/MainMenuController.cs b/Assets/_Scripts/System/MainMenuController.cs
index bd76f5f6d..73679e23a 100644
--- a/Assets/_Scripts/System/MainMenuController.cs
+++ b/Assets/_Scripts/System/MainMenuController.cs
@@ -47,7 +47,7 @@ namespace CosmicShore.Core
 
         [Header("Menu Autopilot Configuration")]
         [SerializeField, Tooltip("Vessel class displayed as the autopilot in the menu background.")]
-        VesselClassType menuVesselClass = VesselClassType.Squirrel;
+        VesselClassType menuVesselClass = VesselClassType.Sparrow;
 
         [SerializeField, Tooltip("Number of AI players for the menu background scene.")]
         int menuPlayerCount = 3;
```

</details>

### `6e3ee3d96` — chore(vessel): change menu default vessel from Squirrel (6) to Sparrow (11)

_Claude, 2026-05-15 17:28:24 +0000_

```text
Update AppManager.ConfigureGameData, MainMenuController.menuVesselClass
code default, and the serialized scene value in Menu_Main.unity.
Scene asset was the root cause — SerializeField overrides code defaults.
```

```text
 Assets/_Scenes/Menu_Main.unity | 2 +-
 1 file changed, 1 insertion(+), 1 deletion(-)
```

### `022ce2699` — feat(vessel): add SetGuns method to FullAutoActionExecutor

_Claude, 2026-05-15 18:37:45 +0000_

```text
Exposes muzzle swap for BrittleStarBoostActionExecutor to switch
between main and secondary gun positions on boost start/end.
```

```text
 Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs | 2 ++
 1 file changed, 2 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
index ac3997e47..99989a672 100644
--- a/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
+++ b/Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/FullAutoActionExecutor.cs
@@ -18,6 +18,8 @@ public sealed class FullAutoActionExecutor : ShipActionExecutorBase
     [SerializeField] private Gun gun;
     [SerializeField] private Transform[] muzzles;
 
+    public void SetGuns(Transform[] newMuzzles) => muzzles = newMuzzles;
+
     [Header("Events")]
     [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;
 
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
