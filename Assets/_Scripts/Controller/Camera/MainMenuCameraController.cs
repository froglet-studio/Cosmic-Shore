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
    /// Manages cameras for the Menu_Main scene with smooth Cinemachine-blended transitions.
    ///
    /// Two camera endpoints:
    ///   A = "CM Main Menu" CinemachineCamera — orbits the crystal
    ///   B = "CM Freestyle Bridge" CinemachineCamera — tracks the vessel via CinemachineFollow
    ///       (same offset/damping as <see cref="CustomCameraController"/>)
    ///
    /// The CinemachineBrain on Game Scene Main Camera blends between A and B.
    /// Both vCams are evaluated every frame during the blend, so A orbits and B tracks
    /// the vessel continuously — the blend path stays natural even when the vessel moves.
    ///
    /// After the enter-freestyle blend completes (A→B), Bridge and PlayerCam are at the
    /// same position (same offset, zero damping), so the handoff is seamless.
    ///
    /// Freestyle state: <see cref="CameraManager.SetupGamePlayCameras"/> activates
    /// the proven <see cref="CustomCameraController"/> ("CM PlayerCam") to follow
    /// the vessel — the same pipeline used by all gameplay scenes.
    ///
    /// Listens to SOAP events independently from <see cref="Core.MainMenuController"/>:
    ///   - <c>OnClientReady</c>        → activate menu camera (immediate, no transition)
    ///   - <c>OnGameStateTransitionStart</c> → blend A→B, then hand off to CustomCameraController
    ///   - <c>OnMenuStateTransitionStart</c> → match Bridge to PlayerCam, blend B→A
    ///   - <c>OnCrystalSpawned</c>     → configure menu orbit target
    ///
    /// Place on the same GameObject as MainMenuController (the Game object in Menu_Main).
    /// Blend duration/curve is controlled by the CinemachineBrain's DefaultBlend setting on
    /// Game Scene Main Camera. Transitions poll <c>IsBlending</c> rather than using a fixed timer.
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

        [Inject] MenuFreestyleEventsContainerSO _freestyleEvents;

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

        // Cached CinemachineBrain on the scene camera — used to force IgnoreTimeScale
        CinemachineBrain _brain;

        const int HighPriority = 20;
        const int LowPriority = 0;

        bool _isInFreestyle;
        CancellationTokenSource _cts;
        CancellationTokenSource _transitionCts;

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            _cts = new CancellationTokenSource();
            CacheMenuVCam();
            CachePlayerCamera();
            CacheBrain();
            EnsureBridgeVCam();
            SubscribeEvents();
        }

        void OnDestroy()
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();

            UnsubscribeEvents();

            // Restore Brain's IgnoreTimeScale so gameplay scenes use scaled time for blends
            if (_brain) _brain.IgnoreTimeScale = false;

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

            _freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleEnterFreestyle;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleExitFreestyle;
            _cellData.OnCrystalSpawned.OnRaised += HandleCrystalSpawned;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            _freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleExitFreestyle;
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

        void CacheBrain()
        {
            var mainCam = Camera.main;
            if (!mainCam) return;

            _brain = mainCam.GetComponent<CinemachineBrain>();
            if (_brain)
                _brain.IgnoreTimeScale = true;
        }

        /// <summary>
        /// Creates or finds the bridge CinemachineCamera used for smooth priority-based
        /// blending during transitions. The bridge tracks the vessel via CinemachineFollow
        /// with zero damping — it is only active during blend transitions, not for ongoing
        /// vessel following (CustomCameraController handles that).
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
            offset = Quaternion.Euler(0, _orbitSpeed * Time.unscaledDeltaTime, 0) * offset;
            _menuFollowTarget.position = pivot + offset;
        }

        // ── Camera Switching ────────────────────────────────────────────

        /// <summary>
        /// Immediate menu camera activation with no transition blend.
        /// Used for initial menu setup when no previous camera state exists.
        /// Skipped if a blend transition is already in progress (e.g. OnClientReady
        /// firing while the player is toggling freestyle).
        /// </summary>
        void ActivateMenuCameraImmediate()
        {
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
        /// Cancels any in-progress camera transition and returns a linked token
        /// that respects both the new transition CTS and the component lifetime CTS.
        /// This allows a new transition to preempt a running one (e.g. the user
        /// toggles exit-freestyle while the enter-freestyle blend is still running).
        /// </summary>
        CancellationToken BeginTransition()
        {
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _transitionCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            return _transitionCts.Token;
        }

        /// <summary>
        /// Smooth transition from menu orbit (A) to vessel follow (B).
        ///
        /// 1. Bridge configured to track vessel (CinemachineFollow, zero damping, same offset
        ///    as <see cref="CustomCameraController"/>). Both A and B are evaluated every frame.
        /// 2. Bridge priority > menu → Brain blends A→B.
        /// 3. After blend, Bridge is at the exact vessel follow position. Hand off to
        ///    CustomCameraController — SnapToTarget computes the same position (same offset),
        ///    so the swap is seamless with no forced position override.
        /// </summary>
        async UniTaskVoid TransitionToGameplayCameraAsync()
        {
            if (!CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var ct = BeginTransition();
            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            EnsureBridgeVCam();
            if (!_bridgeVCam) { FallbackActivateGameplayCamera(followTarget); return; }

            // 1. Configure bridge to track vessel with matching camera offset
            ConfigureBridgeForVessel(followTarget, player.Vessel.VesselStatus.VesselCameraCustomizer);
            _bridgeVCam.PreviousStateIsValid = false;

            // 2. Activate bridge at higher priority → Brain blends menu orbit (A) → bridge (B)
            //    Both vCams evaluated every frame — bridge tracks moving vessel throughout.
            _bridgeVCam.gameObject.SetActive(true);
            SetVCamPriority(_bridgeVCam, HighPriority + 1);
            SetVCamPriority(_menuVCam, HighPriority);

            // 3. Wait for Brain blend to actually complete.
            //    Yield one frame first — the Brain hasn't evaluated the priority
            //    change yet, so IsBlending is false on this frame.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            while (_brain && _brain.IsBlending)
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

            // 4. Hand off to CustomCameraController
            //    Bridge and PlayerCam both compute the same position and LookAt rotation,
            //    so the swap is seamless.
            _bridgeVCam.gameObject.SetActive(false);
            if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            CameraManager.Instance.SetupGamePlayCameras(followTarget);

            _isInFreestyle = true;
        }

        /// <summary>
        /// Smooth transition from vessel follow (B) to menu orbit (A).
        ///
        /// 1. Bridge configured to track vessel (same offset as PlayerCam) → it naturally
        ///    matches PlayerCam's pose without any ForceCameraPosition.
        /// 2. Bridge activates at high priority. The Brain's state is stale (no vCams were
        ///    active during freestyle), so we temporarily set DefaultBlend to CUT — the Brain
        ///    snaps to the bridge (= vessel follow pose) instead of blending from stale state.
        /// 3. PlayerCam deactivated — Brain scene camera is at the same pose, no visible change.
        /// 4. Menu vCam activated at higher priority → Brain blends B→A. Bridge keeps tracking
        ///    the vessel every frame, so the "from" side of the blend stays live.
        /// 5. After blend, bridge deactivated.
        /// </summary>
        async UniTaskVoid TransitionToMenuCameraAsync()
        {
            if (!CameraManager.Instance) return;
            if (!_playerCameraController) { ActivateMenuCameraImmediate(); return; }

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) { ActivateMenuCameraImmediate(); return; }

            var ct = BeginTransition();

            EnsureBridgeVCam();
            if (!_bridgeVCam) { ActivateMenuCameraImmediate(); return; }

            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            // 1. Configure bridge to track the vessel — it computes the same position as
            //    PlayerCam (same offset, zero damping), matching its pose naturally.
            ConfigureBridgeForVessel(followTarget, player.Vessel.VesselStatus.VesselCameraCustomizer);
            _bridgeVCam.PreviousStateIsValid = false;

            // 2. Temporarily set Brain to CUT so it snaps to the bridge instead of blending
            //    from stale state (no Cinemachine vCams were active during freestyle).
            CinemachineBlendDefinition savedBlend = default;
            if (_brain)
            {
                savedBlend = _brain.DefaultBlend;
                _brain.DefaultBlend = new CinemachineBlendDefinition(
                    CinemachineBlendDefinition.Styles.Cut, 0f);
            }

            // 3. Activate bridge — Brain CUTs scene camera to bridge (= vessel follow pose).
            //    CM PlayerCam still renders on top (depth 0), so no visible change yet.
            _bridgeVCam.gameObject.SetActive(true);
            SetVCamPriority(_bridgeVCam, HighPriority);

            // Let the Brain evaluate with CUT blend.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

            // 4. Restore blend setting for the B→A transition.
            if (_brain)
                _brain.DefaultBlend = savedBlend;

            // 5. Deactivate PlayerCam — Brain scene camera is at bridge pose (same as
            //    PlayerCam was), so the swap is invisible.
            CameraManager.Instance.DeactivateAllCameras();

            // 6. Activate menu vCam at higher priority → Brain blends bridge (B) → orbit (A).
            //    Bridge keeps tracking vessel every frame — live "from" side.
            if (_menuVCam)
            {
                SetMenuVCamTarget();
                _menuVCam.gameObject.SetActive(true);
            }
            SetVCamPriority(_menuVCam, HighPriority + 1);

            // 7. Wait for Brain blend to actually complete.
            //    Yield one frame first so Brain detects the priority change.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            while (_brain && _brain.IsBlending)
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

            // 8. Clean up bridge and normalize menu priority.
            _bridgeVCam.gameObject.SetActive(false);
            SetVCamPriority(_menuVCam, HighPriority);

            _isInFreestyle = false;
        }

        /// <summary>
        /// Fallback: immediate switch without blend. Used when bridge vCam setup fails.
        /// </summary>
        void FallbackActivateGameplayCamera(Transform followTarget)
        {
            if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            CameraManager.Instance.SetupGamePlayCameras(followTarget);
            _isInFreestyle = true;
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
