# Branch archive: `claude/fix-menu-camera-transition-GZzsT`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `bad3295bf` (2026-02-28, Merge pull request #274 from froglet-studio/claude/configure-camera-switching-)
- **Tip:** `571dac4c6`
- **Files touched (3):**
  - `Assets/_Scripts/Controller/Camera/CustomCameraController.cs`
  - `Assets/_Scripts/Controller/Managers/CameraManager.cs`
  - `Assets/_Scripts/System/MainMenuController.cs`

### `571dac4c6` — fix(menu): smooth camera transitions between menu and gameplay cameras

_Claude, 2026-02-28 06:19:00 +0000_

```text
The menu/gameplay camera switch was snapping because two separate physical
cameras (CinemachineCamera-driven main cam vs CustomCameraController player
cam) were being toggled with SetActive + SnapToTarget in a single frame.

- Add BlendFromPosition to CustomCameraController: overrides normal
  snap/smooth-damp with a SmoothStep interpolation from a captured start
  pose to the follow-target pose over a configurable duration.
- Add BlendPlayerCameraFrom to CameraManager as the public entry point.
- Menu→Gameplay (HandleEnterFreestyle): capture Camera.main pose before
  disabling the menu vCam, then blend the player camera from that pose
  instead of snapping.
- Gameplay→Menu (HandleExitFreestyle): capture the player camera pose,
  position Camera.main there before enabling the menu vCam so the
  CinemachineBrain blends from the gameplay view to the menu target.
```

