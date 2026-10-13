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
        public static float StellaDistance(Vector3 local, Vector3 half) => StellaDistance(local.x, local.y, local.z, half);

        /// <summary>
        /// <see cref="StellaDistance(Vector3, Vector3)"/> with the point as three floats - the form the
        /// pilot's per-step shell search calls. The whole kernel is written out in floats: it runs inside
        /// every rollout step, and the editor's Mono JIT pays for each Vector3 operator as a call and a
        /// struct copy. Every operation is the one the Vector3 form performed, in the same order
        /// (<c>SkimRaceCourseQueryTests</c> holds the two equal bit for bit).
        /// </summary>
        public static float StellaDistance(float x, float y, float z, Vector3 half)
        {
            float hx = Mathf.Max(half.x, 1e-3f), hy = Mathf.Max(half.y, 1e-3f), hz = Mathf.Max(half.z, 1e-3f);
            return Mathf.Min(TetraDistance(x, y, z, hx, hy, hz, AX, AY, AZ), TetraDistance(x, y, z, hx, hy, hz, BX, BY, BZ));
        }

        /// <summary>The retired face-plane lower bound, kept only so tests can show what it got wrong.</summary>
        public static float FacePlaneBound(Vector3 local, Vector3 half)
        {
            half = new Vector3(Mathf.Max(half.x, 1e-3f), Mathf.Max(half.y, 1e-3f), Mathf.Max(half.z, 1e-3f));
            return Mathf.Max(0f, Mathf.Min(PlaneBound(local, half, A), PlaneBound(local, half, B)));
        }

        // A and B by component, for the float kernel. Each sign is +-1, so sign * value is the value or
        // its negation exactly - the same numbers Vector3.Dot(v[k], u) and Scale(v[k], h) produced.
        static readonly float[] AX = { 1, 1, -1, -1 }, AY = { 1, -1, 1, -1 }, AZ = { 1, -1, -1, 1 };
        static readonly float[] BX = { -1, -1, 1, 1 }, BY = { -1, 1, -1, 1 }, BZ = { -1, 1, 1, -1 };

        static float TetraDistance(float px, float py, float pz, float hx, float hy, float hz,
            float[] sx, float[] sy, float[] sz)
        {
            // Inside: on the inner side of all four face planes, i.e. dot(v_k, p/h) >= -1 for each of
            // the tetrahedron's own vertex directions v_k (see the class comment).
            float ux = (float)(px / hx), uy = (float)(py / hy), uz = (float)(pz / hz);                // u = p / h
            bool inside = true;
            for (int k = 0; k < 4 && inside; k++)
            {
                float dot = (float)(sx[k] * ux + sy[k] * uy + sz[k] * uz);         // Vector3.Dot(v[k], u)
                if (dot < -1f) inside = false;
            }
            if (inside) return 0f;

            // The vertices v_k scaled by h.
            float x0 = sx[0] * hx, y0 = sy[0] * hy, z0 = sz[0] * hz;
            float x1 = sx[1] * hx, y1 = sy[1] * hy, z1 = sz[1] * hz;
            float x2 = sx[2] * hx, y2 = sy[2] * hy, z2 = sz[2] * hz;
            float x3 = sx[3] * hx, y3 = sy[3] * hy, z3 = sz[3] * hz;
            float best = TriangleSqr(px, py, pz, x0, y0, z0, x1, y1, z1, x2, y2, z2);
            float t = TriangleSqr(px, py, pz, x0, y0, z0, x1, y1, z1, x3, y3, z3);
            best = best < t ? best : t; // Mathf.Min(best, t)
            t = TriangleSqr(px, py, pz, x0, y0, z0, x2, y2, z2, x3, y3, z3);
            best = best < t ? best : t;
            t = TriangleSqr(px, py, pz, x1, y1, z1, x2, y2, z2, x3, y3, z3);
            best = best < t ? best : t;
            return Mathf.Sqrt(best);
        }

        /// <summary>
        /// Squared distance from p to triangle abc: the closest point (Ericson, RTCD 5.1.5) minus p,
        /// squared. In floats, rounding exactly where the Vector3 form rounds: every value it rounds -
        /// each component of a Vector3 it builds, each float it returns or passes - is rounded here with
        /// an explicit (float), each expression it keeps whole is whole here, and its own locals are
        /// plain locals. That matters because the editor's Mono computes inside an expression in double
        /// precision and, optimizing, keeps float locals in double registers, while a Vector3 component
        /// always lands in memory as a float (<c>SkimRaceCourseQueryTests</c> runs both forms).
        /// </summary>
        static float TriangleSqr(float px, float py, float pz, float ax, float ay, float az,
            float bx, float by, float bz, float cx, float cy, float cz)
        {
            float abx = (float)(bx - ax), aby = (float)(by - ay), abz = (float)(bz - az);             // ab = b - a
            float acx = (float)(cx - ax), acy = (float)(cy - ay), acz = (float)(cz - az);             // ac = c - a
            float apx = (float)(px - ax), apy = (float)(py - ay), apz = (float)(pz - az);             // ap = p - a
            float d1 = (float)(abx * apx + aby * apy + abz * apz);                  // Dot(ab, ap)
            float d2 = (float)(acx * apx + acy * apy + acz * apz);                  // Dot(ac, ap)
            float qx, qy, qz;
            if (d1 <= 0f && d2 <= 0f) { qx = ax; qy = ay; qz = az; }       // a
            else
            {
                float bpx = (float)(px - bx), bpy = (float)(py - by), bpz = (float)(pz - bz);         // bp = p - b
                float d3 = (float)(abx * bpx + aby * bpy + abz * bpz);              // Dot(ab, bp)
                float d4 = (float)(acx * bpx + acy * bpy + acz * bpz);              // Dot(ac, bp)
                if (d3 >= 0f && d4 <= d3) { qx = bx; qy = by; qz = bz; }   // b
                else
                {
                    float vc = d1 * d4 - d3 * d2;
                    if (vc <= 0f && d1 >= 0f && d3 <= 0f)
                    {
                        float v = (float)(d1 / (d1 - d3));                          // a + v * ab
                        float tx = (float)(abx * v), ty = (float)(aby * v), tz = (float)(abz * v);
                        qx = (float)(ax + tx); qy = (float)(ay + ty); qz = (float)(az + tz);
                    }
                    else
                    {
                        float cpx = (float)(px - cx), cpy = (float)(py - cy), cpz = (float)(pz - cz); // cp = p - c
                        float d5 = (float)(abx * cpx + aby * cpy + abz * cpz);      // Dot(ab, cp)
                        float d6 = (float)(acx * cpx + acy * cpy + acz * cpz);      // Dot(ac, cp)
                        if (d6 >= 0f && d5 <= d6) { qx = cx; qy = cy; qz = cz; } // c
                        else
                        {
                            float vb = d5 * d2 - d1 * d6;
                            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
                            {
                                float w = (float)(d2 / (d2 - d6));                  // a + w * ac
                                float tx = (float)(acx * w), ty = (float)(acy * w), tz = (float)(acz * w);
                                qx = (float)(ax + tx); qy = (float)(ay + ty); qz = (float)(az + tz);
                            }
                            else
                            {
                                float va = d3 * d6 - d5 * d4;
                                if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
                                {
                                    float w = (float)((d4 - d3) / ((d4 - d3) + (d5 - d6))); // b + w * (c - b)
                                    float cbx = (float)(cx - bx), cby = (float)(cy - by), cbz = (float)(cz - bz);
                                    float tx = (float)(cbx * w), ty = (float)(cby * w), tz = (float)(cbz * w);
                                    qx = (float)(bx + tx); qy = (float)(by + ty); qz = (float)(bz + tz);
                                }
                                else
                                {
                                    float denom = 1f / (va + vb + vc);     // a + ab * v + ac * w
                                    float v = (float)(vb * denom), w = (float)(vc * denom);
                                    float t1x = (float)(abx * v), t1y = (float)(aby * v), t1z = (float)(abz * v);
                                    float s1x = (float)(ax + t1x), s1y = (float)(ay + t1y), s1z = (float)(az + t1z);
                                    float t2x = (float)(acx * w), t2y = (float)(acy * w), t2z = (float)(acz * w);
                                    qx = (float)(s1x + t2x); qy = (float)(s1y + t2y); qz = (float)(s1z + t2z);
                                }
                            }
                        }
                    }
                }
            }
            float ex = (float)(qx - px), ey = (float)(qy - py), ez = (float)(qz - pz);                // q - p
            return (float)(ex * ex + ey * ey + ez * ez);                   // .sqrMagnitude
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
