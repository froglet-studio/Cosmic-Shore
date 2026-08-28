using System.Threading;
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
            BloomIn(bloomDuration, this.GetCancellationTokenOnDestroy()).Forget();
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
        // live here. There are exactly two opt-outs, both explicit, both called before Initialize:
        // a smaller radius (a matrix whose station triggers overlap their neighbours', where
        // interpenetrating rings would read as noise) and 0, which waives the ring entirely (the
        // domain changer, whose cones already carry the "fly through me" read).

        float _switchRingRadius = -1f;      // < 0 = derive from the trigger collider
        Color? _switchRingTint;
        Material _switchRingMaterial;

        /// <summary>
        /// Resize, re-tint or waive this toy's switch ring. Call BEFORE <see cref="Initialize"/>.
        /// <paramref name="radius"/> is in the toy root's own space (toy roots are unscaled, so
        /// that is world units) and 0 draws no ring; <paramref name="tint"/> defaults to the
        /// definition's accent; <paramref name="prismMaterial"/> paints the ring in a domain's
        /// prism material instead of a flat tint.
        /// </summary>
        public void ConfigureSwitchRing(float radius, Color? tint = null, Material prismMaterial = null)
        {
            _switchRingRadius = radius;
            _switchRingTint = tint;
            _switchRingMaterial = prismMaterial;
        }

        void BuildSwitchRing(Collider col)
        {
            // The collider's LOCAL radius, not the world one: the ring is a child of the collider's
            // own transform, so a local radius stays correct under any parent scaling - and stays
            // correct DURING the bloom, while the root's scale is still climbing to 1.
            float radius = _switchRingRadius >= 0f
                ? _switchRingRadius
                : col is SphereCollider sphere ? sphere.radius : _triggerWorldRadius;

            Color tint = _switchRingTint ?? (Definition ? Definition.AccentColor : Color.white);
            ToyFactory.AddSwitchRing(transform, radius, tint, _switchRingMaterial);
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

        /// <summary>Called once per local-vessel pass while in freestyle. Implement the toy's effect.</summary>
        protected abstract void OnActivated(IVesselStatus localVessel);
    }
}
