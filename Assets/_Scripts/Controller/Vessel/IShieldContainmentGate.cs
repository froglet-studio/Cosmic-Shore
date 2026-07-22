using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Analytic containment gate for an ENGAGED prism shield shell (octahedron /
    /// stellated octahedron). Implemented by the shield components so the impact
    /// dispatch narrowphase can refine the enlarged shielded broadphase box down
    /// to the visible shell without any mesh collider.
    /// See Docs/CollisionLOD/DESIGN.md §3.1.
    /// </summary>
    public interface IShieldContainmentGate
    {
        /// <summary>
        /// Signed margin of a WORLD point vs the engaged shell surface.
        /// &gt; 0 inside, 0 on surface, &lt; 0 outside (normalized;
        /// magnitude ∝ distance to the surface).
        /// </summary>
        float SignedMargin(Vector3 worldPoint);

        /// <summary>
        /// Signed margin of a WORLD sphere vs the engaged shell surface:
        /// the point margin at the sphere's centre plus the radius scaled by
        /// the shell margin's world-space gradient magnitude. ≥ 0 means the
        /// sphere reaches the shell. Conservative (over-estimates) across
        /// octant/facet boundaries — the safe direction: it never creates a
        /// skim dead zone. See Docs/CollisionLOD/DESIGN.md §7.
        /// </summary>
        float SignedMarginSphere(Vector3 worldCentre, float worldRadius);

        /// <summary>
        /// World-space unit direction of steepest margin INCREASE at a world point
        /// (i.e. toward the shell interior — the inward normal of the nearest
        /// facet). The dispatch narrowphase queries a non-sphere collider's SUPPORT
        /// point in this direction (its farthest point toward the shell), so a hull
        /// is tested where it actually reaches the octahedron/stella surface — the
        /// thin tips included — instead of at a single centre-facing point.
        /// </summary>
        Vector3 ShellInwardNormal(Vector3 worldPoint);

        /// <summary>Convenience: inside or on the surface (SignedMargin ≥ 0).</summary>
        bool ContainsWorldPoint(Vector3 worldPoint);

        /// <summary>
        /// Exact analytic overlap test between the engaged shell and a world-space
        /// oriented box (OBB), the analytic equivalent of a convex octahedron/stella
        /// mesh collider vs a BoxCollider. This is a Separating-Axis Test run in the
        /// shell's normalized frame — no false-reject at the thin tips, unlike the old
        /// two-point support sample.
        ///
        /// <paramref name="worldAxisX"/>/<paramref name="worldAxisY"/>/<paramref name="worldAxisZ"/>
        /// are the box's world-space HALF-EDGE vectors (already rotated + scaled: their
        /// length is the half-size along each box axis). From a BoxCollider bc:
        ///   worldCenter = bc.transform.TransformPoint(bc.center);
        ///   worldAxisX  = bc.transform.TransformVector(new Vector3(bc.size.x*0.5f,0,0)); (etc.)
        ///
        /// <paramref name="inflate"/> is a margin slack in NORMALIZED shell units:
        /// 0 for containment/pop (exact SAT), a positive value grows the shell for a
        /// grazing band (pass the magnitude of a negative <c>ShieldMarginThreshold</c>).
        /// Returns true when the shell and the box overlap.
        /// </summary>
        bool OverlapsWorldBox(Vector3 worldCenter, Vector3 worldAxisX, Vector3 worldAxisY,
            Vector3 worldAxisZ, float inflate);
    }
}
