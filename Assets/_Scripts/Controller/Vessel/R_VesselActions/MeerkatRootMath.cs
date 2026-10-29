using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's Omnicrystal ability, the ROOTS (<c>R_VesselActions/MEERKAT.md</c> §7), as arithmetic: the Vessel
    /// Studio's <c>rng</c>, <c>labLayout</c>, <c>chanPath</c>, <c>labBuild</c>, <c>polyAt</c>, <c>polyFrame</c>,
    /// <c>chanRadius</c>, <c>chanNearest</c>, <c>stepTunnel</c> (a channel's life) and <c>labTransit</c> (the free flight
    /// inside, rounds 20-21) from <c>Docs/Studios/VesselStudio/meerkat.html</c>, line for line.
    ///
    /// <para><b>Doubles, on purpose.</b> The layout is a seeded random tree whose branches turn on comparisons
    /// (a weighted pick, a sort along an axis); the page runs in doubles, so a float port would grow a different tree from
    /// the same seed. Everything here is double (<see cref="D3"/>) and converts at the edge; the tests hold it to the page's
    /// own numbers (<c>Tests/Editor/MeerkatRootMathTests.cs</c>). The same doubles make every peer grow the same roots from
    /// the same published seed and frame.</para>
    /// </summary>
    public static class MeerkatRootMath
    {
        /// <summary>Samples along a channel's centreline (the page's <c>NS</c>).</summary>
        public const int NS = 26;
        /// <summary>A hull is this far inside a wall when it touches it (the page's <c>HULL_R</c>).</summary>
        public const double HullR = 2.5;
        /// <summary>A channel fades this long once it has collapsed (the page's <c>FADE_S</c>).</summary>
        public const double FadeSeconds = 0.7;

        // ------------------------------------------------------------------ a double vector

        /// <summary>A double 3-vector: the page's THREE.Vector3, in the page's precision.</summary>
        public readonly struct D3
        {
            public readonly double X, Y, Z;
            public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
            public static readonly D3 Zero = new(0, 0, 0);
            public static D3 operator +(D3 a, D3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static D3 operator -(D3 a, D3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static D3 operator -(D3 a) => new(-a.X, -a.Y, -a.Z);
            public static D3 operator *(D3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
            public static D3 operator *(double k, D3 a) => new(a.X * k, a.Y * k, a.Z * k);
            public double Dot(D3 b) => X * b.X + Y * b.Y + Z * b.Z;
            public D3 Cross(D3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
            public double LengthSq => X * X + Y * Y + Z * Z;
            public double Length => Math.Sqrt(LengthSq);
            /// <summary>THREE's <c>normalize</c>: divided by the length (or by 1 when it is 0).</summary>
            public D3 Normalized { get { double l = Length; return this * (1.0 / (l == 0 ? 1 : l)); } }
            public double DistanceTo(D3 b) => (this - b).Length;
            public double DistanceToSq(D3 b) => (this - b).LengthSq;
            public static D3 Lerp(D3 a, D3 b, double k) => new(a.X + (b.X - a.X) * k, a.Y + (b.Y - a.Y) * k, a.Z + (b.Z - a.Z) * k);
            public override string ToString() => $"({X:F6}, {Y:F6}, {Z:F6})";
        }

        static double Clamp01(double x) => Math.Max(0, Math.Min(1, x));
        static double Smooth(double a, double b, double x) { double t = Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        /// <summary>The page's <c>easeOutC</c>.</summary>
        public static double EaseOutC(double x) => 1 - Math.Pow(1 - Clamp01(x), 3);
        /// <summary>The page's <c>iclamp</c> (JavaScript's Math.round: halves go up, never to even).</summary>
        static int IClamp(double v, int lo, int hi) => Math.Max(lo, Math.Min(hi, (int)Math.Floor(v + 0.5)));

        // ------------------------------------------------------------------ the page's rng

        /// <summary>The page's <c>rng(seed)</c> (a mulberry32), bit for bit: the same seed, the same numbers.</summary>
        public sealed class Rng
        {
            uint _a;
            public Rng(uint seed) { _a = seed; }
            public double Next()
            {
                unchecked
                {
                    _a += 0x6D2B79F5u;
                    uint t = _a;
                    t = (uint)((int)(t ^ (t >> 15)) * (int)(t | 1));
                    t ^= (uint)((int)t + (int)(t ^ (t >> 7)) * (int)(t | 61));
                    return (t ^ (t >> 14)) / 4294967296.0;
                }
            }
        }

        // ------------------------------------------------------------------ the layout (labLayout)

        /// <summary>The page's labyrinth keys the layout reads.</summary>
        public struct LayoutSettings
        {
            public double BranchPointsMin, BranchPointsMax, OpeningsMin, OpeningsMax, RadiusMin, RadiusMax, Spread, Randomness;
        }

        public sealed class Node
        {
            public int Parent;
            public readonly List<int> Kids = new();
            public int Depth;
            public List<D3> Dirs;
            public D3 Dir, P, T;
            public double Pd;
        }

        public sealed class Edge
        {
            public int Ai, Bi;          // -1: the source
            public D3 Pa, Ta, Pb, Tb;   // its ends and their headings, in the hull's frame (x right, y up, z ahead)
            public List<int> Kids = new();
            public double Za, Zb;       // path depth from the source at its ends
        }

        public sealed class Layout
        {
            public double D, Rad, Size;
            public int Forks, Openings;
            public List<Node> Nodes;
            public List<Edge> Edges;
        }

        /// <summary>
        /// The page's <c>labLayout</c>: the roots in the hull's own frame, from a seed. <paramref name="size"/> is the last
        /// trunk's share of the biggest (0..1); <paramref name="atOnce"/> grows them every way from the hull (round 17).
        /// </summary>
        public static Layout BuildLayout(in LayoutSettings p, uint seed, double size, bool atOnce) =>
            Lay(p, seed, Clamp01(size), atOnce, false, 0, D3.Zero);

        /// <summary>
        /// Round 26, the page's <c>labLayout</c> with <c>net</c> (<c>networkSize</c>): the NETWORK in the hull's frame, its
        /// reach <paramref name="rad"/> (u), its centre <paramref name="centre"/> (the hull's frame, from the source), its
        /// forks and openings at <paramref name="counts"/> of the way from the Min to the Max numbers, its forks placed for
        /// transport (<see cref="NetPlace"/>).
        /// </summary>
        public static Layout BuildNetwork(in LayoutSettings p, uint seed, double rad, D3 centre, double counts) =>
            Lay(p, seed, Clamp01(counts), true, true, rad, centre);

        /// <summary>
        /// The page's <c>networkSize</c> without its Mass and cell lookups: a network of <paramref name="share"/> × the cell's
        /// radius <paramref name="cellR"/> (never under <c>RadiusMin</c>), centred <paramref name="share"/> of the way from
        /// its source <paramref name="a"/> to the cell's centre <paramref name="o"/>; <paramref name="counts"/> is where its
        /// reach lies between <c>RadiusMin</c> and <c>RadiusMax</c>. The centre is returned in the world.
        /// </summary>
        public static void NetworkSize(in LayoutSettings p, double share, double cellR, D3 a, D3 o, out double rad, out D3 centre,
            out double counts)
        {
            rad = Math.Max(p.RadiusMin, share * cellR);
            centre = a + (o - a) * Clamp01(share);
            counts = Clamp01((rad - p.RadiusMin) / Math.Max(1, p.RadiusMax - p.RadiusMin));
        }

        /// <summary>
        /// The page's round-26 <c>netPlace</c> (after Garrett's Omnicrystal Network Lab: every path near the straight line,
        /// the forks early): every opening on the network's sphere, every fork on the line from the fork before it (the
        /// source, for the first) toward the middle of its openings, a short step while they spread wide, a long one once they
        /// lie together. No randomness, so every peer lays the same forks.
        /// </summary>
        static void NetPlace(List<Node> nodes, D3 C, double Rad)
        {
            foreach (var n in nodes) if (n.Kids.Count == 0) n.P = C + n.Dir * Rad;
            void Ends(int i, List<D3> o) { var n = nodes[i]; if (n.Kids.Count == 0) o.Add(n.P); else foreach (int kid in n.Kids) Ends(kid, o); }
            void Place(int i, D3 from)
            {
                var n = nodes[i];
                if (n.Kids.Count == 0) return;
                var E = new List<D3>();
                Ends(i, E);
                var g = D3.Zero;
                foreach (var e in E) g += e;
                g = g * (1.0 / E.Count) - from;
                var dir = g.LengthSq > 1e-6 ? g.Normalized : new D3(0, 0, 1);
                double near = double.PositiveInfinity, cosMin = 1;
                foreach (var e in E)
                {
                    var v = e - from;
                    double l = v.Length;
                    near = Math.Min(near, l);
                    if (l > 1e-6) cosMin = Math.Min(cosMin, v.Dot(dir) / l);
                }
                n.P = from + dir * (near * (0.1 + 0.45 * Clamp01(cosMin)));
                foreach (int kid in n.Kids) Place(kid, n.P);
            }
            Place(0, D3.Zero);
        }

        static Layout Lay(in LayoutSettings p, uint seed, double k, bool atOnce, bool net, double netRad, D3 netC)
        {
            double LerpK(double a, double z) => a + (z - a) * k;
            var R = new Rng(seed);
            double Rad = Math.Max(20, net ? netRad : LerpK(p.RadiusMin, p.RadiusMax));
            int B = IClamp(LerpK(p.BranchPointsMin, p.BranchPointsMax), 1, 24);
            int M = Math.Max(B + 1, IClamp(LerpK(p.OpeningsMin, p.OpeningsMax), 2, 72));
            double half = Math.Min(180, Math.Max(10, p.Spread / 2)) * Math.PI / 180, rnd = Clamp01(p.Randomness);
            var nodes = new List<Node> { new Node { Parent = -1, Depth = 0 } };
            void Add(int par) { nodes.Add(new Node { Parent = par, Depth = nodes[par].Depth + 1 }); nodes[par].Kids.Add(nodes.Count - 1); }
            int forks = 0;
            void Grow(int i) { Add(i); Add(i); forks++; }
            Grow(0);
            while (forks < B)
            {
                var lv = new List<int>();
                for (int i = 0; i < nodes.Count; i++) if (nodes[i].Kids.Count == 0) lv.Add(i);
                var w = lv.Select(i => 1 / (1 + nodes[i].Depth * (1.5 - rnd))).ToList();
                double x = R.Next() * w.Sum();
                int kk = 0;
                while (kk < lv.Count - 1 && (x -= w[kk]) > 0) kk++;
                Grow(lv[kk]);
            }
            int leaves = forks + 1;
            while (leaves < M)
            {
                var fk = new List<int>();
                for (int i = 0; i < nodes.Count; i++) if (nodes[i].Kids.Count > 0 && nodes[i].Kids.Count < 4) fk.Add(i);
                if (fk.Count == 0) break;
                Add(fk[(int)Math.Floor(R.Next() * fk.Count)]);
                leaves++;
            }
            // the openings' directions round the centre
            var dirs = new List<D3>();
            double ga = Math.PI * (3 - Math.Sqrt(5)), c0 = Math.Cos(half);
            for (int i = 0; i < leaves; i++)
            {
                double ct = 1 - (1 - c0) * (i + 0.5 + (R.Next() - 0.5) * rnd) / leaves, st = Math.Sqrt(Math.Max(0, 1 - ct * ct));
                double ph = i * ga + (R.Next() - 0.5) * rnd * 1.2;
                dirs.Add(new D3(st * Math.Cos(ph), st * Math.Sin(ph), ct).Normalized);
            }
            int NLeaves(int i) => nodes[i].Kids.Count > 0 ? nodes[i].Kids.Sum(NLeaves) : 1;
            void Assign(int i, List<D3> pts)
            {
                var n = nodes[i];
                n.Dirs = pts;
                if (n.Kids.Count == 0) { n.Dir = pts[0]; return; }
                var mean = D3.Zero;
                foreach (var q in pts) mean += q;
                mean *= 1.0 / pts.Count;
                var ax = new D3(1, 0.3, 0.2).Normalized;   // the long axis of the points (power iteration on their spread)
                for (int it = 0; it < 8; it++)
                {
                    var nx = D3.Zero;
                    foreach (var q in pts) { var d = q - mean; nx += d * d.Dot(ax); }
                    if (nx.LengthSq < 1e-12) break;
                    ax = nx.Normalized;
                }
                var sorted = pts.OrderBy(q => q.Dot(ax)).ToList();   // stable, as the page's sort
                int at = 0;
                foreach (int kid in n.Kids) { int c = NLeaves(kid); Assign(kid, sorted.GetRange(at, c)); at += c; }
            }
            Assign(0, dirs);
            int maxD = nodes.Max(n => n.Depth);
            var C = net ? netC : new D3(0, 0, Rad * (atOnce ? 0.2 : 0.8));   // at once: spreading every way from where you are
            if (net) NetPlace(nodes, C, Rad);
            else foreach (var n in nodes)
            {
                var mean = D3.Zero;
                foreach (var q in n.Dirs) mean += q;
                var m = mean.Length > 1e-6 ? mean.Normalized : new D3(0, 0, 1);
                if (n.Kids.Count == 0) { n.P = C + n.Dir * Rad; continue; }
                if (n.Depth == 0) { n.P = new D3(0, 0, Rad * (atOnce ? 0.15 : 0.3)); continue; }   // the first fork, soon after the source
                double f = 0.15 + 0.6 * n.Depth / (maxD + 1);
                double jx = R.Next() - 0.5, jy = R.Next() - 0.5, jz = R.Next() - 0.5;
                n.P = C + m * (Rad * f) + new D3(jx, jy, jz) * (Rad * 0.3 * rnd);
            }
            var srcP = D3.Zero;
            D3 Tan(int i)
            {
                var n = nodes[i];
                if (n.Kids.Count == 0) return n.Dir;
                var m = D3.Zero;
                foreach (int kid in n.Kids) m += nodes[kid].P;
                m *= 1.0 / n.Kids.Count;
                var from = n.Parent < 0 ? srcP : nodes[n.Parent].P;
                var t = m - from;
                return t.LengthSq > 1e-6 ? t.Normalized : new D3(0, 0, 1);
            }
            for (int i = 0; i < nodes.Count; i++) nodes[i].T = Tan(i);
            // edges: the source to the first fork, then every fork to each of its channels; path depths from the source
            var edges = new List<Edge> { new Edge { Ai = -1, Bi = 0, Pa = srcP, Ta = new D3(0, 0, 1), Pb = nodes[0].P, Tb = nodes[0].T } };
            nodes[0].Pd = srcP.DistanceTo(nodes[0].P);
            void Walk(int i)
            {
                foreach (int kid in nodes[i].Kids)
                {
                    nodes[kid].Pd = nodes[i].Pd + nodes[i].P.DistanceTo(nodes[kid].P);
                    edges.Add(new Edge { Ai = i, Bi = kid, Pa = nodes[i].P, Ta = nodes[i].T, Pb = nodes[kid].P, Tb = nodes[kid].T });
                    Walk(kid);
                }
            }
            Walk(0);
            foreach (var e in edges)
            {
                for (int j = 0; j < edges.Count; j++) if (edges[j].Ai == e.Bi) e.Kids.Add(j);
                e.Za = e.Ai < 0 ? 0 : nodes[e.Ai].Pd;
                e.Zb = nodes[e.Bi].Pd;
            }
            return new Layout { D = edges.Max(e => e.Zb), Nodes = nodes, Edges = edges, Forks = forks, Openings = leaves, Rad = Rad, Size = k };
        }

        // ------------------------------------------------------------------ a channel's centreline (chanPath)

        /// <summary>A channel's sampled centreline: <see cref="NS"/> points evenly by length, their headings, and a frame
        /// carried along it with no twist.</summary>
        public sealed class Poly
        {
            public double L;
            public D3[] P = new D3[NS], T = new D3[NS], N = new D3[NS], B = new D3[NS];
        }

        /// <summary>The page's <c>chanPath</c>: a Hermite curve from a (heading ta) to b (heading tb), wandering by
        /// <paramref name="gnarl"/> (round 17: a root), sampled evenly by length.</summary>
        public static Poly ChanPath(D3 pa, D3 ta, D3 pb, D3 tb, double gnarl, double gseed)
        {
            double d = pa.DistanceTo(pb);
            var raw = new D3[49];
            var len = new double[49];
            var ch = (pb - pa).Normalized;
            var u1 = ch.Cross(Math.Abs(ch.Y) < 0.9 ? new D3(0, 1, 0) : new D3(1, 0, 0)).Normalized;
            var u2 = ch.Cross(u1);
            double ga = gnarl * d * 0.12;
            for (int i = 0; i <= 48; i++)
            {
                double t = i / 48.0, t2 = t * t, t3 = t2 * t;
                var q = pa * (2 * t3 - 3 * t2 + 1) + ta * (d * (t3 - 2 * t2 + t)) + pb * (-2 * t3 + 3 * t2) + tb * (d * (t3 - t2));
                if (ga > 0)
                {
                    double env = Math.Sin(Math.PI * t);
                    q = q + u1 * (ga * env * Math.Sin(t * 9.4 + gseed)) + u2 * (ga * env * Math.Sin(t * 6.1 + gseed * 1.7));   // a root wanders
                }
                raw[i] = q;
                if (i > 0) len[i] = len[i - 1] + raw[i].DistanceTo(raw[i - 1]);
            }
            var path = new Poly { L = len[48] };
            for (int kk = 0, j = 0; kk < NS; kk++)
            {
                double want = path.L * kk / (NS - 1);
                while (j < 47 && len[j + 1] < want) j++;
                double f = (want - len[j]) / Math.Max(1e-6, len[j + 1] - len[j]);
                path.P[kk] = D3.Lerp(raw[j], raw[j + 1], Clamp01(f));
            }
            for (int kk = 0; kk < NS; kk++) path.T[kk] = (path.P[Math.Min(NS - 1, kk + 1)] - path.P[Math.Max(0, kk - 1)]).Normalized;
            for (int kk = 0; kk < NS; kk++)
            {
                var n = kk > 0 ? path.N[kk - 1] : (Math.Abs(path.T[0].Y) < 0.9 ? new D3(0, 1, 0) : new D3(1, 0, 0));
                n = (n - path.T[kk] * n.Dot(path.T[kk])).Normalized;
                path.N[kk] = n;
                path.B[kk] = path.T[kk].Cross(n);
            }
            return path;
        }

        // ------------------------------------------------------------------ the roots in the world (labBuild)

        /// <summary>The page's keys a channel's size reads.</summary>
        public struct ChannelSettings
        {
            public double Diameter, MouthDiameter, Gnarl, Tip;
        }

        /// <summary>
        /// A tube the flight can slide that is not a sampled root (round 24, the page's <c>slAt</c> / <c>slNear</c> /
        /// <c>slR</c>): the Meerkat's main tunnel, its centreline the exact conic arc and its wall the hyperboloid. Past either
        /// end, straight on along the end heading.
        /// </summary>
        public interface ITubePath
        {
            D3 At(double s, out D3 tangent);
            double Nearest(D3 p);
            double Radius(double s);
        }

        /// <summary>One root (the page's channel <c>wh</c> with <c>chan</c> and <c>root</c> set), or, with
        /// <see cref="Tube"/> set, the main tunnel as a network of one (round 24: "one single, one a network").</summary>
        public sealed class Channel
        {
            public int Index;
            public Poly Path;
            public ITubePath Tube;
            public double L, B, M, Za, Zb, D, Seed;
            public bool MouthIn, MouthOut;
            public int Parent = -1;
            public List<int> Kids = new();
            public D3 A => Tube != null ? Tube.At(0, out _) : Path.P[0];
            public D3 T0 { get { if (Tube == null) return Path.T[0]; Tube.At(0, out var t); return t; } }
        }

        /// <summary>The main tunnel as a network of one channel (round 24): no mouths of its own to flare, no kids, no parent.</summary>
        public static List<Channel> Single(ITubePath tube, double L) => new() { new Channel { Index = 0, Tube = tube, L = L } };

        /// <summary>The point s along any channel and its heading: a root's sampled path, or a tube's own.</summary>
        public static D3 ChanAt(Channel c, double s, out D3 tangent) => c.Tube != null ? c.Tube.At(s, out tangent) : At(c.Path, s, out tangent);

        /// <summary>How far along any channel the point nearest p lies (past an end, on along its end heading).</summary>
        public static double ChanNearest(Channel c, D3 p) => c.Tube != null ? c.Tube.Nearest(p) : Nearest(c.Path, p);

        /// <summary>
        /// The page's <c>labBuild</c>: the layout in the world - its source at <paramref name="a"/>, its axis along
        /// <paramref name="t0"/>, <paramref name="right"/> and <paramref name="up"/> the hull's - each edge a channel.
        /// Parents are set as <c>labCommit</c> sets them (round 20: you can fly back up a root).
        /// </summary>
        public static List<Channel> Build(Layout lay, in ChannelSettings p, D3 a, D3 t0, D3 right, D3 up)
        {
            D3 W(D3 v) => a + right * v.X + up * v.Y + t0 * v.Z;
            D3 Wd(D3 v) => (right * v.X + up * v.Y + t0 * v.Z).Normalized;
            double b = Math.Max(0.5, 0.5 * p.Diameter), M = Math.Max(b, 0.5 * p.MouthDiameter);
            var list = new List<Channel>(lay.Edges.Count);
            for (int i = 0; i < lay.Edges.Count; i++)
            {
                var e = lay.Edges[i];
                var path = ChanPath(W(e.Pa), Wd(e.Ta), W(e.Pb), Wd(e.Tb), e.Ai < 0 ? 0 : p.Gnarl, i * 2.17 + 0.4);
                list.Add(new Channel
                {
                    Index = i, Path = path, L = path.L, B = b, M = M, Za = e.Za, Zb = e.Zb, D = lay.D, Seed = i * 7.31 + 1.3,
                    MouthIn = e.Ai < 0, MouthOut = e.Kids.Count == 0, Kids = new List<int>(e.Kids),
                });
            }
            foreach (var c in list) foreach (int kid in c.Kids) list[kid].Parent = c.Index;
            return list;
        }

        /// <summary>The page's <c>polyAt</c>: the point s along a channel, and its heading (past its ends: straight on).</summary>
        public static D3 At(Poly pp, double s, out D3 tangent)
        {
            int n = NS - 1;
            double f = s / Math.Max(1e-6, pp.L) * n;
            if (f <= 0) { tangent = pp.T[0]; return pp.P[0] + pp.T[0] * s; }
            if (f >= n) { tangent = pp.T[n]; return pp.P[n] + pp.T[n] * (s - pp.L); }
            int i = (int)Math.Floor(f);
            double k = f - i;
            tangent = D3.Lerp(pp.T[i], pp.T[i + 1], k).Normalized;
            return D3.Lerp(pp.P[i], pp.P[i + 1], k);
        }

        /// <summary>The page's <c>polyFrame</c>: the carried normal and binormal at s.</summary>
        public static void Frame(Poly pp, double s, out D3 normal, out D3 binormal)
        {
            int n = NS - 1;
            double f = Math.Max(0, Math.Min(n, s / Math.Max(1e-6, pp.L) * n));
            int i = Math.Min(n - 1, (int)Math.Floor(f));
            double k = f - i;
            normal = D3.Lerp(pp.N[i], pp.N[i + 1], k).Normalized;
            binormal = D3.Lerp(pp.B[i], pp.B[i + 1], k).Normalized;
        }

        /// <summary>The page's <c>chanRadius</c>: the channel's radius at s - tapering to <c>rootTip</c> × toward the tips
        /// (round 17), flaring to the mouth over 3 mouth radii at the source and at an opening.</summary>
        public static double Radius(Channel c, double s, double rootTip)
        {
            if (c.Tube != null) return c.Tube.Radius(s);
            double fl = Math.Min(0.35 * c.L, 3 * c.M);
            double tip = 1 - (1 - Clamp01(rootTip)) * Clamp01((c.Za + (c.Zb - c.Za) * s / c.L) / Math.Max(1, c.D));
            double r = c.B * tip;
            if (c.MouthIn) r += (c.M - c.B) * (1 - Smooth(0, fl, s));
            if (c.MouthOut) r += (c.M * tip - r) * (1 - Smooth(0, fl, c.L - s));
            return r;
        }

        /// <summary>The page's <c>chanNearest</c>: how far along a channel the point nearest p lies (past an end, on along
        /// its end heading, so s &lt; 0 or s &gt; L).</summary>
        public static double Nearest(Poly pp, D3 p)
        {
            int n = NS - 1;
            double seg = pp.L / n, bs = 0, bd = double.PositiveInfinity;
            for (int i = 0; i < n; i++)
            {
                var ab = pp.P[i + 1] - pp.P[i];
                double l2 = ab.LengthSq;
                double k = l2 > 1e-9 ? Math.Max(0, Math.Min(1, (p - pp.P[i]).Dot(ab) / l2)) : 0;
                double d = (pp.P[i] + ab * k).DistanceToSq(p);
                if (d < bd) { bd = d; bs = (i + k) * seg; }
            }
            if (bs <= 1e-6) { double a = (p - pp.P[0]).Dot(pp.T[0]); if (a < 0) bs = a; }
            if (bs >= pp.L - 1e-6) { double a = (p - pp.P[n]).Dot(pp.T[n]); if (a > 0) bs = pp.L + a; }
            return bs;
        }

        // ------------------------------------------------------------------ a channel's life (stepTunnel, round 16)

        public enum Phase { Form = 0, Open = 1, Stay = 2, Collapse = 3, Gone = 4 }

        /// <summary>A channel's dug window [S0, S1] and its life.</summary>
        public struct Life
        {
            public Phase Phase;
            public double S0, S1, Grow, Wait, FormT, Stay, DieT;
            public bool Out, Dying;
        }

        /// <summary>The page's <c>labCommit</c> start: each channel waits until the dig's front (over
        /// <c>labyrinthFormSeconds</c>) reaches its start, then digs out to its end.</summary>
        public static Life NewLife(Channel c, double formSeconds)
        {
            double T = Math.Max(0.05, formSeconds);
            return new Life { Phase = Phase.Form, Wait = T * c.Za / c.D, FormT = Math.Max(0.05, T * (c.Zb - c.Za) / c.D) };
        }

        /// <summary>The page's <c>stepTunnel</c> for a channel: dig out from its start, stay <c>staySeconds</c> once its
        /// rider is out, collapse to its middle over <c>collapseSeconds</c>, then fade (<see cref="FadeSeconds"/>). False
        /// once it is gone.</summary>
        public static bool Step(ref Life w, double L, double staySeconds, double collapseSeconds, double dt)
        {
            if (w.Dying) { w.DieT += dt; if (w.DieT >= FadeSeconds) w.Phase = Phase.Gone; return w.Phase != Phase.Gone; }
            switch (w.Phase)
            {
                case Phase.Form:
                    if (w.Wait > 0) { w.Wait -= dt; w.S0 = w.S1 = 0; }
                    else
                    {
                        w.Grow = Math.Min(1, w.Grow + dt / Math.Max(0.05, w.FormT));
                        w.S0 = 0;
                        w.S1 = L * EaseOutC(w.Grow);
                        if (w.Grow >= 1) { w.S1 = L; w.Phase = w.Out ? Phase.Stay : Phase.Open; }
                    }
                    break;
                case Phase.Stay:
                    w.Stay += dt;
                    if (w.Stay >= Math.Max(0, staySeconds)) w.Phase = Phase.Collapse;
                    break;
                case Phase.Collapse:
                    double rate = L / Math.Max(0.05, collapseSeconds);
                    w.S0 += 0.5 * rate * dt;
                    w.S1 -= 0.5 * rate * dt;
                    if (w.S1 - w.S0 <= 2)
                    {
                        double mid = 0.5 * (w.S0 + w.S1);
                        w.S0 = w.S1 = mid;
                        w.Dying = true;
                        w.DieT = 0;
                    }
                    break;
            }
            return true;
        }

        /// <summary>The rider is out (or the roots are replaced): open channels start their stay.</summary>
        public static void RiderOut(ref Life w) { w.Out = true; if (w.Phase == Phase.Open) w.Phase = Phase.Stay; }

        /// <summary>The page's collapse-at-once (a new network, or a new tunnel, replaces this one).</summary>
        public static void CollapseNow(ref Life w)
        {
            if (w.Dying || w.Phase == Phase.Collapse) return;
            w.Phase = Phase.Collapse;
            w.Grow = 1;
        }

        // ------------------------------------------------------------------ the free flight inside (labTransit)

        /// <summary>The page's round 20-21 keys the flight reads.</summary>
        public struct FlightSettings
        {
            public double Cruise, Thrust, Move, Drag, MaxSpeed, Bounce, Gravity, Tip, ExitMultiplier;
        }

        /// <summary>A rider's state inside the roots (the page's <c>ftLive.gate</c> with <c>net</c> set).</summary>
        public sealed class Flight
        {
            public int Chan, Prev = -1;
            public double S, VIn, Dist;
            public bool Free;
            public D3 Pos, H, Vel, Upv, Side, Dr, NetUp;
            public bool HasEr;
            public D3 Er;
            public int Forks, Bumps;
        }

        public enum FlightResult { Riding = 0, OutOfTip = 1, OutOfSource = 2 }

        static readonly List<int> s_cand = new();

        /// <summary>
        /// A rider entering the roots at <paramref name="pos"/> going <paramref name="vIn"/> (the page's
        /// <c>labCommit</c>'s gate): in the source channel, <c>s</c> = minus its distance to the source's mouth (0 when the
        /// roots grew at the hull).
        /// </summary>
        public static Flight Enter(IReadOnlyList<Channel> net, int entry, D3 pos, double vIn, D3 netUp) => new()
        {
            Chan = entry, S = -pos.DistanceTo(net[entry].A), VIn = Math.Max(1, vIn), Pos = pos, NetUp = netUp,
        };

        /// <summary>
        /// One step of the page's <c>labTransit</c> (rounds 20-21): you cruise along the root at <c>rootCruise</c> (easing
        /// to it), RT (<paramref name="throttle"/>, 0..1) pushes you faster by <c>rootThrust</c> up to <c>rootMaxSpeed</c>;
        /// the left stick (<paramref name="side"/> right +, <paramref name="rise"/> up +) moves you round the root by
        /// <c>rootMove</c>, <c>rootGravity</c> draws you to its floor, <c>rootDrag</c> calms your drift across it; the
        /// walls bounce you (<c>rootBounce</c>); past a fork you are in the root you moved into, back past a root's start in
        /// its parent. <paramref name="alive"/> says which channels still stand.
        /// </summary>
        public static FlightResult Step(Flight g, IReadOnlyList<Channel> net, Func<int, bool> alive, in FlightSettings p,
            double throttle, double side, double rise, double dt, out D3 nose)
        {
            var c = net[g.Chan];
            double vMax = Math.Max(5, p.MaxSpeed);
            nose = g.Free ? g.H : c.T0;
            if (!g.Free && g.S < 0)
            {
                // the dive to the source's mouth (roots dug ahead of you)
                g.S += g.VIn * dt;
                g.Dist += g.VIn * dt;
                g.Pos = c.A + c.T0 * Math.Min(0, g.S);
                g.Vel = c.T0 * g.VIn;
                if (g.S < 0) return FlightResult.Riding;
            }
            if (!g.Free)
            {
                // in: you carry your speed in (up to rootMaxSpeed), along the root
                g.S = Math.Max(0, ChanNearest(c, g.Pos));
                ChanAt(c, Math.Min(g.S, c.L), out var t0);
                g.Free = true;
                g.H = t0;
                g.Vel = t0 * Math.Min(g.VIn, vMax);
                g.Upv = g.NetUp;
                g.Side = D3.Zero;
                g.Dr = D3.Zero;
                g.HasEr = false;
                g.Bumps = 0;
            }
            double th = throttle, mv = Math.Max(0, p.Move);
            double sc0 = Math.Max(0, Math.Min(c.L, g.S)), r0 = Radius(c, sc0, p.Tip);
            ChanAt(c, sc0, out var rnT);
            // the nose follows the root, the way you face along it; up is the network's up, square to the nose
            var want = rnT * (g.H.Dot(rnT) < 0 ? -1 : 1);
            g.H = D3.Lerp(g.H, want, 1 - Math.Exp(-6 * dt)).Normalized;
            var upSq = g.NetUp - g.H * g.NetUp.Dot(g.H);
            if (upSq.LengthSq > 0.06) g.Upv = D3.Lerp(g.Upv, upSq.Normalized, 1 - Math.Exp(-3 * dt));
            g.Upv = g.Upv - g.H * g.Upv.Dot(g.H);
            if (g.Upv.LengthSq < 1e-6) g.Upv = g.H.Cross(Math.Abs(g.H.X) < 0.9 ? new D3(1, 0, 0) : new D3(0, 1, 0));
            g.Upv = g.Upv.Normalized;
            g.Side = g.H.Cross(g.Upv).Normalized;
            // along the root you cruise (RT faster); across it the left stick, gravity to the floor, and the drag
            double vf = g.Vel.Dot(g.H);
            var lat = g.Vel - g.H * vf;
            if (th > 0.02) vf = Math.Min(vMax, vf + Math.Max(0, p.Thrust) * th * dt);
            else vf += (Math.Min(vMax, Math.Max(0, p.Cruise)) - vf) * (1 - Math.Exp(-1.5 * dt));
            var acc = g.Side * (mv * side) + g.Upv * (mv * rise);
            var floor = g.NetUp - rnT * g.NetUp.Dot(rnT);
            acc = acc + floor * -Math.Max(0, p.Gravity);
            lat = (lat + acc * dt) * Math.Exp(-Math.Max(0, p.Drag) * dt);
            g.Vel = g.H * vf + lat;
            double sp0 = g.Vel.Length;
            if (sp0 > vMax) g.Vel = g.Vel * (vMax / sp0);
            g.Pos = g.Pos + g.Vel * dt;
            // which root you are in now: this one; past its end, one of its kids; back past its start, its parent or a sibling
            int best = g.Chan;
            double bestS = Score(c, g.Pos, p.Tip, out double bestQ);
            var cand = s_cand;   // one rider steps at a time, on the main thread: no list a frame
            cand.Clear();
            if (g.S > c.L - 2.5 * r0) cand.AddRange(c.Kids);
            if (g.S < 2.5 * r0 && c.Parent >= 0)
            {
                cand.Add(c.Parent);
                foreach (int k in net[c.Parent].Kids) if (k != g.Chan) cand.Add(k);
            }
            foreach (int i in cand)
            {
                if (alive != null && !alive(i)) continue;
                double s = Score(net[i], g.Pos, p.Tip, out double q);
                if (q < bestQ) { bestQ = q; best = i; bestS = s; }
            }
            if (best != g.Chan)
            {
                if (c.Kids.Contains(best) && c.Kids.Count > 1) g.Forks++;
                g.Prev = g.Chan;
                g.Chan = best;
                c = net[best];
            }
            g.S = bestS;
            double sp = g.Vel.Length;
            g.Dist += sp * dt;
            if (g.S >= c.L && c.Kids.Count == 0) { ChanAt(c, c.L, out nose); return FlightResult.OutOfTip; }   // out of a tip, on along it
            if (g.S <= -HullR && c.Parent < 0) { nose = -c.T0; return FlightResult.OutOfSource; }              // back out of the source
            // the wall: you bounce off it
            double sc = Math.Max(0, Math.Min(c.L, g.S));
            var cp = ChanAt(c, sc, out var tn);
            var dr = g.Pos - cp;
            dr = dr - tn * dr.Dot(tn);
            double rIn = Math.Max(1, Radius(c, sc, p.Tip) - HullR), off = dr.Length;
            if (off > rIn)
            {
                var n = dr * (1 / off);
                g.Pos = g.Pos + n * (rIn - off);
                double vn = g.Vel.Dot(n);
                if (vn > 0) { g.Vel = g.Vel - n * ((1 + Clamp01(p.Bounce)) * vn); if (vn > 4) g.Bumps++; }   // a bump, not a graze
                g.Dr = n * rIn;
            }
            else g.Dr = dr;
            g.HasEr = off > 0.2 * rIn;   // your side of the root (the root you would take at the next fork)
            if (g.HasEr) g.Er = g.Dr.Normalized;
            nose = g.H;
            return FlightResult.Riding;
        }

        /// <summary>The page's <c>rootScore</c>: how far along the channel p is, and how deep inside it (its distance off the
        /// centreline as a share of the radius there; under 1, inside).</summary>
        static double Score(Channel c, D3 p, double tip, out double q)
        {
            double s = ChanNearest(c, p), sc = Math.Max(0, Math.Min(c.L, s));
            var cp = ChanAt(c, sc, out _);
            q = cp.DistanceTo(p) / Math.Max(1, Radius(c, sc, tip));
            return s;
        }

        /// <summary>The page's <c>forkPick</c>: the kid leaving on the side of the root the rider is on, else the straightest
        /// (-1 with no fork ahead).</summary>
        public static int ForkPick(Flight g, IReadOnlyList<Channel> net)
        {
            var c = net[g.Chan];
            if (c.Kids.Count == 0) return -1;
            if (c.Kids.Count < 2) return c.Kids[0];
            var fp = ChanAt(c, c.L, out var ft);
            int best = c.Kids[0];
            double bs = double.NegativeInfinity;
            foreach (int k in c.Kids)
            {
                var q = ForkSide(net[k], fp, ft);
                double s = g.HasEr ? (q.LengthSq > 1e-6 ? q.Normalized.Dot(g.Er) : -1) : -q.Length;
                if (s > bs) { bs = s; best = k; }
            }
            return best;
        }

        /// <summary>The page's <c>forkSide</c>: where a kid heads off, across its parent's end heading.</summary>
        public static D3 ForkSide(Channel kid, D3 fp, D3 ft)
        {
            var q = ChanAt(kid, Math.Min(kid.L, 30), out _) - fp;
            return q - ft * q.Dot(ft);
        }

        /// <summary>The page's <c>netReach</c>: how near a target a channel can take you (its nearest tip).</summary>
        public static double Reach(IReadOnlyList<Channel> net, int i, D3 target)
        {
            var c = net[i];
            if (c.Kids.Count == 0) return ChanAt(c, c.L, out _).DistanceTo(target);
            double m = double.PositiveInfinity;
            foreach (int k in c.Kids) m = Math.Min(m, Reach(net, k, target));
            return m;
        }

        /// <summary>
        /// The page's AI in the roots (round 21, input only): the push across the root that a spring toward
        /// <paramref name="wantAcross"/> (the middle, or the side of the fork it picked) asks for, as left-stick (side, rise)
        /// in -1..1 at <c>rootMove</c>.
        /// </summary>
        public static void AiStick(Flight g, D3 wantAcross, double move, out double side, out double rise)
        {
            var latV = g.Vel - g.H * g.Vel.Dot(g.H);
            var cmd = (wantAcross - g.Dr) * 2.25 + latV * -2.1;
            double mv = Math.Max(1, move);
            side = Math.Max(-1, Math.Min(1, cmd.Dot(g.Side) / mv));
            rise = Math.Max(-1, Math.Min(1, cmd.Dot(g.Upv) / mv));
        }
    }
}
