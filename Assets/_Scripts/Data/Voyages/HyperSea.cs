using System;
using System.Collections.Generic;

namespace CosmicShore.Data
{
    /// <summary>The five seas the crew names by how thick the water feels (Docs/Voyages/VOYAGE_BOARD.md §2).</summary>
    public enum SeaKind
    {
        /// <summary>Under 0.1 g/cm³: as light as fresh snow.</summary>
        Spray = 0,
        /// <summary>Under 0.6 g/cm³: a froth, lighter than water.</summary>
        Froth = 1,
        /// <summary>Under 5 g/cm³: about water, up to iron.</summary>
        Water = 2,
        /// <summary>Under 100 g/cm³: as heavy as lead, gold, osmium and past it.</summary>
        Thick = 3,
        /// <summary>100 g/cm³ and over: no Ark crosses it, except to berth at a place inside it.</summary>
        Reef = 4,
    }

    /// <summary>
    /// The HyperSea as the Voyage Board charts it (https://claude.ai/artifact/25CvRSxBrjcxEUngEiCzEn,
    /// Docs/Voyages/VOYAGE_BOARD.md): our Milky Way, read as a sea. Positions are light years with the galactic centre at
    /// the origin, Sol at (0, <see cref="SolRadius"/>), the board's y running from the centre toward Sol and galactic
    /// longitude 90° to the left; z is height above the midplane. The sea's thickness is the real gas density, which a
    /// traveller perceives in g/cm³ (one hydrogen atom per cm³ reads as water).
    ///
    /// <para>A line-for-line port of the page's arithmetic (its <c>dens</c>, <c>armProfile</c>, <c>vnoise</c>,
    /// <c>hexOf</c> and <c>danger</c>), in doubles, so the same point gives the same number in the browser and in the
    /// game; <c>HyperSeaTests</c> holds the page's own outputs as goldens. Pure C#, no engine type.</para>
    /// </summary>
    public static class HyperSea
    {
        /// <summary>Light years in one cell, and the canon minute of Ark sailing it takes.</summary>
        public const double CellLy = 20;
        public const double MinutesPerCell = 1;
        /// <summary>Sol from the galactic centre, in light years.</summary>
        public const double SolRadius = 26000;
        /// <summary>Sol's height above the midplane.</summary>
        public const double SolHeight = 65;
        /// <summary>Half the disk's thickness (the disk is 1,000 ly thick).</summary>
        public const double DiskHalf = 500;
        /// <summary>The perceived density at which the sea becomes reef.</summary>
        public const double ReefDensity = 100;

        const double Deg = Math.PI / 180;
        static readonly double Sq3 = Math.Sqrt(3);

        /// <summary>Where a real object at galactic longitude <paramref name="l"/>, distance <paramref name="d"/> ly from
        /// Sol and latitude <paramref name="b"/> sits on the board (x, y).</summary>
        public static (double x, double y) FromGalactic(double l, double d, double b = 0)
        {
            double dp = d * Math.Cos(b * Deg);
            return (-dp * Math.Sin(l * Deg), SolRadius - dp * Math.Cos(l * Deg));
        }

        /// <summary>That object's height above the midplane, rounded to a light year.</summary>
        public static double HeightOf(double d, double b) => JsRound(SolHeight + d * Math.Sin(b * Deg));

        /// <summary>The board's x of a galactic-centre object at longitude <paramref name="l"/>.</summary>
        static double FromCentre(double l) => -SolRadius * Math.Sin(l * Deg);

        /// <summary>JavaScript's Math.round (halves round up), which the page uses.</summary>
        public static double JsRound(double v) => Math.Floor(v + 0.5);

        // ── The sea ──────────────────────────────────────────────────────────

        readonly struct Cloud
        {
            public readonly double X, Y, Z, H, Rx, Ry, Peak, C, S;
            public Cloud(double x, double y, double rx, double ry, double peak, double ang, double z)
            {
                X = x; Y = y; Z = z; H = (rx + ry) * .45; Rx = rx; Ry = ry; Peak = peak; C = Math.Cos(ang); S = Math.Sin(ang);
            }
        }

        readonly struct Bubble
        {
            public readonly double X, Y, R, K;
            public Bubble(double x, double y, double r, double k) { X = x; Y = y; R = r; K = k; }
        }

