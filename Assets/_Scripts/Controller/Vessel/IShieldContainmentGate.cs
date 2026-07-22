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

        /// <summary>Convenience: inside or on the surface (SignedMargin ≥ 0).</summary>
        bool ContainsWorldPoint(Vector3 worldPoint);
    }
}
