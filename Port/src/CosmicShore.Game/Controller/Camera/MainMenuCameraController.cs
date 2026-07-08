// Ported verbatim from Assets/_Scripts/Controller/Camera/MainMenuCameraController.cs
// (camera arc 2026-07-08). Mechanical substitutions (README):
// Cysharp.Threading.Tasks → System.Threading.Tasks + CosmicShore.Engine.Tasks
// (UniTaskVoid → Task + .Forget(); UniTask.Delay(TimeSpan, ignoreTimeScale, ct) →
// GameTask.Delay(seconds, unscaledTime: true, ct); UniTask.Yield(PlayerLoopTiming
// .PostLateUpdate, ct) → GameTask.Yield(ct)); Obvious.Soap → CosmicShore.Engine.Soap;
// Reflex.Attributes → CosmicShore.Engine.Injection; UnityEngine → CosmicShore.Engine.
//
// LIVE: the MenuCameraMode enum + all tuning fields, the Mode property +
// ApplyModeChange, ActiveTransitionDuration (the read MenuCrystalClickHandler
// un-carries this iteration), IsVesselMode, the full SOAP event wiring
// (OnClientReady / OnGameStateTransitionStart / OnMenuStateTransitionStart /
// OnCrystalSpawned), the transform-side crystal-orbit rig (SetMenuVCamTarget's
// follow-target positioning + UpdateMenuOrbit's per-frame orbit math + the
// RotateAroundOrigin disable/re-enable), the randomized mode-switch loop, the
// CTS lifecycle (BeginTransition preemption + OnDestroy teardown), and both
// transition entry points — which resolve through UPSTREAM'S OWN no-bridge /
// no-player-camera fallback branches (FallbackActivateGameplayCamera /
// ActivateMenuCameraImmediate → the CameraManager shell's observable state
// mirror), the same "live on upstream's own null branch" precedent as
// HostConnectionService.LeavePartyAsync pre-un-carry.
//
// Deviations (all marked inline, one family): camera arc — Unity.Cinemachine.
// Every CinemachineCamera/Follow/Brain/BlendDefinition/MatchTargetOrientation
// surface (vCam caching/creation/config, brain-blend override + FOV punch,
// blend polling, priority juggling, the BindingMode serialized field) is
// carried as commented source — restore when the Cinemachine replacement ports.
// With no vCams constructible, the `!_bridgeVCam` / `!_menuVCam` guards resolve
// exactly as upstream's missing-object branches; the two spots where that guard
// WRAPS live transform work (SetMenuVCamTarget, ActivateMenuCameraImmediate's
// CrystalOrbit branch) carry the guard itself as the deviation so the
// transform-side rig stays live.

