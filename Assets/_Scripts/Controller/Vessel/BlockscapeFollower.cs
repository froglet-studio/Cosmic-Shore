using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The 2D ride kernel - rolls a vessel across the FACES of a prismscape
    /// (<see cref="CosmicShore.Data.PrismscapeDimension.Surface"/>): gyroid and Schwarz-P
    /// flora, walls, any shell-like build. The 1D counterpart is <see cref="TrailFollower"/>,
    /// which slides ALONG a ribbon.
    ///
    /// Three behaviours compose the roll:
    ///  * FACE CRAWL - movement is the vessel's forward projected onto the current face's
    ///    plane, so steering stays with the pilot and the surface only constrains position.
    ///  * EDGE FOLD - stepping off a face wraps around the box edge onto the adjacent face
    ///    of the SAME prism.
    ///  * PRISM HOP - before folding, the exit point is checked against neighbouring prisms
    ///    via <see cref="PrismSpatialIndex.QuerySphere"/> (the canonical spatial store, never
    ///    physics); a near-enough neighbour becomes the new floor and
    ///    <see cref="OnPrismCrossed"/> fires - the surface analogue of TrailFollower's block
    ///    crossing, and the hook the Urchin's per-prism payoff rides.
    ///
    /// All box math runs in the prism's LOCAL UNIT space, where the block spans ±0.5 on every
    /// axis - a prism's mesh is a unit cube and its transform.localScale IS the block's
    /// dimensions, so InverseTransformPoint lands in that space directly. (The previous
    /// version compared those unit-space coordinates against localScale/2 - WORLD half
    /// extents - so on a 4x4x1 block the edge fold fired four times too late and the
    /// post-fold snap parked the rider several units off the surface.)
    /// </summary>
    [RequireComponent(typeof(IVesselStatus))]
    public class BlockscapeFollower : MonoBehaviour
    {
        public Prism AttachedPrism { get; private set; }

        /// <summary>
        /// Raised when the roll carries the rider onto a DIFFERENT prism. The payoff hook:
        /// <c>GunVesselTransformer.ApplyPrismscapePayoff</c> subscribes so every prism
        /// visited on a surface pays the same restore/grow/steal rule a trail slide pays.
        /// </summary>
        public event Action<Prism> OnPrismCrossed;

        [Tooltip("Roll speed across prisms of the rider's own domain.")]
        [SerializeField] private float FriendlyTerrainSpeed;
        [Tooltip("Roll speed across an enemy domain's prisms.")]
        [SerializeField] private float HostileTerrainSpeed;
        [Tooltip("Roll speed across destroyed prisms.")]
        [SerializeField] private float DestroyedTerrainSpeed;

        [HideInInspector] public float Throttle;

        /// <summary>Half extent of the prism box in its own local space - the mesh is a unit cube.</summary>
        const float LocalHalfExtent = 0.5f;

        /// <summary>World-space hover height above the ridden face.</summary>
        const float SurfaceOffset = 1f;

        /// <summary>
        /// How far (world units) past a neighbour's surface the exit point may sit and still
        /// count as adjacent. Covers the hover offset plus the small authored gaps between
        /// blocks in a gyroid/Schwarz lay; big enough to bridge a seam, small enough not to
        /// teleport across genuine holes in the shell.
        /// </summary>
        const float HopGapTolerance = 2.5f;

        /// <summary>Hop census radius in multiples of the current prism's largest world extent.</summary>
        const float HopSearchRadiusScale = 1.5f;

        // Main-thread only, like every QuerySphere consumer.
        static readonly List<Prism> s_hopCandidates = new(64);

        /// <summary>Face normal in the ATTACHED PRISM's local space - exactly one axis at ±1.</summary>
        Vector3 localSurfaceNormal = Vector3.up;

        private IVesselStatus vesselData;

        private void Awake()
        {
            // Awake, not Start: Attach can arrive from the transformer's first MoveShip,
            // which may run before this component's Start on a freshly-swapped vessel.
            vesselData = GetComponent<IVesselStatus>();
        }

        public void Attach(Prism prism)
        {
            AttachedPrism = prism;
            SettleOntoNearestFace();
        }

        public void Detach()
        {
            AttachedPrism = null;
        }

        public void RideTheTrail()
        {
            if (AttachedPrism == null) return;

            float speed = Throttle * GetTerrainAwareBlockSpeed(AttachedPrism);
            vesselData.Speed = speed;

            Vector3 worldNormal = WorldSurfaceNormal();
            Vector3 projectedMovement = Vector3.ProjectOnPlane(transform.forward * (speed * Time.deltaTime), worldNormal);

            ResolveBoundaryCrossing(ref projectedMovement);

            transform.position += projectedMovement;
        }

        /// <summary>
        /// If the step would leave the current face's footprint: hop to an adjacent prism
        /// when one is near enough, otherwise fold around this prism's own edge.
        /// </summary>
        void ResolveBoundaryCrossing(ref Vector3 projectedMovement)
        {
            Vector3 worldTarget = transform.position + projectedMovement;
            Vector3 lp = AttachedPrism.transform.InverseTransformPoint(worldTarget);

            bool leaving = Mathf.Abs(lp.x) > LocalHalfExtent ||
                           Mathf.Abs(lp.y) > LocalHalfExtent ||
                           Mathf.Abs(lp.z) > LocalHalfExtent;

            // The hover offset holds the rider off its face along the normal axis, so ignore
            // the normal axis when asking "did we run off the face?" - only in-plane excess
            // is an edge crossing.
            Vector3 inPlane = Vector3.one - Abs(localSurfaceNormal);
            bool crossedEdge = Mathf.Abs(lp.x) * inPlane.x > LocalHalfExtent ||
                               Mathf.Abs(lp.y) * inPlane.y > LocalHalfExtent ||
                               Mathf.Abs(lp.z) * inPlane.z > LocalHalfExtent;

            if (!leaving || !crossedEdge) return;

            if (TryHopToAdjacentPrism(worldTarget))
            {
                projectedMovement = Reproject(projectedMovement);
                return;
            }

            // No neighbour - wrap around this prism's own edge onto the adjacent face.
            localSurfaceNormal = DominantExcessAxis(lp);
            SnapOntoCurrentFace(lp);
            projectedMovement = Reproject(projectedMovement);
        }

        bool TryHopToAdjacentPrism(Vector3 worldExitPoint)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;

            var s = AttachedPrism.transform.lossyScale;
            float extent = Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
            float radius = Mathf.Max(1f, extent) * HopSearchRadiusScale + SurfaceOffset;

            index.QuerySphere(worldExitPoint, radius, s_hopCandidates);

            Prism best = null;
            float bestOutside = SurfaceOffset + HopGapTolerance;
            for (int i = 0; i < s_hopCandidates.Count; i++)
            {
                var candidate = s_hopCandidates[i];
                if (!candidate || candidate == AttachedPrism) continue;

                // World-unit distance from the exit point to the candidate's box surface
                // (negative = inside). Per-axis local excess scaled back to world units.
                Vector3 lp = candidate.transform.InverseTransformPoint(worldExitPoint);
                Vector3 cs = candidate.transform.lossyScale;
                float outside = Mathf.Max(
                    (Mathf.Abs(lp.x) - LocalHalfExtent) * Mathf.Abs(cs.x),
                    Mathf.Max((Mathf.Abs(lp.y) - LocalHalfExtent) * Mathf.Abs(cs.y),
                              (Mathf.Abs(lp.z) - LocalHalfExtent) * Mathf.Abs(cs.z)));

                if (outside < bestOutside)
                {
                    bestOutside = outside;
                    best = candidate;
                }
            }

            if (!best) return false;

            AttachedPrism = best;
            SettleOntoNearestFace();
            OnPrismCrossed?.Invoke(best);
            return true;
        }

        /// <summary>
        /// Picks the attached prism's face nearest the rider, stores its local normal, and
        /// snaps the rider onto it at the hover offset. Used at attach and after a hop.
        /// </summary>
        void SettleOntoNearestFace()
        {
            Vector3 lp = AttachedPrism.transform.InverseTransformPoint(transform.position);

            float distX = LocalHalfExtent - Mathf.Abs(lp.x);
            float distY = LocalHalfExtent - Mathf.Abs(lp.y);
            float distZ = LocalHalfExtent - Mathf.Abs(lp.z);

            // Least remaining depth = nearest face. Works from inside AND outside the box
            // (outside, the depths go negative and the most-negative axis is the exit face).
            if (distX < distY && distX < distZ)
                localSurfaceNormal = new Vector3(Mathf.Sign(lp.x), 0, 0);
            else if (distY < distZ)
                localSurfaceNormal = new Vector3(0, Mathf.Sign(lp.y), 0);
            else
                localSurfaceNormal = new Vector3(0, 0, Mathf.Sign(lp.z));

            SnapOntoCurrentFace(lp);
        }

        /// <summary>
        /// Clamps the in-plane axes into the face's footprint and sets the normal axis to
        /// half-extent plus the hover offset (converted to this prism's local units, since
        /// TransformPoint re-applies the block's scale).
        /// </summary>
        void SnapOntoCurrentFace(Vector3 lp)
        {
            Vector3 scale = AttachedPrism.transform.lossyScale;
            Vector3 n = localSurfaceNormal;

            float axisScale = Mathf.Max(0.0001f, Mathf.Abs(Vector3.Dot(scale, Abs(n))));
            float hover = LocalHalfExtent + SurfaceOffset / axisScale;

            lp.x = n.x != 0 ? n.x * hover : Mathf.Clamp(lp.x, -LocalHalfExtent, LocalHalfExtent);
            lp.y = n.y != 0 ? n.y * hover : Mathf.Clamp(lp.y, -LocalHalfExtent, LocalHalfExtent);
            lp.z = n.z != 0 ? n.z * hover : Mathf.Clamp(lp.z, -LocalHalfExtent, LocalHalfExtent);

            transform.position = AttachedPrism.transform.TransformPoint(lp);
        }

        Vector3 WorldSurfaceNormal()
        {
            // Rotation only - a prism's non-uniform scale must not shear the normal.
            return AttachedPrism.transform.TransformDirection(localSurfaceNormal).normalized;
        }

        Vector3 Reproject(Vector3 movement) =>
            Vector3.ProjectOnPlane(movement, WorldSurfaceNormal());

        static Vector3 DominantExcessAxis(Vector3 lp)
        {
            float xExcess = Mathf.Abs(lp.x) - LocalHalfExtent;
            float yExcess = Mathf.Abs(lp.y) - LocalHalfExtent;
            float zExcess = Mathf.Abs(lp.z) - LocalHalfExtent;

            if (xExcess > yExcess && xExcess > zExcess) return new Vector3(Mathf.Sign(lp.x), 0, 0);
            if (yExcess > zExcess) return new Vector3(0, Mathf.Sign(lp.y), 0);
            return new Vector3(0, 0, Mathf.Sign(lp.z));
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private float GetTerrainAwareBlockSpeed(Prism prism)
        {
            if (prism.destroyed) return DestroyedTerrainSpeed;
            return prism.Domain == vesselData.Domain ? FriendlyTerrainSpeed : HostileTerrainSpeed;
        }
    }
}
