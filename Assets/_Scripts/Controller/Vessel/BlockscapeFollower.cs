using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The 2D ride kernel - rolls a vessel across a prismscape SURFACE
    /// (<see cref="CosmicShore.Data.PrismscapeDimension.Surface"/>: gyroid and Schwarz-P flora,
    /// walls, any shell-like build). The 1D counterpart is <see cref="TrailFollower"/>, which
    /// slides ALONG a ribbon.
    ///
    /// The ride models the AGGREGATE surface, never the individual boxes - the pilot must feel
    /// one smooth curved floor (marble-madness rolling: momentum, carved arcs, and wrapping
    /// around a sheet's edge onto its other side), never a prism's edges or the gaps between
    /// prisms:
    ///
    ///  * The surface normal is AUTHORED, not inferred: a surface prismscape's prisms are laid
    ///    with their local Z orthogonal to the surface (as a trail prism's Z is parallel to its
    ///    trail), so the ground prism's <c>transform.forward</c> IS the local normal - no box
    ///    face inference, no edge folding.
    ///  * The ridden normal is a SMOOTHED state, slerped toward each ground prism's authored
    ///    normal - so crossing from prism to prism turns the floor through a continuous arc
    ///    rather than snapping plane to plane.
    ///  * Height above the surface is a soft spring toward a hover offset measured along the
    ///    smoothed normal from the ground prism's mid-plane - so prism-to-prism height steps
    ///    read as a rolling swell, and GAPS (holes, destroyed prisms) are coasted straight
    ///    over on the last known plane: the ground reference is only ever REPLACED by a nearer
    ///    prism, never dropped.
    ///  * Movement is the vessel's forward projected onto the smoothed tangent plane - the
    ///    pilot keeps full steering; the surface constrains position, not attitude. (The
    ///    transformer separately eases the hull's belly onto <see cref="SurfaceNormal"/>.)
    ///
    /// A STACK of nested layers (<see cref="ILayeredPrismscape"/> - the nested gyroid) is ridden one
    /// layer at a time: the ground may only move to a prism of the SAME layer, unless the pilot pitches
    /// toward the next layer, in which case the next strut or sheet in that direction becomes eligible and
    /// the hover spring lets go so the rider can actually reach it (Docs/ECOSYSTEM.md §58.4). Every other
    /// prismscape rides exactly as before - the layered rules engage only while the ground's owner declares
    /// a stack.
    ///
    /// Ground tracking is <see cref="PrismSpatialIndex.QuerySphere"/> - the canonical spatial
    /// store, never physics. When the rider moves onto a different prism,
    /// <see cref="OnPrismCrossed"/> fires - the surface analogue of the trail's block crossing,
    /// and the hook the Urchin's restore/grow/steal payoff rides.
    /// </summary>
    [RequireComponent(typeof(IVesselStatus))]
    public class BlockscapeFollower : MonoBehaviour
    {
        /// <summary>The current ground prism - the anchor of the ridden plane.</summary>
        public Prism AttachedPrism { get; private set; }

        /// <summary>
        /// Raised when the roll carries the rider onto a DIFFERENT prism.
        /// <c>GunVesselTransformer.ApplyPrismscapePayoff</c> subscribes so every prism visited
        /// on a surface pays the same restore/grow/steal rule a trail slide pays.
        /// </summary>
        public event Action<Prism> OnPrismCrossed;

        /// <summary>
        /// The smoothed world normal of the ridden surface - what the transformer eases the
        /// hull's belly onto. Points from the surface toward the rider.
        /// </summary>
        public Vector3 SurfaceNormal { get; private set; } = Vector3.up;

        [Tooltip("Roll speed across prisms of the rider's own domain.")]
        [SerializeField] private float FriendlyTerrainSpeed;
        [Tooltip("Roll speed across an enemy domain's prisms.")]
        [SerializeField] private float HostileTerrainSpeed;
        [Tooltip("Roll speed across destroyed prisms.")]
        [SerializeField] private float DestroyedTerrainSpeed;

        [Tooltip("Hover height (world units) above the ground prism's mid-plane, along the " +
                 "smoothed surface normal.")]
        [SerializeField] float hoverHeight = 2f;

        [Tooltip("How quickly the ridden plane turns toward the ground prism's authored normal " +
                 "(1/s, exponential). This IS the surface feel: low = long smooth arcs that " +
                 "round off the prism-to-prism facets, high = tight tracking of every facet.")]
        [SerializeField] float normalTrackingRate = 5f;

        [Tooltip("How quickly hover error closes (1/s, exponential). Soft, so the small height " +
                 "steps between neighbouring prisms read as swell rather than bumps.")]
        [SerializeField] float hoverTrackingRate = 5f;

        [Tooltip("Ground search radius in multiples of the ground prism's largest extent. Big " +
                 "enough to bridge the authored gaps in a gyroid lattice, small enough not to " +
                 "grab the opposite wall of a channel.")]
        [SerializeField] float groundSearchRadiusScale = 2.5f;

        [Tooltip("How quickly the surface velocity chases the steered target (1/s, " +
                 "exponential). This is the marble's WEIGHT: low = long glides and wide " +
                 "drifting arcs, high = direct control.")]
        [SerializeField] float surfaceInertiaRate = 4f;

        [Header("Layered prismscapes (a stack of nested sheets)")]
        [Tooltip("Share of the crawl speed that carries the rider THROUGH a layered prismscape when the " +
                 "pilot pitches toward the next layer. Only read while the ground belongs to an " +
                 "ILayeredPrismscape; a single shell never climbs.")]
        [Range(0f, 1f)] [SerializeField] float layerClimbFraction = 0.6f;

        [Tooltip("How far the aim must point out of the ridden plane (|dot(forward, normal)|) before the " +
                 "pilot is read as climbing to the next layer. Below it the ride holds its layer, so " +
                 "ordinary steering on a curved sheet never drifts through the stack.")]
        [Range(0f, 0.95f)] [SerializeField] float layerClimbDeadzone = 0.35f;

        [Tooltip("How far past the ground prism's in-plane footprint (in multiples of its " +
                 "largest extent) the wrap completes. Reaching a sheet's EDGE rolls the rider " +
                 "around the rim onto the other side - marble over the table's edge.")]
        [SerializeField] float rimWrapMargin = 1f;

        /// <summary>
        /// SIGNED throttle in [-1, 1]: magnitude is crawl speed, sign moves along or against
        /// the vessel's projected forward (pull the stick to back up along the surface).
        /// </summary>
        [HideInInspector] public float Throttle;

        // Main-thread only, like every QuerySphere consumer.
        static readonly List<Prism> s_groundCandidates = new List<Prism>(64);

        /// <summary>The marble's momentum along the surface (world units/s).</summary>
        Vector3 _surfaceVelocity;

        IVesselStatus vesselData;

        // Layered ride state - null / 0 whenever the ground is not part of a stack.
        ILayeredPrismscape _layers;
        int _groundStack;
        int _climbIntent;        // +1 up the stack, -1 down, 0 = hold the layer
        bool _layerAhead;        // the last refresh saw a layer to climb INTO

        void Awake()
        {
            // Awake, not Start: Attach can arrive from the transformer's first MoveShip on a
            // freshly-swapped vessel, before this component's Start has run.
            vesselData = GetComponent<IVesselStatus>();
        }

        public void Attach(Prism prism)
        {
            AttachedPrism = prism;
            ResolveLayers(prism);
            _climbIntent = 0;
            _layerAhead = false;
            // First orientation has no smoothed state to agree with - point the normal at the
            // side the vessel arrived on.
            SurfaceNormal = OrientNormal(prism, transform.position - prism.transform.position);
            // Land with the arrival momentum, projected onto the new floor - not a dead stop,
            // and not a stale velocity from a previous ride.
            _surfaceVelocity = Vector3.ProjectOnPlane(vesselData.Course * vesselData.Speed, SurfaceNormal);
        }

        public void Detach()
        {
            AttachedPrism = null;
            _layers = null;
        }

        void ResolveLayers(Prism ground)
        {
            _layers = PrismscapeTopology.LayeredOwnerOf(ground);
            if (_layers == null || !_layers.TryGetStackCoordinate(ground, out _groundStack))
            {
                _layers = null;
                _groundStack = 0;
            }
        }

        public void RideTheTrail()
        {
            if (AttachedPrism == null) return;
            float dt = Time.deltaTime;

            // Layered climb intent, read BEFORE the ground refresh because it decides which layers are
            // eligible. The aim's component out of the ridden plane past the deadzone, signed in the
            // STACK's direction (the ground's +z points up the stack).
            float climb = 0f;
            _climbIntent = 0;
            if (_layers != null)
            {
                float pitch = Vector3.Dot(transform.forward, SurfaceNormal) * Mathf.Sign(Throttle);
                if (Mathf.Abs(Throttle) > 0f && Mathf.Abs(pitch) > layerClimbDeadzone)
                {
                    climb = Mathf.Sign(pitch) * (Mathf.Abs(pitch) - layerClimbDeadzone) / (1f - layerClimbDeadzone);
                    _climbIntent = Vector3.Dot(SurfaceNormal, AttachedPrism.transform.forward) * climb > 0f ? 1 : -1;
                }
            }

            RefreshGroundPrism();

            // Turn the ridden plane toward the target normal - CONTINUOUSLY, so a prism
            // crossing is an arc, never a snap. Over the sheet the target is the ground's
            // AUTHORED normal; past the sheet's boundary it blends toward the radial from the
            // RIM, which swings the floor around the edge and rolls the rider onto the other
            // side - marble over the table's edge, no special-case wrap code.
            ResolveSurfaceFrame(out Vector3 targetNormal, out Vector3 hoverAnchor);
            float normalT = 1f - Mathf.Exp(-normalTrackingRate * dt);
            SurfaceNormal = Vector3.Slerp(SurfaceNormal, targetNormal, normalT).normalized;

            float targetSpeed = Mathf.Abs(Throttle) * GetTerrainAwareBlockSpeed(AttachedPrism);
            targetSpeed *= vesselData.VesselTransformer != null ? vesselData.VesselTransformer.SpeedMultiplier : 1f;

            // The marble's momentum: velocity CHASES the steered target instead of being it,
            // so releasing the stick glides, turning carves an arc, and reversing swings
            // through a stop. The stored velocity is re-projected onto the current tangent
            // plane each frame so momentum follows the surface as it curves.
            Vector3 tangent = Vector3.ProjectOnPlane(transform.forward, SurfaceNormal);
            if (tangent.sqrMagnitude > 1e-6f) tangent.Normalize();
            Vector3 desiredVelocity = tangent * (Mathf.Sign(Throttle) * targetSpeed);

            _surfaceVelocity = Vector3.ProjectOnPlane(_surfaceVelocity, SurfaceNormal);
            float inertiaT = 1f - Mathf.Exp(-surfaceInertiaRate * dt);
            _surfaceVelocity = Vector3.Lerp(_surfaceVelocity, desiredVelocity, inertiaT);

            vesselData.Speed = _surfaceVelocity.magnitude;

            Vector3 move = _surfaceVelocity * dt;

            // Through the stack: while climbing toward a layer that EXISTS in reach, the climb carries the
            // rider along the normal and the hover spring lets go - otherwise the spring's equilibrium
            // (climb speed / hoverTrackingRate) sits short of the halfway point to a deep layer and the
            // rider stalls. Pitching out of the outermost skin finds no layer ahead, so the spring holds
            // and the skin stays ridden.
            bool climbing = _layers != null && _climbIntent != 0 && _layerAhead;
            if (climbing) move += SurfaceNormal * (climb * targetSpeed * layerClimbFraction * dt);

            // Soft hover spring toward hoverHeight along the smoothed normal, measured from
            // the resolved anchor (the ground's mid-plane over the sheet, the RIM POINT during
            // a wrap - so the wrap pivots the rider around the edge at hover distance, a
            // rounded lip). Prism-to-prism anchor shifts read as swell through the spring.
            float height = Vector3.Dot(transform.position + move - hoverAnchor, SurfaceNormal);
            float hoverT = 1f - Mathf.Exp(-hoverTrackingRate * dt);
            if (!climbing) move += SurfaceNormal * ((hoverHeight - height) * hoverT);

            transform.position += move;
        }

        /// <summary>
        /// The frame the ride is converging on: a target normal and the anchor the hover
        /// spring measures from. Over the sheet: the ground prism's authored normal, anchored
        /// at its centre (mid-plane). Past the sheet's boundary - no nearer prism took over
        /// and the rider is outside the ground's in-plane footprint - the normal blends toward
        /// the radial FROM THE RIM POINT and the anchor becomes that rim point, so the floor
        /// swings around the edge at hover distance and the rider rolls onto the far side,
        /// where the authored normal (sign resolved toward the ridden side) takes over again.
        /// The two cases meet continuously at the rim: the anchor difference is purely
        /// in-plane, so the measured height agrees at the boundary.
        /// </summary>
        void ResolveSurfaceFrame(out Vector3 targetNormal, out Vector3 hoverAnchor)
        {
            Vector3 center = AttachedPrism.transform.position;
            Vector3 authored = OrientNormal(AttachedPrism, SurfaceNormal);

            Vector3 offset = transform.position - center;
            Vector3 inPlane = offset - authored * Vector3.Dot(offset, authored);
            float inPlaneMag = inPlane.magnitude;

            var s = AttachedPrism.transform.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            float rim = Mathf.Max(1f, extent);

            float overshoot = inPlaneMag - rim;
            if (overshoot <= 0f)
            {
                targetNormal = authored;
                hoverAnchor = center;
                return;
            }

            Vector3 rimPoint = center + inPlane * (rim / Mathf.Max(inPlaneMag, 1e-4f));
            Vector3 radial = transform.position - rimPoint;
            float wrap = Mathf.Clamp01(overshoot / Mathf.Max(0.01f, rim * rimWrapMargin));
            targetNormal = Vector3.Slerp(
                authored,
                radial.sqrMagnitude > 1e-6f ? radial.normalized : authored,
                wrap).normalized;
            hoverAnchor = rimPoint;
        }

        /// <summary>
        /// Track the nearest live prism as the ground. The reference is only ever REPLACED,
        /// never dropped - over a gap (or when the ground is shot out under the rider, which
        /// removes it from the index) the last plane carries the rider smoothly until the far
        /// edge takes over.
        /// </summary>
        void RefreshGroundPrism()
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            var s = AttachedPrism.transform.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            float radius = Mathf.Max(1f, extent) * groundSearchRadiusScale + hoverHeight;

            index.QuerySphere(transform.position, radius, s_groundCandidates);

            _layerAhead = false;
            Prism best = AttachedPrism;
            float bestSq = GroundScore(AttachedPrism);
            for (int i = 0; i < s_groundCandidates.Count; i++)
            {
                var candidate = s_groundCandidates[i];
                if (!candidate || candidate == AttachedPrism) continue;
                if (_layers != null && !IsLayerEligible(candidate)) continue;
                float dSq = GroundScore(candidate);
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = candidate;
                }
            }

            if (best != AttachedPrism)
            {
                AttachedPrism = best;
                ResolveLayers(best);
                OnPrismCrossed?.Invoke(best);
            }
        }

        /// <summary>
        /// Distance used to pick the ground. On a single shell: to the prism's centre (unchanged). In a stack:
        /// to the prism's HOVER POINT - its centre lifted <see cref="hoverHeight"/> along its normal toward the
        /// ridden side - so a climb hands over at the halfway point between two layers' riding heights rather
        /// than wherever the plates' mid-planes happen to fall.
        /// </summary>
        float GroundScore(Prism prism)
        {
            Vector3 c = prism.transform.position;
            if (_layers != null) c += OrientNormal(prism, SurfaceNormal) * hoverHeight;
            return (c - transform.position).sqrMagnitude;
        }

        /// <summary>
        /// In a stack, the ground may stay on its layer, or - only while the pilot climbs - step to the next
        /// strut or sheet in the direction of the climb (stack coordinate ±1 or ±2). A prism of ANOTHER
        /// structure is eligible exactly as it always was: a stack must not wall the rider off the world.
        /// </summary>
        bool IsLayerEligible(Prism candidate)
        {
            var owner = PrismscapeTopology.LayeredOwnerOf(candidate);
            if (owner != _layers || !owner.TryGetStackCoordinate(candidate, out int stack)) return true;
            int delta = stack - _groundStack;
            if (delta == 0) return true;
            bool ahead = _climbIntent != 0 && (delta > 0 ? 1 : -1) == _climbIntent && Mathf.Abs(delta) <= 2;
            _layerAhead |= ahead;
            return ahead;
        }

        /// <summary>
        /// A surface prism's authored normal is its local Z with an ambiguous sign (a shell has
        /// two sides). Resolve the sign toward <paramref name="reference"/> - the smoothed
        /// ridden normal while riding, the arrival direction at attach.
        /// </summary>
        static Vector3 OrientNormal(Prism prism, Vector3 reference)
        {
            Vector3 n = prism.transform.forward;
            return Vector3.Dot(n, reference) < 0f ? -n : n;
        }

        private float GetTerrainAwareBlockSpeed(Prism prism)
        {
            if (prism.destroyed) return DestroyedTerrainSpeed;
            return prism.Domain == vesselData.Domain ? FriendlyTerrainSpeed : HostileTerrainSpeed;
        }
    }
}
