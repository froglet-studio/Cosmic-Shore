using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Contact geometry of a super-shielded track prism, as the Skim Race pilot reasons about it.
    ///
    /// A super-shield engages a stellated octahedron (stella octangula) whose spike tips sit on the
    /// corners of the box of half-extents 1.5 x the prism's leaf size (CLAUDE.md, "a shield is 3x
    /// the prism it replaces"). That solid is the NON-CONVEX union of two regular tetrahedra
    /// inscribed in the box. In box-normalised coordinates u = p / half, tetrahedron A is
    /// { u.x+u.y+u.z, u.x-u.y-u.z, -u.x+u.y-u.z, -u.x-u.y+u.z } >= -1 and B is its mirror.
    ///
    /// <see cref="StellaDistance"/> is the EXACT Euclidean distance (world units) from a point to
    /// that union: zero inside either tetrahedron, otherwise the smaller of the two distances to a
    /// tetrahedron's surface (closest point on each of its four triangles, Ericson, Real-Time
    /// Collision Detection 5.1.5). It is the same construction the game's shell contact tier uses
    /// (<c>CosmicShore.Utility.ShieldShellMath</c>, which the offline harness cannot compile against
    /// Unity.Mathematics), and <c>SkimRaceShellTests</c> holds the two in agreement.
    ///
    /// It replaced a face-plane LOWER bound (max signed distance to a tetrahedron's planes), which
    /// under-reported the distance ~10.6x beside a track plate's long edge, so the pilot's guards and
    /// the simulator both saw contacts the game never registers. Shared by the pilot's hull guard
    /// and the offline simulator so the two agree on what counts as a contact.
    /// </summary>
    public static class SkimRaceShell
    {
        static readonly Vector3[] A = { new(1, 1, 1), new(1, -1, -1), new(-1, 1, -1), new(-1, -1, 1) };
        static readonly Vector3[] B = { new(-1, -1, -1), new(-1, 1, 1), new(1, -1, 1), new(1, 1, -1) };

        /// <summary>Exact distance (world units, 0 inside) from a point in the prism's LOCAL frame to the
        /// stella whose box half-extents are <paramref name="half"/>.</summary>
        public static float StellaDistance(Vector3 local, Vector3 half)
        {
            half = new Vector3(Mathf.Max(half.x, 1e-3f), Mathf.Max(half.y, 1e-3f), Mathf.Max(half.z, 1e-3f));
            return Mathf.Min(TetraDistance(local, half, A), TetraDistance(local, half, B));
        }

        /// <summary>The retired face-plane lower bound, kept only so tests can show what it got wrong.</summary>
        public static float FacePlaneBound(Vector3 local, Vector3 half)
        {
            half = new Vector3(Mathf.Max(half.x, 1e-3f), Mathf.Max(half.y, 1e-3f), Mathf.Max(half.z, 1e-3f));
            return Mathf.Max(0f, Mathf.Min(PlaneBound(local, half, A), PlaneBound(local, half, B)));
        }

        static float TetraDistance(Vector3 p, Vector3 h, Vector3[] v)
        {
            // Inside: on the inner side of all four face planes, i.e. dot(v_k, p/h) >= -1 for each of
            // the tetrahedron's own vertex directions v_k (see the class comment).
            Vector3 u = new(p.x / h.x, p.y / h.y, p.z / h.z);
            bool inside = true;
            for (int k = 0; k < 4 && inside; k++)
                if (Vector3.Dot(v[k], u) < -1f) inside = false;
            if (inside) return 0f;

            Vector3 w0 = Scale(v[0], h), w1 = Scale(v[1], h), w2 = Scale(v[2], h), w3 = Scale(v[3], h);
            float best = (ClosestOnTriangle(p, w0, w1, w2) - p).sqrMagnitude;
            best = Mathf.Min(best, (ClosestOnTriangle(p, w0, w1, w3) - p).sqrMagnitude);
            best = Mathf.Min(best, (ClosestOnTriangle(p, w0, w2, w3) - p).sqrMagnitude);
            best = Mathf.Min(best, (ClosestOnTriangle(p, w1, w2, w3) - p).sqrMagnitude);
            return Mathf.Sqrt(best);
        }

        static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);

        /// <summary>Closest point on triangle abc to p (Ericson, RTCD 5.1.5).</summary>
        static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + d1 / (d1 - d3) * ab;

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + d2 / (d2 - d6) * ac;

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                return b + (d4 - d3) / ((d4 - d3) + (d5 - d6)) * (c - b);

            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }

        static float PlaneBound(Vector3 p, Vector3 h, Vector3[] signs)
        {
            float worst = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                Vector3 s = signs[k];
                Vector3 n = new(-s.x / h.x, -s.y / h.y, -s.z / h.z);
                float f = Vector3.Dot(n, p) - 1f;
                float d = f / n.magnitude;
                if (d > worst) worst = d;
            }
            return worst;
        }
    }
}