```text
 Assets/_Scripts/Controller/Camera/CustomCameraController.cs | 45 +++++++++++++++++++++++++++++++++-
 Assets/_Scripts/Controller/Managers/CameraManager.cs        | 11 +++++++++
 Assets/_Scripts/System/MainMenuController.cs                | 56 ++++++++++++++++++++++++++++++++++++-------
 3 files changed, 102 insertions(+), 10 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 200 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Camera/CustomCameraController.cs b/Assets/_Scripts/Controller/Camera/CustomCameraController.cs
index e8cf10aa5..b489a4ec9 100644
--- a/Assets/_Scripts/Controller/Camera/CustomCameraController.cs
+++ b/Assets/_Scripts/Controller/Camera/CustomCameraController.cs
@@ -24,6 +24,12 @@ namespace CosmicShore.Gameplay
         public bool adaptiveZoomEnabled;
         private float _neutralOffsetZ;
 
+        // --- Blend transition (menu ↔ gameplay) ---
+        private float _blendAlpha = 1f;
+        private float _blendDuration;
+        private Vector3 _blendStartPos;
+        private Quaternion _blendStartRot;
+
         private void Awake()
         {
             Camera = GetComponent<Camera>();
@@ -44,6 +50,27 @@ namespace CosmicShore.Gameplay
                 _lastTargetPos = _followTarget.position;
 
             Vector3 desiredPos = _followTarget.position + _followTarget.rotation * _followOffset;
+
+            // Blend transition override (menu ↔ gameplay camera switch).
+            // While active, smoothly interpolates from the captured start pose
+            // to the follow-target pose, bypassing normal snap/smooth-damp logic.
+            if (_blendAlpha < 1f)
+            {
+                _blendAlpha += Time.deltaTime / _blendDuration;
+                _blendAlpha = Mathf.Clamp01(_blendAlpha);
+                float t = Mathf.SmoothStep(0f, 1f, _blendAlpha);
+
+                transform.position = Vector3.Lerp(_blendStartPos, desiredPos, t);
+
+                if (SafeLookRotation.TryGet(_followTarget.position - transform.position,
+                        _followTarget.up, out var blendRot, this, logError: false))
+                    transform.rotation = Quaternion.Slerp(_blendStartRot, blendRot, t);
+
+                _lastTargetPos = _followTarget.position;
+                _velocity = Vector3.zero;
+                return;
+            }
+
             Vector3 shipDelta = _followTarget.position - _lastTargetPos;
             float fwd = Vector3.Dot(shipDelta, _followTarget.forward);
             float lat = Vector3.Dot(shipDelta, _followTarget.right);
@@ -130,11 +157,27 @@ namespace CosmicShore.Gameplay
             _velocity = Vector3.zero;
         }
 
+        /// <summary>
+        /// Smoothly transitions the camera from the given pose to the current
+        /// follow-target pose over <paramref name="duration"/> seconds.
+        /// Overrides normal smooth-damp / snap logic until the blend completes.
+        /// </summary>
+        public void BlendFromPosition(Vector3 fromPos, Quaternion fromRot, float duration)
+        {
+            _blendStartPos = fromPos;
+            _blendStartRot = fromRot;
+            _blendDuration = Mathf.Max(duration, 0.01f);
+            _blendAlpha = 0f;
+            transform.position = fromPos;
+            transform.rotation = fromRot;
+            _velocity = Vector3.zero;
+        }
+
         public void Activate()
         {
             gameObject.SetActive(true);
             if (!_currentSettings) return;
-            
+
             Camera.nearClipPlane = _currentSettings.nearClipPlane;
             Camera.farClipPlane = _currentSettings.farClipPlane;
         }
diff --git a/Assets/_Scripts/Controller/Managers/CameraManager.cs b/Assets/_Scripts/Controller/Managers/CameraManager.cs
index cd424ffa2..42932f545 100644
--- a/Assets/_Scripts/Controller/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Controller/Managers/CameraManager.cs
@@ -145,6 +145,17 @@ namespace CosmicShore.Gameplay
                 pcc.SnapToTarget();
         }
 
+        /// <summary>
+        /// Blends the player camera from the given pose to its follow target
+        /// over <paramref name="duration"/> seconds. Call after
+        /// <see cref="SetupGamePlayCameras"/> to replace the snap with a smooth transition.
+        /// </summary>
+        public void BlendPlayerCameraFrom(Vector3 fromPos, Quaternion fromRot, float duration)
+        {
+            if (_playerCamera is CustomCameraController pcc)
+                pcc.BlendFromPosition(fromPos, fromRot, duration);
+        }
+
         public void SetNormalizedCloseCameraDistance(float normalizedDistance)
         {
             if (_playerCamera == null) return;
diff --git a/Assets/_Scripts/System/MainMenuController.cs b/Assets/_Scripts/System/MainMenuController.cs
index 1dd015f7d..ebdd56905 100644
--- a/Assets/_Scripts/System/MainMenuController.cs
+++ b/Assets/_Scripts/System/MainMenuController.cs
@@ -45,6 +45,9 @@ namespace CosmicShore.Core
         int menuIntensity = 1;
 
         [Header("Camera Switching")]
+        [SerializeField, Tooltip("Duration of camera blend when switching between menu and gameplay cameras.")]
+        float _cameraBlendDuration = 1.5f;
+
         [SerializeField, Tooltip("SOAP events for entering/exiting freestyle mode.")]
         MenuFreestyleEventsContainerSO _freestyleEvents;
 
@@ -112,8 +115,8 @@ namespace CosmicShore.Core
             if (_gameData?.OnLaunchGame != null)
                 _gameData.OnLaunchGame.OnRaised += HandleLaunchGame;
 
-            _freestyleEvents.OnEnterFreestyle.OnRaised += ActivateGameplayCamera;
-            _freestyleEvents.OnExitFreestyle.OnRaised += ActivateMenuCamera;
+            _freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
+            _freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
         }
 
         void UnsubscribeEvents()
@@ -124,8 +127,8 @@ namespace CosmicShore.Core
             if (_gameData?.OnLaunchGame != null)
                 _gameData.OnLaunchGame.OnRaised -= HandleLaunchGame;
 
-            _freestyleEvents.OnEnterFreestyle.OnRaised -= ActivateGameplayCamera;
-            _freestyleEvents.OnExitFreestyle.OnRaised -= ActivateMenuCamera;
+            _freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
+            _freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;
         }
 
         // ── Game Data Configuration ─────────────────────────────────────
@@ -164,7 +167,8 @@ namespace CosmicShore.Core
 
         /// <summary>
         /// Activates the CM Main Menu Cinemachine camera for menu state.
-        /// Deactivates all CameraManager gameplay cameras.
+        /// Deactivates all CameraManager gameplay cameras. Used for the initial
+        /// menu setup where no blend is needed.
         /// </summary>
         void ActivateMenuCamera()
         {
@@ -174,19 +178,53 @@ namespace CosmicShore.Core
         }
 
         /// <summary>
-        /// Activates CM PlayerCam for freestyle/game state.
-        /// Disables the CM Main Menu Cinemachine camera.
+        /// Smoothly transitions from the gameplay camera to the menu camera.
+        /// Positions Camera.main at the gameplay camera's pose so the CinemachineBrain
```

</details>
