using System;
using System.Collections.Generic;
using System.Globalization;

namespace CosmicShore.Data
{
    /// <summary>What a thing in the sky is.</summary>
    public enum SkyKind
    {
        /// <summary>A catalogue star (HYG, within 1,600 ly of Sol) [S].</summary>
        Star = 0,
        /// <summary>A star drawn from the galaxy model, the same every visit [M].</summary>
        ModelStar = 1,
        /// <summary>A model star near Tumult carrying a name Tumult's crews gave it [A].</summary>
        TumultStar = 2,
        /// <summary>A globular cluster in the halo (Harris) [S].</summary>
        Globular = 3,
        /// <summary>An open cluster (Hunt &amp; Reffert) [S].</summary>
        OpenCluster = 4,
        /// <summary>One of the board's places: a landmark with its own line.</summary>
        Landmark = 5,
    }

    /// <summary>One thing the deep zoom can draw: a star, a cluster or a landmark. Light years, the board's frame.</summary>
    public sealed class SkyObject
    {
        public SkyKind Kind;
        public double X, Y, Z;
        /// <summary>Absolute magnitude (stars and clusters).</summary>
        public float M;
        /// <summary>B-V colour index (stars).</summary>
        public float Ci;
        /// <summary>The zoom level (0 = the whole galaxy, 16 = 2 ly tiles) from which it is drawn.</summary>
        public float Level;
        /// <summary>A catalogue star's name rank, 0-3 (3 = a proper name everyone knows).</summary>
        public byte Rank;
        public byte R, G, B;
        /// <summary>Its name (may be empty), and a designation (HIP number, catalogue id, the crews' TC number).</summary>
        public string Name = string.Empty, Desig = string.Empty;
        /// <summary>A cluster's label on the map when it differs from its name ("47 Tuc" for "NGC 104 - 47 Tuc").</summary>
        public string Label = string.Empty;
        /// <summary>A cluster's members, its log10 age in years, its distance from Sol (ly).</summary>
        public int Members;
        public float LogAge, SolLy;
        /// <summary>A landmark's id, kind (the board's experience: Harbour, Port, Far...), region, source tag
        /// ([S] real, [A] proposed), line and sub-line.</summary>
        public string Id = string.Empty, KindId = string.Empty, Region = string.Empty, Tag = string.Empty,
            Line = string.Empty, Sub = string.Empty;

        public bool IsCluster => Kind == SkyKind.Globular || Kind == SkyKind.OpenCluster;
        public bool IsStar => Kind == SkyKind.Star || Kind == SkyKind.ModelStar || Kind == SkyKind.TumultStar;
        /// <summary>An open cluster known by a name of its own (the Pleiades, the Jewel Box), not only its id.</summary>
        public bool Famous => Kind == SkyKind.OpenCluster && Name != Desig;
    }

    /// <summary>
    /// The Voyage Board's deep zoom (Docs/Voyages/VOYAGE_BOARD.md §4a, <c>VoyageBoard/deepzoom/deepzoom.js</c>) ported line
    /// for line in doubles: the galaxy as a quadtree of tiles (level 0 the whole 140,000 ly square, level 16 tiles of
    /// about 2 ly), every star living in the one level its brightness earns, so a view that draws every level down to the
    /// one holding about 900 stars on screen is full at any zoom but never crowded. Real stars and clusters come from the
    /// catalogue (<see cref="HyperSeaSkyCatalog"/>); the rest of the sky is drawn from the board's galaxy model, seeded
    /// per tile so it never changes. Pure C#, no engine type. Goldens: <c>node Docs/Voyages/VoyageBoard/sky_golden.js</c>.
    /// </summary>
    public static class HyperSeaSky
    {
        public const double Root = 140000, X0 = -70000, Y0 = -70000;
        public const int LMax = 16;
        const double K = 200;
        /// <summary>Stars per square ly in the disk column at Sol [S, approx.].</summary>
        public const double SigSun = 9;
        static readonly double NA = SigSun * Root * Root;
        static readonly double Ln4 = Math.Log(4);

