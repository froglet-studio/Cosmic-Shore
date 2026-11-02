using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Exact analytic overlap predicates between collision probes (sphere,
    /// capsule, oriented box) and the two shield shell shapes:
    ///
    ///  - SHIELDED: the octahedron circumscribing the prism's authored box —
    ///    the L1 unit ball in the shell's normalized frame.
    ///  - SUPER-SHIELDED: the stellated octahedron, the NON-CONVEX UNION of
    ///    two tetrahedra at alternating corners of the normalized cube
    ///    [-1,1]^3. Union semantics are exact: a probe touching a spike tip
    ///    overlaps; a probe threaded between two spikes inside the bounding
    ///    box does NOT (a convex hull or AABB approximation would).
    ///
    /// Shell frame convention: world center C; mutually orthogonal world
    /// semi-axis vectors U/V/W = prismWorldAxis_i * (shieldScale * halfExtent_i)
    /// (rigid rotation x axis-aligned scale — no shear). Normalized coords of a
    /// world point p: n_i = dot(p - C, A_i) / |A_i|^2.
    ///
    /// Booleans (membership, segment clipping, SAT) run in the normalized frame
    /// where both shells are unit shapes; distances (sphere/capsule surface
    /// proximity) run in WORLD space because distance is not affine-invariant
    /// under the non-uniform semi-axes.
    ///
    /// Every predicate is Burst-compatible (no managed state) and was validated
    /// against independent ground truth (QP distance / LP feasibility) over
    /// 7,200 randomized poses + landmark cases (spike-tip graze, inter-spike
    /// gap, face/edge/vertex grazes) with zero disagreements before porting.
    /// The box-vs-shell SAT axis sets reproduce the derivation verified in the
    /// collision-LOD branch (25 axes vs the octahedron; 25 per tetrahedron).
    /// </summary>
    public static class ShieldShellMath
    {
        /// <summary>
        /// One shield shell, fully world-posed. Build via <see cref="CreateFrame"/>
        /// on the main thread or inside a job from stored rotation + semi-axes.
        /// </summary>
        public struct ShellFrame
        {
            public float3 Center;
            public float3 AxisX, AxisY, AxisZ;          // world semi-axis vectors
            public float3 InvRowX, InvRowY, InvRowZ;    // rows of M^-1 (orthogonal axes)
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ShellFrame CreateFrame(float3 center, quaternion rotation, float3 semiAxes)
        {
            var f = new ShellFrame { Center = center };
            f.AxisX = math.mul(rotation, new float3(semiAxes.x, 0f, 0f));
            f.AxisY = math.mul(rotation, new float3(0f, semiAxes.y, 0f));
            f.AxisZ = math.mul(rotation, new float3(0f, 0f, semiAxes.z));
            // Orthogonal axes: M^-1 rows are axis / |axis|^2.
            f.InvRowX = f.AxisX / math.max(math.lengthsq(f.AxisX), 1e-12f);
            f.InvRowY = f.AxisY / math.max(math.lengthsq(f.AxisY), 1e-12f);
            f.InvRowZ = f.AxisZ / math.max(math.lengthsq(f.AxisZ), 1e-12f);
            return f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 ToNormalized(in ShellFrame s, float3 worldPoint)
        {
            float3 d = worldPoint - s.Center;
            return new float3(math.dot(d, s.InvRowX), math.dot(d, s.InvRowY), math.dot(d, s.InvRowZ));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 ToNormalizedVector(in ShellFrame s, float3 worldVector)
            => new float3(math.dot(worldVector, s.InvRowX),
                          math.dot(worldVector, s.InvRowY),
                          math.dot(worldVector, s.InvRowZ));

        // ------------------------------------------------------------------
        // Membership (normalized frame)
        // ------------------------------------------------------------------

        // Octahedron: |x| + |y| + |z| <= 1.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool OctaContainsN(float3 n) => math.abs(n.x) + math.abs(n.y) + math.abs(n.z) <= 1f;

        // The 4 linear forms shared by both tetrahedra:
        // f0=(1,1,1), f1=(1,-1,-1), f2=(-1,1,-1), f3=(-1,-1,1).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float4 TetForms(float3 n) => new float4(
            n.x + n.y + n.z,
            n.x - n.y - n.z,
            -n.x + n.y - n.z,
            -n.x - n.y + n.z);

        // Tet A (verts (1,1,1),(1,-1,-1),(-1,1,-1),(-1,-1,1)): min_i f_i >= -1.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TetAContainsN(float3 n) => math.cmin(TetForms(n)) >= -1f;

        // Tet B (the other 4 cube corners, = -TetA): max_i f_i <= 1.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TetBContainsN(float3 n) => math.cmax(TetForms(n)) <= 1f;

        // ------------------------------------------------------------------
        // Closest-point primitives (world frame) — Ericson, RTCD 5.1.5 / 5.1.9
        // ------------------------------------------------------------------

        static float3 ClosestPtPointTriangle(float3 p, float3 a, float3 b, float3 c)
        {
            float3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = math.dot(ab, ap), d2 = math.dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;

            float3 bp = p - b;
            float d3 = math.dot(ab, bp), d4 = math.dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + (d1 / (d1 - d3)) * ab;

            float3 cp = p - c;
            float d5 = math.dot(ab, cp), d6 = math.dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + (d2 / (d2 - d6)) * ac;

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
                return b + ((d4 - d3) / ((d4 - d3) + (d5 - d6))) * (c - b);

            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float DistSqPointTriangle(float3 p, float3 a, float3 b, float3 c)
            => math.distancesq(p, ClosestPtPointTriangle(p, a, b, c));

        static float DistSqSegmentSegment(float3 p1, float3 q1, float3 p2, float3 q2)
        {
            const float EPS = 1e-12f;
            float3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = math.dot(d1, d1), e = math.dot(d2, d2), f = math.dot(d2, r);
            float s, t;
            if (a <= EPS && e <= EPS) return math.lengthsq(r);
            if (a <= EPS)
            {
                s = 0f;
                t = math.clamp(f / e, 0f, 1f);
            }
            else
            {
                float c = math.dot(d1, r);
                if (e <= EPS)
                {
                    t = 0f;
                    s = math.clamp(-c / a, 0f, 1f);
                }
                else
                {
                    float b = math.dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > EPS ? math.clamp((b * f - c * e) / denom, 0f, 1f) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = math.clamp(-c / a, 0f, 1f);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = math.clamp((b - c) / a, 0f, 1f);
                    }
                }
            }
            return math.distancesq(p1 + d1 * s, p2 + d2 * t);
        }

        // Moller-Trumbore. The near-parallel case returns false and is covered
        // exactly by the edge/vertex distance path in DistSqSegmentTriangle.
        static bool SegmentIntersectsTriangle(float3 a, float3 b, float3 t0, float3 t1, float3 t2)
        {
            float3 e1 = t1 - t0, e2 = t2 - t0, d = b - a;
            float3 h = math.cross(d, e2);
            float det = math.dot(e1, h);
            if (math.abs(det) < 1e-14f) return false;
            float inv = 1f / det;
            float3 s = a - t0;
            float u = math.dot(s, h) * inv;
            if (u < 0f || u > 1f) return false;
            float3 q = math.cross(s, e1);
            float v = math.dot(d, q) * inv;
            if (v < 0f || u + v > 1f) return false;
            float t = math.dot(e2, q) * inv;
            return t >= 0f && t <= 1f;
        }

        static float DistSqSegmentTriangle(float3 a, float3 b, float3 t0, float3 t1, float3 t2)
        {
            if (SegmentIntersectsTriangle(a, b, t0, t1, t2)) return 0f;
            float best = DistSqSegmentSegment(a, b, t0, t1);
            best = math.min(best, DistSqSegmentSegment(a, b, t1, t2));
            best = math.min(best, DistSqSegmentSegment(a, b, t2, t0));
            best = math.min(best, DistSqPointTriangle(a, t0, t1, t2));
            best = math.min(best, DistSqPointTriangle(b, t0, t1, t2));
            return best;
        }

        // ------------------------------------------------------------------
        // Surface distance to the shells (world frame)
        // ------------------------------------------------------------------

        // Min squared distance from p to the octahedron surface: 8 triangular
        // faces (s1*U, s2*V, s3*W), sign combos unrolled via bit walk.
        static float DistSqPointOctaSurface(in ShellFrame s, float3 p)
        {
            float best = float.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                float3 va = s.Center + (((m & 1) == 0) ? s.AxisX : -s.AxisX);
                float3 vb = s.Center + (((m & 2) == 0) ? s.AxisY : -s.AxisY);
                float3 vc = s.Center + (((m & 4) == 0) ? s.AxisZ : -s.AxisZ);
                best = math.min(best, DistSqPointTriangle(p, va, vb, vc));
            }
            return best;
        }

        static float DistSqSegmentOctaSurface(in ShellFrame s, float3 a, float3 b)
        {
            float best = float.MaxValue;
            for (int m = 0; m < 8; m++)
            {
                float3 va = s.Center + (((m & 1) == 0) ? s.AxisX : -s.AxisX);
                float3 vb = s.Center + (((m & 2) == 0) ? s.AxisY : -s.AxisY);
                float3 vc = s.Center + (((m & 4) == 0) ? s.AxisZ : -s.AxisZ);
                best = math.min(best, DistSqSegmentTriangle(a, b, va, vb, vc));
            }
            return best;
        }

        // World vertices of one tetrahedron. tetB negates all four.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void TetWorldVerts(in ShellFrame s, bool tetB, out float3 v0, out float3 v1, out float3 v2, out float3 v3)
        {
            float sign = tetB ? -1f : 1f;
            v0 = s.Center + sign * (s.AxisX + s.AxisY + s.AxisZ);
            v1 = s.Center + sign * (s.AxisX - s.AxisY - s.AxisZ);
            v2 = s.Center + sign * (-s.AxisX + s.AxisY - s.AxisZ);
            v3 = s.Center + sign * (-s.AxisX - s.AxisY + s.AxisZ);
        }

        static float DistSqPointTetSurface(in ShellFrame s, bool tetB, float3 p)
        {
            TetWorldVerts(in s, tetB, out var v0, out var v1, out var v2, out var v3);
            float best = DistSqPointTriangle(p, v0, v1, v2);
            best = math.min(best, DistSqPointTriangle(p, v0, v1, v3));
            best = math.min(best, DistSqPointTriangle(p, v0, v2, v3));
            best = math.min(best, DistSqPointTriangle(p, v1, v2, v3));
            return best;
        }

        static float DistSqSegmentTetSurface(in ShellFrame s, bool tetB, float3 a, float3 b)
        {
            TetWorldVerts(in s, tetB, out var v0, out var v1, out var v2, out var v3);
            float best = DistSqSegmentTriangle(a, b, v0, v1, v2);
            best = math.min(best, DistSqSegmentTriangle(a, b, v0, v1, v3));
            best = math.min(best, DistSqSegmentTriangle(a, b, v0, v2, v3));
            best = math.min(best, DistSqSegmentTriangle(a, b, v1, v2, v3));
            return best;
        }

        // ------------------------------------------------------------------
        // Segment clipping against the shells (normalized frame, exact boolean)
        // ------------------------------------------------------------------

        // Clip a normalized-frame segment against dot(N, x) <= 1 for the 8
        // octahedron planes N = (+-1, +-1, +-1). Nonempty clip => intersects.
        static bool SegmentHitsOctaN(float3 aN, float3 bN)
        {
            float t0 = 0f, t1 = 1f;
            float3 d = bN - aN;
            for (int m = 0; m < 8; m++)
            {
                float3 n = new float3((m & 1) == 0 ? 1f : -1f,
                                      (m & 2) == 0 ? 1f : -1f,
                                      (m & 4) == 0 ? 1f : -1f);
                if (!ClipHalfSpace(n, 1f, aN, d, ref t0, ref t1)) return false;
            }
            return true;
        }

        // TetA = { f_i(n) >= -1 }  <=>  { dot(-F_i, n) <= 1 }.
        static bool SegmentHitsTetAN(float3 aN, float3 bN)
        {
            float t0 = 0f, t1 = 1f;
            float3 d = bN - aN;
            if (!ClipHalfSpace(new float3(-1f, -1f, -1f), 1f, aN, d, ref t0, ref t1)) return false;
            if (!ClipHalfSpace(new float3(-1f, 1f, 1f), 1f, aN, d, ref t0, ref t1)) return false;
            if (!ClipHalfSpace(new float3(1f, -1f, 1f), 1f, aN, d, ref t0, ref t1)) return false;
            if (!ClipHalfSpace(new float3(1f, 1f, -1f), 1f, aN, d, ref t0, ref t1)) return false;
            return true;
        }

        // TetB = { f_i(n) <= 1 }  <=>  { dot(F_i, n) <= 1 }.
        static bool SegmentHitsTetBN(float3 aN, float3 bN)
        {
            float t0 = 0f, t1 = 1f;
            float3 d = bN - aN;
            if (!ClipHalfSpace(new float3(1f, 1f, 1f), 1f, aN, d, ref t0, ref t1)) return false;
            if (!ClipHalfSpace(new float3(1f, -1f, -1f), 1f, aN, d, ref t0, ref t1)) return false;
            if (!ClipHalfSpace(new float3(-1f, 1f, -1f), 1f, aN, d, ref t0, ref t1)) return false;
            if (!ClipHalfSpace(new float3(-1f, -1f, 1f), 1f, aN, d, ref t0, ref t1)) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool ClipHalfSpace(float3 n, float offset, float3 aN, float3 d, ref float t0, ref float t1)
        {
            float dn = math.dot(n, d);
            float an = math.dot(n, aN);
            if (math.abs(dn) < 1e-14f) return an <= offset;
            float t = (offset - an) / dn;
            if (dn > 0f) t1 = math.min(t1, t);
            else t0 = math.max(t0, t);
            return t0 <= t1;
        }

        // ------------------------------------------------------------------
        // PUBLIC PREDICATES — sphere
        // ------------------------------------------------------------------

        /// <summary>Exact sphere-vs-octahedron-shell overlap.</summary>
        public static bool SphereOverlapsOcta(in ShellFrame s, float3 center, float radius)
        {
            if (OctaContainsN(ToNormalized(in s, center))) return true;
            return DistSqPointOctaSurface(in s, center) <= radius * radius;
        }

        /// <summary>
        /// Exact sphere-vs-stella overlap: union of the two tetrahedra. A sphere
        /// in the inter-spike gap overlaps NEITHER tet and returns false.
        /// </summary>
        public static bool SphereOverlapsStella(in ShellFrame s, float3 center, float radius)
        {
            float3 n = ToNormalized(in s, center);
            if (TetAContainsN(n) || TetBContainsN(n)) return true;
            float rSq = radius * radius;
            if (DistSqPointTetSurface(in s, false, center) <= rSq) return true;
            return DistSqPointTetSurface(in s, true, center) <= rSq;
        }

        // ------------------------------------------------------------------
        // PUBLIC PREDICATES — capsule (segment + radius)
        // ------------------------------------------------------------------

        /// <summary>Exact capsule-vs-octahedron-shell overlap.</summary>
        public static bool CapsuleOverlapsOcta(in ShellFrame s, float3 a, float3 b, float radius)
        {
            if (SegmentHitsOctaN(ToNormalized(in s, a), ToNormalized(in s, b))) return true;
            return DistSqSegmentOctaSurface(in s, a, b) <= radius * radius;
        }

        /// <summary>Exact capsule-vs-stella overlap (union of the two tetrahedra).</summary>
        public static bool CapsuleOverlapsStella(in ShellFrame s, float3 a, float3 b, float radius)
        {
            float3 aN = ToNormalized(in s, a);
            float3 bN = ToNormalized(in s, b);
            if (SegmentHitsTetAN(aN, bN) || SegmentHitsTetBN(aN, bN)) return true;
            float rSq = radius * radius;
            if (DistSqSegmentTetSurface(in s, false, a, b) <= rSq) return true;
            return DistSqSegmentTetSurface(in s, true, a, b) <= rSq;
        }

        // ------------------------------------------------------------------
        // PUBLIC PREDICATES — oriented box (SAT, normalized frame)
        // ------------------------------------------------------------------

        /// <summary>
        /// Exact OBB-vs-octahedron-shell overlap. World box: center + half-edge
        /// vectors e1/e2/e3. Mapped into the normalized frame (a general
        /// parallelepiped) and tested with the complete 25-axis SAT: 4
        /// octahedron face normals, 3 box face normals, 18 edge crosses.
        /// </summary>
        public static bool BoxOverlapsOcta(in ShellFrame s, float3 boxCenter, float3 e1, float3 e2, float3 e3)
        {
            float3 cN = ToNormalized(in s, boxCenter);
            float3 f1 = ToNormalizedVector(in s, e1);
            float3 f2 = ToNormalizedVector(in s, e2);
            float3 f3 = ToNormalizedVector(in s, e3);
            return BoxOverlapsOctaN(cN, f1, f2, f3);
        }

        /// <summary>
        /// Exact OBB-vs-stella overlap: SAT against each tetrahedron, OR'd
        /// (union semantics — a box in the inter-spike gap overlaps neither).
        /// </summary>
        public static bool BoxOverlapsStella(in ShellFrame s, float3 boxCenter, float3 e1, float3 e2, float3 e3)
        {
            float3 cN = ToNormalized(in s, boxCenter);
            float3 f1 = ToNormalizedVector(in s, e1);
            float3 f2 = ToNormalizedVector(in s, e2);
            float3 f3 = ToNormalizedVector(in s, e3);
            return BoxOverlapsTetN(false, cN, f1, f2, f3) || BoxOverlapsTetN(true, cN, f1, f2, f3);
        }

        // ------------------------------------------------------------------
        // PUBLIC PREDICATES — plain BOX shell (the prism's own authored box)
        //
        // An UNSHIELDED prism's exact surface IS its authored box, so these are
        // not a fidelity improvement over the PhysX trigger — they exist so the
        // shell tier can own a prism whose COLLIDER cannot be trusted: one whose
        // pose is driven per-frame off the GPU, and one sampled at the render
        // rate rather than the 25 Hz physics tick. Shape parity with PhysX is
        // therefore the CORRECTNESS BAR here, not a bonus: any disagreement is a
        // behaviour change, which is why these are validated against brute force
        // exactly as the octahedron/stella predicates were.
        // ------------------------------------------------------------------

        /// <summary>Exact sphere-vs-box-shell overlap.</summary>
        public static bool SphereOverlapsBox(in ShellFrame s, float3 center, float radius)
        {
            float3 n = ToNormalized(in s, center);
            if (math.cmax(math.abs(n)) <= 1f) return true;
            return DistSqPointBoxSurface(in s, center) <= radius * radius;
        }

        /// <summary>Exact capsule-vs-box-shell overlap.</summary>
        public static bool CapsuleOverlapsBox(in ShellFrame s, float3 a, float3 b, float radius)
        {
            if (SegmentHitsBoxN(ToNormalized(in s, a), ToNormalized(in s, b))) return true;
            return DistSqSegmentBoxSurface(in s, a, b) <= radius * radius;
        }

        /// <summary>
        /// Exact oriented-box-vs-box-shell overlap. The PROBE may be a general
        /// parallelepiped (TransformVector under non-uniform parent scale does not
        /// preserve orthogonality), so its separating axes are the three edge-pair
        /// CROSSES, never its own edge vectors — the shell's axes are orthogonal and
        /// are its own face normals. 15 axes: 3 shell faces, 3 probe faces, 9 crosses.
        /// </summary>
        public static bool BoxOverlapsBox(in ShellFrame s, float3 boxCenter, float3 e1, float3 e2, float3 e3)
        {
            float3 d = boxCenter - s.Center;
            float3 u0 = s.AxisX, u1 = s.AxisY, u2 = s.AxisZ;

            if (SeparatedBoxBox(u0, d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(u1, d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(u2, d, u0, u1, u2, e1, e2, e3)) return false;

            if (SeparatedBoxBox(math.cross(e1, e2), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(e2, e3), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(e3, e1), d, u0, u1, u2, e1, e2, e3)) return false;

            if (SeparatedBoxBox(math.cross(u0, e1), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u0, e2), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u0, e3), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u1, e1), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u1, e2), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u1, e3), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u2, e1), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u2, e2), d, u0, u1, u2, e1, e2, e3)) return false;
            if (SeparatedBoxBox(math.cross(u2, e3), d, u0, u1, u2, e1, e2, e3)) return false;

            return true;
        }

        // A degenerate axis (parallel edges) carries no information — the parallel
        // case is already covered by the two face-normal sets, so skipping it is
        // exact, not an approximation.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool SeparatedBoxBox(float3 axis, float3 d,
                                    float3 u0, float3 u1, float3 u2,
                                    float3 e1, float3 e2, float3 e3)
        {
            if (math.lengthsq(axis) < 1e-12f) return false;
            float rA = math.abs(math.dot(u0, axis)) + math.abs(math.dot(u1, axis)) + math.abs(math.dot(u2, axis));
            float rB = math.abs(math.dot(e1, axis)) + math.abs(math.dot(e2, axis)) + math.abs(math.dot(e3, axis));
            return math.abs(math.dot(d, axis)) > rA + rB;
        }

        // Closest point on the box (clamped in the normalized frame, measured in
        // WORLD space — distance is not affine-invariant under non-uniform axes).
        static float DistSqPointBoxSurface(in ShellFrame s, float3 p)
        {
            float3 n = ToNormalized(in s, p);
            float3 c = math.clamp(n, -1f, 1f);
            float3 world = s.Center + c.x * s.AxisX + c.y * s.AxisY + c.z * s.AxisZ;
            return math.distancesq(p, world);
        }

        // Segment vs the box's 6 faces as 12 triangles — the same construction
        // DistSqSegmentOctaSurface uses over the octahedron's 8 faces.
        static float DistSqSegmentBoxSurface(in ShellFrame s, float3 a, float3 b)
        {
            float best = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                float3 nAxis = axis == 0 ? s.AxisX : axis == 1 ? s.AxisY : s.AxisZ;
                float3 t1 = axis == 0 ? s.AxisY : axis == 1 ? s.AxisZ : s.AxisX;
                float3 t2 = axis == 0 ? s.AxisZ : axis == 1 ? s.AxisX : s.AxisY;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float3 f = s.Center + sign * nAxis;
                    float3 v00 = f - t1 - t2, v10 = f + t1 - t2, v11 = f + t1 + t2, v01 = f - t1 + t2;
                    best = math.min(best, DistSqSegmentTriangle(a, b, v00, v10, v11));
                    best = math.min(best, DistSqSegmentTriangle(a, b, v00, v11, v01));
                }
            }
            return best;
        }

        // Segment vs the unit cube in the normalized frame (slab clipping).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool SegmentHitsBoxN(float3 aN, float3 bN)
        {
            float t0 = 0f, t1 = 1f;
            float3 d = bN - aN;
            for (int i = 0; i < 3; i++)
            {
                float ai = i == 0 ? aN.x : i == 1 ? aN.y : aN.z;
                float di = i == 0 ? d.x : i == 1 ? d.y : d.z;
                if (math.abs(di) < 1e-12f)
                {
                    if (ai < -1f || ai > 1f) return false;
                    continue;
                }
                float lo = (-1f - ai) / di, hi = (1f - ai) / di;
                if (lo > hi) { float tmp = lo; lo = hi; hi = tmp; }
                t0 = math.max(t0, lo);
                t1 = math.min(t1, hi);
                if (t0 > t1) return false;
            }
            return true;
        }

        // Octahedron support on axis a is the L-infinity norm of a (L1-ball dual).
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool SeparatedOnAxisOcta(float3 a, float3 cN, float3 e1, float3 e2, float3 e3)
        {
            if (math.lengthsq(a) < 1e-12f) return false;
            float octR = math.cmax(math.abs(a));
            float cProj = math.dot(a, cN);
            float boxR = math.abs(math.dot(a, e1)) + math.abs(math.dot(a, e2)) + math.abs(math.dot(a, e3));
            return cProj - boxR > octR || cProj + boxR < -octR;
        }

        static bool BoxOverlapsOctaN(float3 cN, float3 e1, float3 e2, float3 e3)
        {
            // (a) 4 octahedron face normals (non-antipodal of (+-1,+-1,+-1)).
            if (SeparatedOnAxisOcta(new float3(1f, 1f, 1f), cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisOcta(new float3(1f, -1f, -1f), cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisOcta(new float3(-1f, 1f, -1f), cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisOcta(new float3(-1f, -1f, 1f), cN, e1, e2, e3)) return false;

            // (b) 3 box face normals.
            if (SeparatedOnAxisOcta(math.cross(e1, e2), cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisOcta(math.cross(e2, e3), cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisOcta(math.cross(e3, e1), cN, e1, e2, e3)) return false;

            // (c) 18 edge crosses: 6 octahedron edge dirs x 3 box edges.
            if (EdgeCrossesSeparateOcta(new float3(1f, -1f, 0f), cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateOcta(new float3(1f, 1f, 0f), cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateOcta(new float3(1f, 0f, -1f), cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateOcta(new float3(1f, 0f, 1f), cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateOcta(new float3(0f, 1f, -1f), cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateOcta(new float3(0f, 1f, 1f), cN, e1, e2, e3)) return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EdgeCrossesSeparateOcta(float3 edgeDir, float3 cN, float3 e1, float3 e2, float3 e3)
        {
            return SeparatedOnAxisOcta(math.cross(edgeDir, e1), cN, e1, e2, e3)
                || SeparatedOnAxisOcta(math.cross(edgeDir, e2), cN, e1, e2, e3)
                || SeparatedOnAxisOcta(math.cross(edgeDir, e3), cN, e1, e2, e3);
        }

        // Tet projection interval on axis a, from its 4 normalized vertices.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool SeparatedOnAxisTet(float3 a, bool tetB, float3 cN, float3 e1, float3 e2, float3 e3)
        {
            if (math.lengthsq(a) < 1e-12f) return false;
            float sign = tetB ? -1f : 1f;
            float p0 = math.dot(a, sign * new float3(1f, 1f, 1f));
            float p1 = math.dot(a, sign * new float3(1f, -1f, -1f));
            float p2 = math.dot(a, sign * new float3(-1f, 1f, -1f));
            float p3 = math.dot(a, sign * new float3(-1f, -1f, 1f));
            float tMin = math.min(math.min(p0, p1), math.min(p2, p3));
            float tMax = math.max(math.max(p0, p1), math.max(p2, p3));
            float cProj = math.dot(a, cN);
            float boxR = math.abs(math.dot(a, e1)) + math.abs(math.dot(a, e2)) + math.abs(math.dot(a, e3));
            return cProj - boxR > tMax || cProj + boxR < tMin;
        }

        static bool BoxOverlapsTetN(bool tetB, float3 cN, float3 e1, float3 e2, float3 e3)
        {
            // (a) 4 tet face normals (the linear-form coefficient vectors;
            //     identical for both tets — Tet B's planes are Tet A's negated).
            if (SeparatedOnAxisTet(new float3(1f, 1f, 1f), tetB, cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisTet(new float3(1f, -1f, -1f), tetB, cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisTet(new float3(-1f, 1f, -1f), tetB, cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisTet(new float3(-1f, -1f, 1f), tetB, cN, e1, e2, e3)) return false;

            // (b) 3 box face normals.
            if (SeparatedOnAxisTet(math.cross(e1, e2), tetB, cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisTet(math.cross(e2, e3), tetB, cN, e1, e2, e3)) return false;
            if (SeparatedOnAxisTet(math.cross(e3, e1), tetB, cN, e1, e2, e3)) return false;

            // (c) 18 edge crosses: 6 tet edge dirs (the octahedron's silhouette
            //     edges, shared by both tets) x 3 box edges.
            if (EdgeCrossesSeparateTet(new float3(0f, 1f, 1f), tetB, cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateTet(new float3(1f, 0f, 1f), tetB, cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateTet(new float3(1f, 1f, 0f), tetB, cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateTet(new float3(1f, -1f, 0f), tetB, cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateTet(new float3(1f, 0f, -1f), tetB, cN, e1, e2, e3)) return false;
            if (EdgeCrossesSeparateTet(new float3(0f, 1f, -1f), tetB, cN, e1, e2, e3)) return false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool EdgeCrossesSeparateTet(float3 edgeDir, bool tetB, float3 cN, float3 e1, float3 e2, float3 e3)
        {
            return SeparatedOnAxisTet(math.cross(edgeDir, e1), tetB, cN, e1, e2, e3)
                || SeparatedOnAxisTet(math.cross(edgeDir, e2), tetB, cN, e1, e2, e3)
                || SeparatedOnAxisTet(math.cross(edgeDir, e3), tetB, cN, e1, e2, e3);
        }
    }
}
