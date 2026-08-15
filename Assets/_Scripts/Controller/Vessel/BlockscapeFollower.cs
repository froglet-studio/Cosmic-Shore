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
    /// one smooth curved floor, never a prism's edges or the gaps between prisms:
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

        /// <summary>
        /// SIGNED throttle in [-1, 1]: magnitude is crawl speed, sign moves along or against
        /// the vessel's projected forward (pull the stick to back up along the surface).
        /// </summary>
        [HideInInspector] public float Throttle;

        // Main-thread only, like every QuerySphere consumer.
        static readonly List<Prism> s_groundCandidates = new List<Prism>(64);

        IVesselStatus vesselData;

        void Awake()
        {
            // Awake, not Start: Attach can arrive from the transformer's first MoveShip on a
            // freshly-swapped vessel, before this component's Start has run.
            vesselData = GetComponent<IVesselStatus>();
        }

        public void Attach(Prism prism)
        {
            AttachedPrism = prism;
            // First orientation has no smoothed state to agree with - point the normal at the
            // side the vessel arrived on.
            SurfaceNormal = OrientNormal(prism, transform.position - prism.transform.position);
        }

        public void Detach()
        {
            AttachedPrism = null;
        }

        public void RideTheTrail()
        {
            if (AttachedPrism == null) return;
            float dt = Time.deltaTime;

            RefreshGroundPrism();

            // Turn the ridden plane toward the ground's authored normal - CONTINUOUSLY, so a
            // prism crossing is an arc, never a snap. The sign follows the smoothed state so
            // riding around a curved sheet cannot flip the floor out from under the vessel.
            Vector3 targetNormal = OrientNormal(AttachedPrism, SurfaceNormal);
            float normalT = 1f - Mathf.Exp(-normalTrackingRate * dt);
            SurfaceNormal = Vector3.Slerp(SurfaceNormal, targetNormal, normalT).normalized;

            float speed = Mathf.Abs(Throttle) * GetTerrainAwareBlockSpeed(AttachedPrism);
            speed *= vesselData.VesselTransformer != null ? vesselData.VesselTransformer.SpeedMultiplier : 1f;
            vesselData.Speed = speed;

            // Crawl along the smoothed tangent plane, signed by the throttle.
            Vector3 tangent = Vector3.ProjectOnPlane(transform.forward, SurfaceNormal);
            if (tangent.sqrMagnitude > 1e-6f) tangent.Normalize();
            Vector3 move = tangent * (Mathf.Sign(Throttle) * speed * dt);

            // Soft hover spring toward the offset plane. Anchored at the ground prism's centre
            // along the SMOOTHED normal, so the anchor changing prisms shifts the target by a
            // fraction of a block and the spring turns that into swell.
            float height = Vector3.Dot(transform.position + move - AttachedPrism.transform.position, SurfaceNormal);
            float hoverT = 1f - Mathf.Exp(-hoverTrackingRate * dt);
            move += SurfaceNormal * ((hoverHeight - height) * hoverT);

            transform.position += move;
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

            Prism best = AttachedPrism;
            float bestSq = (AttachedPrism.transform.position - transform.position).sqrMagnitude;
            for (int i = 0; i < s_groundCandidates.Count; i++)
            {
                var candidate = s_groundCandidates[i];
                if (!candidate || candidate == AttachedPrism) continue;
                float dSq = (candidate.transform.position - transform.position).sqrMagnitude;
                if (dSq < bestSq)
                {
                    bestSq = dSq;
                    best = candidate;
                }
            }

            if (best != AttachedPrism)
            {
                AttachedPrism = best;
                OnPrismCrossed?.Invoke(best);
            }
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
