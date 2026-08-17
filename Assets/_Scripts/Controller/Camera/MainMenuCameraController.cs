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
    /// main camera directly, framing the LOCAL VESSEL through one of a set of
    /// <see cref="MenuCameraConfigSO"/> configurations (orbit, cinematic trail, tight chase,
    /// top-down pan). The vessel target is structural: configs carry framing only and the
    /// controller resolves <c>GameDataSO.LocalPlayer.Vessel</c> every frame - there is no way
    /// to author a menu camera that points at anything else.
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
        [SerializeField, Tooltip("The set of menu camera configurations. Every configuration frames " +
                                 "the LOCAL VESSEL - the target is structural and cannot be changed " +
                                 "per config. Index 0 (or Initial Config Index) is the boot framing. " +
                                 "Leave empty to fall back to built-in defaults of all four rig kinds.")]
        MenuCameraConfigSO[] _configs;

        [SerializeField, Tooltip("Which configuration to start with.")]
        int _initialConfigIndex = 0;

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

        // Rig simulation state.
        Vector3 _rigPos;
        Vector3 _rigVel;
        Quaternion _rigRot = Quaternion.identity;
        float _rigFov = 60f;
        float _orbitPhaseDeg;
        Quaternion _trailYawAnchor = Quaternion.identity;
        Vector3 _lastTargetPos;
        bool _hasLastTargetPos;
        float _switchGlide;      // 1 → 0 after a config switch; scales the smoothing boost
        float _switchTimer;

        // Freestyle transition state.
        float _blendElapsed;
        float _blendDuration;
        Vector3 _exitLocalPos;   // player-cam pose captured in the vessel's local frame
        Quaternion _exitLocalRot = Quaternion.identity;
        float _exitFov = 60f;
        Vector3 _lastGameplayOffset = DefaultGameplayOffset;

        /// <summary>The configuration currently framing the vessel.</summary>
        public MenuCameraConfigSO ActiveConfig =>
            _configs is { Length: > 0 }
                ? _configs[Mathf.Clamp(_activeConfigIndex, 0, _configs.Length - 1)]
                : null;

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

            // If the local pair was already initialized before this scene object subscribed
            // (OnClientReady raced the scene load), take over now instead of waiting for a
            // re-raise that may never come.
            if (ResolveTarget())
                ActivateMenuCameraImmediate();
        }

        void OnDestroy() => UnsubscribeEvents();

        void LateUpdate()
        {
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
            _hasLastTargetPos = false;
            _switchGlide = 1f;
            SeedOrbitPhase(target.position);
            if (MenuCameraConfigSO.TryGetYawAnchor(target.rotation, out var yawAnchor))
                _trailYawAnchor = yawAnchor;

            _blendElapsed = 0f;
            _blendDuration = ActiveTransitionDuration;
            _state = MenuCameraState.ExitingFreestyle;
        }

        // ── State Updates ───────────────────────────────────────────────

        void UpdateMenuState(Transform target, float dt)
        {
            if (!target) return; // hold the current framing until the vessel exists

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
            Vector3 targetPos = target.position;
            Quaternion targetRot = target.rotation;

            HandleTargetDiscontinuity(targetPos);

            // Track the vessel's heading for yaw-only offsets; hold the last good anchor while
            // the vessel points straight up/down so the framing never flips.
            if (MenuCameraConfigSO.TryGetYawAnchor(targetRot, out var yawAnchor))
                _trailYawAnchor = yawAnchor;
            Quaternion offsetAnchor = config.yawOnlyOffset ? _trailYawAnchor : targetRot;

            if (config.rigKind == MenuCameraRigKind.OrbitVessel)
                _orbitPhaseDeg = Mathf.Repeat(_orbitPhaseDeg + config.orbitDegreesPerSecond * dt, 360f);

            Vector3 desired = config.ComputeDesiredPosition(targetPos, offsetAnchor, _orbitPhaseDeg);

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

            Vector3 lookDirection = targetPos - _rigPos;
            Vector3 upHint = config.ComputeLookUpHint(targetRot);
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
        /// A follow-target jump far beyond flight speed is a teleport (spawn park, SetPose
        /// home, vessel swap) - translate the rig along with it so relative framing is
        /// preserved, instead of swooping across the arena to "catch" the vessel.
        /// </summary>
        void HandleTargetDiscontinuity(Vector3 targetPos)
        {
            if (_hasLastTargetPos)
            {
                Vector3 delta = targetPos - _lastTargetPos;
                if (delta.sqrMagnitude > TeleportStep * TeleportStep)
                {
                    _rigPos += delta;
                    _rigVel = Vector3.zero;
                }
            }

            _lastTargetPos = targetPos;
            _hasLastTargetPos = true;
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
            if (_hasLastTargetPos)
                SeedOrbitPhase(_lastTargetPos);
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
            _hasLastTargetPos = false;
            _switchGlide = 1f;

            var target = ResolveTarget();
            if (target)
            {
                SeedOrbitPhase(target.position);
                if (MenuCameraConfigSO.TryGetYawAnchor(target.rotation, out var yawAnchor))
                    _trailYawAnchor = yawAnchor;
            }
        }

        /// <summary>
        /// Starts the orbit at the camera's current bearing around the vessel so an orbit
        /// config picks up from where the camera already is instead of dragging it around.
        /// </summary>
        void SeedOrbitPhase(Vector3 targetPos)
        {
            Vector3 flat = Vector3.ProjectOnPlane(_rigPos - targetPos, Vector3.up);
            _orbitPhaseDeg = flat.sqrMagnitude < 1e-4f
                ? 0f
                : Vector3.SignedAngle(Vector3.back, flat, Vector3.up);
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