        /// <summary>The share of stars (brightest first) held by levels 0..L.</summary>
        public static double FL(double L) => L < 0 ? 0 : Math.Min(1, K * Math.Pow(4, L) / NA);

        public static int LevelOfF(double F) =>
            (int)Math.Max(0, Math.Min(LMax, Math.Ceiling(Math.Log(F * NA / K) / Ln4 - 1e-9)));

        // The luminosity function: log10 of the share of stars brighter than absolute magnitude M [model, after the
        // local census].
        static readonly double[,] LF =
        {
            { -10, -11 }, { -8, -9 }, { -6, -7.5 }, { -4.5, -6.5 }, { -2, -4 }, { 0, -3 }, { 2, -2 }, { 4, -1.52 },
            { 5, -1.22 }, { 8, -0.7 }, { 11, -0.35 }, { 14, -0.07 }, { 16.5, 0 },
        };

        public static double LogF(double M)
        {
            if (M <= LF[0, 0]) return LF[0, 1];
            for (int i = 1; i < LF.GetLength(0); i++)
                if (M <= LF[i, 0])
                {
                    double a = LF[i - 1, 0], fa = LF[i - 1, 1], b = LF[i, 0], fb = LF[i, 1];
                    return fa + (fb - fa) * (M - a) / (b - a);
                }
            return 0;
        }

        public static double Mof(double F)
        {
            double lf = Math.Log10(Math.Max(F, 1e-11));
            for (int i = 1; i < LF.GetLength(0); i++)
                if (lf <= LF[i, 1])
                {
                    double a = LF[i - 1, 0], fa = LF[i - 1, 1], b = LF[i, 0], fb = LF[i, 1];
                    return a + (b - a) * (lf - fa) / (fb - fa);
                }
            return 16.5;
        }

        public static double TileSize(int L) => Root / (1 << L);

        static readonly double ArmSol = HyperSea.ArmProfile(0, HyperSea.SolRadius, HyperSea.SolRadius, 1500);

        /// <summary>Stars per square ly through the whole disk column at (x, y), and the bulge's share: an exponential
        /// disk, the arms, the bulge and the bar.</summary>
        public static (double all, double bulge) Sigma(double x, double y)
        {
            double r = Math.Sqrt(x * x + y * y);
            double disk = SigSun * Math.Exp(-(Math.Max(r, 3000) - HyperSea.SolRadius) / 8500) *
                          (0.6 + 0.8 * HyperSea.ArmProfile(x, y, r, 1500)) / (0.6 + 0.8 * ArmSol);
            if (r > 45000) disk *= Math.Exp(-(r - 45000) / 2500);
            double q = r / 3000;
            double bulge = 220 * Math.Exp(-(q * q)) + 90 * HyperSea.Bar(x, y);
            return (disk + bulge, bulge);
        }

        public static readonly double SigMax = Sigma(0, 0).all * 1.25;

        // A star's colour from its B-V colour index.
        static readonly double[] BvAt = { -0.4, -0.2, 0, 0.3, 0.6, 0.9, 1.2, 1.6, 2.2 };
        static readonly byte[,] BvRgb =
        {
            { 155, 176, 255 }, { 170, 191, 255 }, { 202, 215, 255 }, { 248, 247, 255 }, { 255, 244, 234 },
            { 255, 226, 190 }, { 255, 210, 161 }, { 255, 190, 120 }, { 255, 170, 100 },
        };

        public static (byte r, byte g, byte b) BvColor(double ci)
        {
            double k = HyperSea.JsRound(ci * 10);
            double c = Math.Max(-0.4, Math.Min(2.2, k / 10));
            int n = BvAt.Length;
            for (int i = 1; i < n; i++)
                if (c <= BvAt[i])
                {
                    double a = BvAt[i - 1], b = BvAt[i], t = (c - a) / (b - a);
                    return ((byte)HyperSea.JsRound(BvRgb[i - 1, 0] + (BvRgb[i, 0] - BvRgb[i - 1, 0]) * t),
                        (byte)HyperSea.JsRound(BvRgb[i - 1, 1] + (BvRgb[i, 1] - BvRgb[i - 1, 1]) * t),
                        (byte)HyperSea.JsRound(BvRgb[i - 1, 2] + (BvRgb[i, 2] - BvRgb[i - 1, 2]) * t));
                }
            return (BvRgb[n - 1, 0], BvRgb[n - 1, 1], BvRgb[n - 1, 2]);
        }

