using Unity.Mathematics;
using UnityEngine;
using CosmicShore.ECS;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's HULL is a plough: a prism it rams is shoved aside, to whichever side of the
    /// hull's centreline it already sat on, far enough to clear the hull — it is never destroyed.
    /// Cutting is the sword's job (<see cref="RhinoSkimmerDamagePrismEffectSO"/>); the hull only
    /// clears a furrow. Replaces <see cref="VesselDamagePrismEffectSO"/> in the Rhino's vessel
    /// container only — every other hull still rams as before. Design + verification:
    /// <c>R_VesselActions/RHINO_PLOUGH.md</c>.
    ///
    /// <para>The move follows the platform's live-prism mover contract (the same one
    /// <c>ThreatGrove.PoseAndFly</c> and <c>BuilderPrismWorld.Settle</c> use): the transform —
    /// and with it the collider, spatial index and shell — is FINAL this frame, and the visual
    /// slides out from where it was on the prism clock's flight stamp
    /// (Docs/PRISM_ANIMATION.md §5 C5). So the hull leaves contact on the very next physics step
    /// and nothing per-frame runs on the CPU while the prism slides.</para>
    ///
    /// <para>Runs on every peer, like the damage it replaces (prism contact is simulated per peer);
    /// the side is read off the peer's own hull pose, so it agrees everywhere except for a prism
    /// struck dead on the centreline.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoPloughPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/RhinoPloughPrismEffectSO")]
    public sealed class RhinoPloughPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Clearance")]
        [Tooltip("Air left between the hull's side and the shoved prism's near face, in world units. " +
                 "The clearance itself is measured from the hull colliders and the prism's own size, " +
                 "so this is only the gap on top of just-touching.")]
        [SerializeField, Min(0f)] float clearanceMargin = 1f;

        [Header("Throw")]
        [Tooltip("Extra sideways distance per unit of ram speed, in seconds (distance = speed x this). " +
                 "A faster Rhino throws the furrow wider. 0 = every prism lands just clear of the hull.")]
        [SerializeField, Min(0f)] float throwSecondsOfSpeed = 0.04f;

        [Tooltip("Ceiling on the speed-driven extra throw, in world units, so a ramp-boosted Rhino " +
                 "(~1,200 u/s) does not fling mass across the arena.")]
        [SerializeField, Min(0f)] float maxThrowDistance = 30f;

        [Header("Slide")]
        [Tooltip("Seconds the prism's VISUAL takes to slide from where it was to where it now rests. " +
                 "Animation only: its collider is already at rest on the frame of the ram.")]
        [SerializeField, Min(0.01f)] float slideSeconds = 0.25f;

        [Header("Attach")]
        [Tooltip("ON (default): a vessel RIDING a trail does not also plough it - riding and ramming " +
                 "are the same contact. Same guard as VesselDamagePrismEffectSO.")]
        [SerializeField] bool skipWhileAttached = true;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;
            if (skipWhileAttached && status.IsAttached) return;

            var prism = prismImpactee.Prism;
            if (!prism || prism.destroyed) return;

            // Super-shielded mass is the immovable tier (arena edge linings among it): it
            // deflects - the same jiggle a damage hit gets - and stays exactly where it is.
            if (prism.AbsorbSuperShieldHit(status.Speed)) return;

            var hull = status.ShipTransform;
            Vector3 right = hull.right;
            Vector3 origin = hull.position;
            Vector3 prismPos = prism.transform.position;

            float lateral = Vector3.Dot(prismPos - origin, right);
            float side = RhinoPlough.SideOf(lateral);

            float hullReach = RhinoPlough.HullReach(impactor.HullColliders, origin, right, side);
            float prismHalf = RhinoPlough.PrismHalfExtentAlong(prism, right);
            float clearance = hullReach + prismHalf + clearanceMargin;
            float throwDistance = Mathf.Min(maxThrowDistance, Mathf.Abs(status.Speed) * throwSecondsOfSpeed);

            // Idempotent by construction: a prism already clear of the hull is not shoved again,
            // so the second trigger callback of a multi-collider hull (or the shell tier and the
            // box tier both reporting one ram) is a no-op rather than a double throw.
            float shove = RhinoPlough.LateralShove(lateral, clearance, throwDistance);
            if (shove == 0f) return;

            Slide(prism, prismPos, prismPos + right * shove, slideSeconds);
        }

        static void Slide(Prism prism, Vector3 start, Vector3 end, float seconds)
        {
            prism.transform.position = end;
            // Kept in step the way FullAutoBlockShoot's mover keeps it - nothing reads it today,
            // but a cached position that lies is the next reader's bug.
            if (prism.prismProperties != null) prism.prismProperties.position = end;
            prism.NotifyPositionChanged();

            Vector3 flight = end - start;
            if (flight.sqrMagnitude < 1e-4f) return;
            // StampFlight's full vector is v * 2T / pi, so v = flight * pi / (2T).
            Vector3 v = flight * (Mathf.PI / (2f * seconds));
            if (!PrismRenderService.StampFlight(in prism.RenderHandle, PrismClock.Now, seconds,
                    new float3(v.x, v.y, v.z)))
                return;
            // The render bounds sit on the new anchor; cover the start too, or the sliding visual
            // frustum-culls against its resting box (BuilderPrismWorld.Settle does the same).
            Vector3 local = prism.transform.InverseTransformPoint(start);
            PrismRenderService.EncapsulateBoundsPoint(in prism.RenderHandle,
                new float3(local.x, local.y, local.z), 1f);
        }
    }

    /// <summary>
    /// The plough's geometry, kept pure so it is testable without a scene
    /// (<c>RhinoPloughTests</c>). Every distance is measured along ONE axis — the hull's right —
    /// because a plough throws to the side, never up or down.
    /// </summary>
    public static class RhinoPlough
    {
        /// <summary>+1 for a prism right of the centreline, -1 left. Dead centre goes right — an
        /// arbitrary but fixed tie-break, so the result never depends on float noise sign.</summary>
        public static float SideOf(float lateral) => lateral >= 0f ? 1f : -1f;

        /// <summary>
        /// The signed distance to move a prism along the hull's right axis. A prism whose centre
        /// already sits at least <paramref name="clearance"/> off the centreline is not moved (0);
        /// otherwise it lands at <c>clearance + throwDistance</c> on its own side.
        /// </summary>
        public static float LateralShove(float lateral, float clearance, float throwDistance)
        {
            float distance = Mathf.Abs(lateral);
            if (distance >= clearance) return 0f;
            return SideOf(lateral) * (clearance + Mathf.Max(0f, throwDistance) - distance);
        }

        /// <summary>Half-width of an oriented box along <paramref name="axis"/>: the support
        /// distance of the box from its centre in that direction.</summary>
        public static float BoxHalfExtentAlong(Vector3 axis, Vector3 boxRight, Vector3 boxUp,
                                               Vector3 boxForward, Vector3 halfSize)
            => Mathf.Abs(Vector3.Dot(axis, boxRight)) * halfSize.x
             + Mathf.Abs(Vector3.Dot(axis, boxUp)) * halfSize.y
             + Mathf.Abs(Vector3.Dot(axis, boxForward)) * halfSize.z;

        /// <summary>Half-width of an octahedron (vertices on its local axes at
        /// <paramref name="halfSize"/>) along <paramref name="axis"/> — a SHIELDED prism's shell
        /// once <paramref name="halfSize"/> is scaled by the shell's circumscribing scale.</summary>
        public static float OctahedronHalfExtentAlong(Vector3 axis, Vector3 right, Vector3 up,
                                                      Vector3 forward, Vector3 halfSize)
            => Mathf.Max(Mathf.Abs(Vector3.Dot(axis, right)) * halfSize.x,
               Mathf.Max(Mathf.Abs(Vector3.Dot(axis, up)) * halfSize.y,
                         Mathf.Abs(Vector3.Dot(axis, forward)) * halfSize.z));

        /// <summary>How far the hull reaches from <paramref name="origin"/> along
        /// <c>side * axis</c>: the furthest support point over every hull collider.</summary>
        public static float HullReach(Collider[] hull, Vector3 origin, Vector3 axis, float side)
        {
            Vector3 dir = axis * side;
            float reach = 0f;
            if (hull == null) return reach;
            foreach (var c in hull)
            {
                if (!c || !c.enabled) continue;
                Vector3 centre;
                float half;
                if (c is BoxCollider box)
                {
                    var t = box.transform;
                    centre = t.TransformPoint(box.center);
                    half = BoxHalfExtentAlong(dir, t.right, t.up, t.forward,
                        Vector3.Scale(box.size, Abs(t.lossyScale)) * 0.5f);
                }
                else
                {
                    // Anything else: its world AABB - conservative, so the prism lands a little wide.
                    var b = c.bounds;
                    centre = b.center;
                    half = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y
                         + Mathf.Abs(dir.z) * b.extents.z;
                }
                reach = Mathf.Max(reach, Vector3.Dot(centre - origin, dir) + half);
            }
            return reach;
        }

        /// <summary>The prism's half-width along <paramref name="axis"/> — its box, or its
        /// octahedral shell when shielded (the shell is what the hull actually touches).</summary>
        public static float PrismHalfExtentAlong(Prism prism, Vector3 axis)
        {
            var t = prism.transform;
            Vector3 half = Abs(t.lossyScale) * 0.5f;
            if (prism.TryGetComponent(out BoxCollider box))
                half = Vector3.Scale(box.size, Abs(t.lossyScale)) * 0.5f;
            return prism.prismProperties is { IsShielded: true }
                ? OctahedronHalfExtentAlong(axis, t.right, t.up, t.forward,
                    half * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE)
                : BoxHalfExtentAlong(axis, t.right, t.up, t.forward, half);
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
