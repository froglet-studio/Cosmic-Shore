using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A fly-through <b>station inside a run</b> - a painting's stroke gate, a wander's way home -
    /// the LOCAL player's vessel flies into to activate. Not a toy: toys are chosen in the app
    /// shell's Toy Box (<see cref="Toy"/>); a station is part of a toy's PLAY once it is going.
    ///
    /// A trigger collider + <c>GetComponentInParent&lt;VesselStatus&gt;</c> detection, with
    /// local-user gating, freestyle-only gating, continuity-law bloom-in, a switch ring drawn from
    /// its own trigger, and an <b>exit-gated</b> re-arm so it can never fire again until the local
    /// vessel has physically flown clear of it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class ToyStation : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds for the initial bloom-in (scale-from-zero) on spawn.")]
        float bloomDuration = 1.2f;

        [SerializeField, Tooltip("The local vessel must travel beyond triggerRadius × this before the " +
                                 "station re-arms. >1 adds hysteresis so hovering on the boundary - or a " +
                                 "vessel that re-spawns right inside it after a swap - doesn't " +
                                 "immediately re-trigger it.")]
        float exitRadiusMultiplier = 1.35f;

        protected ToyDefinitionSO Definition { get; private set; }
        protected ToyContext Context { get; private set; }

        /// <summary>
        /// Where and how big this station was placed. <c>default</c> for gates built through
        /// <see cref="ToyFactory.CreateGate"/>, which places them itself.
        /// </summary>
        protected ToyPlacement Placement { get; private set; }

        Vector3 _targetScale = Vector3.one;
        float _triggerWorldRadius;
        bool _armed;
        bool _activating;
        bool _blooming;

        /// <summary>One-line label used in logs.</summary>
        public string DisplayName => Definition ? Definition.DisplayName : name;

        /// <summary>
        /// Wire up the station. Called right after the GameObject + collider + visuals are created.
        /// Starts the bloom-in.
        /// </summary>
        public void Initialize(ToyDefinitionSO definition, ToyContext context, ToyPlacement placement)
        {
            Definition = definition;
            Context = context;
            Placement = placement;
            _targetScale = transform.localScale;

            if (TryGetComponent(out Collider col))
            {
                col.isTrigger = true;
                _triggerWorldRadius = ComputeTriggerWorldRadius(col);
                BuildSwitchRing(col);
            }

            OnInitialized();

            BloomIn(bloomDuration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>Hook for subclasses to do extra setup after <see cref="Initialize"/>.</summary>
        protected virtual void OnInitialized() { }

        // ── The SWITCH ring ──────────────────────────────────────────────────
        //
        // Drawn from the station's OWN trigger collider - the same collider the exit gate measures -
        // so the shape a player is taught to read ("thread this and something happens") can never
        // drift from the volume that actually fires. Docs/ToySystem/ARCHITECTURE.md, "The switch".

        float _switchRingRadius = -1f;      // < 0 = derive from the trigger collider
        ToySwitchSignal _switchSignal = ToySwitchSignal.Neutral;
        Domains _switchDomain = Domains.Blue;

        /// <summary>This station's switch ring, once <see cref="Initialize"/> has drawn it.</summary>
        protected GameObject SwitchRing { get; private set; }

        /// <summary>
        /// Resize this station's switch ring, leaving what it MEANS alone. Call BEFORE
        /// <see cref="Initialize"/>. <paramref name="radius"/> is in the root's own space (roots
        /// are unscaled, so that is world units); 0 draws no ring.
        ///
        /// <para>Deliberately its own overload rather than optional arguments on the one below:
        /// a defaulted signal parameter means every radius-only call silently re-paints the station
        /// Neutral, which on a domain switch is the reservation quietly failing open.</para>
        /// </summary>
        public void ConfigureSwitchRing(float radius) => _switchRingRadius = radius;

        /// <summary>
        /// Resize this station's switch ring AND say what its shader should mean. Call BEFORE
        /// <see cref="Initialize"/>. <paramref name="signal"/> picks the prism material - a
        /// <see cref="ToySwitchSignal.Neutral"/> switch is painted Blue whatever
        /// <paramref name="domain"/> says, so the domain colours stay reserved.
        /// </summary>
        public void ConfigureSwitchRing(float radius, ToySwitchSignal signal, Domains domain)
        {
            _switchRingRadius = radius;
            _switchSignal = signal;
            _switchDomain = domain;
        }

        void BuildSwitchRing(Collider col)
        {
            // The collider's LOCAL radius, not the world one: the ring is a child of the collider's
            // own transform, so a local radius stays correct under any parent scaling - and stays
            // correct DURING the bloom, while the root's scale is still climbing to 1.
            float radius = _switchRingRadius >= 0f
                ? _switchRingRadius
                : col is SphereCollider sphere ? sphere.radius : _triggerWorldRadius;

            SwitchRing = ToyFactory.AddSwitchRing(transform, radius, ToyFactory.Theme(Context),
                                                  _switchSignal, _switchDomain);
        }

        async UniTaskVoid BloomIn(float duration, CancellationToken ct)
        {
            _blooming = true;
            _armed = false;
            transform.localScale = Vector3.zero;

            if (duration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = t * t * (3f - 2f * t); // smoothstep
                    transform.localScale = Vector3.LerpUnclamped(Vector3.zero, _targetScale, eased);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }
            }

            transform.localScale = _targetScale;
            _blooming = false;
            // NOTE: do NOT arm here. Update() re-arms only once the local vessel is confirmed
            // outside the trigger volume, so a station that blooms around a hovering vessel
            // can't fire until that vessel flies clear.
        }

        // Virtual so subclasses that need their own per-frame work EXTEND rather than shadow it -
        // Unity invokes only the most-derived Update(), so a hiding declaration in a subclass would
        // silently disable the exit-gated re-arm below. Overrides must call base.Update().
        protected virtual void Update()
        {
            // The local hull's second body threads switches too (ISwitchThreadProbe: the Thresher's ball, the flail - Garrett,
            // 2026-10-20: "the wrasse does not activate toys, but the thresher flail does"). Sampled every frame, so its
            // next segment is one frame long.
            bool probeFlew = SampleSwitchProbe(out var probePrev, out var probeCur);
            if (_blooming || _activating) return;
            if (_armed)
            {
                if (probeFlew) TryProbeActivate(probePrev, probeCur);
                return;
            }
            if (LocalVesselOutsideTrigger())
                _armed = true;
        }

        // ---- the switch probe: a second body of the LOCAL hull that threads this toy's ring (the trigger sphere) ----
        IVessel _probeVessel;
        ISwitchThreadProbe _switchProbe;
        Vector3 _probeLast;
        int _probeEpoch;
        bool _probeHas;

        /// <summary>A probe step longer than this (u) is a jump, not a flight (a respawn, a hitch).</summary>
        const float MaxProbeStep = 250f;

        bool SampleSwitchProbe(out Vector3 prev, out Vector3 cur)
        {
            prev = cur = default;
            var vessel = Context?.GameData?.LocalPlayer?.Vessel;
            if (vessel == null || vessel is Object uo && !uo || !vessel.Transform) { _probeHas = false; return false; }
            if (!ReferenceEquals(vessel, _probeVessel))
            {
                // resolved once per hull; a hull without one (every hull but the Thresher) is cached as null
                _probeVessel = vessel;
                _switchProbe = vessel.Transform.GetComponentInChildren<ISwitchThreadProbe>(true);
                _probeHas = false;
            }
            if (_switchProbe == null || _switchProbe is Object po && !po || !_switchProbe.TryGetRingProbe(out cur, out int epoch))
            {
                _probeHas = false;
                return false;
            }
            bool flew = _probeHas && epoch == _probeEpoch && (cur - _probeLast).sqrMagnitude <= MaxProbeStep * MaxProbeStep;
            prev = _probeLast;
            _probeLast = cur;
            _probeEpoch = epoch;
            _probeHas = true;
            return flew;
        }

        // the probe's segment passed through the trigger sphere: the same activation a hull's trigger entry makes
        void TryProbeActivate(Vector3 prev, Vector3 cur)
        {
            if (Context?.IsFreestyleActive != null && !Context.IsFreestyleActive()) return;
            Vector3 c = transform.position, d = cur - prev;
            float l2 = d.sqrMagnitude, t = l2 > 0f ? Mathf.Clamp01(Vector3.Dot(c - prev, d) / l2) : 0f;
            if ((prev + d * t - c).sqrMagnitude > _triggerWorldRadius * _triggerWorldRadius) return;
            var status = ResolveLocalVessel();
            if (status == null) return;
            _armed = false; // re-armed only once the hull AND the ball are clear (LocalVesselOutsideTrigger)
            _activating = true;
            ActivateDeferred(status).Forget();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!_armed || _activating) return;
            if (!TryGetLocalVessel(other, out var vessel)) return;

            // Only respond while the player is actually flying freestyle. In the menu
            // (autopilot) the vessel drifts through the lava lamp; toys are visible but inert.
            if (Context?.IsFreestyleActive != null && !Context.IsFreestyleActive()) return;

            _armed = false; // Update() re-arms only after the vessel has flown clear again.
            _activating = true;
            ActivateDeferred(vessel).Forget();
        }

        /// <summary>
        /// Toy effects run on the next Update tick, NOT inside the physics trigger callback:
        /// they reach deep (domain RPC → vessel re-theme → HUD pool rebuilds, networked vessel
        /// swaps), and a swath of engine APIs (DestroyImmediate among them) is illegal during
        /// physics/animation/render callbacks. One frame of deferral is imperceptible.
        /// </summary>
        async UniTaskVoid ActivateDeferred(IVesselStatus vessel)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            try { OnActivated(vessel); }
            finally { _activating = false; }
        }

        /// <summary>
        /// True when the local vessel is confirmed to be well outside this toy's trigger volume
        /// (beyond <see cref="exitRadiusMultiplier"/>× the trigger radius). Returns false when the
        /// vessel is null / mid-swap / destroyed, so the toy stays disarmed until it can be sure -
        /// this is what makes the exit gate robust across a swap's despawn/respawn (which fires no
        /// <c>OnTriggerExit</c>).
        /// </summary>
        bool LocalVesselOutsideTrigger()
        {
            var vessel = Context?.GameData?.LocalPlayer?.Vessel;
            if (vessel == null) return false;
            if (vessel is Object uo && !uo) return false; // destroyed mid-swap
            var t = vessel.Transform;
            if (!t) return false;

            float exitR = _triggerWorldRadius * Mathf.Max(1f, exitRadiusMultiplier);
            if (_probeHas && (_probeLast - transform.position).sqrMagnitude <= exitR * exitR) return false;   // the ball is still in it
            return (t.position - transform.position).sqrMagnitude > exitR * exitR;
        }

        static float ComputeTriggerWorldRadius(Collider col)
        {
            var s = col.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            if (col is SphereCollider sphere) return sphere.radius * maxScale;
            return col.bounds.extents.magnitude;
        }

        /// <summary>
        /// Resolve the colliding object to the LOCAL player's vessel. Never lets a remote (or
        /// AI/autopilot in a party) vessel trip this client's toy. Shared by every toy trigger
        /// (gates, ride milestones) - one rule, one implementation.
        /// </summary>
        internal static bool TryGetLocalVessel(Collider other, out IVesselStatus vessel)
        {
            vessel = null;
            var status = other.GetComponentInParent<VesselStatus>();
            if (!status) return false;
            // A freshly spawned hull (mid vessel-swap) has no Player yet - IsLocalUser would
            // dereference it inside a physics callback.
            if (status.Player == null) return false;
            IVesselStatus iv = status;
            if (!iv.IsLocalUser) return false;
            vessel = iv;
            return true;
        }

        /// <summary>
        /// The local player's vessel, or null when there isn't one to act on (no player yet,
        /// destroyed mid-swap). The app-shell face of a toy whose effect needs a vessel resolves
        /// it through here rather than re-deriving the chain - it is the same walk the exit gate
        /// makes, with the same destroyed-object guard.
        /// </summary>
        protected IVesselStatus ResolveLocalVessel()
        {
            var status = Context?.GameData?.LocalPlayer?.Vessel?.VesselStatus;
            if (status == null) return null;
            if (status is Object uo && !uo) return null;   // destroyed mid-swap
            return status;
        }

        /// <summary>Called once per local-vessel pass while in freestyle. Implement the toy's effect.</summary>
        protected abstract void OnActivated(IVesselStatus localVessel);
    }
}
