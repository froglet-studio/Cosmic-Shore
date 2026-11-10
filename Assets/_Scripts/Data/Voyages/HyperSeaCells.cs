using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The HyperSea's cells in three dimensions, as the Voyage Board draws them (Docs/Voyages/VOYAGE_BOARD.md §4c,
    /// Docs/Voyages/VoyageBoard/deepzoom/cells.js). Every cell grows round one nucleus. The nuclei sit on a body-centred
    /// cubic (BCC) lattice, two cubic lattices <see cref="HyperSea.CellLy"/> apart, one offset from the other by half a
    /// cell on every axis, and each cell is the space nearer its own nucleus than any other: the truncated octahedron,
    /// with 6 square faces 10 ly from the nucleus and 8 hexagonal faces 15 ly out along the diagonals. The lattice is
    /// anchored so the central cell's nucleus is Sgr A*, the Heart [A].
    ///
    /// <para>The map shows the cells as a plane slice at one height (octagons and squares on a layer of nuclei, diamonds
    /// between layers); the edge view slices them upright. A line-for-line port of the page's <c>cellAt</c>,
    /// <c>section</c>, the slice's snapping and the size fades, in doubles; <c>HyperSeaCellsTests</c> holds the page's
    /// own outputs (<c>node Docs/Voyages/VoyageBoard/cells_golden.js</c>). Pure C#, no engine type.</para>
    /// </summary>
    public static class HyperSeaCells
    {
        /// <summary>A cell's width across its square faces, and the lattice's spacing.</summary>
        public const double A = HyperSea.CellLy;
        public const double Half = A / 2;
        /// <summary>The nucleus's radius, ly: the game's nucleus, 392 u at 120 u a light year [S: Docs/ECOSYSTEM.md].</summary>
        public const double NucleusRadius = 392.0 / 120.0;
        /// <summary>The lattice's origin: the board's place "heart" (Sgr A*, HyperSeaSky.txt) is a nucleus [A].</summary>
        public const double HeartX = 0, HeartY = 0, HeartZ = -5;
        /// <summary>How far past the disk's faces the slice may go.</summary>
        public const double SliceLimit = HyperSea.DiskHalf + 100;

        // The cells show from a size on: under this many pixels a cell, the plain sea; the membranes fade in over the
        // next ones and the nuclei from a 4 px radius (full at 8), so neither turns to moire as the cells shrink.
        /// <summary>A cell narrower than this on screen is not drawn (the board's cell zoom).</summary>
        public const double ShowFromPx = 14;

        // The truncated octahedron's 14 faces: a normal (not unit) and the face's distance along it.
        static readonly double[,] Faces =
        {
            { 1, 0, 0, Half }, { -1, 0, 0, Half }, { 0, 1, 0, Half }, { 0, -1, 0, Half }, { 0, 0, 1, Half }, { 0, 0, -1, Half },
            { 1, 1, 1, 3 * A / 4 }, { 1, 1, -1, 3 * A / 4 }, { 1, -1, 1, 3 * A / 4 }, { 1, -1, -1, 3 * A / 4 },
            { -1, 1, 1, 3 * A / 4 }, { -1, 1, -1, 3 * A / 4 }, { -1, -1, 1, 3 * A / 4 }, { -1, -1, -1, 3 * A / 4 },
        };

        /// <summary>The nucleus of the cell a point is in: the nearer of the two lattices' nearest points.</summary>
        public static (double x, double y, double z) CellAt(double x, double y, double z)
        {
            x -= HeartX; y -= HeartY; z -= HeartZ;
            double ax = JsRound(x / A) * A, ay = JsRound(y / A) * A, az = JsRound(z / A) * A;
            double bx = (Math.Floor(x / A) + .5) * A, by = (Math.Floor(y / A) + .5) * A, bz = (Math.Floor(z / A) + .5) * A;
            double da = (ax - x) * (ax - x) + (ay - y) * (ay - y) + (az - z) * (az - z);
            double db = (bx - x) * (bx - x) + (by - y) * (by - y) + (bz - z) * (bz - z);
            return da <= db ? (ax + HeartX, ay + HeartY, az + HeartZ) : (bx + HeartX, by + HeartY, bz + HeartZ);
        }

        /// <summary>A nucleus's place on the lattice, in half cells from the Heart (each index even, or each odd).</summary>
        public static (int i, int j, int k) KeyOf((double x, double y, double z) c) =>
            ((int)JsRound((c.x - HeartX) * 2 / A), (int)JsRound((c.y - HeartY) * 2 / A), (int)JsRound((c.z - HeartZ) * 2 / A));

        /// <summary>True for the central cell, whose nucleus is the Heart.</summary>
        public static bool IsHeart((double x, double y, double z) c) => KeyOf(c) == (0, 0, 0);

        /// <summary>The page's Math.round: halves round up.</summary>
        static double JsRound(double v) => Math.Floor(v + 0.5);

        /// <summary>The layer of nuclei nearest a height: the slice snaps to one, so by default it cuts nuclei.</summary>
        public static double SnapToLayer(double z) => JsRound((z - HeartZ) / Half) * Half + HeartZ;

        /// <summary>The height the map slices the cells at: the focus's layer plus the reader's offset, kept in the disk.</summary>
        public static double SliceZ(double focusZ, double offset) =>
            Math.Max(-SliceLimit, Math.Min(SliceLimit, SnapToLayer(focusZ) + offset));

        /// <summary>How strongly a membrane shows for a cell <paramref name="cellPx"/> wide: none under 22 px, full at 44.</summary>
        public static double MembraneAlpha(double cellPx) => Math.Max(0, Math.Min(1, (cellPx - 22) / 22));

        /// <summary>How strongly a nucleus shows: from a 4 px radius, full at 8.</summary>
        public static double NucleusAlpha(double cellPx) => Math.Max(0, Math.Min(1, (cellPx * NucleusRadius / A - 4) / 4));

        /// <summary>One cell cut by a plane: the polygon (in the plane's own u, v), where the nucleus projects onto the
        /// plane (cu, cv), and how far the nucleus sits off the plane (h, signed along U x V).</summary>
        public struct Section
        {
            public double Cu, Cv, H;
        }

        [ThreadStatic] static List<(double u, double v)> s_ClipOut;

        /// <summary>
        /// The section of the cell round nucleus <paramref name="c"/> by the plane through <paramref name="o"/> spanned by
        /// unit vectors <paramref name="u"/> and <paramref name="v"/>: its convex polygon goes into <paramref name="poly"/>
        /// (cleared first). False when the plane misses the cell. Extra half-planes a u + b v &lt;= r clip it further.
        /// </summary>
        public static bool TrySection((double x, double y, double z) c, (double x, double y, double z) o,
            (double x, double y, double z) u, (double x, double y, double z) v, List<(double u, double v)> poly,
            out Section section, IReadOnlyList<(double a, double b, double r)> extra = null)
        {
            section = default;
            double dx = c.x - o.x, dy = c.y - o.y, dz = c.z - o.z;
            double cu = dx * u.x + dy * u.y + dz * u.z, cv = dx * v.x + dy * v.y + dz * v.z;
            poly.Clear();
            poly.Add((cu - A, cv - A));
            poly.Add((cu + A, cv - A));
            poly.Add((cu + A, cv + A));
            poly.Add((cu - A, cv + A));
            var tmp = s_ClipOut ??= new List<(double u, double v)>(16);
            for (int f = 0; f < 14; f++)
            {
                double nx = Faces[f, 0], ny = Faces[f, 1], nz = Faces[f, 2];
                double a = nx * u.x + ny * u.y + nz * u.z, b = nx * v.x + ny * v.y + nz * v.z;
                double r = Faces[f, 3] + nx * dx + ny * dy + nz * dz;
                Clip(poly, a, b, r, tmp);
                if (poly.Count < 3) return false;
            }
            if (extra != null)
                foreach (var e in extra)
                {
                    Clip(poly, e.a, e.b, e.r, tmp);
                    if (poly.Count < 3) return false;
                }
            double nX = u.y * v.z - u.z * v.y, nY = u.z * v.x - u.x * v.z, nZ = u.x * v.y - u.y * v.x;
            section = new Section { Cu = cu, Cv = cv, H = dx * nX + dy * nY + dz * nZ };
            return true;
        }

        /// <summary>Keep the part of a polygon where a u + b v &lt;= r (Sutherland-Hodgman, the page's <c>clip</c>).</summary>
        static void Clip(List<(double u, double v)> poly, double a, double b, double r, List<(double u, double v)> tmp)
        {
            tmp.Clear();
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                var p = poly[i];
                var q = poly[(i + 1) % n];
                double fp = a * p.u + b * p.v - r, fq = a * q.u + b * q.v - r;
                if (fp <= 0) tmp.Add(p);
                if ((fp < 0) != (fq < 0) && fp != fq)
                {
                    double t = fp / (fp - fq);
                    tmp.Add((p.u + (q.u - p.u) * t, p.v + (q.v - p.v) * t));
                }
            }
            poly.Clear();
            poly.AddRange(tmp);
        }

        /// <summary>
        /// The nuclei whose cells a horizontal slice at height <paramref name="zs"/> crosses over the box x0..x1, y0..y1
        /// (both lattices, each at its layer nearest the slice), in the page's order. Stops at <paramref name="max"/>.
        /// </summary>
        public static void NucleiInSlice(double zs, double x0, double y0, double x1, double y1,
            List<(double x, double y, double z)> into, int max = int.MaxValue)
        {
            into.Clear();
            foreach (double o in Offsets)
            {
                double cz = (JsRound((zs - HeartZ) / A - o) + o) * A + HeartZ;
                if (Math.Abs(cz - zs) > Half) continue;
                int j0 = (int)Math.Floor((y0 - HeartY) / A - o), j1 = (int)Math.Ceiling((y1 - HeartY) / A - o);
                int i0 = (int)Math.Floor((x0 - HeartX) / A - o), i1 = (int)Math.Ceiling((x1 - HeartX) / A - o);
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    if (into.Count >= max) return;
                    into.Add(((i + o) * A + HeartX, (j + o) * A + HeartY, cz));
                }
            }
        }

        /// <summary>
        /// The nuclei whose cells an upright slice at <paramref name="ys"/> crosses over x0..x1 and heights z0..z1 (the
        /// edge view's sheet), in the page's order. Stops at <paramref name="max"/>.
        /// </summary>
        public static void NucleiInUpright(double ys, double x0, double x1, double z0, double z1,
            List<(double x, double y, double z)> into, int max = int.MaxValue)
        {
            into.Clear();
            foreach (double o in Offsets)
            {
                double cy = (JsRound((ys - HeartY) / A - o) + o) * A + HeartY;
                if (Math.Abs(cy - ys) > Half) continue;
                int k0 = (int)Math.Floor((z0 - HeartZ) / A - o) - 1, k1 = (int)Math.Ceiling((z1 - HeartZ) / A - o) + 1;
                int i0 = (int)Math.Floor((x0 - HeartX) / A - o) - 1, i1 = (int)Math.Ceiling((x1 - HeartX) / A - o) + 1;
                for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                {
                    if (into.Count >= max) return;
                    into.Add(((i + o) * A + HeartX, cy, (k + o) * A + HeartZ));
                }
            }
        }

        static readonly double[] Offsets = { 0, .5 };

        /// <summary>
        /// Of the two layers a horizontal slice at <paramref name="zs"/> crosses, the nucleus nearest (x, y), counting its
        /// height double (the page's <c>tags</c>: the cell whose levels are named).
        /// </summary>
        public static (double x, double y, double z) NearestInSlice(double x, double y, double zs)
        {
            (double x, double y, double z) best = default;
            double bd = double.PositiveInfinity;
            foreach (double o in Offsets)
            {
                double kx = (JsRound((x - HeartX) / A - o) + o) * A + HeartX;
                double ky = (JsRound((y - HeartY) / A - o) + o) * A + HeartY;
                double kz = (JsRound((zs - HeartZ) / A - o) + o) * A + HeartZ;
                double d = (kx - x) * (kx - x) + (ky - y) * (ky - y) + 4 * (kz - zs) * (kz - zs);
                if (d < bd)
                {
                    bd = d;
                    best = (kx, ky, kz);
                }
            }
            return best;
        }
    }
}
