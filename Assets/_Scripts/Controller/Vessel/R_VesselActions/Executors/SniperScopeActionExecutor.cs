using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
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
    /// <para><b>No element parameter</b> (Garrett, 2026-10-30): the scope is the Serpent's CHARGE
    /// card, Charge's number is the sniper's recovery, and the zoom is one authored magnification
    /// at every level.</para>
    ///
    /// <para><b>Charge 5 — "Hunter's Sight".</b> While the scope is up, every rival in range
    /// leaves short-lived afterimages (<see cref="ScopeAfterimages"/>), and a sniper round through
    /// one strips the real pilot. Gated on <c>IsUpgradeActive(Element.Charge)</c>, the replicated
    /// unlock bit. The afterimages are recorded only where the strip is decided (the Serpent's
    /// owner) and drawn only for the local pilot.</para>
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

        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        SniperScopeActionSO _activeSo;

        ActionExecutorRegistry _registry;
        SniperShotActionExecutor _shot;
        SniperScopeOverlay _overlay;

        readonly ScopeAfterimages _afterimages = new();

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

        /// <summary>Hunter's Sight's live afterimages; the sniper tests its round against them.</summary>
        public ScopeAfterimages Afterimages => _afterimages;

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
            _afterimages.Dispose();
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
            _afterimages.Clear();
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

            TickAfterimages();
            DrawOverlay();
        }

        /// <summary>
        /// The scope's whole readout — the magnified window, the cone-sized reticle inside it, the
        /// recharge ring around that, and a second cone-sized reticle over the FLIGHT view.
        ///
        /// <para>The flight view's reticle is the same measurement through the gameplay camera's
        /// own optics, so it stays small while the eyepiece's grows with the zoom, and it is
        /// projected at the point the shot reaches rather than assumed to be screen centre. Both
        /// are up only while the scope is: this method is the only thing that draws them and it
        /// runs only while the trigger is held.</para>
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
                          _shot.RangeUnits, _shot.CooldownRemaining01, _shot.TracerColour);
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
        ///
        /// <para><b>Touch has no trigger depth</b> (2026-10-21, Garrett: "the serpent's scope should
        /// open to max zoom"): a phone never writes <c>LeftTriggerAnalog</c>, so the scope opened
        /// unzoomed. Holding the scope on touch IS a full pull; the <c>zoomResponse</c> chase still
        /// ramps it in, so it reads as raised rather than snapped. Every other device is unchanged.</para>
        /// </summary>
        float ResolveZoomTarget()
        {
            var so = ResolvedConfig;
            var input = _status?.InputStatus;
            if (so == null || input == null) return 0f;
            if (input.ActiveInputDevice == InputDeviceType.Touch) return 1f;

            float depth = Mathf.Clamp01(input.LeftTriggerAnalog);
            float dead = so.ZoomDeadzone;
            if (depth <= dead) return 0f;

            return Mathf.InverseLerp(dead, 1f, depth);
        }

        bool IsHuntersSightUnlocked
        {
            get
            {
                var elemental = _status?.ElementalAbilityHandler;
                return elemental != null && elemental.IsUpgradeActive(Element.Charge);
            }
        }

        /// <summary>
        /// Hunter's Sight: record the rivals' afterimages on the machine that decides the sniper's
        /// strip (<see cref="ElementalTransfer.IsDecidedHere"/>), and draw them only for the local
        /// pilot. Everywhere else, and below Charge 5, there are none.
        /// </summary>
        void TickAfterimages()
        {
            var so = ResolvedConfig;
            if (so == null || _status == null || !IsHuntersSightUnlocked ||
                !ElementalTransfer.IsDecidedHere(_status))
            {
                _afterimages.Clear();
                return;
            }

            bool draw = _status.Player != null && _status.Player.IsLocalPilot;
            _afterimages.Tick(_status, so, draw, _gameData);
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

        /// <summary>The field of view at FULL magnification: the authored one, at every level.</summary>
        float ResolveFieldOfViewAtFullZoom()
        {
            var so = ResolvedConfig;
            return so != null ? so.FieldOfViewAtFullZoom : 12f;
        }
    }
}
