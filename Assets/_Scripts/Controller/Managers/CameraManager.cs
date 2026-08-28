using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Obvious.Soap;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Gameplay
{
    public class CameraManager : Singleton<CameraManager>
    {
        [SerializeField]
        SceneNameListSO _sceneNameList;

        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;
        [SerializeField] private ScriptableEventNoParam _onReturnToMainMenu;
        [SerializeField] private ScriptableEventTransform _onInitializePlayerCamera;

        // TODO - Need to have a game over event, to activate the end camera
        // += SetEndCameraActive
        // [SerializeField] private ScriptableEventNoParam _onGameOver;

        private ICameraController _playerCamera;
        private ICameraController _deathCamera;
        private ICameraController _activeController;

        [SerializeField] private CustomCameraController endCamera;
        [SerializeField] private CinemachineCamera mainMenuCamera;
        [SerializeField] private Transform endCameraFollowTarget;
        [SerializeField] private Transform endCameraLookAtTarget;

        private Transform _playerFollowTarget;

        public Transform PlayerFollowTarget
        {
            get => _playerFollowTarget;
            set => _playerFollowTarget = value;
        }

        private Camera _vCam;
        private IVesselStatus vesselStatus;

        public override void Awake()
        {
            base.Awake();
            _playerCamera = GetOrFindCameraController("CM PlayerCam");
            _deathCamera = GetOrFindCameraController("CM DeathCam");
            endCamera = GetOrFindCameraController("CM EndCam") as CustomCameraController;
        }

        private void OnEnable()
        {
            _onReturnToMainMenu.OnRaised += OnEnteredMainMenu;
            _onInitializePlayerCamera.OnRaised += SetupGamePlayCameras;

            // Keep FOV + post-process AA on the managed cameras in sync with the settings panel,
            // live. The cameras are children of this manager (not spawned with the vessel), so this
            // is the spawn-proof place to apply - no per-camera scene reference needed.
            DisplayGraphicsSettings.OnFieldOfViewChanged += HandleCameraGraphicsChanged;
            DisplayGraphicsSettings.OnAnySettingChanged += HandleCameraGraphicsChanged;
        }

        void OnDisable()
        {
            _onReturnToMainMenu.OnRaised -= OnEnteredMainMenu;
            _onInitializePlayerCamera.OnRaised -= SetupGamePlayCameras;

            DisplayGraphicsSettings.OnFieldOfViewChanged -= HandleCameraGraphicsChanged;
            DisplayGraphicsSettings.OnAnySettingChanged -= HandleCameraGraphicsChanged;
        }

        void HandleCameraGraphicsChanged(float _) => ApplyCameraGraphicsSettings();
        void HandleCameraGraphicsChanged(GraphicsSettingsData _) => ApplyCameraGraphicsSettings();

        /// <summary>
        /// Pushes the saved Field-of-View and post-process AA (FXAA/SMAA/TAA) onto every managed
        /// camera. MSAA is global on the URP asset (handled by <see cref="DisplayGraphicsSettings"/>),
        /// so this only sets FOV + the per-camera post-AA mode. Null-safe and called on every camera
        /// setup, so a vessel that spawns later still gets the right look.
        /// </summary>
        public void ApplyCameraGraphicsSettings()
        {
            var s = DisplayGraphicsSettings.Instance;
            if (s == null) return;
            var d = s.Current;
            ApplyToCamera((_playerCamera as CustomCameraController)?.Camera, d);
            ApplyToCamera((_deathCamera as CustomCameraController)?.Camera, d);
            ApplyToCamera(endCamera != null ? endCamera.Camera : null, d);
        }

        static void ApplyToCamera(Camera cam, GraphicsSettingsData d)
        {
            if (cam == null) return;
            if (!cam.orthographic) cam.fieldOfView = d.FieldOfView;
            GraphicsSettingsApplier.ApplyCameraAntiAliasing(cam, d.AntiAliasing);
        }

        void Start()
        {
            _vCam = (_playerCamera as CustomCameraController)?.Camera;
            InitializeSceneCamera();
        }

        private void InitializeSceneCamera()
        {
            var activeScene = SceneManager.GetActiveScene().name;

            if (activeScene == _sceneNameList.MainMenuScene)
            {
                OnEnteredMainMenu();
            }
        }

        private ICameraController GetOrFindCameraController(string name)
        {
            Transform t = transform.Find(name);
            if (t)
            {
                var ctrl = t.GetComponent<ICameraController>();
                if (ctrl == null)
                {
                    ctrl = t.gameObject.AddComponent<CustomCameraController>();
                }
                return ctrl;
            }
            CSDebug.LogWarning($"[CameraManager] Could not find camera controller: {name}");
            return null;
        }

        public Transform GetCloseCamera() => (_playerCamera as CustomCameraController)?.transform;

        void OnEnteredMainMenu()
        {
            SetMainMenuCameraActive();
            _themeManagerData.SetBackgroundColor(Camera.main);
        }

        public void SetupGamePlayCameras(Transform followTarget)
        {
            if(!gameObject.activeInHierarchy) gameObject.SetActive(true);

            _playerFollowTarget = followTarget;
            _playerCamera?.SetFollowTarget(_playerFollowTarget);
            _deathCamera?.SetFollowTarget(_playerFollowTarget);

            SetCloseCameraActive();
            // Use the camera we just activated directly - Camera.main can return null in the
            // first frame after a scene transition because the tag-based lookup hasn't
            // observed the newly-activated GameObject yet.
            var activeCam = (_playerCamera as CustomCameraController)?.Camera;
            _themeManagerData.SetBackgroundColor(activeCam != null ? activeCam : Camera.main);

            var shipGO = _playerFollowTarget.gameObject;
            var shipCustomizer = shipGO.GetComponent<VesselCameraCustomizer>();
            if (shipCustomizer != null)
                shipCustomizer.Configure(_playerCamera);

            // Snap camera to correct initial position to prevent retaining
            // stale end-game or transition state from the previous session.
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();

            ApplyCameraGraphicsSettings();
        }

        /// <summary>
        /// Configures the end camera to follow the given target with the vessel's
        /// camera settings applied. Used by Menu_Main to follow the autopilot vessel.
        /// </summary>
        public void SetupEndCameraFollow(Transform followTarget)
        {
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

            endCamera.SetFollowTarget(followTarget);

            var customizer = followTarget.GetComponent<VesselCameraCustomizer>();
            customizer.Configure(endCamera);

            endCamera.SnapToTarget();
            SetEndCameraActive();
            ApplyCameraGraphicsSettings();
            _themeManagerData.SetBackgroundColor(Camera.main);
        }

        /// <summary>
        /// Activate the end camera as a MANUALLY-DRIVEN replay camera - the shared "replay
        /// camera" for modes that replay a moment (e.g. Astro League goal replays: a fixed
        /// broadcast vantage that PANS to the action rather than chasing it). The follow target
        /// is cleared so <see cref="CustomCameraController"/>'s own follow loop leaves the
        /// transform alone; the caller poses the returned rig transform every frame and calls
        /// <see cref="RestoreGameplayCamera"/> when done. Returns null when no end camera exists.
        /// </summary>
        public Transform BeginManualReplayCamera()
        {
            if (endCamera == null) return null;
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

            endCamera.SetFollowTarget(null);
            // The ONE sanctioned suppression of the prism occlusion corridor
            // (Docs/PRISM_ANIMATION.md §4.7): a manually-posed replay camera is a broadcast
            // vantage that is not looking at the local ship, so a camera→ship capsule would
            // cut a hole through unrelated mass. This is a narrow, symmetric hold — NOT an
            // opt-out: the binding on the vessel stays, and RestoreGameplayCamera lifts it.
            PrismOcclusionCorridor.SetSuppressed(true);
            // Same shape, same reason, for the speed-tunnel law (Docs/SPEED_TUNNEL.md): the
            // replay camera is posed by hand and AstroLeagueGoalReplay reads its field of view
            // to fit the shot, so a live FOV write would fight the pose AND silently mis-frame
            // the replay. A hold, not an opt-out — the vessel binding survives it.
            VesselSpeedTunnel.SetSuppressed(true);
            SetEndCameraActive();
            ApplyCameraGraphicsSettings();
            return endCamera.transform;
        }

        /// <summary>Vertical field of view of the replay/end camera (fit-the-shot framing math).</summary>
        public float ReplayCameraFieldOfView =>
            endCamera != null && endCamera.Camera != null ? endCamera.Camera.fieldOfView : 60f;

        /// <summary>
        /// Return from the replay/end camera to the gameplay follow camera (which still holds its
        /// vessel follow target), snapped so no stale replay framing bleeds into play.
        /// </summary>
        public void RestoreGameplayCamera()
        {
            // Lifting the holds is UNCONDITIONAL and comes FIRST. A replay can finish after its
            // scene has torn down — AstroLeagueGoalReplay.OnDestroy cancels playback, but the
            // pending untokened UniTask.Yield resumes a frame later and runs its finally — and by
            // then _playerFollowTarget is a destroyed Transform. Returning before the lifts would
            // latch BOTH platform laws off for the rest of the session: these statics are
            // otherwise only reset by their RuntimeInitializeOnLoadMethod installers, which run
            // once per app launch, not per scene load. Lifting with no vessel bound is a no-op
            // (both drivers gate on a live target), so there is nothing to protect here.
            PrismOcclusionCorridor.SetSuppressed(false);
            VesselSpeedTunnel.SetSuppressed(false);

            if (_playerFollowTarget == null) return;
            SetCloseCameraActive();
            SnapPlayerCameraToTarget();
        }

        public void SetMainMenuCameraActive()
        {
            // The menu view is the Menu_Main scene camera, driven directly by
            // MainMenuCameraController (a plain-transform rig - no Cinemachine). The legacy
            // "CM Main Menu" vCam is explicitly kept OFF so no CinemachineBrain anywhere has
            // a live camera to grab; this method's job is simply to clear every gameplay
            // camera off the top of the scene camera.
            if (mainMenuCamera != null)
                mainMenuCamera.gameObject.SetActive(false);

            if (_playerCamera is CustomCameraController pcc)
                pcc.Deactivate();
            if (_deathCamera is CustomCameraController dcc)
                dcc.Deactivate();
            if (endCamera != null)
                endCamera.Deactivate();

            _activeController = null;
        }

        public void SetCloseCameraActive() => SetActiveCamera(_playerCamera);

        public void SetDeathCameraActive() => SetActiveCamera(_deathCamera);

        public void SetEndCameraActive() => SetActiveCamera(endCamera);

        void SetActiveCamera(ICameraController controller)
        {
                if (_playerCamera != null) _playerCamera.Deactivate();
                if (_deathCamera != null) _deathCamera.Deactivate();
                if (endCamera != null) endCamera.Deactivate();

            controller?.Activate();
            _activeController = controller;
            if (mainMenuCamera != null)
                mainMenuCamera.gameObject.SetActive(false);
        }

        // ── Windowed player camera (mode preview) ────────────────────────────

        Camera _windowedCamera;
        Transform _windowedPreviousTarget;

        /// <summary>
        /// Point the ordinary gameplay camera at <paramref name="target"/> and render it into
        /// <paramref name="renderTexture"/> instead of the screen — the mode preview's "the game
        /// is playing in that window" view.
        ///
        /// <para>Deliberately NOT <see cref="SetupGamePlayCameras"/>: that routes through
        /// <c>SetActiveCamera</c>, which deactivates every other managed camera and claims
        /// <c>_activeController</c>. A windowed camera is an ADDITIONAL view, not the active one —
        /// the menu keeps whatever camera is drawing the screen, and nothing else's idea of "the
        /// active camera" changes. Using the real gameplay rig is what makes the window show the
        /// real game: the occlusion corridor and the speed tunnel are already bound to it.</para>
        ///
        /// <para>Returns null when there is no player camera to lend.</para>
        /// </summary>
        public Camera BeginWindowedPlayerCamera(Transform target, RenderTexture renderTexture)
        {
            if (_playerCamera == null || renderTexture == null) return null;
            if (!gameObject.activeInHierarchy) gameObject.SetActive(true);

            _windowedPreviousTarget = _playerFollowTarget;

            _playerFollowTarget = target;
            _playerCamera.SetFollowTarget(target);

            if (target && target.TryGetComponent(out VesselCameraCustomizer customizer))
                customizer.Configure(_playerCamera);

            _playerCamera.Activate();
            if (_playerCamera is CustomCameraController pcc)
            {
                pcc.SnapToTarget();
                _windowedCamera = pcc.Camera;
            }

            if (_windowedCamera)
            {
                _windowedCamera.targetTexture = renderTexture;
                _themeManagerData.SetBackgroundColor(_windowedCamera);
            }

            ApplyCameraGraphicsSettings();
            return _windowedCamera;
        }

        /// <summary>
        /// Give the gameplay camera back: clear the render texture, restore the follow target it
        /// had, and stand it down. Safe to call when no windowed camera is running.
        /// </summary>
        public void EndWindowedPlayerCamera()
        {
            if (_windowedCamera)
            {
                _windowedCamera.targetTexture = null;
                _windowedCamera = null;
            }

            _playerFollowTarget = _windowedPreviousTarget;
            _playerCamera?.SetFollowTarget(_windowedPreviousTarget);
            _windowedPreviousTarget = null;

            // Only stand it down if it is not the view the game is actually using: in a gameplay
            // scene this same rig IS the screen camera, and a preview must never be able to
            // switch it off there.
            if (_activeController != _playerCamera)
                _playerCamera?.Deactivate();
        }

        public ICameraController GetActiveController() => _activeController;

        /// <summary>
        /// Deactivates all managed cameras (player, death, end) without activating a replacement.
        /// Used by the menu to hand control to the scene camera driven by MainMenuCameraController.
        /// </summary>
        public void DeactivateAllCameras()
        {
            if (_playerCamera != null) _playerCamera.Deactivate();
            if (_deathCamera != null) _deathCamera.Deactivate();
            if (endCamera != null) endCamera.Deactivate();
            _activeController = null;
        }

        /// <summary>
        /// Snaps the player camera to its follow target's current position.
        /// Call after vessel teleport or end-game cinematic to reset the camera.
        /// </summary>
        public void SnapPlayerCameraToTarget()
        {
            if (_playerCamera is CustomCameraController pcc)
                pcc.SnapToTarget();
        }

        public void SetNormalizedCloseCameraDistance(float normalizedDistance)
        {
            if (_playerCamera == null) return;
            // float close = CloseCamDistance > 0 ? CloseCamDistance : 10f;
            // float far = FarCamDistance > 0 ? FarCamDistance : 40f;
            // float distance = Mathf.Lerp(close, far, normalizedDistance);
            // _playerCamera.SetCameraDistance(distance);
        }
    }
}
