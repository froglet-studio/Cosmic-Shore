using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Contact geometry of a super-shielded track prism, as the Skim Race pilot reasons about it.
    ///
    /// A super-shield engages a stellated octahedron (stella octangula) whose spike tips sit on the
    /// corners of the box of half-extents 1.5 x the prism's leaf size (CLAUDE.md, "a shield is 3x
    /// the prism it replaces"). That solid is the union of two regular tetrahedra inscribed in the
    /// box. In box-normalised coordinates u = p / half, tetrahedron A is
    /// { u.x+u.y+u.z, u.x-u.y-u.z, -u.x+u.y-u.z, -u.x-u.y+u.z } >= -1 and B is its mirror.
    ///
    /// <see cref="StellaDistance"/> returns, for each tetrahedron, the largest signed distance to
    /// its face planes (in world units) and takes the smaller of the two. For a convex solid that
    /// face-plane bound never OVERstates the true distance, so a hull test built on it can only be
    /// conservative. It is shared by the pilot's hull guard and the offline simulator so the two
    /// agree on what counts as a contact.
    /// </summary>
    public static class SkimRaceShell
    {
        static readonly Vector3[] A = { new(1, 1, 1), new(1, -1, -1), new(-1, 1, -1), new(-1, -1, 1) };
        static readonly Vector3[] B = { new(-1, -1, -1), new(-1, 1, 1), new(1, -1, 1), new(1, 1, -1) };

        /// <summary>Distance (world units, 0 inside) from a point in the prism's LOCAL frame to the stella
        /// whose box half-extents are <paramref name="half"/>.</summary>
        public static float StellaDistance(Vector3 local, Vector3 half)
        {
            half = new Vector3(Mathf.Max(half.x, 1e-3f), Mathf.Max(half.y, 1e-3f), Mathf.Max(half.z, 1e-3f));
            return Mathf.Max(0f, Mathf.Min(TetraDistance(local, half, A), TetraDistance(local, half, B)));
        }

        static float TetraDistance(Vector3 p, Vector3 h, Vector3[] signs)
        {
            float worst = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                Vector3 s = signs[k];
                // plane: -(s.x x/hx + s.y y/hy + s.z z/hz) <= 1 is inside, i.e. f = -(s . (p/h)) - 1 <= 0
                Vector3 n = new(-s.x / h.x, -s.y / h.y, -s.z / h.z);
                float f = Vector3.Dot(n, p) - 1f;
                float d = f / n.magnitude;
                if (d > worst) worst = d;
            }
            return worst;
        }
    }
}
