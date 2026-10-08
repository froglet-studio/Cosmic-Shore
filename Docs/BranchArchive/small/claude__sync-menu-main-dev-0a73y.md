# Branch archive: `claude/sync-menu-main-dev-0a73y`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-03-01 by Claude
- **Unmerged commits:** 2
- **Forked from:** `671d74502` (2026-03-01, Merge pull request #308 from froglet-studio/claude/fix-camera-offset-eqVQu)
- **Tip:** `d18ae7850`
- **Files touched (24):**
  - `Assets/_Scenes/Bootstrap.unity`
  - `Assets/_Scenes/Menu_Main.unity`
  - `Assets/_Scripts/Controller/Camera/CinemachineMatchTargetOrientation.cs`
  - `Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs`
  - `Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs.meta`
  - `Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs`
  - `Assets/_Scripts/Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs`
  - `Assets/_Scripts/Data/Enums/MainMenuState.cs`
  - `Assets/_Scripts/Data/Enums/MainMenuState.cs.meta`
  - `Assets/_Scripts/Editor/CanvasGroupEditorWindow.cs`
  - `Assets/_Scripts/Editor/CanvasGroupEditorWindow.cs.meta`
  - `Assets/_Scripts/Editor/PlayModeSOProtector.cs`
  - `Assets/_Scripts/Editor/PlayModeSOProtector.cs.meta`
  - `Assets/_Scripts/System/MainMenuController.cs`
  - `Assets/_Scripts/System/MainMenuController.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/MainMenuStateTests.cs`
  - `Assets/_Scripts/Tests/EditMode/MainMenuStateTests.cs.meta`
  - `Assets/_Scripts/Tests/EditMode/MenuFreestyleToggleTests.cs`
  - `Assets/_Scripts/Tests/EditMode/MenuFreestyleToggleTests.cs.meta`
  - `Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md`
  - `Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md.meta`
  - `Assets/_Scripts/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/UI/Screens/InitialPanelStateApplier.cs`
  - `Assets/_Scripts/UI/Screens/InitialPanelStateApplier.cs.meta`

### `f54fad684` — fix(menu): sync Menu_Main scene and UI with development branch

_Claude, 2026-03-01 10:11:10 +0000_

```text
- Restore Menu_Main.unity scene from development branch
- Revert ScreenSwitcher.cs to match dev behavior (remove freestyle
  integration, restore direct HangarScreen/LeaderboardsMenu references)
- Remove editor tools: CanvasGroupEditorWindow, PlayModeSOProtector
- Remove runtime tools: InitialPanelStateApplier, MainMenuCameraController,
  CinemachineMatchTargetOrientation
- Remove MainMenuController and MainMenuState (not in dev)
- Remove related tests: MainMenuStateTests, MenuFreestyleToggleTests,
  UNIT_TESTING_GUIDE.md
- Clean up Bootstrap scene InitialPanelStateApplier reference
- Preserve center button: MenuCrystalClickHandler,
  MenuFreestyleEventsContainerSO, and SO assets remain intact
```

```text
 Assets/_Scenes/Bootstrap.unity                                        |   45 -
 Assets/_Scenes/Menu_Main.unity                                        | 8280 +++++++++++--------------------
 .../_Scripts/Controller/Camera/CinemachineMatchTargetOrientation.cs   |   48 -
 Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs         |  219 -
 Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs.meta    |    2 -
 Assets/_Scripts/Controller/Multiplayer/MenuCrystalClickHandler.cs     |    8 +-
 .../Controller/Multiplayer/MenuServerPlayerVesselInitializer.cs       |    7 +-
 Assets/_Scripts/Data/Enums/MainMenuState.cs                           |   32 -
 Assets/_Scripts/Data/Enums/MainMenuState.cs.meta                      |    2 -
 Assets/_Scripts/Editor/CanvasGroupEditorWindow.cs                     |  534 --
 Assets/_Scripts/Editor/CanvasGroupEditorWindow.cs.meta                |    2 -
 Assets/_Scripts/Editor/PlayModeSOProtector.cs                         |  116 -
 Assets/_Scripts/Editor/PlayModeSOProtector.cs.meta                    |    2 -
 Assets/_Scripts/System/MainMenuController.cs                          |  221 -
 Assets/_Scripts/System/MainMenuController.cs.meta                     |    2 -
 Assets/_Scripts/Tests/EditMode/MainMenuStateTests.cs                  |  171 -
 Assets/_Scripts/Tests/EditMode/MainMenuStateTests.cs.meta             |    2 -
 Assets/_Scripts/Tests/EditMode/MenuFreestyleToggleTests.cs            |  151 -
 Assets/_Scripts/Tests/EditMode/MenuFreestyleToggleTests.cs.meta       |    2 -
 Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md                           |  170 -
 Assets/_Scripts/Tests/UNIT_TESTING_GUIDE.md.meta                      |    7 -
 Assets/_Scripts/UI/ScreenSwitcher.cs                                  |  130 +-
 Assets/_Scripts/UI/Screens/InitialPanelStateApplier.cs                |   51 -
 Assets/_Scripts/UI/Screens/InitialPanelStateApplier.cs.meta           |    2 -
 24 files changed, 3051 insertions(+), 7155 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 2098 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/Camera/CinemachineMatchTargetOrientation.cs b/Assets/_Scripts/Controller/Camera/CinemachineMatchTargetOrientation.cs
deleted file mode 100644
index 030fad4c2..000000000
--- a/Assets/_Scripts/Controller/Camera/CinemachineMatchTargetOrientation.cs
+++ /dev/null
@@ -1,48 +0,0 @@
-using Unity.Cinemachine;
-using UnityEngine;
-
-namespace CosmicShore.Gameplay
-{
-    /// <summary>
-    /// Cinemachine extension that makes the camera face the same direction as the
-    /// follow target rather than aiming at it. Replaces CinemachineRotationComposer
-    /// for third-person cameras where the camera should share the vessel's orientation
-    /// (forward, up, roll) — critical for space games with full 6DOF movement.
-    ///
-    /// Operates at the Aim pipeline stage: CinemachineFollow handles position (Body),
-    /// then this extension overrides orientation to match the tracking target's rotation.
-    /// During CinemachineBrain blends, the Brain interpolates between vCam CameraStates
-    /// so transitions remain smooth.
-    /// </summary>
-    [AddComponentMenu("")] // Added programmatically by MainMenuCameraController
-    public class CinemachineMatchTargetOrientation : CinemachineExtension
-    {
-        [Tooltip("Rotation damping in seconds. 0 = snap to target orientation instantly.")]
-        public float Damping;
-
-        protected override void PostPipelineStageCallback(
-            CinemachineVirtualCameraBase vcam,
-            CinemachineCore.Stage stage,
-            ref CameraState state,
-            float deltaTime)
-        {
-            if (stage != CinemachineCore.Stage.Aim) return;
-
-            var target = vcam.Follow;
-            if (target == null) return;
-
-            var targetRot = target.rotation;
-
-            if (Damping > 0.001f && deltaTime >= 0f)
-            {
-                // Exponential decay matches CinemachineFollow's smoothing behavior
-                float t = 1f - Mathf.Exp(-deltaTime / Mathf.Max(Damping, 0.0001f));
-                state.RawOrientation = Quaternion.Slerp(state.RawOrientation, targetRot, t);
-            }
-            else
-            {
-                state.RawOrientation = targetRot;
-            }
-        }
-    }
-}
diff --git a/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs b/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs
deleted file mode 100644
index 424583e99..000000000
--- a/Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs
+++ /dev/null
@@ -1,219 +0,0 @@
-using CosmicShore.ScriptableObjects;
-using CosmicShore.Utility;
-using Obvious.Soap;
-using Reflex.Attributes;
-using Unity.Cinemachine;
-using UnityEngine;
-
-namespace CosmicShore.Gameplay
-{
-    /// <summary>
-    /// Manages cameras for the Menu_Main scene.
-    ///
-    /// Menu state: "CM Main Menu" Cinemachine vCam orbits the crystal at a
-    /// configurable radius/height/speed.
-    /// Freestyle state: <see cref="CameraManager.SetupGamePlayCameras"/> activates
-    /// the proven <see cref="CustomCameraController"/> ("CM PlayerCam") to follow
-    /// the vessel — the same pipeline used by all gameplay scenes.
-    ///
-    /// Listens to SOAP events independently from <see cref="Core.MainMenuController"/>:
-    ///   - <c>OnClientReady</c>        → activate menu camera
-    ///   - <c>OnEnterFreestyle</c>     → switch to gameplay camera (CustomCameraController)
-    ///   - <c>OnExitFreestyle</c>      → switch back to menu camera
-    ///   - <c>OnCrystalSpawned</c>     → configure menu orbit target
-    ///
-    /// Place on the same GameObject as MainMenuController (the Game object in Menu_Main).
-    /// </summary>
-    public class MainMenuCameraController : MonoBehaviour
-    {
-        [Header("Menu Camera Orbit")]
-        [SerializeField, Tooltip("Orbit radius from crystal center.")]
-        float _orbitRadius = 80f;
-
-        [SerializeField, Tooltip("Camera height offset above the crystal.")]
-        float _orbitHeight = 30f;
-
-        [SerializeField, Tooltip("Orbit speed in degrees per second.")]
-        float _orbitSpeed = 5f;
-
-        [Header("SOAP Events")]
-        [SerializeField, Tooltip("SOAP events for entering/exiting freestyle mode.")]
-        MenuFreestyleEventsContainerSO _freestyleEvents;
-
-        [SerializeField, Tooltip("Cell runtime data — provides crystal transform and spawn event.")]
-        CellRuntimeDataSO _cellData;
-
-        [Inject] GameDataSO _gameData;
-
-        // Cached menu vCam hierarchy (lives on CameraManager)
-        CinemachineCamera _menuVCam;
-        CinemachineFollow _menuFollow;
-        Transform _menuFollowTarget;
-        RotateAroundOrigin _followTargetRotator;
-        Transform _crystalTarget;
-
-        // ── Unity Lifecycle ─────────────────────────────────────────────
-
-        void Start()
-        {
-            CacheMenuVCam();
-            SubscribeEvents();
-        }
-
-        void OnDestroy()
-        {
-            UnsubscribeEvents();
-
-            // Re-enable RotateAroundOrigin in case CameraManager is reused across scenes
-            if (_followTargetRotator) _followTargetRotator.enabled = true;
-
-            if (_menuVCam)
-                _menuVCam.gameObject.SetActive(false);
-        }
-
-        void Update()
-        {
-            UpdateMenuOrbit();
-        }
-
-        // ── Event Wiring ────────────────────────────────────────────────
-
-        void SubscribeEvents()
-        {
-            if (_gameData?.OnClientReady != null)
-                _gameData.OnClientReady.OnRaised += HandleMenuReady;
-
-            _freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
-            _freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
-            _cellData.OnCrystalSpawned.OnRaised += HandleCrystalSpawned;
-        }
-
```

</details>

_Also contains 1 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
