using System.Collections.Generic;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The DESTRUCTION's fragments (Docs/PRISM_ANIMATION.md §4.11): a Sierpinski-inspired cut of the prism, scaled by
    /// recursion depth.
    ///
    /// <para><b>Depth 0 is the explosion's own cut</b> — every face fanned from its centre into the four isoceles
    /// wedges <c>Prism.asset</c> is made of (the explosion draws exactly these 24 triangles). Each deeper level splits
    /// every triangle still being refined at its edge midpoints into four: the MIDDLE triangle stays whole — the
    /// gasket's hole, here a fragment of its own — and the three CORNERS recurse. Per wedge that is 1, 4, 13, 40, 121
    /// triangles at depths 0–4: a self-similar range of sizes, the middles of every scale plus the finest corners.</para>
    ///
    /// <para><b>Nested, so no level has holes.</b> A depth is the next one with its finest corner triangles merged back
    /// into their parent, so every depth tiles every face completely (the first frame is the whole prism) and, far away,
    /// reads as the deeper one with its small fragments too small to see. Every triangle winds counter-clockwise in
    /// face coordinates, like the face it tiles; every vertex is a dyadic fraction, exact in float.</para>
    ///
    /// <para>Pure (no UnityEngine), so the Collision Burst Lab's JavaScript port is held to it triangle for triangle by
    /// <c>Tools/Build/prism_collision_burst_lab/parity.py</c>, which compiles this file with the .NET SDK and also
    /// checks depth 0 against <c>Prism.asset</c>.</para>
    /// </summary>
    public static class PrismSierpinskiTiling
    {
        /// <summary>The deepest cut the meshes are built to (121 triangles a wedge at 4, 364 at 5).</summary>
        public const int MaxDepth = 5;

        /// <summary>Floats per triangle in <see cref="Triangles"/>: (u0, v0, u1, v1, u2, v2, level).</summary>
        public const int Stride = 7;

        /// <summary>Triangles one wedge holds at <paramref name="depth"/>: 1 + 3 × the next depth up.</summary>
        public static int PerWedge(int depth) => depth <= 0 ? 1 : 1 + 3 * PerWedge(depth - 1);

        /// <summary>Triangles in one prism at <paramref name="depth"/> (four wedges a face, six faces).</summary>
        public static int TriangleCount(int depth) => 24 * PerWedge(Clamp(depth));

        /// <summary>
        /// Appends one face's triangles at <paramref name="depth"/> (0 – <see cref="MaxDepth"/>) as
        /// (u0, v0, u1, v1, u2, v2, level) in face coordinates [0, 1], counter-clockwise, wedge by wedge — level the
        /// depth at which the triangle stopped splitting (a middle: the level it was cut out at).
        /// </summary>
        public static void Triangles(int depth, List<float> into)
        {
            depth = Clamp(depth);
            // The explosion's wedges: the face centre and each edge's two corners, counter-clockwise.
            float[] cu = { 0f, 1f, 1f, 0f };
            float[] cv = { 0f, 0f, 1f, 1f };
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                Split(0.5f, 0.5f, cu[k], cv[k], cu[n], cv[n], 0, depth, into);
            }
        }

        static void Split(float au, float av, float bu, float bv, float cu, float cv, int level, int depth, List<float> into)
        {
            if (level >= depth)
            {
                Emit(au, av, bu, bv, cu, cv, level, into);
                return;
            }
            float abu = (au + bu) * 0.5f, abv = (av + bv) * 0.5f;
            float bcu = (bu + cu) * 0.5f, bcv = (bv + cv) * 0.5f;
            float cau = (cu + au) * 0.5f, cav = (cv + av) * 0.5f;
            // The middle stays whole; the three corners recurse — all four keep the parent's winding.
            Emit(abu, abv, bcu, bcv, cau, cav, level + 1, into);
            Split(au, av, abu, abv, cau, cav, level + 1, depth, into);
            Split(abu, abv, bu, bv, bcu, bcv, level + 1, depth, into);
            Split(cau, cav, bcu, bcv, cu, cv, level + 1, depth, into);
        }

        static void Emit(float au, float av, float bu, float bv, float cu, float cv, int level, List<float> into)
        {
            into.Add(au); into.Add(av);
            into.Add(bu); into.Add(bv);
            into.Add(cu); into.Add(cv);
            into.Add(level);
        }

        static int Clamp(int depth) => depth < 0 ? 0 : depth > MaxDepth ? MaxDepth : depth;
    }
}