        /// <summary>A star's spectral class from its colour index (O/B, B, A, F, G, K, M as 0-6).</summary>
        public static int ClassOf(double ci) =>
            ci < -0.25 ? 0 : ci < 0 ? 1 : ci < 0.3 ? 2 : ci < 0.58 ? 3 : ci < 0.81 ? 4 : ci < 1.4 ? 5 : 6;

        /// <summary>A star's light as a multiple of the Sun's.</summary>
        public static double Luminosity(double M) => Math.Pow(10, 0.4 * (4.83 - M));

        /// <summary>The board's mulberry32 random numbers, bit for bit.</summary>
        public struct Rng
        {
            int _seed;
            public Rng(int seed) => _seed = seed;

            public double Next()
            {
                unchecked
                {
                    _seed += 0x6D2B79F5;
                    int s = _seed;
                    int t = (s ^ (int)((uint)s >> 15)) * (1 | s);
                    t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                    return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
                }
            }
        }

        public static int Poisson(double lam, ref Rng R)
        {
            if (lam > 40)
            {
                double a = R.Next(), b = R.Next();
                return (int)Math.Max(0, HyperSea.JsRound(lam + Math.Sqrt(lam) * Math.Sqrt(-2 * Math.Log(a + 1e-12)) *
                    Math.Cos(6.2832 * b)));
            }
            int k = 0;
            double p = 1, lim = Math.Exp(-lam);
            do { k++; p *= R.Next(); } while (p > lim);
            return k - 1;
        }

        static double Expo(ref Rng R, double h)
        {
            double sign = R.Next() < .5 ? -1 : 1;
            return sign * -Math.Log(1 - R.Next() * .999) * h;
        }

        /// <summary>The board's key for tile (L, i, j).</summary>
        public static long Key(int L, int i, int j) => L * 10000000000L + i * 100000L + j;

        /// <summary>The seed of tile (L, i, j)'s stars.</summary>
        public static int TileSeed(int L, int i, int j)
        {
            unchecked
            {
                return ((L + 1) * (int)0x9E3779B1) ^ ((i + 7) * (int)0x85EBCA77) ^ ((j + 13) * (int)0xC2B2AE3D);
            }
        }

        /// <summary>Tumult's place on the board [A] (Docs/Voyages/VOYAGE_BOARD.md §1).</summary>
        public const double TumultX = 900, TumultY = 2100, TumultZ = 30;
        const double TumultReach = 2600;

