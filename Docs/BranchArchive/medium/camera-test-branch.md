# Branch archive: `camera-test-branch`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2025-07-04 by Shombith03
- **Unmerged commits:** 4
- **Forked from:** `ff6243dfb` (2025-07-02, Merge pull request #47 from froglet-studio/codex/draft-cameramigrationreview.m)
- **Tip:** `35eff6c37`
- **Files touched (38):**
  - `Assets/_Prefabs/CORE/CustomCameraSetup.prefab`
  - `Assets/_Prefabs/CORE/CustomCameraSetup.prefab.meta`
  - `Assets/_SO_Assets/CameraConfigs.meta`
  - `Assets/_SO_Assets/CameraConfigs/DeathCameraConfig.asset`
  - `Assets/_SO_Assets/CameraConfigs/DeathCameraConfig.asset.meta`
  - `Assets/_SO_Assets/CameraConfigs/EndGameCameraConfig.asset`
  - `Assets/_SO_Assets/CameraConfigs/EndGameCameraConfig.asset.meta`
  - `Assets/_SO_Assets/CameraConfigs/GameplayCameraConfig.asset`
  - `Assets/_SO_Assets/CameraConfigs/GameplayCameraConfig.asset.meta`
  - `Assets/_SO_Assets/CameraConfigs/MainMenuCameraConfig.asset`
  - `Assets/_SO_Assets/CameraConfigs/MainMenuCameraConfig.asset.meta`
  - `Assets/_Scripts/Game/Arcade/MiniGame.cs`
  - `Assets/_Scripts/Game/Managers/CameraConfig.cs`
  - `Assets/_Scripts/Game/Managers/CameraConfig.cs.meta`
  - `Assets/_Scripts/Game/Managers/CameraManager.cs`
  - `Assets/_Scripts/Game/Managers/CustomCameraController.cs`
  - `Assets/_Scripts/Game/Managers/CustomCameraController.cs.meta`
  - `Assets/_Scripts/Game/Managers/GameManager.cs`
  - `Assets/_Scripts/Game/Managers/ShipCameraSettings.cs`
  - `Assets/_Scripts/Game/Managers/ShipCameraSettings.cs.meta`
  - `Assets/_Scripts/Game/Ship/ClearPrisms.cs`
  - `Assets/_Scripts/Game/Ship/GunShipTransformer.cs`
  - `Assets/_Scripts/Game/Ship/IShipStatus.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/CustomZoomAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipActions/CustomZoomAction.cs.meta`
  - `Assets/_Scripts/Game/Ship/ShipActions/ZoomOutAction.cs`
  - `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs`
  - `Assets/_Scripts/Game/Ship/ShipStatus.cs`
  - `Assets/_Scripts/Game/Ship/Skimmer.cs`
  - `Assets/_Scripts/Utility/Recording/VCamRecorderController.cs`
  - `Assets/_Scripts/Utility/Tools/StartFromAnyScene.cs`
  - `Docs/CameraMigrationReview.md`
  - `Docs/CameraSystemAnalysis.md`
  - `Docs/CustomCameraSetupGuide.md`
  - `Docs/MigrationTestingGuide.md`
  - `Docs/ShipCameraMigrationGuide.md`
  - `Docs/UpdatedCameraConfiguration.md`
  - `Packages/manifest.json`

### `4bc8d5fb6` — Add zoom distance API to CustomCameraController

_Garrett Milliron, 2025-07-01 20:12:53 -0400_

```text
 Assets/_SO_Assets/CameraConfigs/GameplayCameraConfig.asset            |  21 +++
 Assets/_SO_Assets/CameraConfigs/GameplayCameraConfig.asset.meta       |   8 +
 Assets/_SO_Assets/CameraConfigs/MainMenuCameraConfig.asset            |  21 +++
 Assets/_SO_Assets/CameraConfigs/MainMenuCameraConfig.asset.meta       |   8 +
 Assets/_Scripts/Game/Arcade/MiniGame.cs                               |   4 +-
 Assets/_Scripts/Game/Managers/CameraConfig.cs                         |  16 ++
 .../Game/Managers/{CameraManager.cs.meta => CameraConfig.cs.meta}     |   8 +-
 Assets/_Scripts/Game/Managers/CameraManager.cs                        | 322 --------------------------------
 Assets/_Scripts/Game/Managers/CustomCameraController.cs               | 163 ++++++++++++++++
 Assets/_Scripts/Game/Managers/CustomCameraController.cs.meta          |  11 ++
 Assets/_Scripts/Game/Managers/GameManager.cs                          |   3 +-
 Assets/_Scripts/Game/Managers/ShipCameraSettings.cs                   |  14 ++
 Assets/_Scripts/Game/Managers/ShipCameraSettings.cs.meta              |  11 ++
 Assets/_Scripts/Game/Ship/ClearPrisms.cs                              |   6 +-
 Assets/_Scripts/Game/Ship/GunShipTransformer.cs                       |   4 +-
 Assets/_Scripts/Game/Ship/IShipStatus.cs                              |   3 +-
 Assets/_Scripts/Game/Ship/ShipActions/CustomZoomAction.cs             |  21 +++
 .../ShipActions/{ZoomOutAction.cs.meta => CustomZoomAction.cs.meta}   |   0
 Assets/_Scripts/Game/Ship/ShipActions/ZoomOutAction.cs                |  18 --
 Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs                     |  49 +----
 Assets/_Scripts/Game/Ship/ShipStatus.cs                               |   3 +-
 Assets/_Scripts/Game/Ship/Skimmer.cs                                  |  19 +-
 Assets/_Scripts/Utility/Recording/VCamRecorderController.cs           |  12 +-
 Assets/_Scripts/Utility/Tools/StartFromAnyScene.cs                    |   2 +-
 Docs/CameraSystemAnalysis.md                                          |  75 ++++++++
 Docs/CustomCameraSetupGuide.md                                        |  11 ++
 Docs/MigrationTestingGuide.md                                         |  31 +++
 Docs/ShipCameraMigrationGuide.md                                      |  16 ++
 Packages/manifest.json                                                |   1 -
 36 files changed, 637 insertions(+), 410 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 1075 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Arcade/MiniGame.cs b/Assets/_Scripts/Game/Arcade/MiniGame.cs
index eccb6aeb8..66c138e88 100644
--- a/Assets/_Scripts/Game/Arcade/MiniGame.cs
+++ b/Assets/_Scripts/Game/Arcade/MiniGame.cs
@@ -375,7 +375,7 @@ namespace CosmicShore.Game.Arcade
                     ScoreTracker.GetHighScore(),
                     UserAction.GetGameplayUserActionLabel(gameMode, PlayerShipType, IntensityLevel)));
 
-            CameraManager.Instance.SetEndCameraActive();
+            CustomCameraController.Instance.SetEndCameraActive();
             PauseSystem.TogglePauseGame();
             gameRunning = false;
             EndGameScreen.SetActive(true);
@@ -433,7 +433,7 @@ namespace CosmicShore.Game.Arcade
             ActivePlayer.Ship.ShipStatus.ResourceSystem.Reset();
             ActivePlayer.Ship.SetResourceLevels(ResourceCollection);
 
-            CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.ShipStatus.FollowTarget);
+            CustomCameraController.Instance.SetupGamePlayCameras(ActivePlayer.Ship.ShipStatus.FollowTarget);
 
             // For single player games, don't require the extra button press
             if (Players.Count > 1)
diff --git a/Assets/_Scripts/Game/Managers/CameraConfig.cs b/Assets/_Scripts/Game/Managers/CameraConfig.cs
new file mode 100644
index 000000000..ba4377155
--- /dev/null
+++ b/Assets/_Scripts/Game/Managers/CameraConfig.cs
@@ -0,0 +1,16 @@
+using UnityEngine;
+
+namespace CosmicShore.Core
+{
+    [System.Serializable]
+    public class CameraConfig
+    {
+        public float fieldOfView = 60f;
+        public float nearClip = 0.1f;
+        public float farClip = 1000f;
+        public bool orthographic = false;
+        public float orthoSize = 10f;
+        public Vector3 followOffset = Vector3.zero;
+        public float maxZoomDistance = 10f;
+    }
+}
diff --git a/Assets/_Scripts/Game/Managers/CameraManager.cs b/Assets/_Scripts/Game/Managers/CameraManager.cs
deleted file mode 100644
index 39b8c14ca..000000000
--- a/Assets/_Scripts/Game/Managers/CameraManager.cs
+++ /dev/null
@@ -1,322 +0,0 @@
-using Unity.Cinemachine;
-using CosmicShore.Core;
-using CosmicShore.Utility;
-using System.Collections;
-using UnityEngine;
-using CosmicShore;
-using CosmicShore.Utilities;
-
-public class CameraManager : SingletonPersistent<CameraManager>
-{
-    [SerializeField]
-    ThemeManagerDataContainerSO _themeManagerData;
-
-    [SerializeField] CinemachineCamera mainMenuCamera;
-    [SerializeField] CinemachineVirtualCameraBase playerCamera;
-    [SerializeField] CinemachineVirtualCameraBase deathCamera;
-    [SerializeField] CinemachineVirtualCameraBase endCamera;
-
-    [SerializeField] Transform endCameraFollowTarget;
-    [SerializeField] Transform endCameraLookAtTarget;
-
-    Transform playerFollowTarget;
-    readonly int activePriority = 10;
-    readonly int inactivePriority = 1;
-
-    // Runtime-only properties (not serialized)
-    private Vector3 originalFollowOffset;
-    private Vector3 runtimeFollowOffset;
-    private bool hasOriginalOffset = false;
-
-    // Drift stuff
-    bool zoomingOut;
-
-    public bool FollowOverride = false;
-    public bool FixedFollow = false;
-    public bool isOrthographic = false;
-
-    public float CloseCamDistance;
-    public float FarCamDistance;
-
-    CinemachineCamera vCam;
-    CinemachineFollow transposer;
-
-    Coroutine zoomOutCoroutine;
-    Coroutine returnToNeutralCoroutine;
-    Coroutine lerper;
-
-    private void OnEnable()
-    {
-        GameManager.OnPlayGame += SetupGamePlayCameras;
-        GameManager.OnGameOver += SetEndCameraActive;
-    }
-
-    void OnDisable()
-    {
-        GameManager.OnPlayGame -= SetupGamePlayCameras;
-        GameManager.OnGameOver -= SetEndCameraActive;
-
-        // Restore original offset when disabled
-        RestoreOriginalOffset();
-    }
-
-    void Start()
-    {
-        vCam = playerCamera.gameObject.GetComponent<CinemachineCamera>();
-        OnMainMenu();
-    }
-
-    void LateUpdate()
-    {
-        // Apply runtime offset without modifying the serialized property
-        if (transposer != null && Application.isPlaying && hasOriginalOffset)
-        {
-            ApplyRuntimeOffset();
-        }
-    }
-
-    private void ApplyRuntimeOffset()
-    {
-        // Use reflection to set the offset without marking scene dirty
-        var field = typeof(CinemachineFollow).GetField("m_FollowOffset",
-            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-
-        if (field != null)
-        {
-            field.SetValue(transposer, runtimeFollowOffset);
-        }
-    }
-
-    private void RestoreOriginalOffset()
-    {
-        if (transposer != null && hasOriginalOffset)
-        {
-            var field = typeof(CinemachineFollow).GetField("m_FollowOffset",
-                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
-
-            if (field != null)
-            {
-                field.SetValue(transposer, originalFollowOffset);
-            }
```

</details>

### `5e131f56b` — Fix Reference Bug

_Shombith03, 2025-07-03 01:17:46 +0530_

```text
 Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs | 1 +
 1 file changed, 1 insertion(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs b/Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs
index d98ee8d93..4e0493088 100644
--- a/Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs
+++ b/Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs
@@ -1,5 +1,6 @@
 using UnityEngine;
 using CosmicShore.Core;
+using CosmicShore.Game;
 
 namespace CosmicShore
 {
```

</details>

### `35eff6c37` — docs: update camera migration review

_Shombith03, 2025-07-04 00:28:12 +0530_

```text
 Docs/CameraMigrationReview.md      |  9 ++++-----
 Docs/UpdatedCameraConfiguration.md | 19 +++++++++++++++++++
 2 files changed, 23 insertions(+), 5 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/CameraMigrationReview.md b/Docs/CameraMigrationReview.md
index c49b67022..3fd066830 100644
--- a/Docs/CameraMigrationReview.md
+++ b/Docs/CameraMigrationReview.md
@@ -4,8 +4,8 @@ This document tracks the migration to a new camera system.
 
 ## Files Added
 
-- `Assets/_Prefabs/Cameras/NewShipCamera.prefab` – new Cinemachine driven prefab.
-- `Assets/_Scripts/Game/Camera/CustomCameraController.cs` – controller for runtime input and zoom.
+- `Assets/_Prefabs/CORE/CustomCameraSetup.prefab` – new camera setup prefab.
+- `Assets/_Scripts/Game/Managers/CustomCameraController.cs` – controller for runtime input and zoom.
 - `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs` – exposes per ship overrides.
 
 ## Files Removed
@@ -16,12 +16,11 @@ This document tracks the migration to a new camera system.
 
 - **`CustomCameraController`** – manages camera movement and input.
 - **`ShipCameraCustomizer`** – configures follow targets and offsets for each ship.
-- **`CameraRigAnchor`** – helper for look‑at and follow transforms.
 
 ## Testing the Prefab
 
-1. Open `Assets/_Scenes/TestScenes/CameraTesting.unity`.
-2. Place **NewShipCamera** into the scene.
+1. Open `Assets/_Scenes/TestScenes/Ig/IgSandbox.unity`.
+2. Drag **CustomCameraSetup.prefab** into the scene.
 3. Play the scene and swap ships to verify the camera follows correctly.
 4. Use the mouse wheel or gamepad triggers to zoom in and out.
 5. Respawn the player to ensure orientation resets.
diff --git a/Docs/UpdatedCameraConfiguration.md b/Docs/UpdatedCameraConfiguration.md
new file mode 100644
index 000000000..26a4233c5
--- /dev/null
+++ b/Docs/UpdatedCameraConfiguration.md
@@ -0,0 +1,19 @@
+# Updated Camera Configuration
+
+This document summarizes the final state of the camera migration.
+
+## Prefab and Scripts
+
+- `Assets/_Prefabs/CORE/CustomCameraSetup.prefab` provides the hierarchy of cameras used at runtime.
+- `Assets/_Scripts/Game/Managers/CustomCameraController.cs` drives camera state transitions and zoom input.
+- `Assets/_Scripts/Game/Ship/ShipCameraCustomizer.cs` allows each ship prefab to override follow targets and zoom distances.
+- Configuration ScriptableObjects live in `Assets/_SO_Assets/CameraConfigs`.
+
+## Typical Scene Setup
+
+1. Open any gameplay or sandbox scene such as `Assets/_Scenes/TestScenes/Ig/IgSandbox.unity`.
+2. Drag **CustomCameraSetup.prefab** into the hierarchy if not already present.
+3. Ensure `CustomCameraController` is referenced by the `GameManager` or loaded via `StartFromAnyScene`.
+4. Press Play and confirm the main camera switches between menu, gameplay, death and end game states when triggered.
+
+Consult [CustomCameraSetupGuide](CustomCameraSetupGuide.md) for detailed integration steps.
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
