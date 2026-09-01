using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Base class for a freestyle <b>Toy</b>: a world-space station the LOCAL player's vessel
    /// flies into to activate. Toys have no score and no end condition; they re-arm after each
    /// pass so they can be played with indefinitely.
    ///
    /// Modelled on the existing menu world-triggers (<c>FreestyleSign</c> / <c>ModeSelectTrigger</c>
    /// / <c>ShapeSign</c>): a trigger collider + <c>GetComponentInParent&lt;VesselStatus&gt;</c>
    /// detection. Adds local-user gating, freestyle-only gating, continuity-law bloom-in, and
    /// an <b>exit-gated</b> re-arm so the toy can never fire again until the local vessel has
    /// physically flown clear of it. That exit gate - plus the slow <see cref="regrowDuration"/>
    /// re-grow used when a swap toy flips - is what stops a swap toy from immediately switching
    /// you back before you can escape it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Toy : MonoBehaviour
    {
        [SerializeField, Tooltip("Seconds for the initial bloom-in (scale-from-zero) on spawn.")]
        float bloomDuration = 1.2f;

        [SerializeField, Tooltip("Seconds for the SLOW re-grow after the toy flips / re-arms " +
                                 "(scale-from-zero). Long on purpose: while it re-grows the toy is " +
                                 "inert, so a swap toy that flips to the option you just left can't " +
                                 "trigger again until you've had time to fly clear of it.")]
        float regrowDuration = 5f;

        [SerializeField, Tooltip("The local vessel must travel beyond triggerRadius × this before the " +
                                 "toy re-arms. >1 adds hysteresis so hovering on the boundary - or a " +
                                 "vessel that re-spawns right inside the toy after a swap - doesn't " +
                                 "immediately re-trigger it.")]
        float exitRadiusMultiplier = 1.35f;

        protected ToyDefinitionSO Definition { get; private set; }
        protected ToyContext Context { get; private set; }

        /// <summary>
        /// Where and how big this toy was placed. Kept so subclasses can size their own
        /// decoration against the ACTUAL body radius (the toybox places toys at menu scale -
        /// tens of world units - so decoration authored in raw units disappears inside the
        /// body sphere). <c>default</c> for toys built through
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
        /// Wire up the toy. Called by the <see cref="ToyDefinitionSO"/> factory right after the
        /// GameObject + collider + visuals are created. Starts the bloom-in.
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

            // A toy that can also be played from the app shell announces itself here - the one
            // method every toy passes through, so there is nothing per-toy to remember. Station
            // toys (a gallery's PaintingToy, a flip-set's SwapToy) do not implement the interface
            // and so never register: the shell lists TOYS, not their unfolded choices.
            if (this is IToyShellSurface shellSurface)
                ToyShellRegistry.Register(shellSurface);

            BloomIn(bloomDuration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// Virtual so every subclass EXTENDS teardown rather than hiding it - Unity invokes only
        /// the most-derived <c>OnDestroy</c>, and a hiding declaration would leave this toy in the
        /// app shell's registry as a destroyed reference. Overrides must call base.
        /// </summary>
        protected virtual void OnDestroy()
        {
            if (this is IToyShellSurface shellSurface)
                ToyShellRegistry.Unregister(shellSurface);
        }

        /// <summary>Hook for subclasses to do extra setup after <see cref="Initialize"/>.</summary>
        protected virtual void OnInitialized() { }

        // ── The SWITCH ring ──────────────────────────────────────────────────
        //
        // Every toy is a ring you fly through. The ring is drawn from the toy's OWN trigger
        // collider - the same collider the exit gate measures - so the shape a player is taught to
        // read ("thread this and something happens") can never drift from the volume that actually
        // fires. Docs/ToySystem/ARCHITECTURE.md, "The switch".
        //
        // It is drawn by the BASE, not by each toy's builder, so a toy authored tomorrow wears one
        // without anybody remembering to add it - the same reason the bloom-in and the exit gate
        // live here. There is exactly ONE opt-out left, explicit and called before Initialize: a
        // smaller radius (a matrix whose station triggers overlap their neighbours', where
        // interpenetrating rings would read as noise). The waiver at radius 0 is gone with the
        // domain changer's cones - every toy is a switch now, and what it DOES is said by the
        // ring's SIGNAL rather than by a bespoke body.

        float _switchRingRadius = -1f;      // < 0 = derive from the trigger collider
        ToySwitchSignal _switchSignal = ToySwitchSignal.Neutral;
        Domains _switchDomain = Domains.Blue;

        /// <summary>This toy's switch ring, once <see cref="Initialize"/> has drawn it.</summary>
        protected GameObject SwitchRing { get; private set; }

        /// <summary>
        /// Resize this toy's switch ring, leaving what it MEANS alone. Call BEFORE
        /// <see cref="Initialize"/>. <paramref name="radius"/> is in the toy root's own space
        /// (toy roots are unscaled, so that is world units); 0 draws no ring.
        ///
        /// <para>Deliberately its own overload rather than optional arguments on the one below:
        /// a defaulted signal parameter means every radius-only call silently re-paints the toy
        /// Neutral, which on a domain switch is the reservation quietly failing open.</para>
        /// </summary>
        public void ConfigureSwitchRing(float radius) => _switchRingRadius = radius;

        /// <summary>
        /// Resize this toy's switch ring AND say what its shader should mean. Call BEFORE
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

        /// <summary>
        /// Repaint the live ring for a new signal - a Domain Changer slot flips to the domain you
        /// just left, and the ring is what says which one that is. Safe before
        /// <see cref="Initialize"/> too: it records the signal for the ring still to be built.
        /// </summary>
        public void SetSwitchSignal(ToySwitchSignal signal, Domains domain)
        {
            _switchSignal = signal;
            _switchDomain = domain;
            if (SwitchRing)
                ToyFactory.RepaintSwitchRing(SwitchRing, ToyFactory.Theme(Context), signal, domain);
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

        /// <summary>This toy's emblem, or null when it has none (see <see cref="AttachEmblem"/>).</summary>
        protected ToyEmblem Emblem { get; private set; }

        /// <summary>
        /// Give this toy an <b>iconographic root</b>: a core showing what you are, ringed by
        /// satellites showing what a pass would offer - all built from the toy's OWN content. Call
        /// from <see cref="OnInitialized"/>; it allocates holders only, and the toybox-wide
        /// <see cref="ToyEmblemStreamer"/> fills them one slot per frame inside the bloom-in.
        ///
        /// Opt-in: a toy that already wears a meaningful shape (the domain changer's prism-material
        /// cone) must not have one.
        /// </summary>
        protected ToyEmblem AttachEmblem(ToyEmblem.IEmblemSource source, float orbitRate)
        {
            if (Emblem) return Emblem;
            Color tint = Definition ? Definition.AccentColor : Color.white;
            Emblem = ToyEmblem.Attach(this, Placement.BodyRadius, source, orbitRate, tint);
            return Emblem;
        }

        /// <summary>
        /// Slowly re-grow (a scale-from-zero) to signal an in-place visual change (e.g. a swap-toy
        /// flip). The toy stays inert until it has re-grown AND the local vessel has flown clear.
        /// </summary>
        public void Rebloom()
        {
            if (_blooming) return;
            BloomIn(regrowDuration, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>
        /// Force the toy inert. It will not re-arm until the local vessel is confirmed outside the
        /// trigger volume. Used by a set coordinator to disarm every sibling toy the instant one
        /// fires, so the vessel (which may re-spawn right on top of a neighbouring toy after a swap)
        /// can't chain-trigger the whole set.
        /// </summary>
        public void Disarm() => _armed = false;

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
            // outside the trigger volume, so a toy that blooms/re-grows around a hovering vessel
            // can't fire until that vessel flies clear.
        }

        // Virtual so subclasses that need their own per-frame work (e.g. PaintingToy's choice-gate
        // cleanup) EXTEND rather than shadow it - Unity invokes only the most-derived Update(), so
        // a hiding declaration in a subclass would silently disable the exit-gated re-arm below and
        // the toy would never fire. Overrides must call base.Update().
        protected virtual void Update()
        {
            if (_armed || _blooming || _activating) return;
            if (LocalVesselOutsideTrigger())
                _armed = true;
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