        /// <summary>
        /// The model's own stars in tile (L, i, j), generated from its seed, the same every time (deepzoom.js
        /// <c>tileStars</c>). Near Sol the catalogue already holds every star bright enough to see, so the model only adds
        /// the faint ones; near Tumult the brightest carry invented names (<paramref name="names"/>; null leaves them
        /// unnamed).
        /// </summary>
        public static List<SkyObject> GenerateTile(int L, int i, int j, SkyNames names)
        {
            double s = TileSize(L), x0 = X0 + i * s, y0 = Y0 + j * s, cx = x0 + s / 2, cy = y0 + s / 2;
            var R = new Rng(TileSeed(L, i, j));
            double fl0 = FL(L - 1), dF = FL(L) - fl0;
            var t = new List<SkyObject>();
            if (dF <= 0) return t;
            bool flat = s < 600;                 // small tiles: the density is flat across them
            var sc = flat ? Sigma(cx, cy) : default;
            double smax = flat ? sc.all : SigMax;
            int n = Math.Min(7000, Poisson(smax * s * s * dF, ref R));
            bool nearTum = Hypot(cx - TumultX, cy - TumultY) < TumultReach + s;
            long key = Key(L, i, j);
            for (int q = 0; q < n; q++)
            {
                double x = x0 + R.Next() * s, y = y0 + R.Next() * s;
                var sg = flat ? sc : Sigma(x, y);
                if (!flat && R.Next() * smax > sg.all) continue;
                double F = fl0 + R.Next() * dF, M = Mof(F);
                bool inBulge = R.Next() * sg.all < sg.bulge;
                bool giant = M > -3.5 && M < 1.8 && R.Next() < .45;
                double z = inBulge ? Expo(ref R, 1400) : Expo(ref R, M < 0 ? 260 : M < 4 ? 600 : 950);
                double dx = x, dy = y - HyperSea.SolRadius, dz = z - HyperSea.SolHeight;
                double dS = Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (dS < 1600 && (dS < 30 || M + 5 * Math.Log10(dS / 32.6) < 7)) continue;
                if (Hypot(x - TumultX, y - TumultY) < 1.5) continue;   // Tumult's own system is the place itself
                double ci = giant ? 1.0 + R.Next() * .7
                    : M < -2 ? -0.3 + R.Next() * .1
                    : M < 1 ? -0.15 + (M + 2) * .1
                    : M < 4 ? 0.1 + (M - 1) * .15
                    : M < 8 ? 0.55 + (M - 4) * .2
                    : 1.4 + Math.Min(.5, (M - 8) * .08);
                var (cr, cg, cb) = BvColor(ci);
                double lvl = L - 1 + (F - fl0) / dF;
                var st = new SkyObject
                {
                    Kind = SkyKind.ModelStar, X = x, Y = y, Z = z, M = (float)M, Ci = (float)ci, Level = (float)lvl,
                    R = cr, G = cg, B = cb,
                };
                if (nearTum && Hypot(x - TumultX, y - TumultY) < TumultReach && lvl < 11.5)
                {
                    int nseed;
                    unchecked { nseed = (int)key ^ ((q + 1) * (int)2654435761u); }
                    var NR = new Rng(nseed);
                    st.Kind = SkyKind.TumultStar;
                    st.Name = names != null ? names.Invent(ref NR) : string.Empty;
                    st.Desig = "TC " + L.ToString(CultureInfo.InvariantCulture) + "-" + q.ToString(CultureInfo.InvariantCulture);
                    st.Level = (float)Math.Max(0, lvl - 2.5);
                }
                t.Add(st);
            }
            return t;
        }

        static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);

        // ── The view: how deep to draw, and the height slab ──────────────────

        /// <summary>The seven tiers, from the whole galaxy to one star system: the width a tier's button dives to and the
        /// least width it holds (light years). Their names and lines are authored (VoyageLines.MapTier*).</summary>
        public static readonly (double width, double min)[] Tiers =
        {
            (110000, 40000), (20000, 9000), (4500, 2200), (1200, 550), (280, 110), (50, 22), (10, 0),
        };

        /// <summary>The narrowest view, in light years.</summary>
        public const double MinWidth = 4;
        public const double MaxWidth = 140000;

        public static int TierOf(double w)
        {
            for (int i = 0; i < Tiers.Length; i++)
                if (w >= Tiers[i].min) return i;
            return Tiers.Length - 1;
        }

        /// <summary>How deep a view draws (<c>Lcut</c>), the height window, whether the deep tiers slice the disk, and
        /// the height they slice at.</summary>
        public readonly struct View
        {
            public readonly double Cx, Cy, W, Aspect, Lcut, Zwin, Zf;
            public readonly bool Slab;

            public View(double cx, double cy, double w, double aspect, double lcut, double zwin, bool slab, double zf)
            {
                Cx = cx; Cy = cy; W = w; Aspect = aspect; Lcut = lcut; Zwin = zwin; Slab = slab; Zf = zf;
            }
        }

        static readonly double[,] SigmaProbe = { { 0, 0 }, { -.25, -.2 }, { .25, -.2 }, { -.25, .2 }, { .25, .2 } };