using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine.Tasks;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine;

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

        // PORT Deviation (camera arc 2026-07-08, Unity.Cinemachine.TargetTracking.BindingMode — restore when Cinemachine ports):
        // [SerializeField, Tooltip("Binding mode for the vessel-follow vCam. LazyFollow is the default " +
        //                          "because it keeps world-up (camera doesn't roll with the vessel) and " +
        //                          "trails behind in screen-space — smooth for fast AI pilots. " +
        //                          "LockToTargetWithWorldUp yaws with the vessel; LockToTarget copies " +
        //                          "full orientation (can feel choppy under aggressive AI).")]
        // BindingMode _vesselFollowBindingMode = BindingMode.LazyFollow;

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
        // PORT Deviation (camera arc 2026-07-08, CinemachineCamera/CinemachineFollow — restore when Cinemachine ports):
        // CinemachineCamera _menuVCam;
        // CinemachineFollow _menuFollow;
        Transform _menuFollowTarget;
        RotateAroundOrigin _followTargetRotator;
        Transform _crystalTarget;

        // Vessel-follow menu vCam (created at runtime on CameraManager). Reused across
        // all vessel modes (VesselFollow, VesselChaseTight, VesselTopDownPan) by reconfiguring
        // its offset, damping, binding mode, and LookAt per-mode.
        // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
        // CinemachineCamera _menuVesselFollowVCam;
        // CinemachineFollow _menuVesselFollowFollow;
        // CinemachineMatchTargetOrientation _menuVesselFollowAim;

        // Bridge vCam for smooth transitions (created at runtime on CameraManager)
        // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
        // CinemachineCamera _bridgeVCam;
        // CinemachineFollow _bridgeFollow;
        // CinemachineMatchTargetOrientation _bridgeAim;

        // State saved at transition start so we can restore after the blend.
        // PORT Deviation (camera arc 2026-07-08, CinemachineBlendDefinition — restore when Cinemachine ports):
        // CinemachineBlendDefinition _savedBrainBlend;
        // bool _brainBlendSaved;
        // float _bridgeSavedFov;
        // bool _bridgeFovSaved;

        // Random switch loop — owned by _cts so it dies with the component.
        CancellationTokenSource _randomSwitchCts;

        // Cached player camera (CM PlayerCam)
        CustomCameraController _playerCameraController;

        // Cached CinemachineBrain on the scene camera — used to force IgnoreTimeScale
        // PORT Deviation (camera arc 2026-07-08, CinemachineBrain — restore when Cinemachine ports):
        // CinemachineBrain _brain;

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
            // PORT Deviation (camera arc 2026-07-08, CinemachineBrain — restore when Cinemachine ports):
            // if (_brain)
            // {
            //     if (_brainBlendSaved) _brain.DefaultBlend = _savedBrainBlend;
            //     _brain.IgnoreTimeScale = false;
            // }
            // _brainBlendSaved = false;

            // Re-enable RotateAroundOrigin in case CameraManager is reused across scenes
            if (_followTargetRotator) _followTargetRotator.enabled = true;

            // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
            // if (_menuVCam)
            //     _menuVCam.gameObject.SetActive(false);
            //
            // if (_menuVesselFollowVCam)
            //     _menuVesselFollowVCam.gameObject.SetActive(false);
            //
            // if (_bridgeVCam)
            //     _bridgeVCam.gameObject.SetActive(false);
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
        /// <c>_menuVesselFollowVCam</c> — <see cref="ApplyMenuVesselFollowConfig"/>
        /// reconfigures it per-mode.</summary>
        // PORT Deviation (camera arc 2026-07-08, CinemachineCamera — restore when Cinemachine ports):
        // CinemachineCamera ActiveMenuVCam =>
        //     IsVesselMode ? _menuVesselFollowVCam : _menuVCam;

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

            // PORT Deviation (camera arc 2026-07-08, CinemachineCamera/CinemachineFollow — restore when Cinemachine ports):
            // _menuVCam = cmTransform.GetComponent<CinemachineCamera>();
            // _menuFollow = cmTransform.GetComponent<CinemachineFollow>();

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
            // PORT Deviation (camera arc 2026-07-08, Camera.main + CinemachineBrain — restore when Cinemachine ports):
            // var mainCam = Camera.main;
            // if (!mainCam) return;
            //
            // _brain = mainCam.GetComponent<CinemachineBrain>();
            // if (_brain)
            //     _brain.IgnoreTimeScale = true;
        }

        /// <summary>
        /// Creates or finds the bridge CinemachineCamera used for smooth priority-based
        /// blending during transitions. The bridge tracks the vessel via CinemachineFollow
        /// with zero damping — it is only active during blend transitions, not for ongoing
        /// vessel following (CustomCameraController handles that).
        /// </summary>
        void EnsureBridgeVCam()
        {
            // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when
            // Cinemachine ports; with no bridge constructible the transitions take upstream's
            // own no-bridge fallback branches):
            // if (_bridgeVCam) return;
            // if (!CameraManager.Instance) return;
            // ... (bridge find-or-create + tracker config + priority + SetActive(false))
        }

        /// <summary>
        /// Creates or finds the vessel-follow menu CinemachineCamera used by
        /// <see cref="MenuCameraMode.VesselFollow"/>. Unlike the bridge (zero damping,
        /// tight gameplay offset), this vCam trails the vessel cinematically —
        /// pulled-back offset with moderate damping.
        /// </summary>
        void EnsureMenuVesselFollowVCam()
        {
            // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
            // if (_menuVesselFollowVCam) return;
            // if (!CameraManager.Instance) return;
            // ... (vessel-follow vCam find-or-create + ApplyMenuVesselFollowConfig + priority + SetActive(false))
        }

        /// <summary>
        /// Applies the serialized cinematic offset/damping to the vessel-follow menu vCam,
        /// choosing per-mode values. Called on creation, when the mode changes, and when
        /// inspector values change during play mode.
        /// </summary>
        void ApplyMenuVesselFollowConfig()
        {
            // PORT Deviation (camera arc 2026-07-08, CinemachineFollow tracker settings — restore
            // when Cinemachine ports; the per-mode offset/damping/binding switch reads
            // _vesselChaseTightOffset / _topDown* / _vesselFollow* into the tracker):
            // if (!_menuVesselFollowFollow) return;
            // ... (per-mode offset/posDamp/rotDamp/binding switch → FollowOffset + TrackerSettings + aim damping)
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
            // PORT Deviation (camera arc 2026-07-08, CinemachineCamera.Target — restore when Cinemachine ports):
            // if (!_menuVesselFollowVCam) return;
            //
            // var player = _gameData?.LocalPlayer;
            // var followTarget = player?.Vessel?.VesselStatus?.CameraFollowTarget;
            // if (!followTarget) return;
            // ... (TrackingTarget + per-mode LookAt wiring + ApplyMenuVesselFollowConfig)
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
        /// If <c>_overrideBrainBlendForVesselModes</c> is on and we're in a vessel mode,
        /// temporarily shorten the Brain's DefaultBlend to match <c>_vesselFollowTransitionDuration</c>.
        /// Saved state is restored by <see cref="RestoreBrainBlend"/>.
        /// </summary>
        void MaybeOverrideBrainBlend()
        {
            // PORT Deviation (camera arc 2026-07-08, CinemachineBrain.DefaultBlend — restore when Cinemachine ports):
            // if (!_brain) return;
            // if (!_overrideBrainBlendForVesselModes || !IsVesselMode) return;
            // if (_brainBlendSaved) return;
            // ... (save DefaultBlend → EaseInOut(_vesselFollowTransitionDuration))
        }

        void RestoreBrainBlend()
        {
            // PORT Deviation (camera arc 2026-07-08, CinemachineBrain.DefaultBlend — restore when Cinemachine ports):
            // if (!_brain || !_brainBlendSaved) return;
            // _brain.DefaultBlend = _savedBrainBlend;
            // _brainBlendSaved = false;
        }

        /// <summary>
        /// Narrows the bridge vCam's FOV by <c>_fovPunchDegrees</c> to sell a subtle
        /// "lock on" at the moment camera control locks onto the vessel. Paired with
        /// <see cref="RestoreBridgeFov"/> after the blend completes.
        /// </summary>
        void ApplyBridgeFovPunch()
        {
            // PORT Deviation (camera arc 2026-07-08, CinemachineCamera.Lens — restore when Cinemachine ports):
            // if (!_bridgeVCam || _fovPunchDegrees <= 0f) return;
            // if (_bridgeFovSaved) return;
            // ... (save FOV → punch in by _fovPunchDegrees)
        }

        void RestoreBridgeFov()
        {
            // PORT Deviation (camera arc 2026-07-08, CinemachineCamera.Lens — restore when Cinemachine ports):
            // if (!_bridgeVCam || !_bridgeFovSaved) return;
            // ... (restore saved FOV)
        }

        // ── Random Mode Switching ───────────────────────────────────────

        void StartRandomSwitchLoopIfEnabled()
        {
            if (!_randomSwitchEnabled) return;
            if (_randomSwitchModes == null || _randomSwitchModes.Length < 2) return;
            // OnValidate can fire from the inspector before Start runs, so the parent CTS
            // may not exist yet — in that case Start will pick this up on its own.
            if (_cts == null) return;

            _randomSwitchCts?.Cancel();
            _randomSwitchCts?.Dispose();
            _randomSwitchCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            RandomSwitchLoopAsync(_randomSwitchCts.Token).Forget();
        }

        async Task RandomSwitchLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var interval = Mathf.Max(1f,
                    Random.Range(_randomSwitchIntervalMin, _randomSwitchIntervalMax));

                try
                {
                    // (Port: UniTask.Delay(TimeSpan, ignoreTimeScale: true, ct) →
                    //  GameTask.Delay(seconds, unscaledTime: true, ct).)
                    await GameTask.Delay(interval, unscaledTime: true, cancellationToken: ct);
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
            // PORT Deviation (camera arc 2026-07-08, CinemachineCamera guard — restore when
            // Cinemachine ports; upstream returns when "CM Main Menu" has no vCam, but with
            // no vCam constructible in this build the guard would permanently kill the
            // transform-side orbit rig below, which IS the live surface here):
            // if (!_menuVCam) return;

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
            // PORT Deviation (camera arc 2026-07-08, CinemachineCamera.Target — restore when Cinemachine ports):
            // var target = _menuVCam.Target;
            // target.TrackingTarget = _menuFollowTarget ? _menuFollowTarget : crystalTransform;
            // target.LookAtTarget = crystalTransform;
            // target.CustomLookAtTarget = true;
            // _menuVCam.Target = target;
            //
            // // CinemachineFollow offset provides height above the orbit path
            // if (_menuFollow)
            //     _menuFollow.FollowOffset = new Vector3(0, _orbitHeight, 0);
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
                // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
                // // Disable the crystal-orbit vCam so priorities don't fight.
                // if (_menuVCam) _menuVCam.gameObject.SetActive(false);

                EnsureMenuVesselFollowVCam();
                // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
                // if (_menuVesselFollowVCam)
                // {
                //     ConfigureMenuVesselFollowTarget();
                //     _menuVesselFollowVCam.PreviousStateIsValid = false;
                //     SetVCamPriority(_menuVesselFollowVCam, HighPriority);
                //     _menuVesselFollowVCam.gameObject.SetActive(true);
                // }
            }
            else // CrystalOrbit
            {
                // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when
                // Cinemachine ports; upstream wraps SetMenuVCamTarget in `if (_menuVCam)`, but
                // with no vCam constructible that guard would kill the live transform-side
                // orbit rig — same rationale as the SetMenuVCamTarget entry guard):
                // if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);
                //
                // if (_menuVCam)
                // {
                SetMenuVCamTarget();
                //     _menuVCam.gameObject.SetActive(true);
                // }
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
#pragma warning disable CS1998 // PORT Deviation (camera arc 2026-07-08): the blend awaits live in the commented region below.
        async Task TransitionToGameplayCameraAsync()
        {
            if (!CameraManager.Instance) return;

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) return;

            var ct = BeginTransition();
            var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;

            EnsureBridgeVCam();
            // PORT Deviation (camera arc 2026-07-08, Cinemachine blend machinery — restore when
            // Cinemachine ports; with no bridge constructible, upstream's own no-bridge branch
            // is the live truth — the blend region below is the carried source):
            // if (!_bridgeVCam) { FallbackActivateGameplayCamera(followTarget); return; }
            //
            // var menuVCam = ActiveMenuVCam;
            // MaybeOverrideBrainBlend();
            // ConfigureBridgeForVessel(followTarget, player.Vessel.VesselStatus.VesselCameraCustomizer);
            // _bridgeVCam.PreviousStateIsValid = false;
            // _bridgeVCam.gameObject.SetActive(true);
            // SetVCamPriority(_bridgeVCam, HighPriority + 1);
            // if (menuVCam) SetVCamPriority(menuVCam, HighPriority);
            // ApplyBridgeFovPunch();
            //
            // // Wait for Brain blend to actually complete. Yield one frame first — the Brain
            // // hasn't evaluated the priority change yet, so IsBlending is false on this frame.
            // // (Port: UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct) → GameTask.Yield(ct).)
            // await GameTask.Yield(ct);
            // while (_brain && _brain.IsBlending)
            //     await GameTask.Yield(ct);
            //
            // RestoreBridgeFov();
            // _bridgeVCam.gameObject.SetActive(false);
            // if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            // if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);
            // CameraManager.Instance.SetupGamePlayCameras(followTarget);
            //
            // RestoreBrainBlend();
            // _isInFreestyle = true;

            FallbackActivateGameplayCamera(followTarget);
        }
#pragma warning restore CS1998

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
#pragma warning disable CS1998 // PORT Deviation (camera arc 2026-07-08): the blend awaits live in the commented region below.
        async Task TransitionToMenuCameraAsync()
        {
            if (!CameraManager.Instance) return;
            if (!_playerCameraController) { ActivateMenuCameraImmediate(); return; }

            var player = _gameData.LocalPlayer;
            if (player?.Vessel == null) { ActivateMenuCameraImmediate(); return; }

            var ct = BeginTransition();

            EnsureBridgeVCam();
            // PORT Deviation (camera arc 2026-07-08, Cinemachine blend machinery — restore when
            // Cinemachine ports; with no bridge constructible, upstream's own no-bridge branch
            // is the live truth — the blend region below is the carried source):
            // if (!_bridgeVCam) { ActivateMenuCameraImmediate(); return; }
            //
            // var followTarget = player.Vessel.VesselStatus.CameraFollowTarget;
            // ConfigureBridgeForVessel(followTarget, player.Vessel.VesselStatus.VesselCameraCustomizer);
            // _bridgeVCam.PreviousStateIsValid = false;
            //
            // CinemachineBlendDefinition savedBlend = default;
            // if (_brain)
            // {
            //     savedBlend = _brain.DefaultBlend;
            //     _brain.DefaultBlend = new CinemachineBlendDefinition(
            //         CinemachineBlendDefinition.Styles.Cut, 0f);
            // }
            //
            // _bridgeVCam.gameObject.SetActive(true);
            // SetVCamPriority(_bridgeVCam, HighPriority);
            //
            // // Let the Brain evaluate with CUT blend.
            // // (Port: UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct) → GameTask.Yield(ct).)
            // await GameTask.Yield(ct);
            //
            // if (_brain)
            // {
            //     _brain.DefaultBlend = (_overrideBrainBlendForVesselModes && IsVesselMode)
            //         ? new CinemachineBlendDefinition(
            //             CinemachineBlendDefinition.Styles.EaseInOut,
            //             _vesselFollowTransitionDuration)
            //         : savedBlend;
            // }
            //
            // CameraManager.Instance.DeactivateAllCameras();
            //
            // ... (mode-appropriate menu vCam activation at HighPriority + 1, blend poll on
            //      _brain.IsBlending, bridge deactivation, priority normalization, and
            //      DefaultBlend restore — see upstream lines 920-965)
            //
            // _isInFreestyle = false;

            ActivateMenuCameraImmediate();
        }
#pragma warning restore CS1998

        /// <summary>
        /// Fallback: immediate switch without blend. Used when bridge vCam setup fails.
        /// </summary>
        void FallbackActivateGameplayCamera(Transform followTarget)
        {
            // PORT Deviation (camera arc 2026-07-08, Cinemachine vCam family — restore when Cinemachine ports):
            // if (_menuVCam) _menuVCam.gameObject.SetActive(false);
            // if (_menuVesselFollowVCam) _menuVesselFollowVCam.gameObject.SetActive(false);
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
        // PORT Deviation (camera arc 2026-07-08, CinemachineFollow/CinemachineMatchTargetOrientation — restore when Cinemachine ports):
        // void ConfigureBridgeForVessel(Transform followTarget, VesselCameraCustomizer customizer)
        // {
        //     ... (tracking target, CameraSettingsSO offset incl. DynamicCamera min-distance,
        //          zero position/rotation/aim damping — see upstream lines 987-1017)
        // }

        // PORT Deviation (camera arc 2026-07-08, CinemachineCamera.Priority — restore when Cinemachine ports):
        // static void SetVCamPriority(CinemachineCamera cam, int value)
        // {
        //     if (!cam) return;
        //     var p = cam.Priority;
        //     p.Enabled = true;
        //     p.Value = value;
        //     cam.Priority = p;
        // }
    }
}
