using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Menu_Main camera driver - a plain-transform camera rig with NO Cinemachine.
    ///
    /// While the menu is in autopilot (lava-lamp) state this component drives the scene's
    /// main camera directly through one of a set of <see cref="MenuCameraConfigSO"/>
    /// configurations. What each one frames is structural - it follows from the config's
    /// <see cref="MenuCameraRigKind"/>, resolved here every frame, and a config carries no target
    /// field with which to point a menu camera at anything else:
    ///
    ///   • orbit / cinematic trail / tight chase / top-down pan frame the LOCAL VESSEL
    ///     (<c>GameDataSO.LocalPlayer.Vessel</c>).
    ///
    ///   • <see cref="MenuCameraRigKind.LavaLamp"/> frames the CELL - a distant, very slow orbit
    ///     of the cell centre aimed at its crystal, with the vessel merely one of the things
    ///     drifting through the shot. Being vessel-free, it is the one rig that runs from scene
    ///     load rather than waiting on the spawn chain.
    ///
    /// Transitions to/from the gameplay camera ("CM PlayerCam" / <see cref="CustomCameraController"/>,
    /// which this class never modifies) blend between two LIVE, vessel-anchored endpoints:
    ///
    ///   • Enter freestyle: A = the menu rig (still simulating, riding the vessel under its
    ///     damping), B = the exact pose <see cref="CustomCameraController.SnapToTarget"/> will
    ///     compute from the vessel's <see cref="CameraSettingsSO"/>. Because BOTH endpoints move
    ///     with the vessel, the eased blend rides its motion the whole way - the camera never
    ///     chases a runaway target through world space (the old jank). At completion
    ///     <see cref="CameraManager.SetupGamePlayCameras"/> snaps the player cam onto the very
    ///     pose being rendered, so the handoff is invisible.
    ///
    ///   • Exit freestyle: the player cam's current pose is frozen IN THE VESSEL'S LOCAL FRAME
    ///     (endpoint A - continues to ride the moving vessel exactly as the player saw it) and
    ///     the scene camera takes over at that same pose before the player cam deactivates.
    ///     Endpoint B is the menu rig, re-seeded at that pose and converging toward the active
    ///     config's framing under its own damping. The eased A→B blend is therefore smooth at
    ///     both ends with nothing snapping and nothing chasing.
    ///
    /// Blend pacing is per-config (<see cref="MenuCameraConfigSO.blendDuration"/>), surfaced to
    /// <see cref="MenuCrystalClickHandler"/> via <see cref="ActiveTransitionDuration"/> so the UI
    /// fade and input unlock agree with the camera.
    ///
    /// Config switching (manual or randomized) never cuts: a switch raises a temporary smoothing
    /// boost that decays over <see cref="_configGlideDuration"/>, so even a zero-damping config is
    /// entered as a glide.
    ///
    /// Place on the Game GameObject in Menu_Main (same object as <see cref="MenuCrystalClickHandler"/>).
    /// </summary>
    public class MainMenuCameraController : MonoBehaviour
    {
        enum MenuCameraState
        {
            /// <summary>Before the first OnClientReady - the scene camera keeps its authored pose.</summary>
            Idle = 0,
            /// <summary>Menu/autopilot state - the rig frames the vessel per the active config.</summary>
            Menu = 1,
            /// <summary>Blending menu rig → gameplay pose; ends by activating CM PlayerCam.</summary>
            EnteringFreestyle = 2,
            /// <summary>CM PlayerCam owns the view; the scene camera is disabled.</summary>
            Freestyle = 3,
            /// <summary>Blending the frozen freestyle framing → menu rig.</summary>
            ExitingFreestyle = 4,
        }

        readonly struct CameraPose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float FieldOfView;

            public CameraPose(Vector3 position, Quaternion rotation, float fieldOfView)
            {
                Position = position;
                Rotation = rotation;
                FieldOfView = fieldOfView;
            }

            public static CameraPose Blend(in CameraPose a, in CameraPose b, float t) => new(
                Vector3.Lerp(a.Position, b.Position, t),
                Quaternion.Slerp(a.Rotation, b.Rotation, t),
                Mathf.Lerp(a.FieldOfView, b.FieldOfView, t));
        }

        [Header("Camera Configurations")]
        [SerializeField, Tooltip("The set of menu camera configurations. What a configuration frames " +
                                 "is structural - it comes from the rig kind (the local vessel, or the " +
                                 "cell for the lava lamp) and cannot be retargeted per config. Index 0 " +
                                 "(or Initial Config Index) is the boot framing. Leave empty to fall " +
                                 "back to built-in defaults of every rig kind.")]
        MenuCameraConfigSO[] _configs;

        [SerializeField, Tooltip("Which configuration to start with.")]
        int _initialConfigIndex = 0;

        [Header("Cell Framing (LavaLamp)")]
        [SerializeField, Tooltip("Cell runtime data, used by the LavaLamp rig for the cell centre it " +
                                 "orbits and the crystal it aims at. Optional: with none wired the rig " +
                                 "finds the nearest active cell instead, and aims at the cell centre.")]
        CellRuntimeDataSO _cellData;

        [Header("Config Switching")]
        [SerializeField, Tooltip("If enabled, the active configuration rotates randomly while in menu " +
                                 "state (never during freestyle or a transition). Switches glide - " +
                                 "they are never a cut.")]
        bool _randomSwitchEnabled = true;

        [SerializeField, Range(1f, 120f), Tooltip("Minimum seconds between automatic config switches.")]
        float _randomSwitchIntervalMin = 20f;

        [SerializeField, Range(1f, 120f), Tooltip("Maximum seconds between automatic config switches.")]
        float _randomSwitchIntervalMax = 45f;

        [SerializeField, Range(0.5f, 6f),
         Tooltip("How long the temporary smoothing boost lasts after a config switch. This is what " +
                 "turns a switch into a glide instead of a cut, even into a zero-damping config.")]
        float _configGlideDuration = 3f;

        [Header("Fallbacks")]
        [SerializeField, Range(0.1f, 5f),
         Tooltip("Freestyle blend duration used when no configuration is available.")]
        float _fallbackBlendDuration = 1.2f;

        [Inject] MenuFreestyleEventsContainerSO _freestyleEvents;
        [Inject] GameDataSO _gameData;

        // Smoothing boost applied while gliding into a freshly-switched config.
        const float GlidePositionSmoothTime = 1.2f;
        const float GlideRotationSharpness = 2.5f;
        // Exponential sharpness of the FOV settling toward the active config's lens.
        const float FovGlideSharpness = 3f;
        // A follow-target jump beyond this in one frame is a teleport (spawn park / SetPose
        // home), not flight - carry the rig along instead of swinging across the arena.
        const float TeleportStep = 100f;
        // Gameplay follow offset used only if a vessel has no CameraSettingsSO (matches the
        // CameraSettingsSO field default).
        static readonly Vector3 DefaultGameplayOffset = new(0f, 10f, -20f);

        // The Menu_Main scene camera this controller drives (the camera that used to host the
        // CinemachineBrain), and the gameplay camera read for pose/FOV continuity - never written.
        Camera _cam;
        Camera _playerCam;

        MenuCameraState _state = MenuCameraState.Idle;
        int _activeConfigIndex;

        // Rig simulation state. _lastFramingCenter tracks whatever the ACTIVE config is anchored
        // to (vessel or cell), which is what the teleport guard must watch.
        Vector3 _rigPos;
        Vector3 _rigVel;
        Quaternion _rigRot = Quaternion.identity;
        float _rigFov = 60f;
        float _orbitPhaseDeg;
        Quaternion _trailYawAnchor = Quaternion.identity;
        Vector3 _lastFramingCenter;
        bool _hasLastFramingCenter;
        float _switchGlide;      // 1 → 0 after a config switch; scales the smoothing boost
        float _switchTimer;
        bool _holdingSpeedTunnel; // true only while THIS controller holds the speed-tunnel law

        // Freestyle transition state.
        float _blendElapsed;
        float _blendDuration;
        Vector3 _exitLocalPos;   // player-cam pose captured in the vessel's local frame
        Quaternion _exitLocalRot = Quaternion.identity;
        float _exitFov = 60f;
        Vector3 _lastGameplayOffset = DefaultGameplayOffset;

        /// <summary>The configuration currently driving the rig (vessel- or cell-framing).</summary>
        public MenuCameraConfigSO ActiveConfig =>
            _configs is { Length: > 0 }
                ? _configs[Mathf.Clamp(_activeConfigIndex, 0, _configs.Length - 1)]
                : null;

        /// <summary>
        /// Whether the active configuration cannot frame anything until a vessel exists. False
        /// only for the lava lamp, which frames the cell and therefore runs from scene load.
        /// </summary>
        bool ActiveConfigRequiresVessel
        {
            get
            {
                var config = ActiveConfig;
                return !config || config.RequiresVessel;
            }
        }

        /// <summary>
        /// How long the menu↔freestyle blend lasts for the active configuration. Read by
        /// <see cref="MenuCrystalClickHandler"/> so the UI fade and input unlock agree with
        /// the camera blend.
        /// </summary>
        public float ActiveTransitionDuration
        {
            get
            {
                var config = ActiveConfig;
                return config ? Mathf.Max(0.05f, config.blendDuration) : _fallbackBlendDuration;
            }
        }

        // ── Unity Lifecycle ─────────────────────────────────────────────

        void Start()
        {
            EnsureConfigs();
            CacheCameras();
            DeactivateLegacyMenuVCam();
            SubscribeEvents();

            // Take over now if the local pair was already initialized before this scene object
            // subscribed (OnClientReady raced the scene load) - or if the boot config frames the
            // cell rather than a vessel, in which case there is nothing to wait for and the lava
            // lamp should already be running while the vessel spawns.
            if (ResolveTarget() || !ActiveConfigRequiresVessel)
                ActivateMenuCameraImmediate();
        }

        void OnDestroy() => UnsubscribeEvents();

        /// <summary>
        /// Never leave a platform law latched off. <c>VesselSpeedTunnel</c>'s suppression flag is
        /// a static reset only by its <c>RuntimeInitializeOnLoadMethod</c> installer - once per
        /// app launch, not per scene load - so a hold that outlived this controller would
        /// silently kill the speed tunnel for the rest of the session. Same reasoning as
        /// <c>CameraManager.RestoreGameplayCamera</c> lifting its hold unconditionally and first.
        ///
        /// OnDisable rather than OnDestroy because it covers BOTH exits: Unity raises it before
        /// OnDestroy on teardown, and it also catches a merely-disabled controller, whose
        /// LateUpdate would otherwise stop running with the hold still raised. Re-enabling needs
        /// no counterpart - LateUpdate re-derives the hold from live state every frame.
        /// </summary>
        void OnDisable() => SetSpeedTunnelHold(false);

        void LateUpdate()
        {
            // BEFORE the early returns: the hold has to be re-evaluated on the very frame the
            // state leaves menu ownership, and those states are exactly the ones that return
            // here. Evaluating it after would latch the hold on the moment freestyle begins.
            UpdateSpeedTunnelHold();

            if (_state is MenuCameraState.Idle or MenuCameraState.Freestyle) return;
            if (!_cam) return;

            // Unscaled: the menu camera keeps breathing through pause states, matching the
            // UI fades in MenuCrystalClickHandler.
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;

            var target = ResolveTarget();

            switch (_state)
            {
                case MenuCameraState.Menu:
                    UpdateMenuState(target, dt);
                    break;
                case MenuCameraState.EnteringFreestyle:
                    UpdateEnterTransition(target, dt);
                    break;
                case MenuCameraState.ExitingFreestyle:
                    UpdateExitTransition(target, dt);
                    break;
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (_randomSwitchIntervalMax < _randomSwitchIntervalMin)
                _randomSwitchIntervalMax = _randomSwitchIntervalMin;
        }
#endif

        // ── Event Wiring ────────────────────────────────────────────────

        void SubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised += HandleMenuReady;

            _freestyleEvents.OnGameStateTransitionStart.OnRaised += HandleEnterFreestyle;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised += HandleExitFreestyle;
        }

        void UnsubscribeEvents()
        {
            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleMenuReady;

            _freestyleEvents.OnGameStateTransitionStart.OnRaised -= HandleEnterFreestyle;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised -= HandleExitFreestyle;
        }

        // ── Setup ───────────────────────────────────────────────────────

        /// <summary>
        /// Drops null entries and, when nothing is authored, builds one built-in default per
        /// rig kind so the controller is zero-wire functional (same pattern as ElementalBars).
        /// </summary>
        void EnsureConfigs()
        {
            int valid = 0;
            if (_configs != null)
                foreach (var config in _configs)
                    if (config) valid++;

            if (valid > 0)
            {
                if (valid != _configs.Length)
                {
                    var compact = new MenuCameraConfigSO[valid];
                    int i = 0;
                    foreach (var config in _configs)
                        if (config) compact[i++] = config;
                    _configs = compact;
                }
            }
            else
            {
                _configs = CreateDefaultConfigs();
            }

            _activeConfigIndex = Mathf.Clamp(_initialConfigIndex, 0, _configs.Length - 1);
            ResetSwitchTimer();
        }

        static MenuCameraConfigSO[] CreateDefaultConfigs()
        {
            MenuCameraConfigSO Make(string configName, System.Action<MenuCameraConfigSO> tune)
            {
                var config = ScriptableObject.CreateInstance<MenuCameraConfigSO>();
                config.name = configName;
                config.hideFlags = HideFlags.HideAndDontSave;
                tune(config);
                return config;
            }

            return new[]
            {
                Make("MenuCam_OrbitVessel (default)", c =>
                {
                    c.rigKind = MenuCameraRigKind.OrbitVessel;
                    c.positionSmoothTime = 1.4f;
                    c.rotationSharpness = 2.5f;
                    c.blendDuration = 1.6f;
                }),
                Make("MenuCam_CinematicTrail (default)", c =>
                {
                    c.rigKind = MenuCameraRigKind.CinematicTrail;
                    c.followOffset = new Vector3(30f, 50f, -80f);
                    c.positionSmoothTime = 1f;
                    c.rotationSharpness = 3f;
                    c.blendDuration = 1.2f;
                }),
                Make("MenuCam_ChaseTight (default)", c =>
                {
                    c.rigKind = MenuCameraRigKind.ChaseTight;
                    c.followOffset = new Vector3(0f, 18f, -42f);
                    c.positionSmoothTime = 0.15f;
                    c.rotationSharpness = 10f;
                    c.blendDuration = 0.7f;
                }),
                Make("MenuCam_TopDownPan (default)", c =>
                {
                    c.rigKind = MenuCameraRigKind.TopDownPan;
                    c.positionSmoothTime = 1.5f;
                    c.rotationSharpness = 2f;
                    c.blendDuration = 1.8f;
                }),
                // Mirrors MenuCam_LavaLamp1.asset - see that asset for the derivation of these
                // numbers from the legacy Cinemachine rig.
                Make("MenuCam_LavaLamp1 (default)", c =>
                {
                    c.rigKind = MenuCameraRigKind.LavaLamp;
                    c.lavaLampOrbitRadius = 686f;
                    c.lavaLampOrbitAxis = new Vector3(1f, 1f, 0f);
                    c.positionSmoothTime = 0.3f;
                    c.rotationSharpness = 0.45f;
                    c.blendDuration = 2f;
                }),
            };
        }

        /// <summary>
        /// Finds the Menu_Main scene camera (the enabled MainCamera that is NOT one of
        /// CameraManager's gameplay cameras) and caches the gameplay camera for FOV/pose reads.
        /// </summary>
        void CacheCameras()
        {
            var managerRoot = CameraManager.Instance ? CameraManager.Instance.transform : null;

            foreach (var candidate in Camera.allCameras)
            {
                if (!candidate.CompareTag("MainCamera")) continue;
                if (managerRoot && candidate.transform.IsChildOf(managerRoot)) continue;
                _cam = candidate;
                break;
            }

            if (!_cam)
            {
                // Last resort - but never bind a CameraManager camera (CM PlayerCam is
                // Camera.main during freestyle, and this rig must never drive it).
                var main = Camera.main;
                if (main && (!managerRoot || !main.transform.IsChildOf(managerRoot)))
                    _cam = main;
            }

            if (!_cam)
                CSDebug.LogWarning("[MainMenuCameraController] No scene camera found - menu camera rig disabled.");

            if (CameraManager.Instance)
            {
                var playerCamTransform = CameraManager.Instance.GetCloseCamera();
                if (playerCamTransform) _playerCam = playerCamTransform.GetComponent<Camera>();
            }
        }

        /// <summary>
        /// The legacy "CM Main Menu" Cinemachine vCam must never wake up - with it off (and
        /// CameraManager no longer activating it) no CinemachineBrain anywhere has a camera to
        /// drive, which is what makes this rig the sole owner of the menu view.
        /// </summary>
        static void DeactivateLegacyMenuVCam()
        {
            if (!CameraManager.Instance) return;

            var legacy = CameraManager.Instance.transform.Find("CM Main Menu");
            if (legacy) legacy.gameObject.SetActive(false);
        }

        // ── Event Handlers ──────────────────────────────────────────────

        void HandleMenuReady()
        {
            // OnClientReady can be re-raised while the player is flying (e.g. late pair
            // initialization edge cases) - never let it yank the camera out of freestyle.
            if (_state is MenuCameraState.EnteringFreestyle
                       or MenuCameraState.Freestyle
                       or MenuCameraState.ExitingFreestyle)
                return;

            ActivateMenuCameraImmediate();
        }

        /// <summary>
        /// Takes over the scene camera for menu state with no blend, seeding the rig from the
        /// camera's current pose so the takeover itself is continuous from whatever was on
        /// screen (the authored boot view, or wherever a previous state left the camera).
        /// </summary>
        void ActivateMenuCameraImmediate()
        {
            if (!_cam) CacheCameras();
            if (!_cam) return;

            DeactivateLegacyMenuVCam();
            if (CameraManager.Instance)
                CameraManager.Instance.SetMainMenuCameraActive();

            _cam.enabled = true;
            SeedRigFromCamera();
            _state = MenuCameraState.Menu;
            ResetSwitchTimer();
        }

        void HandleEnterFreestyle()
        {
            var target = ResolveTarget();

            if (!_cam || !target)
            {
                // Nothing to blend with - immediate gameplay handoff keeps the toggle functional.
                if (target && CameraManager.Instance)
                    CameraManager.Instance.SetupGamePlayCameras(target);
                if (_cam) _cam.enabled = false;
                _state = MenuCameraState.Freestyle;
                return;
            }

            // Entering freestyle before the first menu-ready is an edge case (events raced) -
            // seed the rig so endpoint A is valid.
            if (_state == MenuCameraState.Idle)
                SeedRigFromCamera();

            _blendElapsed = 0f;
            _blendDuration = ActiveTransitionDuration;
            _state = MenuCameraState.EnteringFreestyle;
        }

        void HandleExitFreestyle()
        {
            if (!_cam) CacheCameras();
            if (!_cam) return;

            var target = ResolveTarget();

            // Continue from exactly what the player is seeing: the player cam's live pose.
            Vector3 pos;
            Quaternion rot;
            float fov;
            var playerCamTransform = CameraManager.Instance ? CameraManager.Instance.GetCloseCamera() : null;
            if (playerCamTransform)
            {
                pos = playerCamTransform.position;
                rot = playerCamTransform.rotation;
                fov = _playerCam ? _playerCam.fieldOfView : _cam.fieldOfView;
            }
            else
            {
                pos = _cam.transform.position;
                rot = _cam.transform.rotation;
                fov = _cam.fieldOfView;
            }

            // Scene camera takes over at the identical pose BEFORE the player cam deactivates -
            // the ownership swap never shows.
            _cam.transform.SetPositionAndRotation(pos, rot);
            _cam.fieldOfView = fov;
            _cam.enabled = true;
            if (CameraManager.Instance)
                CameraManager.Instance.SetMainMenuCameraActive();

            if (!target)
            {
                // No vessel to anchor the blend to - hold the pose and resume menu framing
                // whenever the vessel returns.
                SeedRigFromCamera();
                _state = MenuCameraState.Menu;
                return;
            }

            // Freeze the freestyle framing in the VESSEL's frame: endpoint A of the exit blend
            // rides the moving vessel exactly as the player cam did.
            Quaternion inverseTargetRot = Quaternion.Inverse(target.rotation);
            _exitLocalPos = inverseTargetRot * (pos - target.position);
            _exitLocalRot = inverseTargetRot * rot;
            _exitFov = fov;

            // Endpoint B: the menu rig, re-seeded here so it converges toward the active
            // config's framing from this very pose under its own damping.
            _rigPos = pos;
            _rigRot = rot;
            _rigVel = Vector3.zero;
            _rigFov = fov;
            _hasLastFramingCenter = false;
            _switchGlide = 1f;
            SeedOrbitPhase(ResolveFramingCenter(ActiveConfig, target));
            if (MenuCameraConfigSO.TryGetYawAnchor(target.rotation, out var yawAnchor))
                _trailYawAnchor = yawAnchor;

            _blendElapsed = 0f;
            _blendDuration = ActiveTransitionDuration;
            _state = MenuCameraState.ExitingFreestyle;
        }

        // ── State Updates ───────────────────────────────────────────────

        void UpdateMenuState(Transform target, float dt)
        {
            // Hold the current framing until the vessel exists - unless the active config frames
            // the cell (lava lamp), which needs no vessel and so runs from scene load.
            if (!target && ActiveConfigRequiresVessel) return;

            TickRandomSwitch(dt);
            ApplyPose(SimulateRig(target, dt));
        }

        void UpdateEnterTransition(Transform target, float dt)
        {
            if (!target)
            {
                // Vessel vanished mid-blend - stay in the menu; the rig still holds a valid pose.
                _state = MenuCameraState.Menu;
                return;
            }

            var from = SimulateRig(target, dt);       // endpoint A: menu rig, still riding the vessel
            var to = ComputeGameplayPose(target);     // endpoint B: the pose SnapToTarget will produce
            _blendElapsed += dt;
            float t = SmootherStep01(Mathf.Clamp01(_blendElapsed / _blendDuration));
            ApplyPose(CameraPose.Blend(from, to, t));

            if (_blendElapsed < _blendDuration) return;

            // Blend complete. SetupGamePlayCameras snaps CM PlayerCam onto the same pose this
            // camera is already rendering, so the swap is invisible; then the scene camera
            // stands down so only the gameplay camera renders.
            if (CameraManager.Instance)
                CameraManager.Instance.SetupGamePlayCameras(target);
            _cam.enabled = false;
            _state = MenuCameraState.Freestyle;
        }

        void UpdateExitTransition(Transform target, float dt)
        {
            if (!target)
            {
                _state = MenuCameraState.Menu; // the rig already holds a valid pose
                return;
            }

            var from = new CameraPose(
                target.position + target.rotation * _exitLocalPos,
                target.rotation * _exitLocalRot,
                _exitFov);
            var to = SimulateRig(target, dt);
            _blendElapsed += dt;
            float t = SmootherStep01(Mathf.Clamp01(_blendElapsed / _blendDuration));
            ApplyPose(CameraPose.Blend(from, to, t));

            if (_blendElapsed < _blendDuration) return;

            _state = MenuCameraState.Menu;
            ResetSwitchTimer();
        }

        // ── Rig Simulation ──────────────────────────────────────────────

        /// <summary>
        /// Advances the active config's rig one step and returns its pose. The rig chases the
        /// config's desired framing with SmoothDamp position + exponential look-at rotation,
        /// optionally boosted by the post-switch glide so config changes never cut.
        /// </summary>
        CameraPose SimulateRig(Transform target, float dt)
        {
            var config = ActiveConfig;
            Quaternion targetRot = target ? target.rotation : Quaternion.identity;

            // What the rig is anchored to, and what it aims at, are both decided by the rig kind -
            // the vessel for every vessel kind, the cell (and its crystal) for the lava lamp.
            Vector3 framingCenter = ResolveFramingCenter(config, target);
            Vector3 lookPoint = ResolveLookPoint(config, target, framingCenter);

            HandleAnchorDiscontinuity(framingCenter);

            // Track the vessel's heading for yaw-only offsets; hold the last good anchor while
            // the vessel points straight up/down so the framing never flips.
            if (target && MenuCameraConfigSO.TryGetYawAnchor(targetRot, out var yawAnchor))
                _trailYawAnchor = yawAnchor;
            Quaternion offsetAnchor = config.yawOnlyOffset ? _trailYawAnchor : targetRot;

            _orbitPhaseDeg = Mathf.Repeat(_orbitPhaseDeg + config.OrbitDegreesPerSecond * dt, 360f);

            Vector3 desired = config.ComputeDesiredPosition(framingCenter, offsetAnchor, _orbitPhaseDeg);

            float glide = SmootherStep01(_switchGlide);
            float positionSmoothTime = Mathf.Max(
                config.positionSmoothTime,
                Mathf.Lerp(config.positionSmoothTime, GlidePositionSmoothTime, glide));
            float rotationSharpness = Mathf.Min(
                config.rotationSharpness,
                Mathf.Lerp(config.rotationSharpness, GlideRotationSharpness, glide));

            _rigPos = positionSmoothTime <= 1e-4f
                ? desired
                : Vector3.SmoothDamp(_rigPos, desired, ref _rigVel, positionSmoothTime,
                                     float.PositiveInfinity, dt);

            Vector3 lookDirection = lookPoint - _rigPos;
            Vector3 upHint = config.ComputeLookUpHint(targetRot, lookDirection);
            if (SafeLookRotation.TryGet(lookDirection, upHint, out var lookRotation, this, logError: false))
            {
                _rigRot = rotationSharpness <= 0f
                    ? lookRotation
                    : Quaternion.Slerp(_rigRot, lookRotation, 1f - Mathf.Exp(-rotationSharpness * dt));
            }

            float fovTarget = config.fieldOfView > 0f ? config.fieldOfView : GameplayFov();
            _rigFov = Mathf.Lerp(_rigFov, fovTarget, 1f - Mathf.Exp(-FovGlideSharpness * dt));

            _switchGlide = Mathf.MoveTowards(_switchGlide, 0f, dt / Mathf.Max(0.1f, _configGlideDuration));

            return new CameraPose(_rigPos, _rigRot, _rigFov);
        }

        /// <summary>
        /// A framing-anchor jump far beyond flight speed is a teleport (spawn park, SetPose
        /// home, vessel swap) - translate the rig along with it so relative framing is
        /// preserved, instead of swooping across the arena to "catch" the vessel.
        ///
        /// Config switches clear the history rather than relying on this: swapping between a
        /// vessel rig and the cell-anchored lava lamp legitimately moves the anchor, and that is
        /// a re-anchor to glide into, not a teleport to carry the rig along with.
        /// </summary>
        void HandleAnchorDiscontinuity(Vector3 framingCenter)
        {
            if (_hasLastFramingCenter)
            {
                Vector3 delta = framingCenter - _lastFramingCenter;
                if (delta.sqrMagnitude > TeleportStep * TeleportStep)
                {
                    _rigPos += delta;
                    _rigVel = Vector3.zero;
                }
            }

            _lastFramingCenter = framingCenter;
            _hasLastFramingCenter = true;
        }

        // ── Speed Tunnel Hold ───────────────────────────────────────────

        /// <summary>
        /// The speed tunnel (`Docs/SPEED_TUNNEL.md`) sells the LOCAL PILOT'S speed to the local
        /// pilot by narrowing the camera's FOV and relaxing the global URP Panini projection as
        /// the vessel goes faster. In the menu the FOV half is already inert - the menu owns the
        /// scene camera and <c>CameraManager.GetActiveController()</c> is null, so
        /// <c>VesselSpeedTunnel.ResolveGameplayCamera</c> returns null - but the **Panini half is
        /// a single GLOBAL override that does not care which camera renders**, so the autopilot
        /// vessel's fluctuating speed keeps warping whatever is on screen.
        ///
        /// While a vessel-framing config is active that is a designed state: the camera is riding
        /// the ship, so the warp tracks the motion being watched. The LAVA LAMP is the case it
        /// breaks on - a detached orbital shot of the cell, aimed at the crystal, that is not
        /// following the vessel at all. There is no speed being sold to anyone, so the pumping
        /// Panini distance reads as exactly what it is: an unexplained rhythmic lens warp.
        ///
        /// Hence a hold, in the shape the law prescribes and for the same reason the one existing
        /// caller (`CameraManager.BeginManualReplayCamera`) uses it: a vantage that is posed
        /// independently of the pilot's vessel. It is a HOLD, not an opt-out - the vessel binding
        /// survives it, the law comes back the instant a vessel-framing config or freestyle takes
        /// over, and nothing has to remember to re-point the tunnel.
        /// </summary>
        void UpdateSpeedTunnelHold()
        {
            bool menuOwnsTheView = _state is MenuCameraState.Menu
                                          or MenuCameraState.EnteringFreestyle
                                          or MenuCameraState.ExitingFreestyle;

            SetSpeedTunnelHold(menuOwnsTheView && !ActiveConfigRequiresVessel);
        }

        /// <summary>
        /// Raise/lift the hold, and only ever write the global flag on an actual edge. The flag
        /// has no ref-counting (the same property that makes the Panini override single-writer),
        /// so an unconditional lift here could stomp the replay camera's hold. Tracking our own
        /// edge keeps this caller symmetric: it can only ever release what it took.
        /// </summary>
        void SetSpeedTunnelHold(bool hold)
        {
            if (hold == _holdingSpeedTunnel) return;

            _holdingSpeedTunnel = hold;
            VesselSpeedTunnel.SetSuppressed(hold);
        }

        // ── Framing Resolution ──────────────────────────────────────────

        /// <summary>
        /// What the active configuration orbits/offsets from: the vessel for every vessel rig,
        /// the CELL CENTRE for the lava lamp. The cell resolves from the wired runtime data, and
        /// falls back to the nearest active cell so the rig works with nothing wired.
        /// </summary>
        Vector3 ResolveFramingCenter(MenuCameraConfigSO config, Transform target)
        {
            if (config != null && config.rigKind == MenuCameraRigKind.LavaLamp)
                return ResolveCellCenter();

            return target ? target.position : _rigPos;
        }

        /// <summary>
        /// The point the camera aims at: the vessel for every vessel rig; for the lava lamp the
        /// cell's crystal (the original behaviour - the respawning crystal is what gives the shot
        /// its slow drift), falling back to the cell centre whenever no crystal exists.
        /// </summary>
        Vector3 ResolveLookPoint(MenuCameraConfigSO config, Transform target, Vector3 framingCenter)
        {
            if (config == null || config.rigKind != MenuCameraRigKind.LavaLamp)
                return target ? target.position : _rigPos + _rigRot * Vector3.forward;

            // TryGetLocalCrystal (not the CrystalTransform property) - the property logs a warning
            // when the cell has no crystal, which at one call per frame would be a log flood.
            if (config.lavaLampAimAtCrystal && _cellData && _cellData.TryGetLocalCrystal(out var crystal) && crystal)
                return crystal.transform.position;

            return framingCenter;
        }

        Vector3 ResolveCellCenter()
        {
            var cellTransform = _cellData ? _cellData.CellTransform : null;
            if (cellTransform) return cellTransform.position;

            var nearest = Cell.FindNearestActiveCell(Vector3.zero);
            return nearest ? nearest.transform.position : Vector3.zero;
        }

        // ── Gameplay Pose (freestyle endpoint) ──────────────────────────

        /// <summary>
        /// The pose <see cref="CustomCameraController.SnapToTarget"/> will compute for this
        /// vessel: follow offset from its <see cref="CameraSettingsSO"/> (dynamic mode uses
        /// (x, y, dynamicMinDistance), mirroring <see cref="CustomCameraController.ApplySettings"/>),
        /// looking at the vessel with its up vector. Blending onto this pose makes the
        /// player-cam handoff seamless by construction.
        /// </summary>
        CameraPose ComputeGameplayPose(Transform target)
        {
            var settings = _gameData?.LocalPlayer?.Vessel?.VesselStatus?.VesselCameraCustomizer?.Settings;
            if (settings)
            {
                _lastGameplayOffset = settings.mode.HasFlag(CameraMode.DynamicCamera)
                    ? new Vector3(settings.followOffset.x, settings.followOffset.y, settings.dynamicMinDistance)
                    : settings.followOffset;
            }

            Vector3 pos = target.position + target.rotation * _lastGameplayOffset;
            if (!SafeLookRotation.TryGet(target.position - pos, target.up, out var rot, this, logError: false))
                rot = _cam.transform.rotation;

            return new CameraPose(pos, rot, GameplayFov());
        }

        float GameplayFov()
        {
            if (_playerCam) return _playerCam.fieldOfView;
            return _cam ? _cam.fieldOfView : 60f;
        }

        // ── Config Switching ────────────────────────────────────────────

        /// <summary>
        /// Switches to another configuration. The change is a glide, never a cut: the rig keeps
        /// its momentum and rides a temporary smoothing boost into the new framing.
        /// </summary>
        public void SetActiveConfig(int index)
        {
            if (_configs is not { Length: > 0 }) return;

            index = Mathf.Clamp(index, 0, _configs.Length - 1);
            if (index == _activeConfigIndex) return;

            _activeConfigIndex = index;
            _switchGlide = 1f;

            // The framing anchor can change with the config (a vessel rig anchors on the vessel,
            // the lava lamp on the cell), so drop the discontinuity history - the anchor swap must
            // not be read as a teleport - and re-seed the orbit phase at the camera's current
            // bearing around the NEW anchor so the switch glides instead of dragging the rig round.
            _hasLastFramingCenter = false;
            SeedOrbitPhase(ResolveFramingCenter(ActiveConfig, ResolveTarget()));
        }

        void TickRandomSwitch(float dt)
        {
            if (!_randomSwitchEnabled || _configs is not { Length: > 1 }) return;

            _switchTimer -= dt;
            if (_switchTimer > 0f) return;
            ResetSwitchTimer();

            int next = _activeConfigIndex;
            for (int guard = 0; guard < 8 && next == _activeConfigIndex; guard++)
                next = Random.Range(0, _configs.Length);

            SetActiveConfig(next);
        }

        void ResetSwitchTimer() =>
            _switchTimer = Random.Range(_randomSwitchIntervalMin,
                                        Mathf.Max(_randomSwitchIntervalMin, _randomSwitchIntervalMax));

        // ── Helpers ─────────────────────────────────────────────────────

        Transform ResolveTarget()
        {
            var followTarget = _gameData?.LocalPlayer?.Vessel?.VesselStatus?.CameraFollowTarget;
            return followTarget ? followTarget : null;
        }

        void SeedRigFromCamera()
        {
            _rigPos = _cam.transform.position;
            _rigRot = _cam.transform.rotation;
            _rigVel = Vector3.zero;
            _rigFov = _cam.fieldOfView;
            _hasLastFramingCenter = false;
            _switchGlide = 1f;

            var target = ResolveTarget();
            SeedOrbitPhase(ResolveFramingCenter(ActiveConfig, target));
            if (target && MenuCameraConfigSO.TryGetYawAnchor(target.rotation, out var yawAnchor))
                _trailYawAnchor = yawAnchor;
        }

        /// <summary>
        /// Starts the orbit at the camera's current bearing around the framing anchor so an orbit
        /// config picks up from where the camera already is instead of dragging it around.
        /// </summary>
        void SeedOrbitPhase(Vector3 framingCenter)
        {
            var config = ActiveConfig;
            _orbitPhaseDeg = config ? config.ComputeOrbitPhaseDegrees(_rigPos - framingCenter) : 0f;
        }

        void ApplyPose(in CameraPose pose)
        {
            _cam.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            _cam.fieldOfView = pose.FieldOfView;
        }

        /// <summary>C2-continuous ease (zero velocity AND acceleration at both ends).</summary>
        static float SmootherStep01(float t) => t * t * t * (t * (6f * t - 15f) + 10f);
    }
}
