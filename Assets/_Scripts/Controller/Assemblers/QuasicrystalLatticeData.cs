using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vertex of the icosahedral quasilattice: its exact address in the six-dimensional
    /// hypercubic lattice Z^6. Six integers - so occupancy, territory and reproduction are
    /// exact integer bookkeeping, even though the three-dimensional pattern the colony
    /// grows NEVER REPEATS. Lexicographic ordering backs the deterministic tie-breaks.
    /// </summary>
    public readonly struct QuasicrystalVertex : IEquatable<QuasicrystalVertex>, IComparable<QuasicrystalVertex>
    {
        public readonly int N0, N1, N2, N3, N4, N5;

        public QuasicrystalVertex(int n0, int n1, int n2, int n3, int n4, int n5)
        {
            N0 = n0; N1 = n1; N2 = n2; N3 = n3; N4 = n4; N5 = n5;
        }

        public static readonly QuasicrystalVertex Zero = new(0, 0, 0, 0, 0, 0);

        public int Component(int axis) => axis switch
        {
            0 => N0, 1 => N1, 2 => N2, 3 => N3, 4 => N4, _ => N5
        };

        /// <summary>One lattice step along an axis. Bond deltas ADD here - upstairs in Z^6
        /// the lattice is honestly Euclidean, so the mirror-composition trap the Schwarz P
        /// tile carries (SchwarzPTileData.NeighbourTile) cannot arise.</summary>
        public QuasicrystalVertex Step(int axis, int sign) => axis switch
        {
            0 => new QuasicrystalVertex(N0 + sign, N1, N2, N3, N4, N5),
            1 => new QuasicrystalVertex(N0, N1 + sign, N2, N3, N4, N5),
            2 => new QuasicrystalVertex(N0, N1, N2 + sign, N3, N4, N5),
            3 => new QuasicrystalVertex(N0, N1, N2, N3 + sign, N4, N5),
            4 => new QuasicrystalVertex(N0, N1, N2, N3, N4 + sign, N5),
            _ => new QuasicrystalVertex(N0, N1, N2, N3, N4, N5 + sign)
        };

        public bool Equals(QuasicrystalVertex o) =>
            N0 == o.N0 && N1 == o.N1 && N2 == o.N2 && N3 == o.N3 && N4 == o.N4 && N5 == o.N5;

        public override bool Equals(object obj) => obj is QuasicrystalVertex o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(N0, N1, N2, N3, N4, N5);

        public int CompareTo(QuasicrystalVertex o)
        {
            int c = N0.CompareTo(o.N0); if (c != 0) return c;
            c = N1.CompareTo(o.N1); if (c != 0) return c;
            c = N2.CompareTo(o.N2); if (c != 0) return c;
            c = N3.CompareTo(o.N3); if (c != 0) return c;
            c = N4.CompareTo(o.N4); if (c != 0) return c;
            return N5.CompareTo(o.N5);
        }

        public override string ToString() => $"({N0},{N1},{N2},{N3},{N4},{N5})";
    }

    /// <summary>
    /// A prism's address on the quasilattice: one EDGE, keyed canonically by its minus-end
    /// vertex plus the axis it runs along (the edge spans MinusEnd to MinusEnd + e_axis).
    /// Exactly one plant is ever that edge's designated layer - the plant owning MinusEnd -
    /// which is what makes the colony's lattice complete with no contested seams.
    /// </summary>
    public readonly struct QuasicrystalEdge : IEquatable<QuasicrystalEdge>
    {
        public readonly QuasicrystalVertex MinusEnd;
        public readonly int Axis;   // 0..5

        public QuasicrystalEdge(QuasicrystalVertex minusEnd, int axis)
        {
            MinusEnd = minusEnd;
            Axis = axis;
        }

        public QuasicrystalVertex PlusEnd => MinusEnd.Step(Axis, 1);

        public bool Equals(QuasicrystalEdge o) => Axis == o.Axis && MinusEnd.Equals(o.MinusEnd);
        public override bool Equals(object obj) => obj is QuasicrystalEdge o && Equals(o);
        public override int GetHashCode() => HashCode.Combine(MinusEnd, Axis);
        public override string ToString() => $"{MinusEnd}+e{Axis}";
    }

    /// <summary>
    /// The icosahedral quasilattice - the vertex-and-edge graph of the Ammann-Kramer-Neri
    /// tiling, the three-dimensional analogue of the Penrose tiling - and the arithmetic
    /// that grows it exactly.
    ///
    /// <para><b>Aperiodic order is a shadow of periodic order.</b> A vertex is a point of
    /// Z^6 whose image under the internal ("perp") projection lands inside a rhombic
    /// triacontahedron window; its physical position is the same integers through the
    /// physical projection. The pattern has perfect long-range icosahedral order - the
    /// "forbidden" five-fold symmetry of Shechtman's quasicrystals - and never repeats,
    /// yet every prism has an exact integer address and occupancy is a dictionary hit.
    /// Every edge joins n to n+-e_i, so every strut has exactly the same length and one
    /// of six orientations, all baked below as measured vectors.</para>
    ///
    /// <para><b>Hearts.</b> A plant is one heart's territory. A heart is a vertex whose
    /// full icosahedral star of twelve neighbours is accepted AND whose window margin is a
    /// local maximum among its twelve-coordinated neighbours (lexicographic tie-break) -
    /// bare twelve-coordination admits ADJACENT hearts, measured and rejected. Hearts land
    /// at a constant nearest spacing (2.3840 edges) and carry the plant's crystal at the
    /// centre of a perfect twelve-strut star.</para>
    ///
    /// <para><b>Territory is a tree.</b> owner(v) follows the lexicographically least
    /// neighbour one graph-step closer to a heart until it arrives at one. parent() is a
    /// pure function of the vertex alone, so chains are suffix-closed and every cell is a
    /// tree rooted at its heart - connected by construction, no distance compares, no
    /// epsilon, no ties. The growth simulation in the measuring script asserts ZERO unlaid
    /// edges over a 1,400-plant colony (a Euclidean-Voronoi territory measured 47 holes
    /// from graph-disconnected pockets - that is why this is a tree).</para>
    ///
    /// <para>Measured and regenerated by <c>Tools/Build/measure_icosahedral_quasilattice.py</c>
    /// (<c>--check</c> verifies, <c>--write</c> rewrites the table below); the shipped file
    /// is re-proven from a fresh derivation by
    /// <c>Tools/Build/verify_icosahedral_quasilattice_tables.py</c>. Design record:
    /// Docs/ECOSYSTEM.md 37.</para>
    /// </summary>
    public static class QuasicrystalLatticeData
    {
        // <<< MEASURED TABLE BEGIN
        // ---- MEASURED by Tools/Build/measure_icosahedral_quasilattice.py. Do not
        // hand-edit; regenerate with --write. Every row is pasted verbatim from the
        // emit and re-proven from a fresh derivation by
        // Tools/Build/verify_icosahedral_quasilattice_tables.py (Docs/ECOSYSTEM.md 37).
        // Census: mean degree 5.9850, heart density 0.0483,
        // heart spacing 2.3840 edges (constant), plant struts
        // 44..97 (mean 58.8), shell lengths [2.384, 2.7528].

        /// <summary>Window face support - identical for all 15 faces (the rhombic
        /// triacontahedron is face-transitive), asserted by the measurement.</summary>
        public const double WindowSupport = 1.376381920471174;

        /// <summary>Acceptance margins measured over a 60k-vertex patch never fall
        /// below this; double rounding at colony addresses is &lt; 1e-12, so the
        /// strict &gt; 0 window test is deterministic with 7 orders of headroom.
        /// (Margins shrink log-slowly with patch size; the population cap bounds
        /// a live colony to the measured scale.)</summary>
        public const double MinAcceptanceMargin = 0.000040287088619;

        /// <summary>Every accepted vertex sits within this many graph steps of a
        /// heart (measured max plus slack) - the BFS bound for HeartDistance.</summary>
        public const int HeartDistanceMax = 5;

        /// <summary>Complete plants measured 44..97 struts; the maturity
        /// floor sits under the minimum so no legitimate plant stalls on it.</summary>
        public const int MinPatchPrisms = 38;

        /// <summary>Physical images of the six Z^6 basis vectors: the icosahedron's
        /// vertex axes. A vertex's position is the integer-weighted sum of these.</summary>
        static readonly double[,] Par =
        {
            { 0.000000000000000, 0.000000000000000, 1.000000000000000 },
            { 0.894427190999916, 0.000000000000000, 0.447213595499958 },
            { 0.276393202250021, 0.850650808352040, 0.447213595499958 },
            { -0.723606797749979, 0.525731112119134, 0.447213595499958 },
            { -0.723606797749979, -0.525731112119133, 0.447213595499958 },
            { 0.276393202250021, -0.850650808352040, 0.447213595499958 },
        };

        /// <summary>Perp images (the second 3D irrep: pentagon angle doubled, z
        /// flipped). The window test lives entirely in this projection.</summary>
        static readonly double[,] Perp =
        {
            { 0.000000000000000, 0.000000000000000, 1.000000000000000 },
            { 0.894427190999916, 0.000000000000000, -0.447213595499958 },
            { -0.723606797749979, 0.525731112119134, -0.447213595499958 },
            { 0.276393202250021, -0.850650808352040, -0.447213595499958 },
            { 0.276393202250021, 0.850650808352040, -0.447213595499958 },
            { -0.723606797749979, -0.525731112119133, -0.447213595499958 },
        };

        /// <summary>The 15 unit face normals of the acceptance triacontahedron.</summary>
        static readonly double[,] WindowFaceNormals =
        {
            { -0.000000000000000, 1.000000000000000, 0.000000000000000 },
            { 0.587785252292473, 0.809016994374947, -0.000000000000000 },
            { 0.951056516295154, 0.309016994374947, -0.000000000000000 },
            { 0.951056516295154, -0.309016994374948, -0.000000000000000 },
            { 0.587785252292473, -0.809016994374948, 0.000000000000000 },
            { 0.262865556059567, 0.809016994374947, 0.525731112119134 },
            { 0.425325404176020, -0.309016994374947, 0.850650808352040 },
            { 0.425325404176020, 0.309016994374947, 0.850650808352040 },
            { 0.262865556059567, -0.809016994374948, 0.525731112119133 },
            { 0.688190960235587, 0.500000000000000, -0.525731112119134 },
            { 0.162459848116453, -0.500000000000000, -0.850650808352040 },
            { 0.525731112119134, -0.000000000000000, -0.850650808352040 },
            { 0.850650808352040, -0.000000000000000, 0.525731112119134 },
            { 0.162459848116453, 0.500000000000000, -0.850650808352040 },
            { 0.688190960235586, -0.500000000000000, -0.525731112119134 },
        };

        /// <summary>Generic window offset - keeps every lattice point clear of the
        /// boundary (no singular cut) and makes the ORIGIN a heart, so a founder
        /// always seeds at the zero address.</summary>
        static readonly double[] Gamma =
            { 0.034005710000000, 0.012673920000000, 0.022090430000000 };

        /// <summary>Per-direction strut frames: LookRotation(forward: Normal, up:
        /// Tangent) puts local +x exactly along the edge axis Par[i]. Vectors, not
        /// rotations - nothing here survives a transform it should not.</summary>
        static readonly Vector3[] StrutNormal =
        {
            new Vector3(0.0000000f, 1.0000000f, 0.0000000f),
            new Vector3(-0.4253254f, -0.3090170f, 0.8506508f),
            new Vector3(0.1624598f, -0.5000000f, 0.8506508f),
            new Vector3(0.5257311f, -0.0000000f, 0.8506508f),
            new Vector3(0.1624598f, 0.5000000f, 0.8506508f),
            new Vector3(-0.9510565f, -0.3090170f, 0.0000000f),
        };

        static readonly Vector3[] StrutTangent =
        {
            new Vector3(1.0000000f, 0.0000000f, 0.0000000f),
            new Vector3(-0.1381966f, 0.9510565f, 0.2763932f),
            new Vector3(-0.9472136f, 0.1624598f, 0.2763932f),
            new Vector3(-0.4472136f, -0.8506508f, 0.2763932f),
            new Vector3(0.6708204f, -0.6881910f, 0.2763932f),
            new Vector3(-0.1381966f, 0.4253254f, 0.8944272f),
        };

        /// <summary>The measured heart-to-heart link census: every integer delta from
        /// a heart to a shell neighbour (physical length under 3.4 edges). Closed
        /// under negation; the heart graph over these is measured connected. The
        /// colony's reproduction frontier walks exactly these steps.</summary>
        static readonly int[,] FrontierDeltas =
        {
            { -1, -1, -1, -1, 0, 0 },
            { -1, -1, -1, 0, 0, -1 },
            { -1, -1, -1, 0, 0, 0 },
            { -1, -1, -1, 0, 1, 0 },
            { -1, -1, 0, 0, -1, -1 },
            { -1, -1, 0, 0, 0, -1 },
            { -1, -1, 0, 1, 0, -1 },
            { -1, 0, -1, -1, -1, 0 },
            { -1, 0, -1, -1, 0, 0 },
            { -1, 0, -1, -1, 0, 1 },
            { -1, 0, 0, -1, -1, -1 },
            { -1, 0, 0, -1, -1, 0 },
            { -1, 0, 0, 0, -1, -1 },
            { -1, 0, 1, 0, -1, -1 },
            { -1, 1, 0, -1, -1, 0 },
            { 0, -1, -1, 0, 1, 0 },
            { 0, -1, -1, 0, 1, 1 },
            { 0, -1, -1, 1, 1, 0 },
            { 0, -1, 0, 1, 0, -1 },
            { 0, -1, 0, 1, 1, -1 },
            { 0, -1, 0, 1, 1, 0 },
            { 0, -1, 1, 1, 0, -1 },
            { 0, 0, -1, -1, 0, 1 },
            { 0, 0, -1, -1, 1, 1 },
            { 0, 0, -1, 0, 1, 1 },
            { 0, 0, 1, 0, -1, -1 },
            { 0, 0, 1, 1, -1, -1 },
            { 0, 0, 1, 1, 0, -1 },
            { 0, 1, -1, -1, 0, 1 },
            { 0, 1, 0, -1, -1, 0 },
            { 0, 1, 0, -1, -1, 1 },
            { 0, 1, 0, -1, 0, 1 },
            { 0, 1, 1, -1, -1, 0 },
            { 0, 1, 1, 0, -1, -1 },
            { 0, 1, 1, 0, -1, 0 },
            { 1, -1, 0, 1, 1, 0 },
            { 1, 0, -1, 0, 1, 1 },
            { 1, 0, 0, 0, 1, 1 },
            { 1, 0, 0, 1, 1, 0 },
            { 1, 0, 0, 1, 1, 1 },
            { 1, 0, 1, 1, 0, -1 },
            { 1, 0, 1, 1, 0, 0 },
            { 1, 0, 1, 1, 1, 0 },
            { 1, 1, 0, -1, 0, 1 },
            { 1, 1, 0, 0, 0, 1 },
            { 1, 1, 0, 0, 1, 1 },
            { 1, 1, 1, 0, -1, 0 },
            { 1, 1, 1, 0, 0, 0 },
            { 1, 1, 1, 0, 0, 1 },
            { 1, 1, 1, 1, 0, 0 },
        };
        // <<< MEASURED TABLE END

        public const int AxisCount = 6;

        public static int FrontierDeltaCount => FrontierDeltas.GetLength(0);

        // Pure-function memos. The lattice is one global abstract object (frames only map
        // it into world space), so these are shared by every colony. Bounded in practice by
        // the population caps - probing only happens near live growth.
        static readonly Dictionary<QuasicrystalVertex, double> marginCache = new();
        static readonly Dictionary<QuasicrystalVertex, bool> heartCache = new();
        static readonly Dictionary<QuasicrystalVertex, int> distCache = new();
        static readonly Dictionary<QuasicrystalVertex, QuasicrystalVertex> ownerCache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForDomainReload()
        {
            marginCache.Clear();
            heartCache.Clear();
            distCache.Clear();
            ownerCache.Clear();
        }

        /// <summary>
        /// Window margin of a vertex: its distance inside the acceptance triacontahedron
        /// (negative = outside). Doubles throughout - the margin can be as small as ~4e-5
        /// while double rounding at colony addresses stays under 1e-12 (see
        /// <see cref="MinAcceptanceMargin"/>), so the strict test is deterministic.
        /// </summary>
        public static double AcceptMargin(QuasicrystalVertex v)
        {
            if (marginCache.TryGetValue(v, out double cached)) return cached;

            double px = v.N0 * Perp[0, 0] + v.N1 * Perp[1, 0] + v.N2 * Perp[2, 0] +
                        v.N3 * Perp[3, 0] + v.N4 * Perp[4, 0] + v.N5 * Perp[5, 0] - Gamma[0];
            double py = v.N0 * Perp[0, 1] + v.N1 * Perp[1, 1] + v.N2 * Perp[2, 1] +
                        v.N3 * Perp[3, 1] + v.N4 * Perp[4, 1] + v.N5 * Perp[5, 1] - Gamma[1];
            double pz = v.N0 * Perp[0, 2] + v.N1 * Perp[1, 2] + v.N2 * Perp[2, 2] +
                        v.N3 * Perp[3, 2] + v.N4 * Perp[4, 2] + v.N5 * Perp[5, 2] - Gamma[2];

            double margin = double.MaxValue;
            for (int k = 0; k < 15; k++)
            {
                double d = px * WindowFaceNormals[k, 0] + py * WindowFaceNormals[k, 1] +
                           pz * WindowFaceNormals[k, 2];
                double m = WindowSupport - (d < 0 ? -d : d);
                if (m < margin) margin = m;
            }

            marginCache[v] = margin;
            return margin;
        }

        public static bool IsAccepted(QuasicrystalVertex v) => AcceptMargin(v) > 0.0;

        /// <summary>All twelve neighbours accepted - a full icosahedral star.</summary>
        public static bool IsTwelveCoordinated(QuasicrystalVertex v)
        {
            if (!IsAccepted(v)) return false;
            for (int axis = 0; axis < AxisCount; axis++)
            {
                if (!IsAccepted(v.Step(axis, 1))) return false;
                if (!IsAccepted(v.Step(axis, -1))) return false;
            }
            return true;
        }

        /// <summary>
        /// The heart rule: twelve-coordinated AND a local maximum of window margin among
        /// its twelve-coordinated neighbours, lexicographic address tie-break. Exact and
        /// closed-form; the local-max half is load-bearing (bare twelve-coordination
        /// admits ADJACENT hearts - crystal pairs one edge apart and two-vertex runt
        /// plants; the measuring script keeps that as a negative control).
        /// </summary>
        public static bool IsHeart(QuasicrystalVertex v)
        {
            if (heartCache.TryGetValue(v, out bool cached)) return cached;

            bool heart = IsTwelveCoordinated(v);
            if (heart)
            {
                double mv = AcceptMargin(v);
                for (int axis = 0; axis < AxisCount && heart; axis++)
                {
                    for (int sign = -1; sign <= 1 && heart; sign += 2)
                    {
                        var u = v.Step(axis, sign);
                        if (!IsTwelveCoordinated(u)) continue;
                        double mu = AcceptMargin(u);
                        if (mu > mv || (mu == mv && u.CompareTo(v) < 0)) heart = false;
                    }
                }
            }

            heartCache[v] = heart;
            return heart;
        }

        /// <summary>
        /// Graph distance from an accepted vertex to its nearest heart. Measured max 3
        /// over a 60k-vertex patch; the shipped bound (<see cref="HeartDistanceMax"/>)
        /// carries slack. Returns -1 - loudly, once - if the bound is ever exceeded,
        /// which would mean the shipped window constants are corrupt.
        /// </summary>
        public static int HeartDistance(QuasicrystalVertex v)
        {
            if (distCache.TryGetValue(v, out int cached)) return cached;

            int result = -1;
            if (IsHeart(v)) result = 0;
            else
            {
                var seen = new HashSet<QuasicrystalVertex> { v };
                var ring = new List<QuasicrystalVertex> { v };
                var next = new List<QuasicrystalVertex>();
                for (int depth = 1; depth <= HeartDistanceMax && result < 0; depth++)
                {
                    next.Clear();
                    foreach (var w in ring)
                    {
                        for (int axis = 0; axis < AxisCount; axis++)
                        {
                            for (int sign = -1; sign <= 1; sign += 2)
                            {
                                var u = w.Step(axis, sign);
                                if (!seen.Add(u) || !IsAccepted(u)) continue;
                                next.Add(u);
                            }
                        }
                    }
                    foreach (var u in next)
                    {
                        if (!IsHeart(u)) continue;
                        result = depth;
                        break;
                    }
                    (ring, next) = (next, ring);
                }
                if (result < 0)
                    CSDebug.LogError($"[Quasicrystal] no heart within {HeartDistanceMax} steps of {v} - " +
                                     "the shipped lattice table is corrupt " +
                                     "(regenerate: Tools/Build/measure_icosahedral_quasilattice.py --write)");
            }

            distCache[v] = result;
            return result;
        }

        /// <summary>
        /// The heart whose territory this vertex belongs to - the end of the lex-parent
        /// chain: from v, step to the lexicographically least accepted neighbour one graph
        /// step closer to a heart, and repeat. parent() is a pure function of the vertex
        /// alone, so chains are suffix-closed and every territory is a connected TREE
        /// rooted at its heart. No distance compare, no epsilon, no tie to break.
        /// </summary>
        public static bool TryGetOwningHeart(QuasicrystalVertex v, out QuasicrystalVertex heart)
        {
            if (ownerCache.TryGetValue(v, out heart)) return true;

            if (IsHeart(v))
            {
                ownerCache[v] = v;
                heart = v;
                return true;
            }

            int dv = HeartDistance(v);
            if (dv < 0) { heart = v; return false; }

            bool found = false;
            QuasicrystalVertex parent = default;
            for (int axis = 0; axis < AxisCount; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var u = v.Step(axis, sign);
                    if (!IsAccepted(u) || HeartDistance(u) != dv - 1) continue;
                    if (!found || u.CompareTo(parent) < 0)
                    {
                        parent = u;
                        found = true;
                    }
                }
            }
            if (!found) { heart = v; return false; }   // unreachable while HeartDistance holds

            if (!TryGetOwningHeart(parent, out heart)) return false;
            ownerCache[v] = heart;
            return true;
        }

        /// <summary>Physical position of a vertex, in edge-length units. The integer sum is
        /// accumulated in doubles and collapsed to float once - positions grow with the
        /// colony (no cancellation), so float world coordinates are exactly as safe here
        /// as anywhere else in the engine.</summary>
        public static Vector3 ParUnit(QuasicrystalVertex v) => new(
            (float)(v.N0 * Par[0, 0] + v.N1 * Par[1, 0] + v.N2 * Par[2, 0] +
                    v.N3 * Par[3, 0] + v.N4 * Par[4, 0] + v.N5 * Par[5, 0]),
            (float)(v.N0 * Par[0, 1] + v.N1 * Par[1, 1] + v.N2 * Par[2, 1] +
                    v.N3 * Par[3, 1] + v.N4 * Par[4, 1] + v.N5 * Par[5, 1]),
            (float)(v.N0 * Par[0, 2] + v.N1 * Par[1, 2] + v.N2 * Par[2, 2] +
                    v.N3 * Par[3, 2] + v.N4 * Par[4, 2] + v.N5 * Par[5, 2]));

        /// <summary>Unit direction of an edge axis in lattice space.</summary>
        public static Vector3 AxisUnit(int axis) => new(
            (float)Par[axis, 0], (float)Par[axis, 1], (float)Par[axis, 2]);

        /// <summary>
        /// Lattice-local strut orientation for an edge axis:
        /// LookRotation(forward: StrutNormal, up: StrutTangent) puts local +x exactly
        /// along the axis, so leafSize.x is the strut's length along the edge. Derived
        /// from baked VECTORS - no rotation is ever baked (Docs/ECOSYSTEM.md 34.1).
        /// </summary>
        public static Quaternion StrutRotationLocal(int axis) =>
            Quaternion.LookRotation(StrutNormal[axis], StrutTangent[axis]);

        /// <summary>A heart's k-th shell neighbour candidate (test it with
        /// <see cref="IsHeart"/> - the delta census is measured over real hearts, but any
        /// individual candidate may not be one at this address).</summary>
        public static QuasicrystalVertex FrontierNeighbour(QuasicrystalVertex heart, int k) => new(
            heart.N0 + FrontierDeltas[k, 0], heart.N1 + FrontierDeltas[k, 1],
            heart.N2 + FrontierDeltas[k, 2], heart.N3 + FrontierDeltas[k, 3],
            heart.N4 + FrontierDeltas[k, 4], heart.N5 + FrontierDeltas[k, 5]);
    }
}
