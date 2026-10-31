using System;
using System.Collections.Generic;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The DESTRUCTION's second cut (Docs/PRISM_ANIMATION.md §4.12): the explosion's wedges dissected by the SILVER
    /// GNOMON, scaled by recursion depth — a range of triangle sizes in the silver ratio, every piece one of two shapes.
    ///
    /// <para><b>The two shapes.</b> The SILVER triangle S (45° apex, 67.5° base angles) and its GNOMON G (45°, 22.5°,
    /// 112.5°). Each cuts into one of each, exactly: S → G + a smaller S (a line from a base corner to the far leg at
    /// (√2 − 1) of its length, so the smaller S is 2·sin 22.5° ≈ 0.765 of the larger — the whirl of the designer's
    /// figure), and G → S + a smaller G (a line from its 112.5° corner to its long side, where the S's legs are equal).
    /// The two children take √2 − 1 and 2 − √2 of their parent's area, so every piece is one of the two shapes and none
    /// is a sliver.</para>
    ///
    /// <para><b>Depth 0 is the explosion's cut</b> — the four right-isosceles wedges a face fanned from its centre, as
    /// <see cref="PrismSierpinskiTiling"/> and <c>Prism.asset</c>. A wedge's 90° corner is 67.5° + 22.5°, so it cuts into
    /// one S and one G. Then each piece recurses on a budget: the child of the OTHER shape on one less, the child of the
    /// SAME shape (the larger, the one that continues the whirl) on two less. So the larger pieces stay whole longer
    /// while the smaller are cut finer — a wide range of sizes (lengths spanning 4.5× at depth 4, 28× at depth 10) at
    /// Fibonacci counts, 1, 2, 3, 5, 8, 13, 21 … a wedge.</para>
    ///
    /// <para><b>Nested.</b> Raising the depth by one splits some leaves once and keeps the rest, so every depth is the
    /// next with some fragments merged into their parents, and every depth tiles every face completely (the first frame
    /// is the whole prism). Every triangle winds counter-clockwise in face coordinates.</para>
    ///
    /// <para>Pure (no UnityEngine); computed in double and emitted as float, so the Collision Burst Lab's JavaScript
    /// port, doing the same double arithmetic in the same order, is held to it value for value by
    /// <c>Tools/Build/prism_collision_burst_lab/parity.py</c>, which compiles this file with the .NET SDK.</para>
    /// </summary>
    public static class PrismSilverGnomonTiling
    {
        /// <summary>The deepest cut the meshes are built to (377 triangles a wedge).</summary>
        public const int MaxDepth = 12;

        /// <summary>Floats per triangle in <see cref="Triangles"/>: (u0, v0, u1, v1, u2, v2, level).</summary>
        public const int Stride = 7;

        /// <summary>√2 − 1: where an S's cut meets its far leg, as a share of the leg (the silver ratio's inverse).</summary>
        public const double SilverCut = 0.41421356237309503;

        const double WedgeArea = 0.25;

        /// <summary>Triangles one wedge holds at <paramref name="depth"/>: 1, 2, 3, 5, 8 … (Fibonacci).</summary>
        public static int PerWedge(int depth)
        {
            depth = Clamp(depth);
            int a = 1, b = 1;   // N(d - 2), N(d - 1) with N(≤ 0) = 1
            for (int d = 1; d <= depth; d++)
            {
                int n = a + b;
                a = b;
                b = n;
            }
            return depth <= 0 ? 1 : b;
        }

        /// <summary>Triangles in one prism at <paramref name="depth"/> (four wedges a face, six faces).</summary>
        public static int TriangleCount(int depth) => 24 * PerWedge(depth);

        /// <summary>
        /// Appends one face's triangles at <paramref name="depth"/> (0 – <see cref="MaxDepth"/>) as
        /// (u0, v0, u1, v1, u2, v2, level) in face coordinates [0, 1], counter-clockwise, wedge by wedge
        /// (<see cref="PerWedge"/> each, so <see cref="PrismSierpinskiTiling.WedgeOf"/>'s rule finds a triangle's
        /// wedge). level is the fragment's SIZE as log2(its wedge's size / its own), sizes measured as √area:
        /// 0 for a whole wedge, a fraction deeper.
        /// </summary>
        public static void Triangles(int depth, List<float> into)
        {
            depth = Clamp(depth);
            double[] cu = { 0.0, 1.0, 1.0, 0.0 };
            double[] cv = { 0.0, 0.0, 1.0, 1.0 };
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                if (depth <= 0)
                {
                    Emit(0.5, 0.5, cu[k], cv[k], cu[n], cv[n], into);
                    continue;
                }
                // The wedge (centre C, corners P, Q): its 90° corner at C is 67.5° + 22.5°. X on PQ with |PX| = |PC|.
                double xu = cu[k] + (cu[n] - cu[k]) * Half2;
                double xv = cv[k] + (cv[n] - cv[k]) * Half2;
                Silver(cu[k], cv[k], xu, xv, 0.5, 0.5, depth - 2, into);   // S, apex P (the larger: the same-shape rule)
                Gnomon(cu[n], cv[n], 0.5, 0.5, xu, xv, depth - 1, into);   // G (45° at Q, 22.5° at C, 112.5° at X)
            }
        }

        /// <summary>√2 / 2: where a wedge's cut meets its outer edge, as a share of the edge.</summary>
        const double Half2 = 0.70710678118654757;

        // S with apex x (45°) and base corners y, z: P on the leg xz at (√2 − 1) of it; G = (x, y, P) with 45° at x,
        // 22.5° at y, 112.5° at P; the smaller S has apex y and base (z, P).
        static void Silver(double xu, double xv, double yu, double yv, double zu, double zv, int budget, List<float> into)
        {
            if (budget <= 0)
            {
                Emit(xu, xv, yu, yv, zu, zv, into);
                return;
            }
            double pu = xu + (zu - xu) * SilverCut;
            double pv = xv + (zv - xv) * SilverCut;
            Gnomon(xu, xv, yu, yv, pu, pv, budget - 1, into);
            Silver(yu, yv, zu, zv, pu, pv, budget - 2, into);
        }

        // G with 45° at a, 22.5° at b, 112.5° at c: P on the long side ab with |aP| = |ac|; S = apex a, base (c, P);
        // the smaller G = (c, b, P) with 45° at c, 22.5° at b, 112.5° at P.
        static void Gnomon(double au, double av, double bu, double bv, double cu, double cv, int budget, List<float> into)
        {
            if (budget <= 0)
            {
                Emit(au, av, bu, bv, cu, cv, into);
                return;
            }
            double t = Length(cu - au, cv - av) / Length(bu - au, bv - av);
            double pu = au + (bu - au) * t;
            double pv = av + (bv - av) * t;
            Silver(au, av, cu, cv, pu, pv, budget - 1, into);
            Gnomon(cu, cv, bu, bv, pu, pv, budget - 2, into);
        }

        static double Length(double du, double dv) => Math.Sqrt(du * du + dv * dv);

        static void Emit(double au, double av, double bu, double bv, double cu, double cv, List<float> into)
        {
            double cross = (bu - au) * (cv - av) - (cu - au) * (bv - av);
            if (cross < 0.0)
            {
                (bu, cu) = (cu, bu);
                (bv, cv) = (cv, bv);
                cross = -cross;
            }
            double level = 0.5 * Math.Log(WedgeArea / (0.5 * cross)) / Math.Log(2.0);
            into.Add((float)au); into.Add((float)av);
            into.Add((float)bu); into.Add((float)bv);
            into.Add((float)cu); into.Add((float)cv);
            into.Add((float)level);
        }

        static int Clamp(int depth) => depth < 0 ? 0 : depth > MaxDepth ? MaxDepth : depth;
    }
}
