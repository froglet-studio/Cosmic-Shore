using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's <b>scope</b>. Hold the left trigger: the view drops into the cockpit
    /// (<see cref="VesselFirstPersonView"/>) and magnifies with the trigger's own depth, and the
    /// right trigger stops being the cloak and becomes the sniper shot
    /// (<see cref="SniperShotActionExecutor"/>). Release and everything returns.
    ///
    /// <para><b>This executor is the AUTHORITY on "am I scoped"</b>, and that is why
    /// <see cref="IsScoped"/> is maintained on EVERY peer rather than only where the camera is.
    /// <c>R_VesselActionHandler</c> round-trips every press and release through the server, so
    /// <see cref="Engage"/> and <see cref="Release"/> already run on every machine; the sniper
    /// resolves its own shot on every machine too, and both it and the cloak read this flag to
    /// decide whether they may fire. Had the flag been kept only for the local pilot, a remote
    /// replica would have run the cloak and the shot on the same press.</para>
    ///
    /// <para>Only the CAMERA half is local-pilot-only. A camera is a thing one machine has.</para>
    ///
    /// <para><b>Element → parameter: SPACE sets the magnification</b> — the authored field of view
    /// at full zoom divided by Space's multiplier, floored so the scope can never narrow into a
    /// soda straw. Read at USE time (every frame while held), never cached at init, so a crystal
    /// collected mid-scope widens the reach immediately.</para>
    ///
    /// <para><b>Space 5 — "Steady Eye".</b> Below the upgrade the zoom BLEEDS OFF as the pilot
    /// turns: hauling the stick over eases the magnification back out, so a scoped Serpent is
    /// committed to a line and has to settle before it can take a long shot. At Space 5 the bleed
    /// is gone and the scope holds its magnification through any manoeuvre. Gated on
    /// <c>IsUpgradeActive(Element.Space)</c> — the replicated unlock bit — rather than a raw local
    /// level read, because it is read on every peer.</para>
    ///
    /// <para><b>It touches no mass and no speed.</b> The whole ability is a camera pose, a field
    /// of view and a flag. A pilot who wants to hold still to take a shot already has a way: the
    /// Serpent's own stationary mode on the face button. Nothing here duplicates it.</para>
    /// </summary>
    public sealed class SniperScopeActionExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Wired directly rather than resolved from the binding maps. A lazy sweep is " +
                 "correct for an ability that HAS an input and this one does, but a direct " +
                 "reference makes a missing wire visible in the inspector instead of silently " +
                 "falling back to field initializers.")]
        [SerializeField] private SniperScopeActionSO config;

        [Header("Steady Eye (below Space 5)")]
        [Tooltip("Fraction of the zoom that survives at FULL stick deflection before the Space-5 " +
                 "upgrade. 1 disables the bleed entirely; the upgrade sets it to 1 at runtime.")]
        [SerializeField, Range(0.05f, 1f)] private float steadyZoomFloor = 0.35f;

        IVesselStatus _status;
        SniperScopeActionSO _activeSo;

        ActionExecutorRegistry _registry;
        SniperShotActionExecutor _shot;
        SniperScopeOverlay _overlay;

        bool _engaged;
        float _zoom01;   // the APPLIED zoom, chasing the trigger

        /// <summary>
        /// True while the pilot is holding the scope. Maintained on every peer — see the class
        /// summary. The sniper shot and the cloak both gate on it.
        /// </summary>
        public bool IsScoped => _engaged;

        /// <summary>The applied magnification, 0..1. Read by the HUD.</summary>
        public float Zoom01 => _zoom01;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;

            // GetComponentInParent, NOT GetComponent: the registry lives on the "ShipActions"
            // container and each executor sits on a CHILD of it. includeInactive so a vessel
            // initialized while deactivated still resolves.
            _registry = GetComponentInParent<ActionExecutorRegistry>(true);
            if (_shot == null && _registry != null) _shot = _registry.Get<SniperShotActionExecutor>();

            // A re-init hands this component to a different pilot (the vessel swap, the Cellular
            // Duel ownership swap). Whatever the last pilot was holding is not this one's.
            ReleaseInternal();
        }

        void OnDestroy()
        {
            // The overlay is a GameObject of its own, so it does not die with the vessel by
            // parentage - and the PIP behind it owns a RenderTexture, which is a leak nothing
            // reports if it outlives its owner.
            if (_overlay != null) { _overlay.Dispose(); _overlay = null; }
        }

        void OnDisable()
        {
            // Unconditional and idempotent: a scoped vessel that is despawned, pooled or
            // deactivated must hand the camera back, and OnDisable is the one place that runs
            // however the vessel goes away. The input-pause case is covered a layer up by
            // R_VesselActionHandler.ReleaseHeldInputs, which sends a real release.
            ReleaseInternal();
        }

        public void Engage(SniperScopeActionSO so, IVesselStatus status)
        {
            if (so == null) return;
            _activeSo = so;
            if (_status == null) _status = status;

            _engaged = true;
            // Start from no magnification and ramp in, so the scope reads as being RAISED rather
            // than as the world snapping. Continuity of existence, applied to a view.
            _zoom01 = 0f;
            PushView();
        }

        public void Release(SniperScopeActionSO so, IVesselStatus status) => ReleaseInternal();

        void ReleaseInternal()
        {
            bool was = _engaged;
            _engaged = false;
            _zoom01 = 0f;
            _activeSo = null;
            if (_overlay != null) _overlay.Hide();
            if (was) PushView();
        }

        void Update()
        {
            if (!_engaged) return;

            float target = ResolveZoomTarget();
            float response = ResolvedConfig != null ? ResolvedConfig.ZoomResponse : 6f;

            // Frame-rate independent chase. On a pad this is nearly a pass-through of an already
            // continuous value; on mouse/keyboard, whose trigger reports 0 or 1, it IS the zoom
            // ramp — one mechanism, so no device needs a branch of its own.
            _zoom01 = Mathf.MoveTowards(_zoom01, target, response * Time.deltaTime);

            PushView();
            DrawOverlay();
        }

        /// <summary>
        /// The scope's own readout — the cone-sized reticle, the recharge arc and the flight PIP.
        ///
        /// <para>LOCAL PILOT ONLY, for the same reason as <see cref="PushView"/>: a screen is a
        /// thing one machine has. It is built lazily on the first frame a local pilot actually
        /// holds the scope, so a vessel that is never scoped — every AI, every remote replica —
        /// costs nothing at all.</para>
        ///
        /// <para>It carries the weapon's readiness because the fleet's ability lockup cannot: the
        /// Serpent binds none of its four ability icons, so the cooldown veil pushed into
        /// <c>SerpentVesselHUDController</c> has no icon to sit on. Both are driven, so the day
        /// that vessel's icons are authored the row lights up and this stays correct.</para>
        /// </summary>
        void DrawOverlay()
        {
            if (_status?.Player == null || !_status.Player.IsLocalPilot) return;
            if (_shot == null && _registry != null) _shot = _registry.Get<SniperShotActionExecutor>();
            if (_shot == null) return;

            // An explicit == null, NOT ??=: the null-coalescing operators compare by REFERENCE
            // and so cannot see a destroyed UnityEngine.Object, which would leave this holding a
            // dead overlay forever.
            if (_overlay == null) _overlay = SniperScopeOverlay.Create();
            _overlay.Tick(_shot.ConeHalfAngleDegrees, _shot.CooldownRemaining01, _shot.TracerColour);
        }

        SniperScopeActionSO ResolvedConfig => _activeSo != null ? _activeSo : config;

        /// <summary>
        /// Where the pilot is asking the zoom to sit this frame: their trigger depth, deadzoned,
        /// and — below Space 5 — bled off by how hard they are turning.
        /// </summary>
        float ResolveZoomTarget()
        {
            var so = ResolvedConfig;
            var input = _status?.InputStatus;
            if (so == null || input == null) return 0f;

            float depth = Mathf.Clamp01(input.LeftTriggerAnalog);
            float dead = so.ZoomDeadzone;
            if (depth <= dead) return 0f;
            depth = Mathf.InverseLerp(dead, 1f, depth);

            return depth * SteadyFactor(input);
        }

        /// <summary>
        /// 1 while the pilot is flying straight, falling toward <see cref="steadyZoomFloor"/> as
        /// they haul the stick over — and pinned at 1 outright once Space 5 is unlocked.
        ///
        /// <para>The stick rather than the vessel's measured turn rate, deliberately: the stick is
        /// the pilot's INTENT and is the same number on every device, where a measured rate also
        /// moves under an impact slow, a danger-prism hit or a blast knockback — none of which the
        /// pilot asked for, and all of which would read as the scope breaking on its own.</para>
        /// </summary>
        float SteadyFactor(IInputStatus input)
        {
            if (IsSteadyEyeUnlocked) return 1f;

            // Single-stick hull: the Serpent flies on the left stick alone
            // (IsSingleStickControls), so this is the whole of its turning input.
            float stick = Mathf.Clamp01(input.EasedLeftJoystickPosition.magnitude);
            return Mathf.Lerp(1f, steadyZoomFloor, stick);
        }

        bool IsSteadyEyeUnlocked
        {
            get
            {
                var elemental = _status?.ElementalAbilityHandler;
                return elemental != null && elemental.IsUpgradeActive(Element.Space);
            }
        }

        /// <summary>
        /// Hand the view the vantage and the magnification together. LOCAL PILOT ONLY: a camera
        /// is a thing one machine has, and <see cref="VesselFirstPersonView"/> is a static bound
        /// to whichever vessel that machine's pilot is flying. A remote replica running this
        /// method would seat the local player in a stranger's cockpit.
        /// </summary>
        void PushView()
        {
            if (_status?.Player == null || !_status.Player.IsLocalPilot) return;

            VesselFirstPersonView.SetEngaged(_engaged, _zoom01, ResolveFieldOfViewAtFullZoom());
        }

        /// <summary>
        /// The field of view at FULL magnification, after Space. Space's multiplier is a zoom
        /// DEPTH, so it divides the angle — more Space, narrower scope, further sight — and the
        /// result is floored by the authored minimum so no element level can narrow the view into
        /// something unflyable.
        /// </summary>
        float ResolveFieldOfViewAtFullZoom()
        {
            var so = ResolvedConfig;
            if (so == null) return 22f;

            // minMul 1 rather than the default 0.25: Space may only ever ADD reach here. A
            // Space-starved Serpent keeps the authored scope instead of having it widened into
            // uselessness, which is the deficit band doing something the design never asked for.
            float depth = ElementalScaling.Multiplier(
                _status, Element.Space, atFull: so.ZoomDepthAtFullSpace, minMul: 1f);

            return Mathf.Max(so.MinFieldOfView, so.FieldOfViewAtFullZoom / Mathf.Max(0.0001f, depth));
        }
    }
}
