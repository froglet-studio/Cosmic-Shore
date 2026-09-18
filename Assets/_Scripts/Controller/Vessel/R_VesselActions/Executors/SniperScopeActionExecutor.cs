using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's <b>scope</b>. Hold the left trigger: a magnified forward view opens in a
    /// window beside the flight view, and the right trigger stops being the cloak and becomes the
    /// sniper shot (<see cref="SniperShotActionExecutor"/>). Release and the window closes.
    ///
    /// <para><b>The pilot's own view never moves.</b> The first cut put the whole screen in the
    /// cockpit and magnified it, which read as nauseating, so the magnified picture now lives in
    /// the scope WINDOW (<c>SniperScopeOverlay</c> → <c>ScopePipView</c>) and the flight view is
    /// untouched. That inversion retired the main-camera cockpit outright — there is no longer a
    /// <c>VesselFirstPersonView</c>, and <c>CustomCameraController</c> no longer carries a
    /// first-person flag. <c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 4.</para>
    ///
    /// <para><b>The zoom is a pure function of the trigger's own depth.</b> Nothing else reaches
    /// it — not the stick (the retired "Steady Eye" bleed), not the vessel's speed (the retired
    /// read of the speed-tunnel-narrowed camera). The rule this leaves behind is general:
    /// <i>a magnified view is a lever on every input that reaches it</i>, so a zoom that is a
    /// function of anything but the zoom control amplifies motion the pilot never asked for, and
    /// the pilot has no way to tell which half of what they are seeing they caused.</para>
    ///
    /// <para><b>This executor is the AUTHORITY on "am I scoped"</b>, and that is why
    /// <see cref="IsScoped"/> is maintained on EVERY peer rather than only where the camera is.
    /// <c>R_VesselActionHandler</c> round-trips every press and release through the server, so
    /// <see cref="Engage"/> and <see cref="Release"/> already run on every machine; the sniper
    /// resolves its own shot on every machine too, and both it and the cloak read this flag to
    /// decide whether they may fire. Had the flag been kept only for the local pilot, a remote
    /// replica would have run the cloak and the shot on the same press.</para>
    ///
    /// <para>Only the WINDOW is local-pilot-only. A screen is a thing one machine has.</para>
    ///
    /// <para><b>Element → parameter: SPACE sets the magnification</b> — the authored field of view
    /// at full zoom divided by Space's multiplier, floored so the scope can never narrow into a
    /// soda straw. Read at USE time (every frame while held), never cached at init, so a crystal
    /// collected mid-scope widens the reach immediately.</para>
    ///
    /// <para><b>Space 5 — "Deep Focus".</b> One more magnification step: the upgrade multiplies
    /// the zoom depth AND divides the floor by the same number, so the extra reach is actually
    /// reachable rather than running straight into a ceiling the upgrade cannot pass. Gated on
    /// <c>IsUpgradeActive(Element.Space)</c> — the replicated unlock bit — rather than a raw local
    /// level read, because the same value sizes the reticle, which is drawn from it.</para>
    ///
    /// <para><b>It touches no mass and no speed.</b> The whole ability is a window, a field of
    /// view and a flag. A pilot who wants to hold still to take a shot already has a way: the
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

        IVesselStatus _status;
        SniperScopeActionSO _activeSo;

        ActionExecutorRegistry _registry;
        SniperShotActionExecutor _shot;
        SniperScopeOverlay _overlay;

        bool _engaged;
        float _zoom01;   // the APPLIED zoom, chasing the trigger
        Transform _hull; // resolved once by ResolveHull; see why it is not ShipTransform

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
            // Duel ownership swap). Whatever the last pilot was holding is not this one's, and
            // the hull it was seated on may not be either.
            _hull = null;
            ReleaseInternal();
        }

        void OnDestroy()
        {
            // The overlay is a GameObject of its own, so it does not die with the vessel by
            // parentage - and the scope window behind it owns a RenderTexture, which is a leak
            // nothing reports if it outlives its owner.
            if (_overlay != null) { _overlay.Dispose(); _overlay = null; }
        }

        void OnDisable()
        {
            // Unconditional and idempotent: a scoped vessel that is despawned, pooled or
            // deactivated must close its window, and OnDisable is the one place that runs however
            // the vessel goes away. The input-pause case is covered a layer up by
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
            // than as the window snapping to a magnification the pilot has not asked for yet.
            _zoom01 = 0f;
        }

        public void Release(SniperScopeActionSO so, IVesselStatus status) => ReleaseInternal();

        void ReleaseInternal()
        {
            _engaged = false;
            _zoom01 = 0f;
            _activeSo = null;
            if (_overlay != null) _overlay.Hide();
        }

        void Update()
        {
            if (!_engaged) return;

            float target = ResolveZoomTarget();
            float response = ResolvedConfig != null ? ResolvedConfig.ZoomResponse : 12f;

            // Frame-rate independent chase, and the ONE term here that is not the raw trigger.
            // It is a function of the trigger's own history rather than of the world: on a pad it
            // is nearly a pass-through of an already continuous value, and on mouse/keyboard,
            // whose trigger reports 0 or 1, it IS the zoom ramp — one mechanism, so no device
            // needs a branch of its own.
            _zoom01 = Mathf.MoveTowards(_zoom01, target, response * Time.deltaTime);

            DrawOverlay();
        }

        /// <summary>
        /// The scope's whole readout — the magnified window, the cone-sized reticle inside it and
        /// the recharge ring around it.
        ///
        /// <para>LOCAL PILOT ONLY: a screen is a thing one machine has. It is built lazily on the
        /// first frame a local pilot actually holds the scope, so a vessel that is never scoped —
        /// every AI, every remote replica — costs nothing at all.</para>
        ///
        /// <para>It carries the weapon's readiness IN ADDITION to the fleet's ability lockup
        /// rather than instead of it: the lockup's veil now draws on a locked card too, so the
        /// Serpent's recharge reads on the HUD row whether or not the scope is up, and this
        /// duplicate is the one a pilot looking down the scope can see without leaving it.</para>
        /// </summary>
        void DrawOverlay()
        {
            if (_status?.Player == null)
            {
                SniperScopeDiagnostics.Decline(SniperScopeDiagnostics.Reason.NoPlayer);
                return;
            }

            if (!_status.Player.IsLocalPilot)
            {
                SniperScopeDiagnostics.Decline(SniperScopeDiagnostics.Reason.NotLocalPilot);
                return;
            }

            if (_shot == null && _registry != null) _shot = _registry.Get<SniperShotActionExecutor>();
            if (_shot == null)
            {
                SniperScopeDiagnostics.Decline(SniperScopeDiagnostics.Reason.NoShotExecutor);
                return;
            }

            var ship = ResolveHull();
            if (ship == null)
            {
                SniperScopeDiagnostics.Decline(SniperScopeDiagnostics.Reason.NoHull);
                return;
            }

            // An explicit == null, NOT ??=: the null-coalescing operators compare by REFERENCE
            // and so cannot see a destroyed UnityEngine.Object, which would leave this holding a
            // dead overlay forever.
            if (_overlay == null) _overlay = SniperScopeOverlay.Create();

            _overlay.Tick(ship, ResolveScopeFieldOfView(), _shot.ConeHalfAngleDegrees,
                          _shot.CooldownRemaining01, _shot.TracerColour);
        }

        /// <summary>
        /// The hull the scope's eye sits in front of, resolved ONCE and cached.
        ///
        /// <para>It does not read <c>IVesselStatus.ShipTransform</c>, which is
        /// <c>Vessel.Transform</c> with no guard: on a vessel whose <c>_shipInstance</c> is
        /// unreferenced, <c>VesselStatus.Vessel</c> logs an error and returns null, so that
        /// property THROWS rather than answering null — and a throw here takes the whole
        /// instrument with it, not just the window, because <c>Tick</c> is downstream of this
        /// line. The fallback to this executor's own root is deliberate for the same reason: the
        /// eye's pose only needs a hull-shaped transform, and a scope that is slightly
        /// mis-seated is worth far more than one that does not exist.</para>
        ///
        /// <para>Cached because <c>VesselStatus.Vessel</c>'s null path logs an ERROR, which on a
        /// per-frame read is the console spam the logging convention exists to prevent. Re-resolved
        /// only while the answer is still null, so a vessel that wires up late is picked up.</para>
        /// </summary>
        Transform ResolveHull()
        {
            if (_hull != null) return _hull;
            _hull = _status?.Vessel?.Transform;
            if (_hull == null) _hull = transform.root;
            return _hull;
        }

        SniperScopeActionSO ResolvedConfig => _activeSo != null ? _activeSo : config;

        /// <summary>
        /// Where the pilot is asking the zoom to sit this frame: their trigger depth, deadzoned,
        /// and nothing else at all. See the class summary on why there is nothing else.
        /// </summary>
        float ResolveZoomTarget()
        {
            var so = ResolvedConfig;
            var input = _status?.InputStatus;
            if (so == null || input == null) return 0f;

            float depth = Mathf.Clamp01(input.LeftTriggerAnalog);
            float dead = so.ZoomDeadzone;
            if (depth <= dead) return 0f;

            return Mathf.InverseLerp(dead, 1f, depth);
        }

        bool IsDeepFocusUnlocked
        {
            get
            {
                var elemental = _status?.ElementalAbilityHandler;
                return elemental != null && elemental.IsUpgradeActive(Element.Space);
            }
        }

        /// <summary>
        /// The window's field of view this frame: the wide authored value with the scope merely
        /// raised, narrowing to <see cref="ResolveFieldOfViewAtFullZoom"/> as the trigger goes
        /// down. The wide end is AUTHORED rather than read off the live gameplay camera, because
        /// that camera is narrowed by the speed tunnel and reading it back is exactly how the
        /// magnification became a function of how fast the pilot was going.
        /// </summary>
        public float ResolveScopeFieldOfView()
        {
            var so = ResolvedConfig;
            if (so == null) return 60f;
            return Mathf.Lerp(so.UnscopedFieldOfView, ResolveFieldOfViewAtFullZoom(), _zoom01);
        }

        /// <summary>
        /// The field of view at FULL magnification, after Space. Space's multiplier is a zoom
        /// DEPTH, so it divides the angle — more Space, narrower scope, further sight — and the
        /// result is floored by the authored minimum so no element level can narrow the view into
        /// something unreadable. Deep Focus (Space 5) multiplies the depth and divides the floor
        /// by the same number, so the upgrade moves the ceiling with the dial.
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

            float upgrade = IsDeepFocusUnlocked ? Mathf.Max(1f, so.UpgradeZoomDepthMultiplier) : 1f;
            depth *= upgrade;

            float floor = so.MinFieldOfView / upgrade;
            return Mathf.Max(floor, so.FieldOfViewAtFullZoom / Mathf.Max(0.0001f, depth));
        }
    }
}
