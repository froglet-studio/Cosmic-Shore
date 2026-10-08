# Branch archive: `claude/beautiful-dirac-K5720`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-05-26 by Claude
- **Unmerged commits:** 3
- **Forked from:** `e81a9a5e7` (2026-05-22, Merge pull request #528 from froglet-studio/claude/kind-hawking-jCpSO)
- **Tip:** `2822e83f2`
- **Files touched (19):**
  - `Assets/_Prefabs/CORE/CameraManager.prefab`
  - `Assets/_Prefabs/CORE/Game Scene Main Camera.prefab`
  - `Assets/_Prefabs/CORE/Game Scene Main Camera.prefab.meta`
  - `Assets/_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameCrystalCaptureMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameTournamentMultuplayer.unity`
  - `Assets/_Scenes/Multiplayer Scenes/MinigameWildlifeBlitzMultuplayerCoOp.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity`
  - `Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity`
  - `Assets/_Scenes/Tools/MattsRecording Studio.unity`
  - `Assets/_Scenes/Tools/Recording Studio.unity`
  - `Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs`
  - `Assets/_Scripts/Controller/Managers/CameraManager.cs`
  - `Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs`

### `59dbca815` — perf(camera): disable redundant CinemachineBrain camera in minigame scenes

_Claude, 2026-05-26 16:43:40 +0000_

```text
The "Mini Game Main Camera" prefab instance in each minigame scene only
exists to host a CinemachineBrain, but minigame scenes have no vCams to
drive — so its full render pass (~11ms in profiler) is wasted alongside
CM PlayerCam's own render.

SetupGamePlayCameras now disables that camera's Camera component at
runtime, keeping the GameObject (and AudioListener) alive. CM PlayerCam,
CM DeathCam, and CM EndCam are tagged MainCamera in the CameraManager
prefab so the 16 Camera.main call sites still resolve to the active
gameplay camera (SetActiveCamera ensures only one is enabled at a time).
```

```text
 Assets/_Prefabs/CORE/CameraManager.prefab            |  6 +++---
 Assets/_Scripts/Controller/Managers/CameraManager.cs | 13 ++++++++++++-
 2 files changed, 15 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/CameraManager.cs b/Assets/_Scripts/Controller/Managers/CameraManager.cs
index 3d932cb75..428665afd 100644
--- a/Assets/_Scripts/Controller/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Controller/Managers/CameraManager.cs
@@ -113,9 +113,10 @@ namespace CosmicShore.Gameplay
             _playerFollowTarget = followTarget;
             _playerCamera?.SetFollowTarget(_playerFollowTarget);
             _deathCamera?.SetFollowTarget(_playerFollowTarget);
-            _themeManagerData.SetBackgroundColor(Camera.main);
 
             SetCloseCameraActive();
+            DisableMinigameBrainCamera();
+            _themeManagerData.SetBackgroundColor(Camera.main);
 
             var shipGO = _playerFollowTarget.gameObject;
             var shipCustomizer = shipGO.GetComponent<VesselCameraCustomizer>();
@@ -195,6 +196,16 @@ namespace CosmicShore.Gameplay
 
         public ICameraController GetActiveController() => _activeController;
 
+        // CinemachineBrain camera has no vCams in minigame scenes — disabling its Camera component
+        // skips a redundant full render pass. Keeps the GameObject (and its AudioListener) alive;
+        // CM PlayerCam carries the MainCamera tag now so Camera.main still resolves.
+        private void DisableMinigameBrainCamera()
+        {
+            var go = GameObject.Find("Mini Game Main Camera");
+            if (go != null && go.TryGetComponent<Camera>(out var cam))
+                cam.enabled = false;
+        }
+
         /// <summary>
         /// Deactivates all managed cameras (player, death, end) without activating a replacement.
         /// Used by the menu to hand control to the Cinemachine-driven main menu camera.
```

</details>

### `f41ed04aa` — chore(camera): remove vestigial Game Scene Main Camera prefab from 13 scenes

_Claude, 2026-05-26 19:29:06 +0000_

```text
Finishes the previous commit's perf fix by physically removing the redundant
CinemachineBrain-host camera from every minigame and tool scene. With no
brain camera left to disable, the runtime DisableMinigameBrainCamera hack
is gone too — SetupGamePlayCameras goes back to plain setup.

CM PlayerCam picks up an AudioListener in the CameraManager prefab so the
single-listener invariant holds in gameplay; Menu_Main keeps its own
listener on the menu Camera. The orphaned Game Scene Main Camera.prefab
and .meta are deleted (no remaining references in the project).

Net: -1592 lines across 17 files, one fewer render pass per minigame frame,
one fewer name-based runtime lookup.
```

```text
 Assets/_Prefabs/CORE/CameraManager.prefab                             |   9 ++
 Assets/_Prefabs/CORE/Game Scene Main Camera.prefab                    | 172 --------------------------------
 Assets/_Prefabs/CORE/Game Scene Main Camera.prefab.meta               |   7 --
 .../_Scenes/Multiplayer Scenes/ArcadeGameMultiplayer2v2CoOpVsAI.unity | 104 -------------------
 .../MinigameCrystalCaptureMultiplayer_Gameplay.unity                  | 104 -------------------
 .../Multiplayer Scenes/MinigameDuelForCellMultiplayer_Gameplay.unity  | 104 -------------------
 .../Multiplayer Scenes/MinigameFreestyleMultiplayer_Gameplay.unity    | 104 -------------------
 Assets/_Scenes/Multiplayer Scenes/MinigameHexRace.unity               | 119 ----------------------
 Assets/_Scenes/Multiplayer Scenes/MinigameJoust_Gameplay.unity        | 104 -------------------
 Assets/_Scenes/Multiplayer Scenes/MinigameTournamentMultuplayer.unity | 104 -------------------
 .../Multiplayer Scenes/MinigameWildlifeBlitzMultuplayerCoOp.unity     | 104 -------------------
 Assets/_Scenes/Singleplayer Scenes/MinigameCellularDuel.unity         | 119 ----------------------
 Assets/_Scenes/Singleplayer Scenes/MinigameFreestyle.unity            | 114 ---------------------
 Assets/_Scenes/Singleplayer Scenes/MinigameWildlifeBlitz.unity        | 104 -------------------
 Assets/_Scenes/Tools/MattsRecording Studio.unity                      | 109 --------------------
 Assets/_Scenes/Tools/Recording Studio.unity                           | 109 --------------------
 Assets/_Scripts/Controller/Managers/CameraManager.cs                  |  11 --
 17 files changed, 9 insertions(+), 1592 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Managers/CameraManager.cs b/Assets/_Scripts/Controller/Managers/CameraManager.cs
index 428665afd..dbf0207ad 100644
--- a/Assets/_Scripts/Controller/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Controller/Managers/CameraManager.cs
@@ -115,7 +115,6 @@ namespace CosmicShore.Gameplay
             _deathCamera?.SetFollowTarget(_playerFollowTarget);
 
             SetCloseCameraActive();
-            DisableMinigameBrainCamera();
             _themeManagerData.SetBackgroundColor(Camera.main);
 
             var shipGO = _playerFollowTarget.gameObject;
@@ -196,16 +195,6 @@ namespace CosmicShore.Gameplay
 
         public ICameraController GetActiveController() => _activeController;
 
-        // CinemachineBrain camera has no vCams in minigame scenes — disabling its Camera component
-        // skips a redundant full render pass. Keeps the GameObject (and its AudioListener) alive;
-        // CM PlayerCam carries the MainCamera tag now so Camera.main still resolves.
-        private void DisableMinigameBrainCamera()
-        {
-            var go = GameObject.Find("Mini Game Main Camera");
-            if (go != null && go.TryGetComponent<Camera>(out var cam))
-                cam.enabled = false;
-        }
-
         /// <summary>
         /// Deactivates all managed cameras (player, death, end) without activating a replacement.
         /// Used by the menu to hand control to the Cinemachine-driven main menu camera.
```

</details>

### `2822e83f2` — fix(perf): stop ShipAudioController re-searching for nonexistent StudioListener every frame

_Claude, 2026-05-26 20:18:52 +0000_

```text
The project doesn't actually have a StudioListener anywhere — verified
zero references across all scenes and prefabs. The Listener attach path
was searching the whole scene with Object.FindFirstObjectByType every
frame because:

  1. _listener stayed null (nothing to find)
  2. The fallback to ship attachment set _attachMode=Ship while
     ResolveDesiredAttachMode kept returning Listener for the local
     player, so the desired/current mismatch tripped every frame.

Profiler showed GetListenerTransform consuming 23.44 ms / 15.1% of the
frame in a typical session.

Fix:
- _listenerSearched gates GetListenerTransform's project-wide search to
  one attempt per controller lifetime
- _listenerAttachFailed gates TryRouteAttachment so the detach/reattach
  cycle stops cold after the first failed listener attempt

Also fix two regressions from the camera-cleanup commit:
- CameraManager.SetupGamePlayCameras now passes the just-activated
  player camera to ThemeManagerDataContainerSO instead of relying on
  Camera.main, which can return null in the first frame after a scene
  transition.
- MainMenuCameraController.StartRandomSwitchLoopIfEnabled guards
  against a null parent CTS so OnValidate doesn't NRE when the
  inspector fires it before Start has run.
```

```text
 Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs  |  3 +++
 Assets/_Scripts/Controller/Managers/CameraManager.cs           |  6 +++++-
 Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs | 23 +++++++++++++++++------
 3 files changed, 25 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs b/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs
index 587a524dd..006ec930e 100644
--- a/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs
+++ b/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs
@@ -641,6 +641,9 @@ namespace CosmicShore.Gameplay
         {
             if (!_randomSwitchEnabled) return;
             if (_randomSwitchModes == null || _randomSwitchModes.Length < 2) return;
+            // OnValidate can fire from the inspector before Start runs, so the parent CTS
+            // may not exist yet — in that case Start will pick this up on its own.
+            if (_cts == null) return;
 
             _randomSwitchCts?.Cancel();
             _randomSwitchCts?.Dispose();
diff --git a/Assets/_Scripts/Controller/Managers/CameraManager.cs b/Assets/_Scripts/Controller/Managers/CameraManager.cs
index dbf0207ad..a1c88763d 100644
--- a/Assets/_Scripts/Controller/Managers/CameraManager.cs
+++ b/Assets/_Scripts/Controller/Managers/CameraManager.cs
@@ -115,7 +115,11 @@ namespace CosmicShore.Gameplay
             _deathCamera?.SetFollowTarget(_playerFollowTarget);
 
             SetCloseCameraActive();
-            _themeManagerData.SetBackgroundColor(Camera.main);
+            // Use the camera we just activated directly — Camera.main can return null in the
+            // first frame after a scene transition because the tag-based lookup hasn't
+            // observed the newly-activated GameObject yet.
+            var activeCam = (_playerCamera as CustomCameraController)?.Camera;
+            _themeManagerData.SetBackgroundColor(activeCam != null ? activeCam : Camera.main);
 
             var shipGO = _playerFollowTarget.gameObject;
             var shipCustomizer = shipGO.GetComponent<VesselCameraCustomizer>();
diff --git a/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs b/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs
index 85b105e07..9b58d817d 100644
--- a/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs
+++ b/Assets/_Scripts/Controller/Vessel/Audio/ShipAudioController.cs
@@ -275,6 +275,8 @@ namespace CosmicShore.Gameplay.Audio
         IVesselStatus _status;
         ResourceSystem _resourceSystem;
         StudioListener _listener;
+        bool _listenerSearched;
+        bool _listenerAttachFailed;
         EventInstance _instance;
         PARAMETER_ID _speedParamId;
         PARAMETER_ID _tiltParamId;
@@ -768,6 +770,10 @@ namespace CosmicShore.Gameplay.Audio
 
             AttachMode desired = ResolveDesiredAttachMode();
             if (desired == _attachMode) return;
+            // Once we've discovered there's no listener to attach to, stop retrying every
+            // frame — the project-wide FindFirstObjectByType in GetListenerTransform is the
+            // dominant audio cost when this loop runs unbounded.
+            if (desired == AttachMode.Listener && _listenerAttachFailed) return;
 
             // Detach from the previous target (harmless if not attached).
             RuntimeManager.DetachInstanceFromGameObject(_instance);
@@ -787,12 +793,13 @@ namespace CosmicShore.Gameplay.Audio
                     {
                         attachTarget = listenerTransform;
                         _attachMode = AttachMode.Listener;
+                        _listenerAttachFailed = false;
                     }
                     else
                     {
-                        // No listener found yet — fall back to ship attachment and try again next frame.
                         attachTarget = transform;
                         _attachMode = AttachMode.Ship;
+                        _listenerAttachFailed = true;
                     }
                     break;
 
@@ -831,14 +838,18 @@ namespace CosmicShore.Gameplay.Audio
         {
             if (_listener != null) return _listener.transform;
 
-            // StudioListener is the canonical FMOD listener. Fall back to Camera.main only
-            // if nothing else is found (rare; most FMOD projects place a StudioListener on
-            // the main camera).
+            // StudioListener is the canonical FMOD listener. The project-wide search is
+            // expensive, so do it at most once per controller lifetime; if nothing is
+            // found, _listenerAttachFailed will gate the caller off entirely.
+            if (!_listenerSearched)
+            {
 #if UNITY_2023_1_OR_NEWER
-            _listener = Object.FindFirstObjectByType<StudioListener>();
+                _listener = Object.FindFirstObjectByType<StudioListener>();
 #else
-            _listener = Object.FindObjectOfType<StudioListener>();
+                _listener = Object.FindObjectOfType<StudioListener>();
 #endif
+                _listenerSearched = true;
+            }
             if (_listener != null) return _listener.transform;
 
             var cam = Camera.main;
```

</details>
