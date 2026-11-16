using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>
    /// The HyperSea's cells in three dimensions, as the Voyage Board draws them (Docs/Voyages/VOYAGE_BOARD.md §4c, §4d;
    /// Docs/Voyages/VoyageBoard/deepzoom/cells.js). Every cell grows round one nucleus. The nuclei start on a body-centred
    /// cubic (BCC) lattice, two cubic lattices <see cref="HyperSea.CellLy"/> apart, one offset from the other by half a
    /// cell on every axis, anchored so the central cell's nucleus is Sgr A*, the Heart [A]. The lattice is loosened into a
    /// foam (Shombith, 2026-11-13: "a Kelvin foam BCC structure" with "unevenness and inconsistency amongst cells"):
    /// each lattice point's nucleus drifts off its point, is fuller or emptier than its neighbours (a weight), and where
    /// a smooth field runs high the point splits into a knot of small cells. Everything is hashed from the lattice point,
    /// so any patch of the galaxy builds the same way every time. A cell is the space whose weighted distance to its
    /// nucleus is least (a power diagram), so it stays a convex polyhedron and <see cref="CellAt"/> stays a lookup.
    ///
    /// <para>A cell is named by its nucleus's position (what <see cref="CellAt"/>, <see cref="NucleiInSlice"/> and
    /// <see cref="NucleiInUpright"/> return); its middle, where its nucleus is drawn, and the nucleus's radius come with
    /// its <see cref="Section"/>. A line-for-line port of the page's <c>sitesOf</c>, <c>cellAt</c>, <c>polyOf</c>,
    /// <c>section</c> and <c>wetOutline</c>, in doubles; <c>HyperSeaCellsTests</c> holds the page's own outputs
    /// (<c>node Docs/Voyages/VoyageBoard/cells_golden.js</c>). Pure C#, no engine type.</para>
    /// </summary>
    public static class HyperSeaCells
    {
        /// <summary>A lattice cell's width across its square faces, and the lattice's spacing.</summary>
        public const double A = HyperSea.CellLy;
        public const double Half = A / 2;
        /// <summary>The nucleus's radius in a full-size cell, ly: the game's nucleus, 392 u at 120 u a light year
        /// [S: Docs/ECOSYSTEM.md]. A smaller or larger cell's nucleus scales with it (<see cref="Section.NucleusR"/>).</summary>
        public const double NucleusRadius = 392.0 / 120.0;
        /// <summary>The lattice's origin: the board's place "heart" (Sgr A*, HyperSeaSky.txt) is a nucleus [A].</summary>
        public const double HeartX = 0, HeartY = 0, HeartZ = -5;
        /// <summary>How far past the disk's faces the slice may go.</summary>
        public const double SliceLimit = HyperSea.DiskHalf + 100;

        // The cells show from a size on: under this many pixels a cell, the plain sea; the membranes fade in over the
        // next ones and the nuclei from a 4 px radius (full at 8), so neither turns to moire as the cells shrink.
        /// <summary>A cell narrower than this on screen is not drawn (the board's cell zoom).</summary>
        public const double ShowFromPx = 14;

        // ── The foam (cells.js FOAM) ─────────────────────────────────────────

        /// <summary>How far a nucleus drifts off its lattice point, a share of a cell (one sigma).</summary>
        public const double Drift = .12;
        /// <summary>How much fuller or emptier one cell is than the next (its weight's sigma).</summary>
        public const double SizeSpread = .2;
        /// <summary>How much of the sea splits into knots of small cells.</summary>
        public const double Knots = .42;
        /// <summary>A smooth field over the weights, so big and small cells come in neighbourhoods.</summary>
        public const double SizeField = .16;
        /// <summary>How much liquid lies between cells: it pulls each outline in and rounds its corners.</summary>
        public const double Wetness = .5;

        /// <summary>One cell: its nucleus's position, its lattice point, its weight and, once built, its polyhedron.</summary>
        sealed class Site
        {
            public int I, J, K, O, M;
            public double Lx, Ly, Lz, Px, Py, Pz, W, H, NucS;
            public List<List<double[]>> Faces;
            public double Vol, Mx, My, Mz, NucleusR;
        }

        // The page's integer arithmetic: Math.imul, >>> and | 0.
        static int Imul(int a, int b) => unchecked(a * b);
        static int ToInt32(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            double t = Math.Truncate(v), m = t - Math.Floor(t / 4294967296.0) * 4294967296.0;
            return unchecked((int)(uint)m);
        }

        /// <summary>The page's <c>hU</c>: a hash of four integers, uniform in [0, 1).</summary>
        static double HashU(int a, int b, int c, int d)
        {
            int h = Imul(a, 73856093) ^ Imul(b, 19349663) ^ Imul(c, 83492791) ^ Imul(d, unchecked((int)2654435761u));
            h = Imul(h ^ (int)((uint)h >> 13), 1274126177);
            h ^= (int)((uint)h >> 16);
            return (uint)h / 4294967296.0;
        }

        /// <summary>The page's <c>hG</c>: a normally distributed hash (Box-Muller).</summary>
        static double HashG(int a, int b, int c, int d)
        {
            double u = Math.Max(1e-9, HashU(a, b, c, d)), v = HashU(a + 7, b - 3, c + 11, d + 5);
            return Math.Sqrt(-2 * Math.Log(u)) * Math.Cos(6.2832 * v);
        }

        /// <summary>The page's <c>vn3</c>: smooth value noise.</summary>
        static double Noise(double x, double y, double z, int s)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y), zi = (int)Math.Floor(z);
            double fx = x - xi, fy = y - yi, fz = z - zi;
            static double Sm(double t) => t * t * (3 - 2 * t);
            static double L(double a, double b, double t) => a + (b - a) * t;
            double u = Sm(fx), v = Sm(fy), w = Sm(fz);
            double H(int a, int b, int c) => HashU(a, b, c, s);
            return L(L(L(H(xi, yi, zi), H(xi + 1, yi, zi), u), L(H(xi, yi + 1, zi), H(xi + 1, yi + 1, zi), u), v),
                L(L(H(xi, yi, zi + 1), H(xi + 1, yi, zi + 1), u), L(H(xi, yi + 1, zi + 1), H(xi + 1, yi + 1, zi + 1), u), v), w);
        }

        static readonly Dictionary<(int, int, int, int), Site[]> s_Sites = new();
        static readonly Dictionary<(double, double, double), Site> s_ByPosition = new();
        static readonly object s_Lock = new();

        /// <summary>The cells a lattice point holds (cells.js <c>sitesOf</c>): i, j, k count cells from the Heart, o = 0 or
        /// 1 picks the sublattice. The first is the point's own cell; any after it are its knot.</summary>
        static Site[] SitesOf(int i, int j, int k, int o)
        {
            lock (s_Lock)
            {
                if (s_Sites.TryGetValue((i, j, k, o), out var got)) return got;
                double lx = i + o / 2.0, ly = j + o / 2.0, lz = k + o / 2.0;
                double Lx = lx * A + HeartX, Ly = ly * A + HeartY, Lz = lz * A + HeartZ;
                bool heart = i == 0 && j == 0 && k == 0 && o == 0;
                double jit = heart ? 0 : Drift * A;
                double px = Lx + HashG(i, j, k, o * 2 + 1) * jit, py = Ly + HashG(i, j, k, o * 2 + 21) * jit, pz = Lz + HashG(i, j, k, o * 2 + 41) * jit;
                double w = (heart ? 0 : SizeSpread * .5 * HashG(i, j, k, o * 2 + 61)) + SizeField * (Noise(lx / 2.4 + 21, ly / 2.4 + 2, lz / 2.4 + 8, 91) - .5);
                var list = new List<Site>();
                double f = heart ? 0 : Noise(lx / 1.7 + 3, ly / 1.7 + 11, lz / 1.7 + 5, 17);
                if (f >= 1 - Knots)
                {
                    int n = f > 1 - Knots * .3 ? 5 : f > 1 - Knots * .6 ? 3 : 1;
                    double sh = n > 1 ? .05 : .03, rr = (n > 1 ? .2 : .26) * A;
                    for (int m = 0; m < n; m++)
                    {
                        double dx = HashG(i, j, k, 100 + m * 3 + o), dy = HashG(i, j, k, 200 + m * 3 + o), dz = HashG(i, j, k, 300 + m * 3 + o);
                        double dl = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dl == 0) dl = 1;
                        list.Add(new Site { M = m + 1, Px = px + dx / dl * rr, Py = py + dy / dl * rr, Pz = pz + dz / dl * rr, W = -sh, H = HashU(i, j, k, 400 + m + o * 9) });
                    }
                    w -= sh;
                }
                list.Insert(0, new Site { M = 0, Px = px, Py = py, Pz = pz, W = w, H = HashU(i, j, k, 500 + o) });
                foreach (var s in list)
                {
                    s.I = i; s.J = j; s.K = k; s.O = o; s.Lx = Lx; s.Ly = Ly; s.Lz = Lz;
                    s.W *= .55 * A * A;
                    s.NucS = 1 + .16 * HashG(ToInt32(s.H * 1e6), 3, 5, 7);
                }
                if (s_Sites.Count > 200000) { s_Sites.Clear(); s_ByPosition.Clear(); }
                var arr = list.ToArray();
                s_Sites[(i, j, k, o)] = arr;
                foreach (var s in arr) s_ByPosition[(s.Px, s.Py, s.Pz)] = s;
                return arr;
            }
        }

        static int JsRoundI(double v) => (int)Math.Floor(v + 0.5);

        /// <summary>Every cell whose lattice point lies within r lattice steps of a point (cells.js <c>sitesNear</c>).</summary>
        static void SitesNear(double x, double y, double z, int r, List<Site> into)
        {
            into.Clear();
            for (int o = 0; o < 2; o++)
            {
                int ci = JsRoundI((x - HeartX) / A - o / 2.0), cj = JsRoundI((y - HeartY) / A - o / 2.0), ck = JsRoundI((z - HeartZ) / A - o / 2.0);
                for (int i = ci - r; i <= ci + r; i++)
                for (int j = cj - r; j <= cj + r; j++)
                for (int k = ck - r; k <= ck + r; k++)
                    into.AddRange(SitesOf(i, j, k, o));
            }
        }

        [ThreadStatic] static List<Site> s_Near;

        static Site SiteAt(double x, double y, double z)
        {
            var near = s_Near ??= new List<Site>(128);
            SitesNear(x, y, z, 1, near);
            Site best = null;
            double bd = double.PositiveInfinity;
            foreach (var s in near)
            {
                double d = (s.Px - x) * (s.Px - x) + (s.Py - y) * (s.Py - y) + (s.Pz - z) * (s.Pz - z) - s.W;
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }

        /// <summary>The cell a position names: the nucleus at exactly that position, or else the cell the point is in.</summary>
        static Site SiteOf((double x, double y, double z) c)
        {
            lock (s_Lock)
                if (s_ByPosition.TryGetValue(c, out var s)) return s;
            return SiteAt(c.x, c.y, c.z);
        }

        /// <summary>The nucleus of the cell a point is in: the least weighted distance (a power diagram).</summary>
        public static (double x, double y, double z) CellAt(double x, double y, double z)
        {
            var s = SiteAt(x, y, z);
            return (s.Px, s.Py, s.Pz);
        }

        /// <summary>A cell's lattice point, in half cells from the Heart (each index even, or each odd). A knot's small
        /// cells share their lattice point, and with it their sea.</summary>
        public static (int i, int j, int k) KeyOf((double x, double y, double z) c)
        {
            var s = SiteOf(c);
            return (2 * s.I + s.O, 2 * s.J + s.O, 2 * s.K + s.O);
        }

        /// <summary>A cell's lattice point, where its sea is read (cells.js <c>densOf</c>).</summary>
        public static (double x, double y, double z) LatticePoint((double x, double y, double z) c)
        {
            var s = SiteOf(c);
            return (s.Lx, s.Ly, s.Lz);
        }

        /// <summary>True for the central cell, whose nucleus is the Heart.</summary>
        public static bool IsHeart((double x, double y, double z) c)
        {
            var s = SiteOf(c);
            return s.I == 0 && s.J == 0 && s.K == 0 && s.O == 0 && s.M == 0;
        }

        /// <summary>The page's Math.round: halves round up.</summary>
        static double JsRound(double v) => Math.Floor(v + 0.5);

        /// <summary>The layer of lattice points nearest a height: the slice snaps to one, so by default it cuts nuclei.</summary>
        public static double SnapToLayer(double z) => JsRound((z - HeartZ) / Half) * Half + HeartZ;

        /// <summary>The height the map slices the cells at: the focus's layer plus the reader's offset, kept in the disk.</summary>
        public static double SliceZ(double focusZ, double offset) =>
            Math.Max(-SliceLimit, Math.Min(SliceLimit, SnapToLayer(focusZ) + offset));

        /// <summary>How strongly a membrane shows for a cell <paramref name="cellPx"/> wide: none under 22 px, full at 44.</summary>
        public static double MembraneAlpha(double cellPx) => Math.Max(0, Math.Min(1, (cellPx - 22) / 22));

        /// <summary>How strongly a nucleus shows: from a 4 px radius, full at 8.</summary>
        public static double NucleusAlpha(double cellPx) => Math.Max(0, Math.Min(1, (cellPx * NucleusRadius / A - 4) / 4));

        // ── A cell's polyhedron (cells.js polyOf) ────────────────────────────

        const double EpsF = 1e-7;

        static double Dot(double[] a, double[] b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
        static double[] Cross(double[] a, double[] b) => new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };

        /// <summary>Keep the part of a polyhedron (faces about its nucleus) where n . p &lt;= d, capping the cut.</summary>
        static List<List<double[]>> Clip3(List<List<double[]>> faces, double[] n, double d)
        {
            bool any = false;
            foreach (var f in faces)
            {
                foreach (var p in f) if (Dot(n, p) - d > EpsF) { any = true; break; }
                if (any) break;
            }
            if (!any) return faces;
            var output = new List<List<double[]>>();
            var cap = new List<double[]>();
            foreach (var f in faces)
            {
                var o2 = new List<double[]>();
                for (int i = 0; i < f.Count; i++)
                {
                    var p = f[i];
                    var q = f[(i + 1) % f.Count];
                    double fp = Dot(n, p) - d, fq = Dot(n, q) - d;
                    if (fp <= EpsF)
                    {
                        o2.Add(p);
                        if (fp >= -EpsF) cap.Add(p);
                    }
                    if ((fp < -EpsF && fq > EpsF) || (fp > EpsF && fq < -EpsF))
                    {
                        double t = fp / (fp - fq);
                        var r = new[] { p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t, p[2] + (q[2] - p[2]) * t };
                        o2.Add(r);
                        cap.Add(r);
                    }
                }
                if (o2.Count >= 3) output.Add(o2);
            }
            var u = new List<double[]>();
            foreach (var p in cap)
            {
                bool dup = false;
                foreach (var q in u)
                    if (Math.Abs(q[0] - p[0]) + Math.Abs(q[1] - p[1]) + Math.Abs(q[2] - p[2]) < 1e-5) { dup = true; break; }
                if (!dup) u.Add(p);
            }
            if (u.Count >= 3)
            {
                double cx = 0, cy = 0, cz = 0;
                foreach (var p in u) { cx += p[0]; cy += p[1]; cz += p[2]; }
                cx /= u.Count; cy /= u.Count; cz /= u.Count;
                double nl = Math.Sqrt(Dot(n, n));
                var nn = new[] { n[0] / nl, n[1] / nl, n[2] / nl };
                var ax = Math.Abs(nn[0]) < .9 ? new double[] { 1, 0, 0 } : new double[] { 0, 1, 0 };
                var U = Cross(nn, ax);
                var V = Cross(nn, U);
                double Ang(double[] p)
                {
                    double ex = p[0] - cx, ey = p[1] - cy, ez = p[2] - cz;
                    return Math.Atan2(ex * V[0] + ey * V[1] + ez * V[2], ex * U[0] + ey * U[1] + ez * U[2]);
                }
                u.Sort((a, b) => Ang(a).CompareTo(Ang(b)));
                output.Add(u);
            }
            return output;
        }

        [ThreadStatic] static List<Site> s_Cand;

        static void Build(Site s)
        {
            if (s.Faces != null) return;
            double h = 1.3 * A;
            double[] V(double x, double y, double z) => new[] { x * h, y * h, z * h };
            var F = new List<List<double[]>>
            {
                new() { V(1, -1, -1), V(1, 1, -1), V(1, 1, 1), V(1, -1, 1) },
                new() { V(-1, -1, -1), V(-1, -1, 1), V(-1, 1, 1), V(-1, 1, -1) },
                new() { V(-1, 1, -1), V(-1, 1, 1), V(1, 1, 1), V(1, 1, -1) },
                new() { V(-1, -1, -1), V(1, -1, -1), V(1, -1, 1), V(-1, -1, 1) },
                new() { V(-1, -1, 1), V(1, -1, 1), V(1, 1, 1), V(-1, 1, 1) },
                new() { V(-1, -1, -1), V(-1, 1, -1), V(1, 1, -1), V(1, -1, -1) },
            };
            var near = s_Cand ??= new List<Site>(512);
            SitesNear(s.Px, s.Py, s.Pz, 2, near);
            var cand = new List<(double dd, int at, Site q)>();
            double lim = (2.1 * A) * (2.1 * A);
            for (int idx = 0; idx < near.Count; idx++)
            {
                var q = near[idx];
                if (q == s) continue;
                double dx = q.Px - s.Px, dy = q.Py - s.Py, dz = q.Pz - s.Pz, dd = dx * dx + dy * dy + dz * dz;
                if (dd < lim) cand.Add((dd, idx, q));
            }
            // the page's sort is stable: equal distances keep their order
            cand.Sort((a, b) => a.dd != b.dd ? a.dd.CompareTo(b.dd) : a.at.CompareTo(b.at));
            double maxR = h * Math.Sqrt(3);
            foreach (var (dd, _, q) in cand)
            {
                double nl = Math.Sqrt(dd), d = (dd - q.W + s.W) / 2;
                if (d / nl > maxR) continue;
                F = Clip3(F, new[] { q.Px - s.Px, q.Py - s.Py, q.Pz - s.Pz }, d);
                if (F.Count < 4) break;
                maxR = 0;
                foreach (var f in F)
                foreach (var p in f) maxR = Math.Max(maxR, Math.Sqrt(Dot(p, p)));
            }
            double vol = 0, mx = 0, my = 0, mz = 0;
            foreach (var f in F)
                for (int i = 1; i < f.Count - 1; i++)
                {
                    double[] a = f[0], b = f[i], c = f[i + 1];
                    double v = Dot(a, Cross(b, c)) / 6;
                    vol += v;
                    mx += (a[0] + b[0] + c[0]) * v / 4;
                    my += (a[1] + b[1] + c[1]) * v / 4;
                    mz += (a[2] + b[2] + c[2]) * v / 4;
                }
            s.Vol = Math.Abs(vol);
            if (vol != 0) { mx /= vol; my /= vol; mz /= vol; }
            else mx = my = mz = 0;
            // the nucleus sits at its cell's middle, sized to its cell and never crowding its walls
            s.Mx = s.Px + mx; s.My = s.Py + my; s.Mz = s.Pz + mz;
            double cr = Math.Cbrt(s.Vol * 3 / (4 * Math.PI));
            s.NucleusR = Math.Min(cr * .32, NucleusRadius * s.NucS * Math.Cbrt(Math.Max(.05, s.Vol / (A * A * A / 2))));
            s.Faces = F;
        }

        // ── Sections ─────────────────────────────────────────────────────────

        /// <summary>One cell cut by a plane: where its nucleus's position projects onto the plane (cu, cv) and how far
        /// it sits off the plane (h, signed along U x V); where its middle projects (mu, mv) and how far that sits off
        /// the plane (mh) - the nucleus is drawn there - and the nucleus's radius.</summary>
        public struct Section
        {
            public double Cu, Cv, H;
            public double Mu, Mv, Mh, NucleusR;
        }

        [ThreadStatic] static List<(double u, double v)> s_ClipOut, s_Pts;

        /// <summary>
        /// The section of the cell <paramref name="c"/> names by the plane through <paramref name="o"/> spanned by unit
        /// vectors <paramref name="u"/> and <paramref name="v"/>: its convex polygon goes into <paramref name="poly"/>
        /// (cleared first). False when the plane misses the cell. Extra half-planes a u + b v &lt;= r clip it further.
        /// </summary>
        public static bool TrySection((double x, double y, double z) c, (double x, double y, double z) o,
            (double x, double y, double z) u, (double x, double y, double z) v, List<(double u, double v)> poly,
            out Section section, IReadOnlyList<(double a, double b, double r)> extra = null)
        {
            section = default;
            poly.Clear();
            var s = SiteOf(c);
            Build(s);
            double nX = u.y * v.z - u.z * v.y, nY = u.z * v.x - u.x * v.z, nZ = u.x * v.y - u.y * v.x;
            double dx = s.Px - o.x, dy = s.Py - o.y, dz = s.Pz - o.z;
            double cu = dx * u.x + dy * u.y + dz * u.z, cv = dx * v.x + dy * v.y + dz * v.z, hgt = dx * nX + dy * nY + dz * nZ;
            var pts = s_Pts ??= new List<(double u, double v)>(32);
            pts.Clear();
            foreach (var f in s.Faces)
                for (int i = 0; i < f.Count; i++)
                {
                    var p = f[i];
                    var q = f[(i + 1) % f.Count];
                    double fp = p[0] * nX + p[1] * nY + p[2] * nZ + hgt + 1.3e-5, fq = q[0] * nX + q[1] * nY + q[2] * nZ + hgt + 1.3e-5;
                    if ((fp < 0) != (fq < 0))
                    {
                        double t = fp / (fp - fq);
                        double rx = p[0] + (q[0] - p[0]) * t, ry = p[1] + (q[1] - p[1]) * t, rz = p[2] + (q[2] - p[2]) * t;
                        pts.Add((cu + rx * u.x + ry * u.y + rz * u.z, cv + rx * v.x + ry * v.y + rz * v.z));
                    }
                }
            if (pts.Count < 3) return false;
            double mu = 0, mv = 0;
            foreach (var p in pts) { mu += p.u; mv += p.v; }
            mu /= pts.Count; mv /= pts.Count;
            pts.Sort((a, b) => Math.Atan2(a.v - mv, a.u - mu).CompareTo(Math.Atan2(b.v - mv, b.u - mu)));
            foreach (var p in pts)
                if (poly.Count == 0 || Math.Abs(p.u - poly[poly.Count - 1].u) + Math.Abs(p.v - poly[poly.Count - 1].v) > 1e-4) poly.Add(p);
            if (poly.Count > 2 && Math.Abs(poly[0].u - poly[poly.Count - 1].u) + Math.Abs(poly[0].v - poly[poly.Count - 1].v) < 1e-4)
                poly.RemoveAt(poly.Count - 1);
            if (poly.Count < 3) return false;
            if (extra != null)
            {
                var tmp = s_ClipOut ??= new List<(double u, double v)>(32);
                foreach (var e in extra)
                {
                    Clip(poly, e.a, e.b, e.r, tmp);
                    if (poly.Count < 3) return false;
                }
            }
            double ex = s.Mx - o.x, ey = s.My - o.y, ez = s.Mz - o.z;
            section = new Section
            {
                Cu = cu, Cv = cv, H = hgt,
                Mu = ex * u.x + ey * u.y + ez * u.z, Mv = ex * v.x + ey * v.y + ez * v.z, Mh = ex * nX + ey * nY + ez * nZ,
                NucleusR = s.NucleusR,
            };
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

        [ThreadStatic] static List<(double u, double v)> s_WetQ, s_WetR;

        /// <summary>
        /// A section as the wet foam draws it (cells.js <c>wetOutline</c> and <c>roundPath</c>): every side pulled in by the
        /// liquid between cells, each corner rounded into a curve sampled <paramref name="perCorner"/> times. The outline
        /// goes into <paramref name="into"/> (cleared first); with no samples asked for, the pulled-in corners alone. False for a sliver the slice only grazes, which the liquid
        /// fills instead.
        /// </summary>
        public static bool TryWetOutline(List<(double u, double v)> poly, List<(double u, double v)> into, int perCorner = 3)
        {
            into.Clear();
            int n = poly.Count;
            if (n < 3) return false;
            double dl = Wetness * .035 * A, rad = Wetness * .24 * A;
            var P = s_WetR ??= new List<(double u, double v)>(32);
            P.Clear();
            P.AddRange(poly);
            double a2 = 0;
            for (int i = 0; i < n; i++) a2 += P[i].u * P[(i + 1) % n].v - P[(i + 1) % n].u * P[i].v;
            if (a2 < 0) P.Reverse();
            var Q = s_WetQ ??= new List<(double u, double v)>(32);
            Q.Clear();
            for (int i = 0; i < n; i++)
            {
                var (a1, b1, c1) = Line(P, (i + n - 1) % n, dl);
                var (a2_, b2, c2) = Line(P, i, dl);
                double det = a1 * b2 - a2_ * b1;
                if (Math.Abs(det) < 1e-9) continue;
                Q.Add(((c1 * b2 - c2 * b1) / det, (a1 * c2 - a2_ * c1) / det));
            }
            bool ok = Q.Count == n;
            if (ok)
                for (int i = 0; i < n; i++)
                {
                    var a = P[i];
                    var b = P[(i + 1) % n];
                    var qa = Q[i];
                    var qb = Q[(i + 1) % n];
                    if ((b.u - a.u) * (qb.u - qa.u) + (b.v - a.v) * (qb.v - qa.v) <= 0) { ok = false; break; }
                }
            if (!ok)
            {
                double cx = 0, cy = 0;
                foreach (var p in P) { cx += p.u / n; cy += p.v / n; }
                double R = 0;
                foreach (var p in P) R = Math.Max(R, Math.Sqrt((p.u - cx) * (p.u - cx) + (p.v - cy) * (p.v - cy)));
                double k = Math.Max(.2, 1 - dl / R * 1.4);
                Q.Clear();
                foreach (var p in P) Q.Add((cx + (p.u - cx) * k, cy + (p.v - cy) * k));
            }
            // drop slivers and straight-through corners, which would round into loops
            for (int pass = 0; pass < 3 && Q.Count > 3; pass++)
            {
                var R = new List<(double u, double v)>(Q.Count);
                for (int i = 0; i < Q.Count; i++)
                {
                    var p = Q[(i + Q.Count - 1) % Q.Count];
                    var v = Q[i];
                    var nx = Q[(i + 1) % Q.Count];
                    double ax = v.u - p.u, ay = v.v - p.v, bx = nx.u - v.u, by = nx.v - v.v;
                    double la = Math.Sqrt(ax * ax + ay * ay), lb = Math.Sqrt(bx * bx + by * by);
                    if (la < .025 * A && R.Count > 0) continue;
                    if (la > 1e-6 && lb > 1e-6 && (ax * by - ay * bx) / (la * lb) < .1 && ax * bx + ay * by > 0) continue;
                    R.Add(v);
                }
                if (R.Count < 3) break;
                Q.Clear();
                Q.AddRange(R);
            }
            double ar = 0;
            for (int i = 0; i < Q.Count; i++) ar += Q[i].u * Q[(i + 1) % Q.Count].v - Q[(i + 1) % Q.Count].u * Q[i].v;
            if (ar / 2 < .02 * A * A) return false;
            double mx = 0, my = 0;
            foreach (var p in Q) { mx += p.u; my += p.v; }
            mx /= Q.Count; my /= Q.Count;
            double minW = double.PositiveInfinity;
            for (int i = 0; i < Q.Count; i++)
            {
                var a = Q[i];
                var b = Q[(i + 1) % Q.Count];
                double l = Math.Sqrt((b.u - a.u) * (b.u - a.u) + (b.v - a.v) * (b.v - a.v));
                if (l == 0) l = 1;
                minW = Math.Min(minW, Math.Abs((b.u - a.u) * (my - a.v) - (b.v - a.v) * (mx - a.u)) / l);
            }
            if (minW < .05 * A) return false;
            // each corner a curve from a point on its incoming side to one on its outgoing side, never past half a side
            // (none asked for: the corners themselves, as the page's wetOutline returns them)
            if (perCorner <= 0)
            {
                into.AddRange(Q);
                return true;
            }
            int m = Q.Count;
            for (int i = 0; i < m; i++)
            {
                var v = Q[i];
                var p = Q[(i + m - 1) % m];
                var q = Q[(i + 1) % m];
                double lp = Math.Sqrt((p.u - v.u) * (p.u - v.u) + (p.v - v.v) * (p.v - v.v)), lq = Math.Sqrt((q.u - v.u) * (q.u - v.u) + (q.v - v.v) * (q.v - v.v));
                if (lp == 0) lp = 1;
                if (lq == 0) lq = 1;
                double tp = Math.Min(.5, rad / lp), tq = Math.Min(.5, rad / lq);
                (double u, double v) s0 = (v.u + (p.u - v.u) * tp, v.v + (p.v - v.v) * tp), e0 = (v.u + (q.u - v.u) * tq, v.v + (q.v - v.v) * tq);
                for (int k = 0; k <= perCorner; k++)
                {
                    double t = (double)k / perCorner, a = (1 - t) * (1 - t), b = 2 * (1 - t) * t, cc = t * t;
                    into.Add((a * s0.u + b * v.u + cc * e0.u, a * s0.v + b * v.v + cc * e0.v));
                }
            }
            return true;
        }

        static (double a, double b, double c) Line(List<(double u, double v)> P, int i, double dl)
        {
            var a = P[i];
            var b = P[(i + 1) % P.Count];
            double dx = b.u - a.u, dy = b.v - a.v, l = Math.Sqrt(dx * dx + dy * dy);
            if (l == 0) l = 1;
            double nx = -dy / l, ny = dx / l;
            return (nx, ny, a.u * nx + a.v * ny + dl);
        }

        // ── Which cells a view holds ─────────────────────────────────────────

        /// <summary>
        /// The nuclei whose cells a horizontal slice at height <paramref name="zs"/> may cross over the box x0..x1, y0..y1
        /// (cells.js <c>sitesCrossing</c>), in the page's order. Stops at <paramref name="max"/>.
        /// </summary>
        public static void NucleiInSlice(double zs, double x0, double y0, double x1, double y1,
            List<(double x, double y, double z)> into, int max = int.MaxValue)
        {
            into.Clear();
            for (int o = 0; o < 2; o++)
            {
                int k0 = (int)Math.Floor((zs - HeartZ) / A - o / 2.0) - 1, k1 = (int)Math.Ceiling((zs - HeartZ) / A - o / 2.0) + 1;
                int j0 = (int)Math.Floor((y0 - HeartY) / A - o / 2.0) - 1, j1 = (int)Math.Ceiling((y1 - HeartY) / A - o / 2.0) + 1;
                int i0 = (int)Math.Floor((x0 - HeartX) / A - o / 2.0) - 1, i1 = (int)Math.Ceiling((x1 - HeartX) / A - o / 2.0) + 1;
                for (int k = k0; k <= k1; k++)
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    foreach (var s in SitesOf(i, j, k, o))
                    {
                        if (Math.Abs(s.Pz - zs) >= A * 1.1) continue;
                        if (into.Count >= max) return;
                        into.Add((s.Px, s.Py, s.Pz));
                    }
            }
        }

        /// <summary>
        /// The nuclei whose cells an upright slice at <paramref name="ys"/> may cross over x0..x1 and heights z0..z1 (the
        /// edge view's sheet), in the page's order. Stops at <paramref name="max"/>.
        /// </summary>
        public static void NucleiInUpright(double ys, double x0, double x1, double z0, double z1,
            List<(double x, double y, double z)> into, int max = int.MaxValue)
        {
            into.Clear();
            for (int o = 0; o < 2; o++)
            {
                int j0 = (int)Math.Floor((ys - HeartY) / A - o / 2.0) - 1, j1 = (int)Math.Ceiling((ys - HeartY) / A - o / 2.0) + 1;
                int k0 = (int)Math.Floor((z0 - HeartZ) / A - o / 2.0) - 1, k1 = (int)Math.Ceiling((z1 - HeartZ) / A - o / 2.0) + 1;
                int i0 = (int)Math.Floor((x0 - HeartX) / A - o / 2.0) - 1, i1 = (int)Math.Ceiling((x1 - HeartX) / A - o / 2.0) + 1;
                for (int j = j0; j <= j1; j++)
                for (int k = k0; k <= k1; k++)
                for (int i = i0; i <= i1; i++)
                    foreach (var s in SitesOf(i, j, k, o))
                    {
                        if (Math.Abs(s.Py - ys) > A * 1.1) continue;
                        if (into.Count >= max) return;
                        into.Add((s.Px, s.Py, s.Pz));
                    }
            }
        }

        /// <summary>
        /// The nucleus nearest (x, y) at the slice, counting its height double (the page's <c>tags</c>: the cell whose
        /// levels are named).
        /// </summary>
        public static (double x, double y, double z) NearestInSlice(double x, double y, double zs)
        {
            var near = s_Near ??= new List<Site>(128);
            SitesNear(x, y, zs, 1, near);
            Site best = null;
            double bd = double.PositiveInfinity;
            foreach (var s in near)
            {
                double d = (s.Px - x) * (s.Px - x) + (s.Py - y) * (s.Py - y) + 4 * (s.Pz - zs) * (s.Pz - zs);
                if (d < bd) { bd = d; best = s; }
            }
            return (best.Px, best.Py, best.Pz);
        }
    }
}