        /// <summary>The view state of a view <paramref name="w"/> ly across centred on (cx, cy), <paramref name="aspect"/>
        /// its height over its width; the deep tiers slice at the height of the nearest of <paramref name="places"/>
        /// (deepzoom.js <c>viewState</c> and <c>focusZ</c>).</summary>
        public static View ViewOf(double cx, double cy, double w, double aspect, IReadOnlyList<SkyObject> places)
        {
            double zwin = Math.Max(30, w * 2.5);
            bool slab = zwin < 1200;
            double sg = 0;
            for (int k = 0; k < 5; k++) sg += Sigma(cx + SigmaProbe[k, 0] * w, cy + SigmaProbe[k, 1] * w * aspect).all;
            sg /= 5;
            double sEff = sg * (slab ? Math.Min(1, zwin / 1000) : 1);
            double A = w * w * aspect;
            double Fcut = Math.Min(1, 900 / Math.Max(1e-6, sEff * A));
            double Lcut = Math.Min(LMax + 1, Math.Max(0.3, Math.Log(Fcut * NA / K) / Ln4));
            return new View(cx, cy, w, aspect, Lcut, zwin, slab, slab ? FocusZ(cx, cy, w, places) : 0);
        }

        static double FocusZ(double cx, double cy, double w, IReadOnlyList<SkyObject> places)
        {
            SkyObject best = null;
            double bd = double.PositiveInfinity;
            if (places != null)
                foreach (var p in places)
                {
                    double d = Hypot(p.X - cx, p.Y - cy);
                    if (d < bd) { bd = d; best = p; }
                }
            return best != null && bd < Math.Max(3000, w * 2) ? best.Z : 0;
        }

        /// <summary>How much of the galaxy's look a view keeps: 1 from 30,000 ly in, falling to 0 at the whole galaxy.</summary>
        public static double GalaxyFade(double w) =>
            Math.Min(1, Math.Max(0, (Math.Log(110000) - Math.Log(w)) / (Math.Log(110000) - Math.Log(30000))));

        /// <summary>A star's opacity in a view, 0 when it is not drawn (deepzoom.js <c>draw</c>, the stars).</summary>
        public static double StarAlpha(SkyObject o, in View v)
        {
            double rel = v.Lcut - o.Level;
            if (rel < -1) return 0;
            double a = rel < 0 ? (1 + rel) * .7 : Math.Min(1, .62 + .14 * rel);
            if (v.Slab)
            {
                double dz = Math.Abs(o.Z - v.Zf) / v.Zwin;
                if (dz > 1.6) return 0;
                a *= Math.Exp(-dz * dz * 1.2);
            }
            a *= .35 + .65 * GalaxyFade(v.W);
            return a < .05 ? 0 : a;
        }

        /// <summary>A star's radius in pixels for its standing in the view.</summary>
        public static double StarRadius(double rel) => Math.Max(.45, Math.Min(3.6, .55 + .42 * rel));

        /// <summary>True when a drawn star shows its name (deepzoom.js: named, opaque enough and bright enough for its kind;
        /// at the System tier every catalogue star shows its designation).</summary>
        public static bool StarLabelled(SkyObject o, double rel, double a, bool systemTier)
        {
            bool named = o.Name.Length > 0 || (systemTier && o.Kind == SkyKind.Star && o.Desig.Length > 0);
            if (!named || a <= .35) return false;
            double need = o.Kind == SkyKind.Star ? (o.Rank == 3 ? 2 : 3.2) : 3.4;
            return rel > need || (systemTier && rel > .5);
        }

        /// <summary>A cluster's opacity in a view, 0 when it is not drawn (deepzoom.js <c>draw</c>, the clusters).</summary>
        public static double ClusterAlpha(SkyObject c, in View v)
        {
            double rel = v.Lcut - c.Level;
            if (rel < -0.6) return 0;
            double a = rel < 0 ? (1 + rel / .6) * .8 : Math.Min(1, .7 + .1 * rel);
            bool glob = c.Kind == SkyKind.Globular;
            if (v.Slab)
            {
                double dz = Math.Abs(c.Z - v.Zf) / Math.Max(v.Zwin, 200);
                if (dz > 2) return 0;
                a *= Math.Exp(-dz * dz * .6);
            }
            else if (glob && v.W < 30000)
            {
                // halo clusters far above or below the slice fade out
                double dz = Math.Abs(c.Z - HyperSea.SolHeight) / (v.W * 1.5);
                if (dz > 2) return 0;
                a *= Math.Exp(-dz * dz);
            }
            return a < .06 ? 0 : a;
        }

