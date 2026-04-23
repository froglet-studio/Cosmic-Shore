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
    /// Which camera behavior to use while the menu is in autopilot state.
    /// Switchable at runtime via the inspector — use this to compare feels.
    /// </summary>
    public enum MenuCameraMode
    {
        /// <summary>"CM Main Menu" vCam orbits the crystal. Transition travels
        /// a long spatial distance to reach the vessel — cinematic but jarring.</summary>
        CrystalOrbit = 0,

        /// <summary>"CM Menu Vessel Follow" vCam (created at runtime) trails the
        /// vessel with a cinematic offset. Transition is a small offset tightening
        /// — near-instant handoff with minimal camera motion.</summary>
        VesselFollow = 1,

        /// <summary>Tight snap-behind camera — zero damping, tight offset.
        /// Multiplayer-friendly: responds instantly to the vessel regardless of
        /// its speed, so you don't get the "camera lags then catches up" stutter.</summary>
        VesselChaseTight = 2,

        /// <summary>Elevated pan camera — sits high above the vessel and looks down
        /// at it with damped trailing. The "further top-down" framing reads almost
        /// like a map view and is very forgiving of fast vessel motion because most
        /// of the motion vector projects onto a short camera-space direction.</summary>
        VesselTopDownPan = 3,
    }

    /// <summary>
    /// Manages cameras for the Menu_Main scene with smooth Cinemachine-blended transitions.
    ///
    /// Two selectable menu camera modes (see <see cref="MenuCameraMode"/>):
    ///   • CrystalOrbit — "CM Main Menu" orbits the crystal.
    ///   • VesselFollow — "CM Menu Vessel Follow" trails the vessel cinematically.
    ///
    /// Transition endpoints:
    ///   A = the active menu vCam (depends on mode)
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
        [Header("Camera Mode")]
        [SerializeField, Tooltip("Which camera behaviour to use while in menu/autopilot state. " +
                                 "Can be switched at runtime — the active vCam updates immediately.")]
        MenuCameraMode _mode = MenuCameraMode.CrystalOrbit;

        [Header("Transition Tuning")]
        [SerializeField, Range(0.1f, 5f),
         Tooltip("How long the menu→freestyle blend lasts in CrystalOrbit mode. " +
                 "The crystal-to-vessel spatial distance is large, so this wants ~2s.")]
        float _crystalOrbitTransitionDuration = 2f;

        [SerializeField, Range(0.1f, 5f),
         Tooltip("How long the menu→freestyle blend lasts in any vessel-follow mode. " +
                 "The camera is already near the vessel, so 0.4–0.6s reads tighter than a long blend.")]
        float _vesselFollowTransitionDuration = 0.5f;

        [SerializeField, Tooltip("While transitioning in a vessel-follow mode, temporarily override " +
                                 "CinemachineBrain.DefaultBlend with a snappy Cut/EaseInOut to match " +
                                 "the shorter transition duration. Restored after the blend completes.")]
        bool _overrideBrainBlendForVesselModes = true;

        [SerializeField, Range(0f, 10f),
         Tooltip("Subtle FOV punch-in applied to the bridge vCam during the blend. Narrows the lens " +
                 "by this many degrees as the camera locks onto the vessel, then restores — a free " +
                 "'lock on' cue. Set to 0 to disable.")]
        float _fovPunchDegrees = 3f;

        [Header("Menu Camera Orbit (CrystalOrbit mode)")]
        [SerializeField, Tooltip("Orbit radius from crystal center.")]
        float _orbitRadius = 80f;

        [SerializeField, Tooltip("Camera height offset above the crystal.")]
        float _orbitHeight = 30f;

        [SerializeField, Tooltip("Orbit speed in degrees per second.")]
        float _orbitSpeed = 5f;

        [Header("Menu Vessel Follow (VesselFollow mode)")]
        [SerializeField, Tooltip("Cinematic offset from the vessel follow target while in menu state. " +
                                 "Typically pulled farther back and slightly higher than the gameplay offset " +
                                 "so entering freestyle produces a gentle tighten-in rather than a big move.")]
        Vector3 _vesselFollowOffset = new(0f, 14f, -28f);

        [SerializeField, Range(0f, 5f),
         Tooltip("Position damping for the menu vessel-follow vCam (seconds of lag). " +
                 "Lower = camera sticks closer to a fast vessel (less stutter). Higher = smoother trail.")]
        float _vesselFollowPositionDamping = 0.4f;

        [SerializeField, Range(0f, 5f),
         Tooltip("Rotation damping for the menu vessel-follow vCam. Low values reduce choppiness when " +
                 "the vessel banks or yaws sharply under AI control.")]
        float _vesselFollowRotationDamping = 0.3f;

        [SerializeField, Tooltip("Binding mode for the vessel-follow vCam. LazyFollow is the default " +
                                 "because it keeps world-up (camera doesn't roll with the vessel) and " +
                                 "trails behind in screen-space — smooth for fast AI pilots. " +
                                 "LockToTargetWithWorldUp yaws with the vessel; LockToTarget copies " +
                                 "full orientation (can feel choppy under aggressive AI).")]
        BindingMode _vesselFollowBindingMode = BindingMode.LazyFollow;

        [Header("Vessel Chase Tight (VesselChaseTight mode)")]
        [SerializeField, Tooltip("Zero-damping chase offset. Tight, responsive, good for multiplayer " +
                                 "where you don't want the camera to lag behind a fast vessel.")]
        Vector3 _vesselChaseTightOffset = new(0f, 6f, -14f);

        [Header("Vessel Top-Down Pan (VesselTopDownPan mode)")]
        [SerializeField, Tooltip("Height above the vessel for the top-down pan camera. Higher = more " +
                                 "map-like framing.")]
        float _topDownHeight = 70f;

        [SerializeField, Tooltip("Horizontal back-offset from the vessel. Zero = pure straight-down. " +
                                 "A small negative Z gives a slight 3/4 tilt so you can read vessel " +
                                 "facing at a glance.")]
        float _topDownBackOffset = -12f;

        [SerializeField, Range(0f, 5f),
         Tooltip("Position damping for the top-down pan. Moderate damping (0.8–1.5) gives a smooth " +
                 "cinematic pan rather than a rigid stick-to-target feel.")]
        float _topDownPositionDamping = 1.0f;

        [SerializeField, Range(0f, 5f),
         Tooltip("Rotation damping for the top-down pan. The camera looks at the vessel with this " +
                 "smoothing — higher values hide sharp AI maneuvers.")]
        float _topDownRotationDamping = 0.6f;

        [Header("Randomized Mode Switching")]
        [SerializeField, Tooltip("If enabled, the mode rotates through RandomSwitchModes while in menu " +
                                 "state (skipped during freestyle). Switches cross-blend via Cinemachine " +
                                 "so the change isn't jarring.")]
        bool _randomSwitchEnabled = false;

        [SerializeField, Tooltip("Pool of modes to pick from when auto-switching. Empty = no switching.")]
        MenuCameraMode[] _randomSwitchModes = {
            MenuCameraMode.CrystalOrbit,
            MenuCameraMode.VesselFollow,
        };

        [SerializeField, Range(1f, 120f),
         Tooltip("Minimum seconds between automatic mode switches.")]
        float _randomSwitchIntervalMin = 20f;

        [SerializeField, Range(1f, 120f),
         Tooltip("Maximum seconds between automatic mode switches.")]
        float _randomSwitchIntervalMax = 45f;

        [Inject] MenuFreestyleEventsContainerSO _freestyleEvents;

        [SerializeField, Tooltip("Cell runtime data — provides crystal transform and spawn event.")]
        CellRuntimeDataSO _cellData;

        [Inject] GameDataSO _gameData;

        /// <summary>Active menu camera behaviour. Setting this at runtime re-activates
        /// the correct vCam if the menu is currently visible.</summary>
        public MenuCameraMode Mode
        {
            get => _mode;
            set
            {
                if (_mode == value) return;
                _mode = value;
                ApplyModeChange();
            }
        }

        /// <summary>How long the menu↔freestyle blend should last for the active mode.
        /// Read by <see cref="MenuCrystalClickHandler"/> so both sides agree on pacing.</summary>
        public float ActiveTransitionDuration =>
            _mode == MenuCameraMode.CrystalOrbit
                ? _crystalOrbitTransitionDuration
                : _vesselFollowTransitionDuration;

        /// <summary>True for any mode whose menu vCam is already vessel-relative —
        /// the blend is a small tighten rather than a cross-scene dolly.</summary>
        bool IsVesselMode =>
            _mode == MenuCameraMode.VesselFollow ||
            _mode == MenuCameraMode.VesselChaseTight ||
            _mode == MenuCameraMode.VesselTopDownPan;

        // Cached menu vCam hierarchy (lives on CameraManager)
        CinemachineCamera _menuVCam;
        CinemachineFollow _menuFollow;
        Transform _menuFollowTarget;
        RotateAroundOrigin _followTargetRotator;
        Transform _crystalTarget;

        // Vessel-follow menu vCam (created at runtime on CameraManager). Reused across
        // all vessel modes (VesselFollow, VesselChaseTight, VesselTopDownPan) by reconfiguring
        // its offset, damping, binding mode, and LookAt per-mode.
        CinemachineCamera _menuVesselFollowVCam;
        CinemachineFollow _menuVesselFollowFollow;
        CinemachineMatchTargetOrientation _menuVesselFollowAim;

        // Bridge vCam for smooth transitions (created at runtime on CameraManager)
        CinemachineCamera _bridgeVCam;
        CinemachineFollow _bridgeFollow;
        CinemachineMatchTargetOrientation _bridgeAim;

        // State saved at transition start so we can restore after the blend.
        CinemachineBlendDefinition _savedBrainBlend;
        bool _brainBlendSaved;
        float _bridgeSavedFov;
        bool _bridgeFovSaved;

        // Random switch loop — owned by _cts so it dies with the component.
        CancellationTokenSource _randomSwitchCts;

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
            EnsureMenuVesselFollowVCam();
            SubscribeEvents();
            StartRandomSwitchLoopIfEnabled();
        }

        void OnDestroy()
        {
            _randomSwitchCts?.Cancel();
            _randomSwitchCts?.Dispose();
            _transitionCts?.Cancel();
            _transitionCts?.Dispose();
            _cts?.Cancel();
            _cts?.Dispose();

            UnsubscribeEvents();

            // Restore Brain state — IgnoreTimeScale + any saved DefaultBlend override.
            if (_brain)
            {
                if (_brainBlendSaved) _brain.DefaultBlend = _savedBrainBlend;
                _brain.IgnoreTimeScale = false;
            }
            _brainBlendSaved = false;

            // Re-enable RotateAroundOrigin in case CameraManager is reused across scenes
            if (_followTargetRotator) _followTargetRotator.enabled = true;

            if (_menuVCam)
                _menuVCam.gameObject.SetActive(false);

            if (_menuVesselFollowVCam)
                _menuVesselFollowVCam.gameObject.SetActive(false);

            if (_bridgeVCam)
                _bridgeVCam.gameObject.SetActive(false);
        }

        void Update()
        {
            // Orbit only matters in CrystalOrbit mode.
            if (_mode == MenuCameraMode.CrystalOrbit)
                UpdateMenuOrbit();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Keep min ≤ max for the random-switch interval.
            if (_randomSwitchIntervalMax < _randomSwitchIntervalMin)
                _randomSwitchIntervalMax = _randomSwitchIntervalMin;

            // When values change in the inspector during play mode, apply immediately
            // so we can A/B test feels without re-entering play.
            if (!Application.isPlaying) return;
            ApplyModeChange();
            ApplyMenuVesselFollowConfig();

            // Restart or cancel the random-switch loop so the toggle takes effect live.
            if (_randomSwitchEnabled) StartRandomSwitchLoopIfEnabled();
            else { _randomSwitchCts?.Cancel(); _randomSwitchCts?.Dispose(); _randomSwitchCts = null; }
        }
