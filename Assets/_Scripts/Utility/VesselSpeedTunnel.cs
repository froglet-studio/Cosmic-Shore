using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The speed-tunnel quasi dolly zoom: as the local pilot's vessel gets faster, the
    /// gameplay camera's field of view NARROWS below its home value while the URP Panini
    /// projection distance falls below the profile's own baseline with it. The tightening
    /// FOV magnifies the frame centre (telephoto push-in) as the Panini compression relaxes
    /// toward rectilinear — tunnel vision, with no camera-distance change at all.
    ///
    /// PLATFORM LAW (Docs/SPEED_TUNNEL.md). Speed is a property of every vessel, so the
    /// visual language for speed belongs to every vessel. This is NOT a feature a vessel or
    /// a game mode may choose, and it must not be possible to author one in which it is off.
    /// Three structural properties make that true rather than aspirational:
    ///
    ///   1. It is bound in <c>VesselController.Initialize</c> under <c>IPlayer.IsLocalPilot</c>
    ///      — the one method every vessel must call to become a player's vessel, on every
    ///      spawn path (single-player, multiplayer, menu autopilot, runtime swap). There is
    ///      nothing per-vessel and nothing per-scene to wire, and therefore nothing to forget.
    ///   2. It is a STATIC driver with ONE global publisher, not a per-vessel component. That
    ///      is not merely tidier: <c>PostProcessingManager.SetSpeedTunnelPanini</c> is a
    ///      single global override with no ref-counting, so N per-vessel writers race it and
    ///      an outgoing vessel's teardown stomps the incoming vessel's value mid-swap. One
    ///      driver cannot race itself.
    ///   3. The mapping is ABSOLUTE. <c>SpeedTunnelConfigSO.Effect01</c> is one function of
    ///      measured speed shared by the whole fleet, so THE SAME SPEED ON ANY VESSEL LOOKS
    ///      THE SAME, and a faster vessel reaches deeper into the tunnel because it is
    ///      genuinely faster. There is no per-vessel number anywhere — which is also why
    ///      there is nothing a vessel could author to escape.
    ///
    /// The drive signal is measured <see cref="IVesselStatus.Speed"/>, never boost state, so
    /// the effect follows every speed source that exists or will exist — trigger boosts,
    /// constant-acceleration ramps, skim charges, throttle modifiers, crystal buffs — with
    /// nothing to bind and nothing to keep in step.
    ///
    /// Local pilot only: a remote or AI vessel never touches the local camera or the global
    /// post volume. The only sanctioned hold is <see cref="SetSuppressed"/>, used by exactly
    /// one caller (the manual replay camera), and it is a HOLD, not an opt-out — the vessel
    /// binding survives it.
    /// </summary>
    public static class VesselSpeedTunnel
    {
        const string ConfigResourcePath = "SpeedTunnelConfig";

        static IVesselStatus _target;
        static Transform _targetKey;
        static bool _suppressed;

        static float _effect01;
        static bool _applied;
        static Camera _appliedCamera;
        static float _homeFov;

        static SpeedTunnelConfigSO _config;
        static bool _configResolved;

        static bool _warnedNoPostProcessing;
        static bool _warnedNoCameraManager;
        static bool _warnedForeignController;

        /// <summary>The vessel the tunnel is currently driven by, or null when it is off.</summary>
        public static IVesselStatus Target => _target;

        /// <summary>Current effect strength in [0, 1] — 0 while the law is idle.</summary>
        public static float Effect01 => _effect01;

        /// <summary>True while the tunnel is holding a camera and/or the Panini override.</summary>
        public static bool IsActive => _applied;

        /// <summary>True while <see cref="SetSuppressed"/> is holding the tunnel closed.</summary>
        public static bool IsSuppressed => _suppressed;

        /// <summary>
        /// Tuning. Falls back to the SO's own defaults when no <c>Resources/SpeedTunnelConfig</c>
        /// asset exists, so the law holds with no authoring at all.
        /// </summary>
        public static SpeedTunnelConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<SpeedTunnelConfigSO>(ConfigResourcePath);
                    if (_config == null)
                        _config = ScriptableObject.CreateInstance<SpeedTunnelConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>
        /// Drive the tunnel from the local pilot's vessel. The ONLY caller is
        /// <c>VesselController.Initialize</c> under <c>IPlayer.IsLocalPilot</c> — deliberately
        /// the universal vessel entry point rather than any camera, mode, or per-vessel
        /// component, so the law cannot be omitted by authoring. Do not add call sites that
        /// give a mode a way to point it somewhere else, and do not reintroduce a per-vessel
        /// component that writes the camera or the Panini override directly.
        /// </summary>
        public static void SetTarget(IVesselStatus status, Transform key)
        {
            _target = status;
            _targetKey = key;
        }

        /// <summary>
        /// Stop driving, but only if <paramref name="key"/> is still the vessel in force — so a
        /// late teardown from an outgoing vessel cannot cancel the incoming one's binding
        /// during a swap, where OnDestroy runs AFTER the new vessel's Initialize.
        /// </summary>
        public static void ClearTarget(Transform key)
        {
            if (_targetKey == key)
                ClearTarget();
        }

        /// <summary>Unconditional off (scene teardown, returning to a vessel-less camera).</summary>
        public static void ClearTarget()
        {
            _target = null;
            _targetKey = null;
        }

        /// <summary>
        /// Temporarily hold the tunnel closed WITHOUT unbinding the vessel. The one sanctioned
        /// caller is <c>CameraManager</c>'s manual replay camera: a broadcast vantage is posed
        /// by hand and its framing math reads the camera's field of view
        /// (<c>AstroLeagueGoalReplay</c>), so a live FOV write would both fight the pose and
        /// silently mis-fit the shot. Symmetric — <c>RestoreGameplayCamera</c> lifts it,
        /// unconditionally and before its own early return, so a replay that finishes after its
        /// scene tore down cannot latch the hold on for the rest of the session.
        ///
        /// The hold is IMMEDIATE: <see cref="Tick"/> tests it at the point of application, not
        /// only when computing the target. Unlike the occlusion corridor this driver carries
        /// smoothing state, so a target-only hold would keep writing a decaying effect — onto
        /// the very replay camera it exists to leave alone, since the same caller cuts to it.
        /// </summary>
        public static void SetSuppressed(bool suppressed) => _suppressed = suppressed;

        /// <summary>Drop the cached config so the next frame re-reads the asset (editor tooling).</summary>
        public static void InvalidateConfig()
        {
            _config = null;
            _configResolved = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallDriver()
        {
            // Statics survive play-mode exit in the editor, so a stale binding from the previous
            // session would drive a camera around a vessel that no longer exists.
            _target = null;
            _targetKey = null;
            _suppressed = false;
            _effect01 = 0f;
            _applied = false;
            _appliedCamera = null;
            _warnedNoPostProcessing = false;
            _warnedNoCameraManager = false;
            _warnedForeignController = false;

            // HideInHierarchy (NOT HideAndDontSave — that exempts the object from play-mode-exit
            // cleanup), the same pattern PrismOcclusionCorridor's publisher uses.
            var go = new GameObject("[VesselSpeedTunnel]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Driver>();
        }

        /// <summary>
        /// One frame of the law. The only caller is <see cref="Driver"/>; it is public so the
        /// effect can be stepped deterministically from a test or a diagnostic without a
        /// play-mode session (nothing does today — the shipped tests cover the pure math and
        /// the source-level gates instead, since the camera and post volume this touches only
        /// exist in play mode).
        /// </summary>
        public static void Tick(float deltaTime)
        {
            var config = Config;

            float target01 = 0f;
            if (!_suppressed && config.Enabled && IsTargetLive())
                target01 = config.Effect01(_target.Speed);

            _effect01 = Mathf.Lerp(_effect01, target01,
                                   1f - Mathf.Exp(-config.Responsiveness * Mathf.Max(0f, deltaTime)));

            // Suppression is tested HERE, at the point of application, not only where target01 is
            // computed — the hold has to be IMMEDIATE. This driver carries smoothing state, so
            // zeroing the target alone would let the effect decay for ~0.5s (longer under a
            // slow-motion timescale) while still writing every frame. Worse, the same call that
            // suppresses also CUTS to the replay camera, so those writes would land on the
            // hand-posed broadcast camera whose field of view the replay just read to fit its
            // shot — precisely the outcome the hold exists to prevent.
            if (!_suppressed && _effect01 > 0.001f)
                Apply(config, _effect01);
            else if (_applied)
                Release();
        }

        /// <summary>
        /// The bound vessel is still a live, active object AND still owns a Player. A vessel
        /// that has been despawned mid-swap must stop driving immediately rather than holding
        /// the camera at its last speed.
        /// </summary>
        static bool IsTargetLive()
        {
            if (_target == null || !_targetKey) return false;
            if (!_targetKey.gameObject.activeInHierarchy) return false;
            return _target.Player != null;
        }

        static void Apply(SpeedTunnelConfigSO config, float t)
        {
            var cam = ResolveGameplayCamera();

            // The active camera can change mid-effect (end cam, death cam, menu Cinemachine).
            // Restore the one we actually pushed before touching the new one, so no camera is
            // left with a baked-in narrowed FOV and no other camera gets our restore write.
            if (_appliedCamera != null && _appliedCamera != cam)
                RestoreFov(_appliedCamera);

            if (cam != null && !cam.orthographic)
            {
                // Home = whatever FOV the camera is ACTUALLY running with the moment the effect
                // takes over. Never a constant and never a settings default — anchoring to
                // either reads as a snap onto foreign values the instant the tunnel engages.
                if (cam != _appliedCamera)
                    _homeFov = cam.fieldOfView;

                cam.fieldOfView = config.FovFor(_homeFov, t);
                _appliedCamera = cam;
            }
            else
            {
                _appliedCamera = null;
            }

            var post = PostProcessing;
            if (post != null)
                post.SetSpeedTunnelPanini(config.PaniniOffsetFor(t));
            else
                WarnNoPostProcessing();

            _applied = true;
        }

        static void Release()
        {
            _applied = false;

            if (_appliedCamera != null)
                RestoreFov(_appliedCamera);
            _appliedCamera = null;

            var post = PostProcessing;
            if (post != null)
                post.SetSpeedTunnelPanini(0f);
        }

        static void RestoreFov(Camera cam)
        {
            if (!cam.orthographic)
                cam.fieldOfView = _homeFov;
        }

        /// <summary>
        /// The player's FOV setting can change WHILE the tunnel is engaged
        /// (<c>CameraManager.ApplyCameraGraphicsSettings</c> writes the settings value straight
        /// onto every managed camera). Without this, the effect would keep narrowing from its
        /// stale home and then restore the OLD value on release — silently discarding the
        /// player's new setting until the next camera change.
        /// </summary>
        static void OnHomeFieldOfViewChanged(float fov)
        {
            _homeFov = fov;
            // Re-apply immediately so the frame the slider moves shows the new home, not the old.
            if (_applied && _appliedCamera != null && !_appliedCamera.orthographic)
                _appliedCamera.fieldOfView = Config.FovFor(_homeFov, _effect01);
        }

        static PostProcessingManager PostProcessing => PostProcessingManager.Instance;

        /// <summary>
        /// The live gameplay follow camera, or null when Cinemachine is driving the view (the
        /// menu). There the FOV half simply does not apply and the Panini half carries the
        /// effect alone — a designed state, not a fault, so it is deliberately NOT warned about.
        /// </summary>
        static Camera ResolveGameplayCamera()
        {
            var manager = CameraManager.Instance;
            if (manager == null)
            {
                WarnNoCameraManager();
                return null;
            }

            var controller = manager.GetActiveController();
            if (controller == null)
                return null; // Cinemachine (menu) — expected.

            if (controller is CustomCameraController custom)
                return custom.Camera;

            WarnForeignController(controller);
            return null;
        }

        // Fail loud, once per session per fault — a platform law that quietly does nothing is
        // the failure mode the whole enforcement ladder exists to prevent. Each message names
        // the fix rather than just the symptom.

        static void WarnNoPostProcessing()
        {
            if (_warnedNoPostProcessing) return;
            _warnedNoPostProcessing = true;
            CSDebug.LogWarning(
                "[VesselSpeedTunnel] No PostProcessingManager — the Panini half of the speed " +
                "tunnel is inert. It lives on PostProcessing.prefab in the Bootstrap scene; a " +
                "scene entered directly (tool scenes, play-from-scene) has no instance.");
        }

        static void WarnNoCameraManager()
        {
            if (_warnedNoCameraManager) return;
            _warnedNoCameraManager = true;
            CSDebug.LogWarning(
                "[VesselSpeedTunnel] No CameraManager — the FOV half of the speed tunnel is " +
                "inert. Enter play from the Bootstrap scene.");
        }

        static void WarnForeignController(ICameraController controller)
        {
            if (_warnedForeignController) return;
            _warnedForeignController = true;
            CSDebug.LogWarning(
                $"[VesselSpeedTunnel] Active camera controller is {controller.GetType().Name}, " +
                "which exposes no Camera — the FOV half of the speed tunnel cannot apply. Any " +
                "gameplay camera controller must derive from CustomCameraController or expose " +
                "its Camera for the platform law to reach it.");
        }

        /// <summary>
        /// LateUpdate so the tunnel is applied after the vessel has moved, after the transformer
        /// has written this frame's Speed, and after the camera controllers have posed
        /// themselves — the same reasoning as the occlusion corridor's publisher.
        /// </summary>
        sealed class Driver : MonoBehaviour
        {
            void OnEnable() => DisplayGraphicsSettings.OnFieldOfViewChanged += OnHomeFieldOfViewChanged;

            void LateUpdate() => Tick(Time.deltaTime);

            void OnDisable()
            {
                DisplayGraphicsSettings.OnFieldOfViewChanged -= OnHomeFieldOfViewChanged;
                if (_applied) Release();
                _effect01 = 0f;
            }
        }
    }
}