        /// <summary>A cluster's drawn radius in pixels at <paramref name="pixelsPerLy"/> (a drawing size, not a measured one).</summary>
        public static double ClusterRadius(SkyObject c, double pixelsPerLy)
        {
            bool glob = c.Kind == SkyKind.Globular;
            double physR = glob ? 60 : 8 + Math.Sqrt(c.Members > 0 ? c.Members : 50) * .5;
            return Math.Max(glob ? 3.2 : 2.6, Math.Min(glob ? 12 : 40, physR * pixelsPerLy));
        }

        public static bool ClusterLabelled(SkyObject c, double rel, double a) =>
            rel > (c.Famous ? 0.4 : 1.6) && a > .4;

        /// <summary>When a landmark shows at view width <paramref name="w"/> (the board's showPlace): far landmarks only
        /// beyond 3,500 ly, Tumult, Sol and the Heart always, the rest inside 16,000 ly.</summary>
        public static bool LandmarkShown(SkyObject p, double w)
        {
            if (p.Region == "far") return w > 3500;
            if (p.Id == "tumult" || p.Id == "sol" || p.Id == "heart") return true;
            return w < 16000;
        }
    }

    /// <summary>The syllables the brightest model stars near Tumult are named from [A] (the sky table's own, authored on
    /// the board).</summary>
    public sealed class SkyNames
    {
        public readonly List<string> First = new(), Second = new(), Suffix = new();

        public string Invent(ref HyperSeaSky.Rng R)
        {
            if (First.Count == 0 || Second.Count == 0) return string.Empty;
            string a = First[(int)Math.Floor(R.Next() * First.Count)], b = Second[(int)Math.Floor(R.Next() * Second.Count)];
            if (R.Next() < .3 && Suffix.Count > 0) return a + b + " " + Suffix[(int)Math.Floor(R.Next() * Suffix.Count)];
            return a + b;
        }
    }

    /// <summary>
    /// The sky table (<c>Assets/Resources/Voyages/HyperSeaSky.txt</c>, written from the board by
    /// <c>Docs/Voyages/VoyageBoard/export_sky.js</c>): the catalogue's stars and clusters, the board's landmarks, the
    /// names of the kinds of place and the syllables of Tumult's star names. Read a slice per frame
    /// (<see cref="Step"/>) so the 97,000 stars never stall a frame; usable for the model sky before it is done.
    /// </summary>
    public sealed class HyperSeaSkyCatalog
    {
        readonly string _text;
        int _at;
        string _section = string.Empty;

        /// <summary>The catalogue's stars, by the tile of the level they are drawn from (<see cref="HyperSeaSky.Key"/>).</summary>
        public readonly Dictionary<long, List<SkyObject>> Stars = new();
        public readonly List<SkyObject> Clusters = new();
        public readonly List<SkyObject> Landmarks = new();
        public readonly SkyNames Names = new();
        /// <summary>A kind of place's label (the board's experience id to what a reader sees).</summary>
        public readonly Dictionary<string, string> KindLabels = new();
        /// <summary>The catalogues' credits: stars, globs, ocs.</summary>
        public readonly Dictionary<string, string> Sources = new();
        public int StarCount { get; private set; }
        public bool Done => _at >= _text.Length;

        public HyperSeaSkyCatalog(string text) => _text = text ?? string.Empty;

        /// <summary>Read up to <paramref name="maxLines"/> more lines; true once the whole table is read.</summary>
        public bool Step(int maxLines)
        {
            var cols = new List<(int start, int length)>(12);
            for (int n = 0; n < maxLines && !Done; n++)
            {
                int end = _text.IndexOf('\n', _at);
                if (end < 0) end = _text.Length;
                int start = _at;
                _at = end + 1;
                int len = end - start;
                if (len > 0 && _text[start + len - 1] == '\r') len--;
                if (len == 0 || _text[start] == '#') continue;
                Split(start, len, cols);
                if (_text[start] == '@')
                {
                    Directive(cols);
                    continue;
                }
                try
                {
                    Row(cols);
                }
                catch (FormatException)
                {
                    // A malformed row is skipped; export_sky.js --check is the gate that keeps the table whole.
                }
            }
            return Done;
        }

        void Split(int start, int len, List<(int, int)> cols)
        {
            cols.Clear();
            int c = start, end = start + len;
            while (c <= end)
            {
                int tab = _text.IndexOf('\t', c, end - c);
                if (tab < 0) tab = end;
                cols.Add((c, tab - c));
                c = tab + 1;
            }
        }

        string Str(List<(int start, int length)> cols, int i) =>
            i < cols.Count ? _text.Substring(cols[i].start, cols[i].length) : string.Empty;

        double Num(List<(int start, int length)> cols, int i) =>
            i < cols.Count && cols[i].length > 0
                ? double.Parse(_text.AsSpan(cols[i].start, cols[i].length), NumberStyles.Float, CultureInfo.InvariantCulture)
                : 0;

        void Directive(List<(int start, int length)> cols)
        {
            string d = Str(cols, 0);
            switch (d)
            {
                case "@src": Sources[Str(cols, 1)] = Str(cols, 2); break;
                case "@names1": for (int i = 1; i < cols.Count; i++) Names.First.Add(Str(cols, i)); break;
                case "@names2": for (int i = 1; i < cols.Count; i++) Names.Second.Add(Str(cols, i)); break;
                case "@names3": for (int i = 1; i < cols.Count; i++) Names.Suffix.Add(Str(cols, i)); break;
                case "@kind": KindLabels[Str(cols, 1)] = Str(cols, 2); break;
                default: _section = d; break;
            }
        }

        void Row(List<(int start, int length)> cols)
        {
            switch (_section)
            {
                case "@landmarks":
                    Landmarks.Add(new SkyObject
                    {
                        Kind = SkyKind.Landmark, Id = Str(cols, 0), Name = Str(cols, 1), KindId = Str(cols, 2),
                        Region = Str(cols, 3), Tag = Str(cols, 4), X = Num(cols, 5), Y = Num(cols, 6), Z = Num(cols, 7),
                        Line = Str(cols, 8), Sub = Str(cols, 9),
                    });
                    break;
                case "@stars":
                {
                    double ci = Num(cols, 4);
                    var (r, g, b) = HyperSeaSky.BvColor(ci);
                    var s = new SkyObject
                    {
                        Kind = SkyKind.Star, X = Num(cols, 0), Y = Num(cols, 1), Z = Num(cols, 2), M = (float)Num(cols, 3),
                        Ci = (float)ci, Rank = (byte)Num(cols, 5), Level = (float)Num(cols, 6), Name = Str(cols, 7),
                        Desig = Str(cols, 8), R = r, G = g, B = b,
                    };
                    int L = (int)Math.Max(0, Math.Min(HyperSeaSky.LMax, Math.Ceiling(s.Level)));
                    double size = HyperSeaSky.TileSize(L);
                    long key = HyperSeaSky.Key(L, (int)Math.Floor((s.X - HyperSeaSky.X0) / size),
                        (int)Math.Floor((s.Y - HyperSeaSky.Y0) / size));
                    if (!Stars.TryGetValue(key, out var list)) Stars[key] = list = new List<SkyObject>();
                    list.Add(s);
                    StarCount++;
                    break;
                }
                case "@clusters":
                {
                    bool glob = Num(cols, 4) > 0;
                    Clusters.Add(new SkyObject
                    {
                        Kind = glob ? SkyKind.Globular : SkyKind.OpenCluster, X = Num(cols, 0), Y = Num(cols, 1),
                        Z = Num(cols, 2), M = (float)Num(cols, 3), Level = (float)Num(cols, 5), Name = Str(cols, 6),
                        Label = Str(cols, 7), Desig = Str(cols, 8), Members = (int)Num(cols, 9),
                        LogAge = (float)Num(cols, 10), SolLy = (float)Num(cols, 11),
                    });
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The sky a view shows (deepzoom.js <c>each</c> and <c>draw</c>): the catalogue's stars and the model's, tile by tile
    /// from the whole galaxy down to the view's depth, the clusters, all with their opacity. Generated tiles are kept
    /// (the 600 most recent) and made a few per call, so a long dive never stalls a frame: <see cref="Pending"/> asks
    /// for another call.
    /// </summary>
    public sealed class HyperSeaSkyField
    {
        const int Budget = 3;
        const int KeepTiles = 600, DropTiles = 150;
        const int MaxTilesPerLevel = 400;

        readonly Dictionary<long, List<SkyObject>> _gen = new();
        readonly List<long> _order = new();
        int _budget;

        public HyperSeaSkyCatalog Catalog;
        /// <summary>True when the last <see cref="Collect"/> left tiles to make (call again next frame).</summary>
        public bool Pending { get; private set; }

        /// <summary>One thing drawn: what it is, its opacity and its standing in the view (Lcut minus its level).</summary>
        public struct Mark
        {
            public SkyObject Object;
            public float Alpha, Rel;
        }

        /// <summary>
        /// Every star and cluster <paramref name="v"/> draws inside the world box (x0, y0)-(x1, y1), into
        /// <paramref name="stars"/> and <paramref name="clusters"/> (cleared first).
        /// </summary>
        public void Collect(in HyperSeaSky.View v, double x0, double y0, double x1, double y1, List<Mark> stars,
            List<Mark> clusters)
        {
            stars.Clear();
            clusters.Clear();
            _budget = Budget;
            Pending = false;
            int top = Math.Min(HyperSeaSky.LMax, (int)Math.Ceiling(v.Lcut) + 1);
            for (int L = 0; L <= top; L++)
            {
                double s = HyperSeaSky.TileSize(L);
                int n = (1 << L) - 1;
                int i0 = Math.Max(0, (int)Math.Floor((x0 - HyperSeaSky.X0) / s)), i1 = Math.Min(n, (int)Math.Floor((x1 - HyperSeaSky.X0) / s));
                int j0 = Math.Max(0, (int)Math.Floor((y0 - HyperSeaSky.Y0) / s)), j1 = Math.Min(n, (int)Math.Floor((y1 - HyperSeaSky.Y0) / s));
                if ((long)(i1 - i0 + 1) * (j1 - j0 + 1) > MaxTilesPerLevel) continue;
                for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (Catalog != null && Catalog.Stars.TryGetValue(HyperSeaSky.Key(L, i, j), out var real))
                        AddStars(real, v, stars);
                    AddStars(Tile(L, i, j), v, stars);
                }
            }
            if (Catalog == null) return;
            foreach (var c in Catalog.Clusters)
            {
                if (c.X < x0 || c.X > x1 || c.Y < y0 || c.Y > y1) continue;
                double a = HyperSeaSky.ClusterAlpha(c, v);
                if (a > 0) clusters.Add(new Mark { Object = c, Alpha = (float)a, Rel = (float)(v.Lcut - c.Level) });
            }
        }

        static void AddStars(List<SkyObject> list, in HyperSeaSky.View v, List<Mark> into)
        {
            if (list == null) return;
            foreach (var o in list)
            {
                double a = HyperSeaSky.StarAlpha(o, v);
                if (a > 0) into.Add(new Mark { Object = o, Alpha = (float)a, Rel = (float)(v.Lcut - o.Level) });
            }
        }

        /// <summary>Forget every generated tile (once the catalogue's names arrive, Tumult's stars are made again with them).</summary>
        public void Clear()
        {
            _gen.Clear();
            _order.Clear();
        }

        List<SkyObject> Tile(int L, int i, int j)
        {
            long k = HyperSeaSky.Key(L, i, j);
            if (_gen.TryGetValue(k, out var t)) return t;
            if (_budget <= 0)
            {
                Pending = true;
                return null;
            }
            _budget--;
            t = HyperSeaSky.GenerateTile(L, i, j, Catalog?.Names);
            _gen[k] = t;
            _order.Add(k);
            if (_order.Count > KeepTiles)
            {
                for (int q = 0; q < DropTiles; q++) _gen.Remove(_order[q]);
                _order.RemoveRange(0, DropTiles);
            }
            return t;
        }
    }
}