#endif

        /// <summary>The menu-side vCam for the current mode. All three vessel modes reuse
        /// <see cref="_menuVesselFollowVCam"/> — <see cref="ApplyMenuVesselFollowConfig"/>
        /// reconfigures it per-mode.</summary>
        CinemachineCamera ActiveMenuVCam =>
            IsVesselMode ? _menuVesselFollowVCam : _menuVCam;

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

        /// <summary>
        /// Creates or finds the vessel-follow menu CinemachineCamera used by
        /// <see cref="MenuCameraMode.VesselFollow"/>. Unlike the bridge (zero damping,
        /// tight gameplay offset), this vCam trails the vessel cinematically —
        /// pulled-back offset with moderate damping.
        /// </summary>
        void EnsureMenuVesselFollowVCam()
        {
            if (_menuVesselFollowVCam) return;
            if (!CameraManager.Instance) return;

            var parent = CameraManager.Instance.transform;
            var existing = parent.Find("CM Menu Vessel Follow");

            if (existing)
            {
                _menuVesselFollowVCam = existing.GetComponent<CinemachineCamera>();
                _menuVesselFollowFollow = existing.GetComponent<CinemachineFollow>();
                _menuVesselFollowAim = existing.GetComponent<CinemachineMatchTargetOrientation>();
                if (!_menuVesselFollowAim)
                    _menuVesselFollowAim = existing.gameObject.AddComponent<CinemachineMatchTargetOrientation>();
            }
            else
            {
                var go = new GameObject("CM Menu Vessel Follow");
                go.transform.SetParent(parent, false);

                _menuVesselFollowVCam = go.AddComponent<CinemachineCamera>();
                _menuVesselFollowFollow = go.AddComponent<CinemachineFollow>();
                _menuVesselFollowAim = go.AddComponent<CinemachineMatchTargetOrientation>();

                var tracker = _menuVesselFollowFollow.TrackerSettings;
                tracker.BindingMode = BindingMode.LockToTarget;
                _menuVesselFollowFollow.TrackerSettings = tracker;
            }

            ApplyMenuVesselFollowConfig();
            SetVCamPriority(_menuVesselFollowVCam, LowPriority);
            _menuVesselFollowVCam.gameObject.SetActive(false);
        }

        /// <summary>
        /// Applies the serialized cinematic offset/damping to the vessel-follow menu vCam,
        /// choosing per-mode values. Called on creation, when the mode changes, and when
        /// inspector values change during play mode.
        /// </summary>
        void ApplyMenuVesselFollowConfig()
        {
            if (!_menuVesselFollowFollow) return;

            Vector3 offset;
            Vector3 posDamp;
            Vector3 rotDamp;
            BindingMode binding;

            switch (_mode)
            {
                case MenuCameraMode.VesselChaseTight:
                    offset = _vesselChaseTightOffset;
                    posDamp = Vector3.zero;
                    rotDamp = Vector3.zero;
                    binding = BindingMode.LazyFollow;
                    break;

                case MenuCameraMode.VesselTopDownPan:
                    // Camera is parked high above the vessel with a small back-offset so the
                    // vessel's facing direction reads at a glance. WorldSpace binding keeps the
                    // offset a stable world vector (no roll/yaw inheritance from the vessel),
                    // and LookAt (wired in ConfigureMenuVesselFollowTarget) points the camera at
                    // the vessel. Moderate damping gives the slow "map-pan" feel.
                    offset = new Vector3(0f, _topDownHeight, _topDownBackOffset);
                    posDamp = Vector3.one * _topDownPositionDamping;
                    rotDamp = Vector3.one * _topDownRotationDamping;
                    binding = BindingMode.WorldSpace;
                    break;

                default: // VesselFollow
                    offset = _vesselFollowOffset;
                    posDamp = Vector3.one * _vesselFollowPositionDamping;
                    rotDamp = Vector3.one * _vesselFollowRotationDamping;
                    binding = _vesselFollowBindingMode;
                    break;
            }

            _menuVesselFollowFollow.FollowOffset = offset;
            var tracker = _menuVesselFollowFollow.TrackerSettings;
            tracker.BindingMode = binding;
            tracker.PositionDamping = posDamp;
            tracker.RotationDamping = rotDamp;
            _menuVesselFollowFollow.TrackerSettings = tracker;

            if (_menuVesselFollowAim)
                _menuVesselFollowAim.Damping = rotDamp.x;
        }

        /// <summary>
        /// Configures the vessel-follow menu vCam for the current vessel-based mode.
        /// VesselFollow / VesselChaseTight: tracks the vessel follow target (no LookAt — the
        ///   follow offset defines both position and orientation via the binding mode).
        /// VesselTopDownPan: tracks the vessel via a high world-space offset, and LookAt
        ///   aims the camera down at the vessel.
        /// Safe to call repeatedly (e.g. after vessel swap).
        /// </summary>
        void ConfigureMenuVesselFollowTarget()
        {
            if (!_menuVesselFollowVCam) return;

            var player = _gameData?.LocalPlayer;
            var followTarget = player?.Vessel?.VesselStatus?.CameraFollowTarget;
            if (!followTarget) return;

            var target = _menuVesselFollowVCam.Target;
            target.TrackingTarget = followTarget;

            if (_mode == MenuCameraMode.VesselTopDownPan)
            {
                // Top-down mode needs an explicit LookAt so the camera aims down at the vessel
                // instead of keeping the initial world-forward orientation.
                target.LookAtTarget = followTarget;
                target.CustomLookAtTarget = true;
            }
            else
            {
                target.LookAtTarget = null;
                target.CustomLookAtTarget = false;
            }

            _menuVesselFollowVCam.Target = target;
            ApplyMenuVesselFollowConfig();
        }

        /// <summary>
        /// Called when <see cref="Mode"/> changes at runtime. Swaps which menu vCam
        /// is active if we're currently in menu state.
        /// </summary>
        void ApplyModeChange()
        {
            // If we're in freestyle, nothing to do — PlayerCam is driving.
            // The new mode will take effect on the next exit-freestyle blend.
            if (_isInFreestyle) return;

            EnsureMenuVesselFollowVCam();
            ActivateMenuCameraImmediate();
        }

        // ── Brain Blend Override + FOV Punch (transition polish) ────────

        /// <summary>
        /// If <see cref="_overrideBrainBlendForVesselModes"/> is on and we're in a vessel mode,
        /// temporarily shorten the Brain's DefaultBlend to match <see cref="_vesselFollowTransitionDuration"/>.
        /// Saved state is restored by <see cref="RestoreBrainBlend"/>.
        /// </summary>
        void MaybeOverrideBrainBlend()
        {
            if (!_brain) return;
            if (!_overrideBrainBlendForVesselModes || !IsVesselMode) return;
            if (_brainBlendSaved) return;

            _savedBrainBlend = _brain.DefaultBlend;
            _brainBlendSaved = true;
            _brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.EaseInOut,
                _vesselFollowTransitionDuration);
        }

        void RestoreBrainBlend()
        {
            if (!_brain || !_brainBlendSaved) return;
            _brain.DefaultBlend = _savedBrainBlend;
            _brainBlendSaved = false;
        }

        /// <summary>
        /// Narrows the bridge vCam's FOV by <see cref="_fovPunchDegrees"/> to sell a subtle
        /// "lock on" at the moment camera control locks onto the vessel. Paired with
        /// <see cref="RestoreBridgeFov"/> after the blend completes.
        /// </summary>
        void ApplyBridgeFovPunch()
        {
            if (!_bridgeVCam || _fovPunchDegrees <= 0f) return;
            if (_bridgeFovSaved) return;

            var lens = _bridgeVCam.Lens;
            _bridgeSavedFov = lens.FieldOfView;
            _bridgeFovSaved = true;
            lens.FieldOfView = Mathf.Max(1f, _bridgeSavedFov - _fovPunchDegrees);
            _bridgeVCam.Lens = lens;
        }

        void RestoreBridgeFov()
        {
            if (!_bridgeVCam || !_bridgeFovSaved) return;
            var lens = _bridgeVCam.Lens;
            lens.FieldOfView = _bridgeSavedFov;
            _bridgeVCam.Lens = lens;
            _bridgeFovSaved = false;
        }

        // ── Random Mode Switching ───────────────────────────────────────

        void StartRandomSwitchLoopIfEnabled()
        {
            if (!_randomSwitchEnabled) return;
            if (_randomSwitchModes == null || _randomSwitchModes.Length < 2) return;

            _randomSwitchCts?.Cancel();
            _randomSwitchCts?.Dispose();
            _randomSwitchCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            RandomSwitchLoopAsync(_randomSwitchCts.Token).Forget();
        }

        async UniTaskVoid RandomSwitchLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var interval = Mathf.Max(1f,
                    Random.Range(_randomSwitchIntervalMin, _randomSwitchIntervalMax));

                try
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(interval),
                        ignoreTimeScale: true,
                        cancellationToken: ct);
                }
                catch (System.OperationCanceledException) { return; }

                // Skip the switch if we're mid-freestyle or a transition is already running —
                // the blend machinery is busy.
                if (_isInFreestyle) continue;
                if (_randomSwitchModes == null || _randomSwitchModes.Length < 2) continue;

                // Pick a mode different from the current one.
                MenuCameraMode next = _mode;
                for (int guard = 0; guard < 8 && next == _mode; guard++)
                    next = _randomSwitchModes[Random.Range(0, _randomSwitchModes.Length)];

                if (next != _mode)
                    Mode = next; // setter calls ApplyModeChange()
            }
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

            if (IsVesselMode)
            {
                // Disable the crystal-orbit vCam so priorities don't fight.
                if (_menuVCam) _menuVCam.gameObject.SetActive(false);

                EnsureMenuVesselFollowVCam();
                if (_menuVesselFollowVCam)
                {
                    ConfigureMenuVesselFollowTarget();
                    _menuVesselFollowVCam.PreviousStateIsValid = false;
                    SetVCamPriority(_menuVesselFollowVCam, HighPriority);
                    _menuVesselFollowVCam.gameObject.SetActive(true);
                }
            }
            else // CrystalOrbit
            {
                // Disable the vessel-follow vCam so priorities don't fight.
                if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);

                if (_menuVCam)
                {
                    SetMenuVCamTarget();
                    _menuVCam.gameObject.SetActive(true);
                }
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

            // Any prior transition that got cancelled mid-blend may have left the Brain
            // and bridge in an overridden state — restore before starting fresh.
            RestoreBrainBlend();
            RestoreBridgeFov();

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

            // The "from" side of the blend is whichever menu vCam is active for the current mode.
            var menuVCam = ActiveMenuVCam;

            // Vessel modes want a snappier blend — override the Brain's DefaultBlend to match
            // the shorter transition duration. Restored at the end.
            MaybeOverrideBrainBlend();

            // 1. Configure bridge to track vessel with matching camera offset
            ConfigureBridgeForVessel(followTarget, player.Vessel.VesselStatus.VesselCameraCustomizer);
            _bridgeVCam.PreviousStateIsValid = false;

            // 2. Activate bridge at higher priority → Brain blends menu (A) → bridge (B)
            //    Both vCams evaluated every frame — bridge tracks moving vessel throughout.
            _bridgeVCam.gameObject.SetActive(true);
            SetVCamPriority(_bridgeVCam, HighPriority + 1);
            if (menuVCam) SetVCamPriority(menuVCam, HighPriority);

            // Subtle FOV punch-in — narrows the lens as we lock onto the vessel.
            ApplyBridgeFovPunch();

            // 3. Wait for Brain blend to actually complete.
            //    Yield one frame first — the Brain hasn't evaluated the priority
            //    change yet, so IsBlending is false on this frame.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            while (_brain && _brain.IsBlending)
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

            // 4. Hand off to CustomCameraController
            //    Bridge and PlayerCam both compute the same position and LookAt rotation,
            //    so the swap is seamless.
            RestoreBridgeFov();
            _bridgeVCam.gameObject.SetActive(false);
            if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);
            CameraManager.Instance.SetupGamePlayCameras(followTarget);

            RestoreBrainBlend();
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

            // 4. Pick the blend for the B→A transition. Vessel modes want a shorter
            //    EaseInOut; other modes restore the original blend curve.
            if (_brain)
            {
                _brain.DefaultBlend = (_overrideBrainBlendForVesselModes && IsVesselMode)
                    ? new CinemachineBlendDefinition(
                        CinemachineBlendDefinition.Styles.EaseInOut,
                        _vesselFollowTransitionDuration)
                    : savedBlend;
            }

            // 5. Deactivate PlayerCam — Brain scene camera is at bridge pose (same as
            //    PlayerCam was), so the swap is invisible.
            CameraManager.Instance.DeactivateAllCameras();

            // 6. Activate the mode-appropriate menu vCam at higher priority → Brain blends
            //    bridge (B) → menu (A). Bridge keeps tracking vessel every frame — live "from" side.
            CinemachineCamera menuVCam = null;
            if (IsVesselMode)
            {
                // Ensure the crystal-orbit vCam is off so it doesn't contend for priority.
                if (_menuVCam) _menuVCam.gameObject.SetActive(false);

                EnsureMenuVesselFollowVCam();
                if (_menuVesselFollowVCam)
                {
                    ConfigureMenuVesselFollowTarget();
                    _menuVesselFollowVCam.PreviousStateIsValid = false;
                    _menuVesselFollowVCam.gameObject.SetActive(true);
                    menuVCam = _menuVesselFollowVCam;
                }
            }
            else // CrystalOrbit
            {
                if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);

                if (_menuVCam)
                {
                    SetMenuVCamTarget();
                    _menuVCam.gameObject.SetActive(true);
                    menuVCam = _menuVCam;
                }
            }

            if (menuVCam) SetVCamPriority(menuVCam, HighPriority + 1);

            // 7. Wait for Brain blend to actually complete.
            //    Yield one frame first so Brain detects the priority change.
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            while (_brain && _brain.IsBlending)
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);

            // 8. Clean up bridge and normalize menu priority.
            _bridgeVCam.gameObject.SetActive(false);
            if (menuVCam) SetVCamPriority(menuVCam, HighPriority);

            // Restore the Brain's original DefaultBlend so gameplay scenes use the original
            // curve next time. (We overrode it to EaseInOut for vessel modes in step 4.)
            if (_brain) _brain.DefaultBlend = savedBlend;

            _isInFreestyle = false;
        }

        /// <summary>
        /// Fallback: immediate switch without blend. Used when bridge vCam setup fails.
        /// </summary>
        void FallbackActivateGameplayCamera(Transform followTarget)
        {
            if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);
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
