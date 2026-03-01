using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using Reflex.Attributes;
using System.Threading;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Manages cameras for the Menu_Main scene with smooth transitions.
    ///
    /// Menu state: "CM Main Menu" Cinemachine vCam orbits the crystal at a
    /// configurable radius/height/speed.
    /// Freestyle state: <see cref="CameraManager.SetupGamePlayCameras"/> activates
    /// the proven <see cref="CustomCameraController"/> ("CM PlayerCam") to follow
    /// the vessel — the same pipeline used by all gameplay scenes.
    ///
    /// Menu → Freestyle: Captures the current Cinemachine camera pose, deactivates the
    /// menu vCam, and activates <see cref="CustomCameraController"/> at that pose in
    /// transition mode. <c>SmoothDamp</c> recalculates the vessel's follow position
    /// every frame, producing a jerk-free blend even when the vessel is moving.
    ///
    /// Freestyle → Menu: Uses a "CM Freestyle Bridge" CinemachineCamera as a static
    /// snapshot of the current vessel-follow position, then CinemachineBrain blends
    /// from the bridge to the menu orbit vCam.
    ///
    /// Listens to SOAP events independently from <see cref="Core.MainMenuController"/>:
    ///   - <c>OnClientReady</c>        → activate menu camera (immediate, no transition)
    ///   - <c>OnEnterFreestyle</c>     → SmoothDamp transition to vessel follow
    ///   - <c>OnExitFreestyle</c>      → Cinemachine blend from vessel position back to menu orbit
    ///   - <c>OnCrystalSpawned</c>     → configure menu orbit target
    ///
    /// Place on the same GameObject as MainMenuController (the Game object in Menu_Main).
    /// </summary>
    public class MainMenuCameraController : MonoBehaviour
    {
        [Header("Menu Camera Orbit")]
        [SerializeField, Tooltip("Orbit radius from crystal center.")]
        float _orbitRadius = 80f;

        [SerializeField, Tooltip("Camera height offset above the crystal.")]
        float _orbitHeight = 30f;

        [SerializeField, Tooltip("Orbit speed in degrees per second.")]
        float _orbitSpeed = 5f;

        [Header("Camera Transition")]
        [SerializeField, Tooltip("Duration to wait for the CinemachineBrain blend to complete. " +
                                 "Should match the DefaultBlend.Time on Game Scene Main Camera.")]
        float _transitionDuration = 2f;

        [Header("SOAP Events")]
        [SerializeField, Tooltip("SOAP events for entering/exiting freestyle mode.")]
        MenuFreestyleEventsContainerSO _freestyleEvents;

        [SerializeField, Tooltip("Cell runtime data — provides crystal transform and spawn event.")]
        CellRuntimeDataSO _cellData;

        [Inject] GameDataSO _gameData;

        // Cached menu vCam hierarchy (lives on CameraManager)
        CinemachineCamera _menuVCam;
        CinemachineFollow _menuFollow;
        Transform _menuFollowTarget;
        RotateAroundOrigin _followTargetRotator;
        Transform _crystalTarget;

        // Bridge vCam for smooth transitions (created at runtime on CameraManager)
        CinemachineCamera _bridgeVCam;
        CinemachineFollow _bridgeFollow;
        CinemachineMatchTargetOrientation _bridgeAim;

        // Cached player camera (CM PlayerCam)
        CustomCameraController _playerCameraController;

        const int HighPriority = 20;
        const int LowPriority = 0;

        bool _isInFreestyle;
        bool _isTransitioning;
        CancellationTokenSource _cts;

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            _cts = new CancellationTokenSource();
            CacheMenuVCam();
            CachePlayerCamera();
            EnsureBridgeVCam();
            SubscribeEvents();
        }

        void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();

            UnsubscribeEvents();

            // Re-enable RotateAroundOrigin in case CameraManager is reused across scenes
            if (_followTargetRotator) _followTargetRotator.enabled = true;

            if (_menuVCam)
                _menuVCam.gameObject.SetActive(false);

            if (_bridgeVCam)
                _bridgeVCam.gameObject.SetActive(false);
        }

        void Update()
        {
            UpdateMenuOrbit();
        }

        // ── Event Wiring ────────────────────────────────────────────────

        void SubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised += HandleMenuReady;

            _freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
            _cellData.OnCrystalSpawned.OnRaised += HandleCrystalSpawned;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            _freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;
            _cellData.OnCrystalSpawned.OnRaised -= HandleCrystalSpawned;
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady() => ActivateMenuCameraImmediate();
        void HandleEnterFreestyle() => TransitionToGameplayCameraAsync().Forget();
        void HandleExitFreestyle() => TransitionToMenuCameraAsync().Forget();
        void HandleCrystalSpawned() => SetMenuVCamTarget();

        // ── vCam Caching ────────────────────────────────────────────────

        void CacheMenuVCam()
        {
            if (!CameraManager.Instance) return;

            var cmTransform = CameraManager.Instance.transform.Find("CM Main Menu");
            if (!cmTransform) return;

            _menuVCam = cmTransform.GetComponent<CinemachineCamera>();
            _menuFollow = cmTransform.GetComponent<CinemachineFollow>();

            var followTransform = CameraManager.Instance.transform.Find("Main Menu Follow Target");
            if (followTransform)
            {
                _menuFollowTarget = followTransform;
                _followTargetRotator = followTransform.GetComponent<RotateAroundOrigin>();
            }
        }

        void CachePlayerCamera()
        {
            if (!CameraManager.Instance) return;

            var t = CameraManager.Instance.transform.Find("CM PlayerCam");
            if (t) _playerCameraController = t.GetComponent<CustomCameraController>();
        }

        /// <summary>
        /// Creates or finds the bridge CinemachineCamera used for the freestyle → menu
        /// transition. The bridge is positioned as a static snapshot at CM PlayerCam's
        /// current location so the CinemachineBrain can blend from there back to the orbit.
        /// Only active during that blend — not used for ongoing vessel following.
        /// </summary>
        void EnsureBridgeVCam()
        {
            if (_bridgeVCam) return;
            if (!CameraManager.Instance) return;

            var parent = CameraManager.Instance.transform;
            var existing = parent.Find("CM Freestyle Bridge");

            if (existing)
            {
                _bridgeVCam = existing.GetComponent<CinemachineCamera>();
                _bridgeFollow = existing.GetComponent<CinemachineFollow>();
                _bridgeAim = existing.GetComponent<CinemachineMatchTargetOrientation>();
                if (!_bridgeAim)
                    _bridgeAim = existing.gameObject.AddComponent<CinemachineMatchTargetOrientation>();
            }
            else
            {
                var go = new GameObject("CM Freestyle Bridge");
                go.transform.SetParent(parent, false);

                _bridgeVCam = go.AddComponent<CinemachineCamera>();
                _bridgeFollow = go.AddComponent<CinemachineFollow>();
                _bridgeAim = go.AddComponent<CinemachineMatchTargetOrientation>();

                var tracker = _bridgeFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTarget;
                _bridgeFollow.TrackerSettings = tracker;
            }

            SetVCamPriority(_bridgeVCam, LowPriority);
            _bridgeVCam.gameObject.SetActive(false);
        }

        // ── Menu Camera Orbit ───────────────────────────────────────────

        void SetMenuVCamTarget()
        {
            if (!_menuVCam) return;

            var crystalTransform = _cellData.CrystalTransform;
            if (!crystalTransform) return;

            _crystalTarget = crystalTransform;

            // Position follow target at orbit radius from crystal
            if (_menuFollowTarget)
            {
                _menuFollowTarget.position = crystalTransform.position + Vector3.back * _orbitRadius;

                // Disable default RotateAroundOrigin — it orbits world origin, not the crystal
                if (_followTargetRotator) _followTargetRotator.enabled = false;
            }

            // TrackingTarget = orbiting follow target (for camera positioning)
            // LookAtTarget = crystal (for camera aiming via CinemachineRotationComposer)
            var target = _menuVCam.Target;
            target.TrackingTarget = _menuFollowTarget ? _menuFollowTarget : crystalTransform;
            target.LookAtTarget = crystalTransform;
            target.CustomLookAtTarget = true;
            _menuVCam.Target = target;

            // CinemachineFollow offset provides height above the orbit path
            if (_menuFollow)
                _menuFollow.FollowOffset = new Vector3(0, _orbitHeight, 0);
        }

        void UpdateMenuOrbit()
        {
            if (!_crystalTarget || !_menuFollowTarget) return;

            var pivot = _crystalTarget.position;
            var offset = _menuFollowTarget.position - pivot;
            offset = Quaternion.Euler(0, _orbitSpeed * Time.deltaTime, 0) * offset;
            _menuFollowTarget.position = pivot + offset;
        }

        // ── Camera Switching ────────────────────────────────────────────

        /// <summary>
        /// Immediate menu camera activation with no transition blend.
        /// Used for initial menu setup when no previous camera state exists.
        /// Skipped if a blend transition is already in progress or if the player
        /// is currently in freestyle mode (e.g. OnClientReady firing after the
        /// player toggled freestyle — we must not snap back to the orbit camera).
        /// </summary>
        void ActivateMenuCameraImmediate()
        {
            if (_isTransitioning || _isInFreestyle) return;
            if (!CameraManager.Instance) return;

            CameraManager.Instance.SetMainMenuCameraActive();

            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
            }

            _isInFreestyle = false;
        }

        /// <summary>
        /// Smooth transition from menu orbit to vessel follow.
        ///
        /// Captures the current camera world pose (from CinemachineBrain rendering the
        /// menu orbit), deactivates all Cinemachine cameras, then activates
        /// <see cref="CustomCameraController"/> at that captured pose. The controller's
        /// <see cref="CustomCameraController.StartTransitionFromPose"/> puts it in
        /// transition mode where <c>SmoothDamp</c> is forced regardless of
        /// <c>_disableRotationLerp</c>, recalculating the vessel's follow position
        /// every frame so the camera converges smoothly even while the vessel moves.
        ///
        /// This avoids the CinemachineBrain bridge blend entirely for this direction,
        /// eliminating both the algorithm mismatch at handoff (CinemachineFollow vs
        /// CustomCameraController compute slightly different positions) and the velocity
        /// discontinuity when the blend curve ends.
        /// </summary>
        async UniTaskVoid TransitionToGameplayCameraAsync()
        {
            if (_isTransitioning || !CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            _isTransitioning = true;
            var ct = _cts.Token;
            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            // Capture current camera world pose (CinemachineBrain rendering the menu orbit)
            var mainCam = Camera.main;
            if (!mainCam) { FallbackActivateGameplayCamera(followTarget); return; }

            var startPos = mainCam.transform.position;
            var startRot = mainCam.transform.rotation;

            // Deactivate Cinemachine cameras — CustomCameraController takes over from here
            if (_menuVCam) _menuVCam.gameObject.SetActive(false);

            // Activate CustomCameraController at the captured pose with SmoothDamp transition.
            // Each frame, UpdateCamera recalculates the vessel's follow position and SmoothDamps
            // toward it — no jerk even when the vessel is moving.
            CameraManager.Instance.SetupGamePlayCamerasWithTransition(
                followTarget, startPos, startRot, _transitionDuration);

            // Wait for the transition to complete (blocks toggle spam via _isTransitioning)
            await UniTask.Delay(
                (int)(_transitionDuration * 1000),
                ignoreTimeScale: true,
                cancellationToken: ct);

            _isInFreestyle = true;
            _isTransitioning = false;
        }

        /// <summary>
        /// Smooth transition from vessel follow (X) to menu orbit (Y).
        ///
        /// 1. Positions the bridge vCam as a static snapshot at CustomCameraController's
        ///    current location.
        /// 2. Activates bridge at high priority — CinemachineBrain snaps Game Scene Main Camera
        ///    to the bridge position (hidden behind CM PlayerCam which still renders on top).
        /// 3. Deactivates CM PlayerCam — now only Game Scene Main Camera renders (at bridge pos).
        /// 4. Activates menu vCam at higher priority — CinemachineBrain blends from bridge
        ///    (vessel position) to menu orbit.
        /// 5. After blend completes, deactivates bridge.
        /// </summary>
        async UniTaskVoid TransitionToMenuCameraAsync()
        {
            if (_isTransitioning || !CameraManager.Instance) return;
            if (!_playerCameraController) { ActivateMenuCameraImmediate(); return; }

            _isTransitioning = true;
            var ct = _cts.Token;

            EnsureBridgeVCam();
            if (!_bridgeVCam) { ActivateMenuCameraImmediate(); _isTransitioning = false; return; }

            // 1. Position bridge at CM PlayerCam's current location (static snapshot)
            ConfigureBridgeAsSnapshot(
                _playerCameraController.transform.position,
                _playerCameraController.transform.rotation);

            // 2. Activate bridge — CinemachineBrain moves Game Scene Main Camera to bridge pos
            _bridgeVCam.gameObject.SetActive(true);
            SetVCamPriority(_bridgeVCam, HighPriority);

            // Wait one frame for CinemachineBrain to process the new vCam
            await UniTask.Yield(ct);

            // 3. Deactivate CM PlayerCam — Game Scene Main Camera is already at the same position
            //    (renders at depth -1, now unobstructed since CM PlayerCam at depth 0 is gone)
            CameraManager.Instance.DeactivateAllCameras();

            // 4. Activate menu vCam at higher priority → CinemachineBrain blends bridge → orbit
            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
            }
            SetVCamPriority(_menuVCam, HighPriority + 1);

            // 5. Wait for blend to complete
            //    Bridge must stay active as the "from" side of the CinemachineBrain blend.
            await UniTask.Delay(
                (int)(_transitionDuration * 1000),
                ignoreTimeScale: true,
                cancellationToken: ct);

            // 6. Clean up bridge and normalize menu priority
            _bridgeVCam.gameObject.SetActive(false);
            SetVCamPriority(_menuVCam, HighPriority);

            _isInFreestyle = false;
            _isTransitioning = false;
        }

        /// <summary>
        /// Fallback: immediate switch without blend. Used when bridge vCam setup fails.
        /// </summary>
        void FallbackActivateGameplayCamera(Transform followTarget)
        {
            if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            CameraManager.Instance.SetupGamePlayCameras(followTarget);
            _isInFreestyle = true;
            _isTransitioning = false;
        }

        // ── Bridge vCam Configuration ───────────────────────────────────

        /// <summary>
        /// Configures the bridge vCam to track the vessel with CinemachineFollow,
        /// matching CustomCameraController's follow offset from <see cref="CameraSettingsSO"/>.
        /// Zero damping ensures the bridge accurately represents where CustomCameraController
        /// would position the camera at any given moment.
        /// </summary>
        void ConfigureBridgeForVessel(Transform followTarget, VesselCameraCustomizer customizer)
        {
            // Enable tracking components
            if (_bridgeFollow) _bridgeFollow.enabled = true;
            if (_bridgeAim) _bridgeAim.enabled = true;

            // Set tracking target
            var target = _bridgeVCam.Target;
            target.TrackingTarget = followTarget;
            _bridgeVCam.Target = target;

            // Apply vessel camera settings (offset)
            if (_bridgeFollow && customizer?.Settings != null)
            {
                var settings = customizer.Settings;
                _bridgeFollow.FollowOffset = settings.mode == CameraMode.DynamicCamera
                    ? new Vector3(settings.followOffset.x, settings.followOffset.y, settings.dynamicMinDistance)
                    : settings.followOffset;

                // Zero damping — bridge should be at the exact computed position so
                // the handoff to CustomCameraController is seamless.
                var tracker = _bridgeFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTarget;
                tracker.PositionDamping = Vector3.zero;
                tracker.RotationDamping = Vector3.zero;
                _bridgeFollow.TrackerSettings = tracker;
            }

            // Zero aim damping — snap to target orientation
            if (_bridgeAim) _bridgeAim.Damping = 0f;
        }

        /// <summary>
        /// Configures the bridge vCam as a static snapshot at the given world-space pose.
        /// Disables CinemachineFollow so the bridge stays put during the menu orbit blend.
        /// </summary>
        void ConfigureBridgeAsSnapshot(Vector3 position, Quaternion rotation)
        {
            // Disable tracking — make it a static camera
            if (_bridgeFollow) _bridgeFollow.enabled = false;
            if (_bridgeAim) _bridgeAim.enabled = false;

            // Clear tracking target
            var target = _bridgeVCam.Target;
            target.TrackingTarget = null;
            _bridgeVCam.Target = target;

            // Position at the snapshot location
            _bridgeVCam.transform.SetPositionAndRotation(position, rotation);
        }

        static void SetVCamPriority(CinemachineCamera cam, int value)
        {
            if (!cam) return;
            var p = cam.Priority;
            p.Enabled = true;
            p.Value = value;
            cam.Priority = p;
        }
    }
}