        static readonly Cloud[] Clouds = MakeClouds();
        static readonly Bubble[] Bubbles = MakeBubbles();

        static Cloud[] MakeClouds()
        {
            var list = new List<Cloud>();
            void Near(double l, double d, double b, double rx, double ry, double peak, double ang)
            {
                var (x, y) = FromGalactic(l, d, b);
                list.Add(new Cloud(x, y, rx, ry, peak, ang, HeightOf(d, b)));
            }
            void Far(double l, double d, double rx, double ry, double peak)
            {
                var (x, y) = FromGalactic(l, d);
                list.Add(new Cloud(x, y, rx, ry, peak, 0, 0));
            }
            void At(double x, double y, double rx, double ry, double peak, double ang = 0, double z = 0) =>
                list.Add(new Cloud(x, y, rx, ry, peak, ang, z));

            Near(172, 450, -16, 50, 32, 400, .6);       // Taurus
            Near(211, 1350, -19.4, 62, 24, 600, 1.1);   // Orion A
            Near(206, 1360, -15, 34, 30, 380, 0);       // Orion B
            Near(353, 460, 16.7, 26, 22, 600, 0);       // Rho Ophiuchi
            Near(340, 500, 15, 26, 18, 260, .4);        // Lupus
            Near(301, 600, -1, 30, 26, 300, 0);         // Coalsack
            Near(28, 700, 3, 120, 40, 260, .3);         // Aquila Rift
            Near(159, 1000, -20, 70, 26, 300, -.4);     // Perseus cloud
            Near(110, 1000, 15, 60, 50, 250, 0);        // Cepheus Flare
            Far(80, 4600, 220, 160, 420);               // Cygnus X
            Far(287.6, 7500, 160, 110, 420);            // Carina
            Far(17, 5700, 110, 90, 320);                // Eagle
            Far(6, 4100, 100, 80, 320);                 // Lagoon
            // The centre's ring of reefs, Sgr B2 and the Brick.
            for (int i = 0; i < 12; i++)
            {
                double a = i / 12.0 * Math.PI * 2 + .2;
                At(-40 + Math.Cos(a) * 330, 30 + Math.Sin(a) * 190, 46, 40, 260);
            }
            At(FromCentre(0.67), 420, 70, 70, 1500);
            At(FromCentre(0.253), 210, 30, 26, 800);
            At(FromCentre(-0.57), 60, 50, 44, 400);
            // Reefs around Tumult (proposed).
            At(900, 1660, 130, 70, 420, .2, -30); At(600, 1480, 90, 60, 300, 0, -70); At(1270, 1900, 80, 80, 260, 0, -100);
            At(1830, 2120, 160, 90, 380, 1.2, 60); At(340, 1820, 70, 55, 300, 0, -90); At(1400, 1180, 120, 90, 380, .5, -50);
            At(700, 1110, 90, 70, 330, 0, 40); At(-60, 1060, 110, 60, 300, -.3, 30);
            return list.ToArray();
        }

        static Bubble[] MakeBubbles()
        {
            var (lx, ly) = FromGalactic(230, 80);
            var (ox, oy) = FromGalactic(200, 720);
            var (ix, iy) = FromGalactic(330, 360);
            return new[]
            {
                new Bubble(lx, ly, 470, .02),   // the Local Bubble, Sol inside
                new Bubble(ox, oy, 320, .05),   // the Orion-Eridanus superbubble
                new Bubble(ix, iy, 290, .04),   // Loop I
                new Bubble(960, 2160, 330, .03), // Tumult's bubble (proposed)
            };
        }

        /// <summary>The page's integer hash, in [0, 1).</summary>
        public static double Hash(int i, int j)
        {
            unchecked
            {
                int h = i * 374761393 + j * 668265263;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>Smooth value noise, in [0, 1).</summary>
        public static double ValueNoise(double x, double y)
        {
            double xi = Math.Floor(x), yi = Math.Floor(y), xf = x - xi, yf = y - yi;
            double u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            int ix = (int)xi, iy = (int)yi;
            double a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        static double Fbm(double x, double y) =>
            ValueNoise(x, y) * .55 + ValueNoise(x * 2.3 + 17, y * 2.3 - 9) * .3 + ValueNoise(x * 5.1 - 31, y * 5.1 + 4) * .15;

        // Four trailing log-spiral arms at a 13° pitch, plus the Orion Spur through Sol.
        static readonly double ArmB = Math.Tan(13 * Deg), ArmSin = Math.Sin(13 * Deg);
        static readonly double SagittariusCarina = Math.PI / 2 + Math.Log(20000) / ArmB;
        static readonly double[] Arms =
        {
            SagittariusCarina, SagittariusCarina + Math.PI / 2, SagittariusCarina + Math.PI, SagittariusCarina - Math.PI / 2,
        };
        static readonly double SpurB = Math.Tan(10 * Deg), SpurPhi = Math.PI / 2 + Math.Log(26000) / SpurB, SpurSin = Math.Sin(10 * Deg);

        static double Wrap(double a)
        {
            a = (a + Math.PI) % (2 * Math.PI);
            if (a < 0) a += 2 * Math.PI;
            return a - Math.PI;
        }

        /// <summary>How deep in a spiral arm (x, y) lies, 0 to 1.</summary>
        public static double ArmProfile(double x, double y, double r, double width)
        {
            if (r < 6000) return 0;
            double th = Math.Atan2(y, x), ramp = Math.Min(1, (r - 6000) / 5000);
            double best = 0;
            foreach (double phi in Arms)
            {
                // A log spiral crosses the circle at the pitch angle, so arc offset x sin(pitch) is the distance to the arm.
                double d = Math.Abs(Wrap(th + Math.Log(r) / ArmB - phi)) * r * ArmSin;
                double v = Math.Exp(-(d * d) / (width * width));
                if (v > best) best = v;
            }
            if (r > 23000 && r < 30000)
            {
                double ds = Math.Abs(Wrap(th + Math.Log(r) / SpurB - SpurPhi)) * r * SpurSin;
                double thw = Math.Abs(Wrap(th - Math.PI / 2));
                if (thw < .45) best = Math.Max(best, .7 * Math.Exp(-(ds * ds) / (width * width * .5)) * (1 - thw / .45));
            }
            return best * ramp;
        }

        static readonly double BarC = Math.Cos(-27 * Deg), BarS = Math.Sin(-27 * Deg);

        static double Bar(double x, double y)
        {
            double u = x * BarC - y * BarS, v = x * BarS + y * BarC;
            return Math.Exp(-Math.Pow(v / 12500, 4) - (u / 3000) * (u / 3000));
        }

        /// <summary>The sea's perceived density (g/cm³) through the whole column at (x, y), as the chart draws it.</summary>
        public static double Density(double x, double y) => Dens(x, y, 0, column: true);

        /// <summary>The sea's perceived density (g/cm³) at (x, y) and height <paramref name="z"/>.</summary>
        public static double Density(double x, double y, double z) => Dens(x, y, z, column: false);

        static double Dens(double x, double y, double z, bool column)
        {
            double r = Math.Sqrt(x * x + y * y);
            double arm = ArmProfile(x, y, r, 1100);
            double n = (0.28 + 2.2 * arm + 1.2 * Bar(x, y) * 0.5) * (1 + 4 * Math.Exp(-r / 2500));
            if (r > 45000) n *= Math.Exp(-(r - 45000) / 3000);
            n *= Math.Exp((Fbm(x / 520, y / 520) - .5) * 2.4 + (ValueNoise(x / 110, y / 110) - .5) * .9);
            foreach (var b in Bubbles)
            {
                double bx = x - b.X, by = y - b.Y;
                double d = Math.Sqrt(bx * bx + by * by) * (1 + (ValueNoise(x / 90, y / 90) - .5) * .35);
                if (d < b.R)
                {
                    double t = d < b.R * .65 ? 1 : 1 - (d - b.R * .65) / (b.R * .35);
                    n *= 1 + (b.K - 1) * t;
                }
            }
            if (!column) n *= Math.Exp(-((z / 360) * (z / 360)));   // warm gas thins away from the midplane
            foreach (var c in Clouds)
            {
                double dx = x - c.X, dy = y - c.Y;
                double reach = c.Rx * 3.2 + c.Ry * 3.2;
                if (Math.Abs(dx) > reach || Math.Abs(dy) > reach) continue;
                // Ragged edges: each cloud is warped by noise at its own scale, so reefs have spurs and channels.
                double sc = Math.Max(18, (c.Rx + c.Ry) * .35);
                dx += (ValueNoise(x / sc + 3.1, y / sc) - .5) * (c.Rx + c.Ry) * .8;
                dy += (ValueNoise(x / sc, y / sc + 7.7) - .5) * (c.Rx + c.Ry) * .8;
                double u = dx * c.C + dy * c.S, v = -dx * c.S + dy * c.C;
                double e = (u * u) / (c.Rx * c.Rx) + (v * v) / (c.Ry * c.Ry);
                if (e < 9)
                {
                    double dz = (z - c.Z) / c.H;
                    n += c.Peak * Math.Exp(-e) * (.35 + 1.3 * Fbm(x / (sc * .6), y / (sc * .6))) * (column ? 1 : Math.Exp(-(dz * dz)));
                }
            }
            return column ? n : n + .003;   // the halo above and below the disk is the thinnest spray
        }

        /// <summary>Which sea a density is.</summary>
        public static SeaKind Sea(double n) =>
            n < 0.1 ? SeaKind.Spray : n < 0.6 ? SeaKind.Froth : n < 5 ? SeaKind.Water : n < ReefDensity ? SeaKind.Thick : SeaKind.Reef;

        /// <summary>How rough a cell is, 0 to 3: thicker water is richer and rougher.</summary>
        public static double Danger(double n) => Math.Max(0, Math.Min(3, 0.9 * Math.Log10(Math.Max(n, 1e-3)) + 0.6));

        // The board's colour key, by log density: spray, froth, water, thick water, reef.
        static readonly double[][] Stops =
        {
            new[] { -2.2, 26, 34, 52 }, new[] { -1.3, 74, 86, 112 }, new[] { -0.6, 30, 92, 110 }, new[] { 0.0, 26, 72, 140 },
            new[] { 0.9, 70, 50, 150 }, new[] { 1.7, 110, 44, 140 }, new[] { 1.999, 130, 40, 120 }, new[] { 2.0, 214, 92, 70 },
            new[] { 3.2, 255, 150, 100 },
        };

        /// <summary>The board's colour for a density, each channel 0 to 255.</summary>
        public static (double r, double g, double b) SeaColor(double n)
        {
            double l = Math.Log10(Math.Max(n, 1e-3));
            if (l <= Stops[0][0]) return (Stops[0][1], Stops[0][2], Stops[0][3]);
            for (int i = 1; i < Stops.Length; i++)
            {
                if (l > Stops[i][0]) continue;
                double[] a = Stops[i - 1], b = Stops[i];
                double t = (l - a[0]) / (b[0] - a[0]);
                return (a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t, a[3] + (b[3] - a[3]) * t);
            }
            var last = Stops[Stops.Length - 1];
            return (last[1], last[2], last[3]);
        }

        // ── Cells, 20 ly each (pointy-top hexes) ─────────────────────────────

        /// <summary>The centre of cell (q, r).</summary>
        public static (double x, double y) CellCentre(int q, int r) => (CellLy * (q + r / 2.0), CellLy * Sq3 / 2 * r);

        /// <summary>The cell (q, r) holding the point (x, y).</summary>
        public static (int q, int r) CellOf(double x, double y)
        {
            double fr = y / (CellLy * Sq3 / 2), fq = x / CellLy - fr / 2, fs = -fq - fr;
            double q = JsRound(fq), r = JsRound(fr), s = JsRound(fs);
            double dq = Math.Abs(q - fq), dr = Math.Abs(r - fr), ds = Math.Abs(s - fs);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            return ((int)q, (int)r);
        }

        /// <summary>Steps between two cells.</summary>
        public static int CellSteps((int q, int r) a, (int q, int r) b) =>
            (Math.Abs(a.q - b.q) + Math.Abs(a.r - b.r) + Math.Abs(a.q + a.r - b.q - b.r)) / 2;

        /// <summary>A cell can also climb one layer: cells pack like foam, a layer 0.82 of a cell high.</summary>
        public static readonly double LayerLy = CellLy * Math.Sqrt(2.0 / 3.0);
    }

    /// <summary>A place an Ark can berth at (its cells are passable even in reef) or a closed well no Ark enters.</summary>
    public readonly struct HyperSeaPlace
    {
        public readonly double X, Y;
        public readonly int Radius;
        public readonly bool Closed;

        public HyperSeaPlace(double x, double y, int radius, bool closed = false)
        {
            X = x; Y = y; Radius = radius; Closed = closed;
        }
    }

    /// <summary>One cell of a measured course: where, how high, how thick and how rough.</summary>
    public readonly struct HyperSeaStep
    {
        public readonly int Q, R;
        public readonly double X, Y, Z, Density, Danger;

        public HyperSeaStep(int q, int r, double x, double y, double z, double density, double danger)
        {
            Q = q; R = r; X = x; Y = y; Z = z; Density = density; Danger = danger;
        }
    }

    /// <summary>A course measured cell by cell (the page's <c>measure</c>).</summary>
    public sealed class HyperSeaCourse
    {
        /// <summary>The cells sailed, after the start: the planar steps or the climb's layers, whichever is more.</summary>
        public readonly List<HyperSeaStep> Steps = new();
        /// <summary>The course drawn flat: the start's centre, then each planar cell's.</summary>
        public readonly List<(double x, double y)> Points = new();
        public int Layers;
        public int FlatSteps;
        public double MaxDanger;
        public double AverageDanger;
        /// <summary>The thickest water crossed outside a berth.</summary>
        public double ThickestDensity;

        public int Cells => Steps.Count;
        public double Minutes => Cells * HyperSea.MinutesPerCell;
        /// <summary>Light years sailed; light takes as many years.</summary>
        public double LightYears => Cells * HyperSea.CellLy;
        /// <summary>The course's own intensity, I1 to I4.</summary>
        public int Intensity => Math.Max(1, Math.Min(4, 1 + (int)HyperSea.JsRound(0.6 * MaxDanger + 0.4 * AverageDanger)));
    }

    /// <summary>
    /// Courses over the HyperSea's cells (the page's <c>search</c>, <c>trace</c> and <c>measure</c>): the cheapest
    /// way between two cells round the reefs, which an Ark crosses only where it berths. Sol's well (one cell) and the
    /// Heart's (four) are closed to every Ark. Densities are cached per chart.
    /// </summary>
    public sealed class HyperSeaChart
    {
        readonly HashSet<(int, int)> _berths = new();
        readonly HashSet<(int, int)> _closed = new();
        readonly Dictionary<(int, int), double> _density = new();

        static readonly (int q, int r)[] Neighbours = { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };

        public HyperSeaChart(IEnumerable<HyperSeaPlace> places)
        {
            Add(new HyperSeaPlace(0, HyperSea.SolRadius, 1, closed: true));
            Add(new HyperSeaPlace(0, 0, 4, closed: true));
            if (places == null) return;
            foreach (var p in places) Add(p);
        }

        void Add(HyperSeaPlace p)
        {
            var (q, r) = HyperSea.CellOf(p.X, p.Y);
            for (int dq = -p.Radius; dq <= p.Radius; dq++)
            for (int dr = -p.Radius; dr <= p.Radius; dr++)
            {
                if (HyperSea.CellSteps((0, 0), (dq, dr)) > p.Radius) continue;
                (p.Closed ? _closed : _berths).Add((q + dq, r + dr));
            }
        }

        /// <summary>The column density at a cell's centre.</summary>
        public double CellDensity(int q, int r)
        {
            if (_density.TryGetValue((q, r), out double n)) return n;
            var (x, y) = HyperSea.CellCentre(q, r);
            n = HyperSea.Density(x, y);
            if (_density.Count > 400000) _density.Clear();
            _density[(q, r)] = n;
            return n;
        }

        public bool IsBerth(int q, int r) => _berths.Contains((q, r));

        public bool Passable(int q, int r) =>
            !_closed.Contains((q, r)) && (_berths.Contains((q, r)) || CellDensity(q, r) < HyperSea.ReefDensity);

        /// <summary>The quick cost: every cell a minute, a touch more in rough water.</summary>
        public static double QuickCost(double n) => 1 + 0.002 * HyperSea.Danger(n);

        /// <summary>The safe cost: rough water costs the square of its danger.</summary>
        public static double SafeCost(double n)
        {
            double d = HyperSea.Danger(n);
            return 1 + 0.9 * d * d;
        }

        /// <summary>The cells from <paramref name="start"/> to <paramref name="goal"/> (both included), or null when no
        /// course within <paramref name="maxSteps"/> of the start reaches it.</summary>
        public List<(int q, int r)> FindCourse((int q, int r) start, (int q, int r) goal, bool safe = false, int maxSteps = 61)
        {
            var dist = new Dictionary<(int, int), double>();
            var prev = new Dictionary<(int, int), (int, int)>();
            var heap = new List<(double d, (int q, int r) k)>();

            void Swap(int a, int b) => (heap[a], heap[b]) = (heap[b], heap[a]);
            void Push((int, int) k, double d)
            {
                heap.Add((d, k));
                int i = heap.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (heap[p].d <= heap[i].d) break;
                    Swap(p, i);
                    i = p;
                }
            }
            (double d, (int q, int r) k) Pop()
            {
                var top = heap[0];
                var last = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                if (heap.Count > 0)
                {
                    heap[0] = last;
                    int i = 0;
                    for (;;)
                    {
                        int l = 2 * i + 1, r = l + 1, m = i;
                        if (l < heap.Count && heap[l].d < heap[m].d) m = l;
                        if (r < heap.Count && heap[r].d < heap[m].d) m = r;
                        if (m == i) break;
                        Swap(m, i);
                        i = m;
                    }
                }
                return top;
            }

            dist[start] = 0;
            Push(start, 0);
            int guard = 0;
            while (heap.Count > 0 && guard++ < 160000)
            {
                var (d, k) = Pop();
                if (d > dist[k]) continue;
                if (k == goal) break;
                foreach (var (a, b) in Neighbours)
                {
                    var nk = (k.q + a, k.r + b);
                    if (HyperSea.CellSteps(start, nk) > maxSteps || !Passable(nk.Item1, nk.Item2)) continue;
                    double n = CellDensity(nk.Item1, nk.Item2);
                    double nd = d + (safe ? SafeCost(n) : QuickCost(n));
                    if (nd < (dist.TryGetValue(nk, out double old) ? old : double.PositiveInfinity))
                    {
                        dist[nk] = nd;
                        prev[nk] = k;
                        Push(nk, nd);
                    }
                }
            }

            if (!dist.ContainsKey(goal)) return null;
            var cells = new List<(int q, int r)> { goal };
            var at = goal;
            while (at != start)
            {
                if (!prev.TryGetValue(at, out var p)) return null;
                at = p;
                cells.Add(at);
            }
            cells.Reverse();
            return cells;
        }

        /// <summary>Measure <paramref name="cells"/> (the start first) sailed from height <paramref name="z0"/> to
        /// <paramref name="z1"/>: each step's sea at its height, the roughest and average danger, the thickest water.</summary>
        public HyperSeaCourse Measure(IReadOnlyList<(int q, int r)> cells, double z0 = 0, double z1 = 0)
        {
            var course = new HyperSeaCourse();
            if (cells == null || cells.Count == 0) return course;
            int flatN = cells.Count - 1;
            int layers = (int)Math.Ceiling(Math.Abs(z1 - z0) / HyperSea.LayerLy);
            int steps = Math.Max(flatN, layers);
            course.Layers = layers;
            course.FlatSteps = flatN;
            course.Points.Add(HyperSea.CellCentre(cells[0].q, cells[0].r));
            for (int i = 1; i < cells.Count; i++) course.Points.Add(HyperSea.CellCentre(cells[i].q, cells[i].r));
            if (steps == 0 || flatN == 0) return course;

            double max = double.NegativeInfinity, sum = 0;
            int thick = 0;
            for (int i = 0; i < steps; i++)
            {
                double t = (i + 1) / (double)steps;
                int fi = Math.Min(flatN - 1, Math.Max(0, (int)Math.Ceiling(t * flatN) - 1));
                var f = cells[fi + 1];
                var (x, y) = HyperSea.CellCentre(f.q, f.r);
                double z = z0 + (z1 - z0) * t;
                double n = HyperSea.Density(x, y, z);
                double danger = HyperSea.Danger(n);
                course.Steps.Add(new HyperSeaStep(f.q, f.r, x, y, z, n, danger));
                max = Math.Max(max, danger);
                sum += danger;
                if (i > 0 && n > course.Steps[thick].Density && !IsBerth(f.q, f.r)) thick = i;
            }
            course.MaxDanger = max;
            course.AverageDanger = sum / steps;
            course.ThickestDensity = course.Steps[thick].Density;
            return course;
        }
    }
}
