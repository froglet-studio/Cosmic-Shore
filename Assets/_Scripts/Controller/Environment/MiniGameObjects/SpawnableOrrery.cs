using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "Orrery" - the celestial-clock cell environment (~34k prisms): flying INSIDE an
    /// astronomical machine. A porous golden sun-shell (super-shielded core ring) sits at the
    /// centre of seven tilted orbit rings, each carrying a phyllotaxis-shelled planet (outer
    /// ones with moons; the fourth ringed like Saturn); armature spokes reach from the sun to
    /// every planet; a zodiac band of twelve polygon constellation-gates circles the rim; gear
    /// rosettes, swinging pendulum chains, a star-mote firmament, and one eccentric comet
    /// whose spark tail is the machine's only danger. Brass gold + steel blue; ruby jewel
    /// bearings (shielded) anchor each ring. The airiest cell - all lines and voids.
    ///
    /// <para>Every prism is laid FLUSH (NO PRISM MAY INTERPENETRATE ANOTHER, Docs/ECOSYSTEM.md
    /// §49.10), at authoring time and with the same prisms in the same places: the shells' panes
    /// and the chains' links are sized from their own spacing (<see cref="FlushPane"/>,
    /// <see cref="FlushArc"/>), and where one structure threads another - a rail through its
    /// planet, a spoke into it, the zodiac band through its gates, a gear's rim through its own
    /// teeth - the links at the crossing shorten (<see cref="FitLink"/>) or, where a centre sits
    /// inside the other prism, both part (<see cref="SplitShare"/>). Two centres that coincided
    /// (ring 0's bearing on its planet's core, the hoop link a pendulum hangs from) are the only
    /// prisms moved, by the smallest step that parts them.</para>
    /// </summary>
    public class SpawnableOrrery : CellEnvironmentSpawnableBase
    {
        protected override int DefaultSeed => 31;
        protected override int BuildParameterHash() => System.HashCode.Combine(nameof(SpawnableOrrery), 3);

        /// <summary>The Orrery lays every prism flush (NO PRISM MAY INTERPENETRATE ANOTHER,
        /// Docs/ECOSYSTEM.md §49.10), and in a few places flush is thinner than the prism prefab's
        /// 0.5 floor: the comet's spark tail packs 140 sparks into ~35 units, and a rail link, a
        /// zodiac strand or a gear link is cut short where it threads a planet, a gate or a rim.
        /// The prefab would grow those back into their neighbours, so this world admits its
        /// authored sizes (every other size it lays is inside the window already, so nothing else
        /// changes).</summary>
        protected override bool AdmitsAuthoredPrismScale => true;

        static readonly (float R, float tiltDeg, Domains dom)[] RingDefs =
        {
            (95f, 8f, Domains.Blue), (148f, -12f, Domains.Gold), (200f, 22f, Domains.Blue),
            (252f, -8f, Domains.Jade), (304f, 15f, Domains.Blue), (356f, -18f, Domains.Gold),
            (408f, 26f, Domains.Jade),
        };

        /// <summary>Half the diagonal of a rail strand's 1.2 x 1 cross-section - the furthest it
        /// reaches off its centreline, whatever its roll (FlushArc).</summary>
        static readonly float RailHalfDiag = 0.5f * Mathf.Sqrt(1.2f * 1.2f + 1f * 1f);

        static readonly Domains[] PlanetDoms =
        {
            Domains.Ruby, Domains.Jade, Domains.Gold, Domains.Blue, Domains.Jade, Domains.Ruby, Domains.Gold,
        };

        protected override void BuildEnvironment()
        {
            FitSpokesThroughGears();
            BuildSun();
            BuildRingsAndPlanets();
            BuildZodiac();
            BuildComet();
            BuildGears();
            BuildPendulums();
            BuildSuspensionHoop();
            BuildStars();
        }

        /// <summary>Phyllotaxis point on the unit sphere.</summary>
        static Vector3 SpherePoint(int i, int n)
        {
            float u = (i + 0.5f) / n;
            float y = 1f - 2f * u;
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = i * GoldenAngle;
            return new Vector3(r * Mathf.Cos(a), y, r * Mathf.Sin(a));
        }

        /// <summary>Pane tangent to a sphere at direction d (thin axis along the normal).</summary>
        static Quaternion ShellRot(Vector3 d, int i)
        {
            float a = i * GoldenAngle;
            return SpawnPoint.LookRotation(d, new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)));
        }

        /// <summary>
        /// The FLUSH footprint of a pane in an <paramref name="n"/>-point phyllotaxis shell of
        /// radius <paramref name="r"/>: <paramref name="pack"/> x the shell's mean point spacing
        /// sqrt(4 pi r^2 / n). Every shell here used to lay panes 1.5-3x that spacing, so each
        /// pane passed through five or six of its neighbours (NO PRISM MAY INTERPENETRATE ANOTHER,
        /// Docs/ECOSYSTEM.md §49.10). <paramref name="pack"/> is the largest lattice factor at
        /// which the shell is flush - the panes are square and turned to their own latitude, so
        /// the binding pairs are the i +/- 21/34/55 parastichy neighbours where the spiral's
        /// lattice is most rhombic, and a small shell's stronger curvature tips its pane edges
        /// into each other sooner (measured by Tools/Build/audit_prism_clipping.py: ~0.66 on the
        /// sun and the planets, ~0.63 on a moon or a bob).
        /// </summary>
        static float FlushPane(float r, int n, float pack) => pack * r * Mathf.Sqrt(4f * Mathf.PI / n);

        /// <summary>
        /// The FLUSH length of one of <paramref name="n"/> prisms chained round a circle of radius
        /// <paramref name="r"/>, each laid along its tangent with a cross-section that reaches at
        /// most <paramref name="halfReach"/> off the centreline: the inner corners meet first, so
        /// the length is the chord at the inner reach, 2 (r - halfReach) tan(pi / n), a hair
        /// under contact.
        /// </summary>
        static float FlushArc(float r, int n, float halfReach) =>
            2f * (r - halfReach) * Mathf.Tan(Mathf.PI / n) * Flush;

        /// <summary>A hair under exact contact, so float noise never turns a touching pair into a
        /// clip.</summary>
        const float Flush = 0.995f;

        /// <summary>Half the thickest planet pane (0.7 jittered up to +20%) - how far a shell's
        /// panes reach either side of its radius.</summary>
        const float PlanetPaneHalfThick = 0.42f;

        /// <summary>The shortest half-length a link is cut to on its own (the 0.5 the prism prefab
        /// used to floor every axis at). A link the crossing would cut shorter than this, at any
        /// width, has its centre IN the structure it crosses: it and the prisms it sits among part
        /// by <see cref="SplitShare"/> instead (<see cref="FitLink"/>).</summary>
        const float MinLinkHalf = 0.25f;

        /// <summary>
        /// Something a chain threads through and must not enter: a SHELL (nothing between radii
        /// <c>In</c> and <c>Out</c> about <c>C</c> - a phyllotaxis shell's panes, their thickness
        /// and their corners' bulge) or a BOX (an oriented box: a cube, one prism, or a whole bar
        /// of flush links treated as one).
        /// </summary>
        readonly struct Keepout
        {
            public readonly bool IsShell;
            public readonly Vector3 C, X, Y, Z, H;   // H = box half extents; shells use H.x/H.y as In/Out

            Keepout(bool shell, Vector3 c, Vector3 x, Vector3 y, Vector3 z, Vector3 h)
            { IsShell = shell; C = c; X = x; Y = y; Z = z; H = h; }

            public static Keepout Shell(Vector3 c, float rIn, float rOut) =>
                new(true, c, Vector3.right, Vector3.up, Vector3.forward, new Vector3(rIn, rOut, 0f));

            public static Keepout Cube(Vector3 c, float half) => Block(c, new Vector3(half, half, half));

            public static Keepout Block(Vector3 c, Vector3 half) =>
                new(false, c, Vector3.right, Vector3.up, Vector3.forward, half);

            public static Keepout Bar(Vector3 c, Quaternion rot, float hx, float hy, float hz) =>
                new(false, c, rot * Vector3.right, rot * Vector3.up, rot * Vector3.forward, new Vector3(hx, hy, hz));

            /// <summary>One prism exactly as it is laid (full sizes, not halves).</summary>
            public static Keepout Prism(Vector3 c, Quaternion rot, Vector3 size) =>
                Bar(c, rot, 0.5f * size.x, 0.5f * size.y, 0.5f * size.z);

            public Vector3 Axis(int k) => k == 0 ? X : k == 1 ? Y : Z;
            public float Half(int k) => k == 0 ? H.x : k == 1 ? H.y : H.z;

            /// <summary>This box's reach along <paramref name="axis"/> from its centre.</summary>
            public float Extent(Vector3 axis) => Project(axis);

            float Reach => IsShell ? H.y : H.magnitude;

            /// <summary>The longest half-length a link centred at <paramref name="b"/> (axes x/y/z,
            /// cross-section half hx/hy) can have and stay clear; negative when even a sliver on
            /// that centre would not. Exact for a shell (the link's farthest corner inside In, or its
            /// nearest point outside Out); for a box, the best of the separating axes that are the
            /// box's own and the link's cross-section axes.</summary>
            public float ClearHalf(Vector3 b, Vector3 x, Vector3 y, Vector3 z, float hx, float hy)
            {
                var c = b - C;
                if (c.sqrMagnitude > (Reach + hx + hy + 8f) * (Reach + hx + hy + 8f)) return float.MaxValue;   // out of any link's reach
                if (IsShell)
                {
                    float best = -1f;
                    // inside: |c + t z|^2 + K <= In^2 at both ends
                    float zc = Vector3.Dot(z, c);
                    float k = 2f * (hx * Mathf.Abs(Vector3.Dot(x, c)) + hy * Mathf.Abs(Vector3.Dot(y, c))) + hx * hx + hy * hy;
                    float disc = zc * zc - (c.sqrMagnitude + k - H.x * H.x);
                    if (disc >= 0f)
                    {
                        float r = Mathf.Sqrt(disc), lo = -zc - r, hi = -zc + r;
                        if (lo <= 0f && hi >= 0f) best = Mathf.Min(-lo, hi);
                    }
                    // outside: the nearest point of the link at least Out from the centre
                    float lx = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(x, c)) - hx);
                    float ly = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(y, c)) - hy);
                    float lat = lx * lx + ly * ly;
                    if (lat >= H.y * H.y) return float.MaxValue;
                    float s = Mathf.Sqrt(H.y * H.y - lat);
                    best = Mathf.Max(best, Mathf.Abs(zc) - s);
                    return best;
                }
                float most = -1f;
                // the link's own cross-section axes: independent of its length
                if (Mathf.Abs(Vector3.Dot(c, x)) >= hx + Project(x)) return float.MaxValue;
                if (Mathf.Abs(Vector3.Dot(c, y)) >= hy + Project(y)) return float.MaxValue;
                for (int a = 0; a < 3; a++)
                {
                    var e = a == 0 ? X : a == 1 ? Y : Z;
                    float he = a == 0 ? H.x : a == 1 ? H.y : H.z;
                    float room = Mathf.Abs(Vector3.Dot(c, e)) - he - hx * Mathf.Abs(Vector3.Dot(x, e)) - hy * Mathf.Abs(Vector3.Dot(y, e));
                    if (room < 0f) continue;
                    float ze = Mathf.Abs(Vector3.Dot(z, e));
                    if (ze < 1e-6f) return float.MaxValue;
                    most = Mathf.Max(most, room / ze);
                }
                return most;
            }

            float Project(Vector3 axis) =>
                H.x * Mathf.Abs(Vector3.Dot(X, axis)) + H.y * Mathf.Abs(Vector3.Dot(Y, axis)) + H.z * Mathf.Abs(Vector3.Dot(Z, axis));

            /// <summary>The longest stretch [lo, hi] of [-cell, cell] (offsets along z from b) a link
            /// can fill and stay clear - for a link whose own centre is unusable.</summary>
            public void LongestClear(Vector3 b, Vector3 x, Vector3 y, Vector3 z, float hx, float hy, float cell,
                ref float bestLo, ref float bestHi)
            {
                var c = b - C;
                if (IsShell)
                {
                    float zc = Vector3.Dot(z, c);
                    float k = 2f * (hx * Mathf.Abs(Vector3.Dot(x, c)) + hy * Mathf.Abs(Vector3.Dot(y, c))) + hx * hx + hy * hy;
                    float disc = zc * zc - (c.sqrMagnitude + k - H.x * H.x);
                    if (disc >= 0f) Take(-zc - Mathf.Sqrt(disc), -zc + Mathf.Sqrt(disc), cell, ref bestLo, ref bestHi);
                    float lx = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(x, c)) - hx);
                    float ly = Mathf.Max(0f, Mathf.Abs(Vector3.Dot(y, c)) - hy);
                    float lat = lx * lx + ly * ly;
                    if (lat >= H.y * H.y) { Take(-cell, cell, cell, ref bestLo, ref bestHi); return; }
                    float s = Mathf.Sqrt(H.y * H.y - lat);
                    Take(-cell, -zc - s, cell, ref bestLo, ref bestHi);
                    Take(-zc + s, cell, cell, ref bestLo, ref bestHi);
                    return;
                }
                for (int a = 0; a < 3; a++)
                {
                    var e = a == 0 ? X : a == 1 ? Y : Z;
                    float he = a == 0 ? H.x : a == 1 ? H.y : H.z;
                    float m = he + hx * Mathf.Abs(Vector3.Dot(x, e)) + hy * Mathf.Abs(Vector3.Dot(y, e));
                    float ce = Vector3.Dot(c, e), ze = Vector3.Dot(z, e);
                    if (Mathf.Abs(ze) < 1e-6f)
                    {
                        if (Mathf.Abs(ce) >= m) Take(-cell, cell, cell, ref bestLo, ref bestHi);
                        continue;
                    }
                    // |ce + t ze| < m is the blocked interval of t
                    float t0 = (-m - ce) / ze, t1 = (m - ce) / ze;
                    if (t0 > t1) (t0, t1) = (t1, t0);
                    Take(-cell, t0, cell, ref bestLo, ref bestHi);
                    Take(t1, cell, cell, ref bestLo, ref bestHi);
                }
            }

            static void Take(float lo, float hi, float cell, ref float bestLo, ref float bestHi)
            {
                lo = Mathf.Max(lo, -cell); hi = Mathf.Min(hi, cell);
                if (hi - lo > bestHi - bestLo) { bestLo = lo; bestHi = hi; }
            }
        }

        /// <summary>
        /// Size one link of a chain (laid along its local z, cross-section half <paramref name="hx"/>
        /// x <paramref name="hy"/>, never longer than <paramref name="half0"/> either side of its
        /// centre) where the chain threads something: the link keeps its centre and shortens to
        /// the clearance the keep-outs leave it (a hair under contact). Where that would leave a
        /// sliver (shorter than <see cref="MinLinkHalf"/>) - a link grazing a small shell, or
        /// skimming a cube's edge - it is also narrowed, by the step of <see cref="LinkNarrowing"/>
        /// that keeps it the most volume (<paramref name="narrow"/>). A link with no usable size
        /// at any width has its centre IN what it threads: it returns that keep-out's index, sized
        /// against the others only, and the caller parts it from the prisms it actually sits among
        /// (<see cref="SplitShare"/>) - both give up the same fraction, nobody moves. A few dot
        /// products per link; nothing is resolved after the lay.
        /// </summary>
        static int FitLink(Vector3 at, Quaternion rot, float hx, float hy, float half0,
            Keepout[] keep, int count, out float half, out float narrow)
        {
            Vector3 x = rot * Vector3.right, y = rot * Vector3.up, z = rot * Vector3.forward;
            half = half0; narrow = 1f;
            if (count == 0) return -1;

            float bestVol = -1f;
            for (int w = 0; w < LinkNarrowing.Length; w++)
            {
                float f = LinkNarrowing[w], h = half0;
                for (int k = 0; k < count; k++)
                    h = Mathf.Min(h, keep[k].ClearHalf(at, x, y, z, hx * f, hy * f) * Flush);
                if (h < MinLinkHalf && !(w == 0 && h >= half0)) continue;
                if (f * f * h > bestVol) { bestVol = f * f * h; half = h; narrow = f; }
                if (h >= half0) break;   // the whole link fits: no narrower width can do better
            }
            if (bestVol > 0f) return -1;

            int worst = 0;
            float worstH = float.MaxValue;
            for (int k = 0; k < count; k++)
            {
                float h = keep[k].ClearHalf(at, x, y, z, hx, hy);
                if (h < worstH) { worstH = h; worst = k; }
            }
            half = half0; narrow = 1f;
            for (int k = 0; k < count; k++)
                if (k != worst) half = Mathf.Min(half, keep[k].ClearHalf(at, x, y, z, hx, hy) * Flush);
            return worst;
        }

        /// <summary>The cross-section factors <see cref="FitLink"/> tries, widest first.</summary>
        static readonly float[] LinkNarrowing = { 1f, 0.75f, 0.5f, 0.35f, 0.25f };

        /// <summary>
        /// For a link whose centre COINCIDES with a prism it threads (one centre on another, which
        /// no size can part): slide it the smallest way, along its own line and within its own
        /// cell (<paramref name="cell"/> either side) so no neighbour is touched, onto the longest
        /// stretch clear of <paramref name="obstacle"/>.
        /// </summary>
        static void SlideLink(ref Vector3 at, Quaternion rot, float hx, float hy, float half0, float cell,
            in Keepout obstacle, out float half)
        {
            Vector3 x = rot * Vector3.right, y = rot * Vector3.up, z = rot * Vector3.forward;
            float lo = 0f, hi = 0f;
            obstacle.LongestClear(at, x, y, z, hx, hy, cell, ref lo, ref hi);
            at += z * (0.5f * (lo + hi));
            half = Mathf.Min(half0, 0.5f * (hi - lo) * Flush);
        }

        /// <summary>
        /// Two prisms that cross with neither able to step aside (each centre sits in the other's
        /// structure) part by sharing the cost: on the face axis that separates them best, each
        /// gives up the same fraction of its one half-extent that reaches furthest along it, and
        /// keeps every other size. Returns the factor for each of <paramref name="self"/>'s axes
        /// (1 = untouched). Symmetric - the pair is taken in a fixed order, so each prism computes
        /// the other's half of the same split - and per axis, so a prism crossing several takes
        /// the smallest factor per axis and stays clear of all of them. Where no one axis can do
        /// it, both shrink uniformly to the pair's flush scale.
        /// </summary>
        static Vector3 SplitShare(in Keepout self, in Keepout other)
        {
            if (Apart(self, other)) return Vector3.one;
            bool swap = other.C.x < self.C.x || (other.C.x == self.C.x &&
                (other.C.y < self.C.y || (other.C.y == self.C.y && other.C.z < self.C.z)));
            Keepout a = swap ? other : self, b = swap ? self : other;
            var d = b.C - a.C;
            float best = -1f;
            int bestA = 0, bestB = 0;
            for (int l = 0; l < 6; l++)
            {
                var axis = l < 3 ? a.Axis(l) : b.Axis(l - 3);
                float ca = -1f, cb = -1f, sa = 0f, sb = 0f;
                int ia = 0, ib = 0;
                for (int k = 0; k < 3; k++)
                {
                    float c1 = a.Half(k) * Mathf.Abs(Vector3.Dot(a.Axis(k), axis));
                    float c2 = b.Half(k) * Mathf.Abs(Vector3.Dot(b.Axis(k), axis));
                    sa += c1; sb += c2;
                    if (c1 > ca) { ca = c1; ia = k; }
                    if (c2 > cb) { cb = c2; ib = k; }
                }
                float budget = Mathf.Abs(Vector3.Dot(d, axis)) - (sa - ca) - (sb - cb);
                if (budget <= 0f || ca + cb <= 0f) continue;
                float f = budget / (ca + cb);
                if (f > best) { best = f; bestA = ia; bestB = ib; }
            }
            var share = Vector3.one;
            if (best > 0f)
            {
                share[swap ? bestB : bestA] = Mathf.Min(1f, best) * Flush;
                return share;
            }
            float u = UniformFlush(a, b) * Flush;
            return new Vector3(u, u, u);
        }

        static Vector3 Min3(Vector3 a, Vector3 b) => new(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));

        /// <summary>The 15-axis separating-axis test: true when the two boxes do not interpenetrate.</summary>
        static bool Apart(in Keepout a, in Keepout b)
        {
            var d = b.C - a.C;
            for (int l = 0; l < 15; l++)
            {
                var axis = l < 3 ? a.Axis(l) : l < 6 ? b.Axis(l - 3) : Vector3.Cross(a.Axis((l - 6) / 3), b.Axis((l - 6) % 3));
                float len = axis.magnitude;
                if (len < 1e-5f) continue;
                if (Mathf.Abs(Vector3.Dot(d, axis)) >= a.Extent(axis) + b.Extent(axis)) return true;
            }
            return false;
        }

        /// <summary>The largest uniform scale (about each centre) at which two boxes are flush.</summary>
        static float UniformFlush(in Keepout a, in Keepout b)
        {
            var d = b.C - a.C;
            float best = 0f;
            for (int l = 0; l < 15; l++)
            {
                var axis = l < 3 ? a.Axis(l) : l < 6 ? b.Axis(l - 3) : Vector3.Cross(a.Axis((l - 6) / 3), b.Axis((l - 6) % 3));
                float reach = a.Extent(axis) + b.Extent(axis);
                if (reach > 1e-6f) best = Mathf.Max(best, Mathf.Abs(Vector3.Dot(d, axis)) / reach);
            }
            return Mathf.Min(1f, best);
        }

        /// <summary>
        /// A phyllotaxis shell of panes, for the links whose centres sit in it: the pane boxes
        /// (at their thickest, so a thinner jittered pane only clears more) and the panes that can
        /// reach a point - the shell's points are ordered by height, so only a band of indices
        /// can lie within reach.
        /// </summary>
        readonly struct PaneShell
        {
            public readonly Vector3 C;
            public readonly float R, Pane, Thick;
            public readonly int N;

            public PaneShell(Vector3 c, float r, int n, float pane, float thick)
            { C = c; R = r; N = n; Pane = pane; Thick = thick; }

            public Keepout PaneBox(int i)
            {
                var d = SpherePoint(i, N);
                return Keepout.Prism(C + d * R, ShellRot(d, i), new Vector3(Pane, Pane, Thick));
            }

            /// <summary>The factors for <paramref name="link"/> parted from every pane it crosses.</summary>
            public Vector3 ShareFor(in Keepout link)
            {
                var share = Vector3.one;
                float reach = link.H.magnitude + Pane + Thick;
                float y = (link.C - C).y / R, dy = reach / R;
                int i0 = Mathf.Max(0, Mathf.FloorToInt(0.5f * N * (1f - y - dy)) - 1);
                int i1 = Mathf.Min(N - 1, Mathf.CeilToInt(0.5f * N * (1f - y + dy)) + 1);
                for (int i = i0; i <= i1; i++)
                {
                    var pane = PaneBox(i);
                    if ((pane.C - link.C).sqrMagnitude < reach * reach) share = Min3(share, SplitShare(link, pane));
                }
                return share;
            }
        }

        /// <summary>The factors for a prism parted from every one of <paramref name="links"/> it crosses.</summary>
        static Vector3 ShareWith(in Keepout box, System.Collections.Generic.List<Keepout> links)
        {
            var share = Vector3.one;
            if (links == null) return share;
            float r = box.H.magnitude;
            for (int i = 0; i < links.Count; i++)
            {
                float reach = r + links[i].H.magnitude;
                if ((links[i].C - box.C).sqrMagnitude < reach * reach) share = Min3(share, SplitShare(box, links[i]));
            }
            return share;
        }

        void BuildSun()
        {
            const int n = 3400;
            // Flush panes (FlushPane): the jitter's one draw now varies only the THICKNESS, the
            // axis no neighbour lies along, so the shell keeps its texture and no pane grows into
            // the next.
            float pane = FlushPane(46f, n, 0.655f);
            for (int i = 0; i < n; i++)
            {
                var d = SpherePoint(i, n);
                var p = d * 46f;
                if (N01(p.x * 0.06f, p.y * 0.06f, p.z * 0.06f, 0) < 0.30f) continue; // flame gaps
                Emit(p, ShellRot(d, i), new Vector3(pane, pane, Jit(new Vector3(4.6f, 4.6f, 0.8f), 0.2f).z),
                    Domains.Gold);
            }
            for (int i = 0; i < 36; i++)
            {
                float a = 2f * Mathf.PI * i / 36f;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(radial * 20f, SpawnPoint.LookRotation(new Vector3(-radial.z, 0f, radial.x), radial),
                    new Vector3(3f, 3f, 3f), Domains.Gold, PrismKind.SuperShielded);
            }
        }

        void BuildRingsAndPlanets()
        {
            var keep = new Keepout[8];
            for (int ri = 0; ri < RingDefs.Length; ri++)
            {
                var (R, tiltDeg, dom) = RingDefs[ri];
                float tr = tiltDeg * Mathf.Deg2Rad;

                // Everything the rails, the spoke and the Saturn ring thread THROUGH, worked out
                // before the first strand is laid (pure functions of the ring - no draw is taken):
                // the planet's shell and core, the bearing, and the moons' shells.
                float ba = ri * 2.31f;
                float bx = R * Mathf.Cos(ba), bz0 = R * Mathf.Sin(ba);
                var bearing = new Vector3(bx, bz0 * Mathf.Sin(tr), bz0 * Mathf.Cos(tr));
                float pa = ri * GoldenAngle * 4.1f;
                var pc = PlanetCentre(ri);
                float pr = 10f + ri * 2.6f;
                int np = 560 + ri * 170;
                float pane = FlushPane(pr, np, 0.645f);
                float moonPane = FlushPane(4.2f, 150, 0.62f);
                int moons = ri >= 2 ? ri - 1 : 0;

                // Ring 0's bearing angle (0) is its planet's angle, so the bearing was laid on the
                // planet's core - one centre on another, which no size can part. It slides the
                // smallest arc along its own ring that clears the core (the two are axis-aligned
                // cubes, so that is the sum of their half sizes on the tangent's largest axis).
                {
                    var off = bearing - pc;
                    float cheb = Mathf.Max(Mathf.Abs(off.x), Mathf.Max(Mathf.Abs(off.y), Mathf.Abs(off.z)));
                    float clear = (1.2f + 1.3f) / Flush;
                    if (cheb < clear)
                    {
                        var bt = new Vector3(-Mathf.Sin(ba), Mathf.Cos(ba) * Mathf.Sin(tr), Mathf.Cos(ba) * Mathf.Cos(tr));
                        float big = Mathf.Max(Mathf.Abs(bt.x), Mathf.Max(Mathf.Abs(bt.y), Mathf.Abs(bt.z)));
                        ba += (clear - cheb) / big / R;
                        bx = R * Mathf.Cos(ba); bz0 = R * Mathf.Sin(ba);
                        bearing = new Vector3(bx, bz0 * Mathf.Sin(tr), bz0 * Mathf.Cos(tr));
                    }
                }

                // The shells as keep-outs (their envelope: thickest pane, corners' bulge) and as
                // panes; keep[0] the planet, keep[1] its core, keep[2] the bearing, keep[3..] moons.
                var shells = new PaneShell[1 + moons];
                shells[0] = new PaneShell(pc, pr, np, pane, 2f * PlanetPaneHalfThick);
                int nk = 0;
                keep[nk++] = Keepout.Shell(pc, pr - PlanetPaneHalfThick,
                    Mathf.Sqrt((pr + PlanetPaneHalfThick) * (pr + PlanetPaneHalfThick) + 0.5f * pane * pane));
                keep[nk++] = Keepout.Cube(pc, 1.3f);
                keep[nk++] = Keepout.Cube(bearing, 1.2f);
                for (int mo = 0; mo < moons; mo++)
                {
                    var mc = MoonCentre(pc, pr, ri, mo);
                    shells[1 + mo] = new PaneShell(mc, 4.2f, 150, moonPane, 0.6f);
                    keep[nk++] = Keepout.Shell(mc, 4.2f - 0.3f, Mathf.Sqrt(4.5f * 4.5f + 0.5f * moonPane * moonPane));
                }
                // The links whose centres sit IN a shell, per shell: they and the panes around them
                // part by SplitShare (both sides compute it, so the panes laid later agree).
                var sitting = new System.Collections.Generic.List<Keepout>[shells.Length];
                var onCube = new System.Collections.Generic.List<Keepout>[2];   // inside the core, the bearing
                var coreBox = Keepout.Prism(pc, Quaternion.identity, new Vector3(2.6f, 2.6f, 2.6f));
                var bearingBox = Keepout.Prism(bearing, Quaternion.identity, new Vector3(2.4f, 2.4f, 2.4f));

                // Rail-pair orbit ring (strands chained along the circumference).
                int n = (int)(2f * Mathf.PI * R / 1.7f);
                // Each strand is the flush chord of its rail (it was 4.6 on a 1.7 step, so every
                // link ran through the four after it). The y offset slides a rail's circle, it
                // does not change its radius. Where a rail threads its planet (and the core, the
                // bearing and the moons inside and around it) the strands at the crossing yield
                // (FitLink).
                float railLen = FlushArc(R, n, RailHalfDiag);
                var railSize = new Vector3[2 * n];
                for (int i = 0; i < n; i++)
                    for (int pair = -1; pair <= 1; pair += 2)
                    {
                        RailLink(ri, n, i, pair, out var at, out var rot);
                        int j = 2 * i + (pair + 1) / 2;
                        int on = FitLink(at, rot, 0.6f, 0.5f, 0.5f * railLen, keep, nk, out float half, out float w);
                        railSize[j] = new Vector3(1.2f * w, 1f * w, 2f * half);
                        if (on < 0) continue;
                        var link = Keepout.Prism(at, rot, new Vector3(1.2f, 1f, railLen));
                        Vector3 share;
                        if (on == 1 || on == 2)
                        {
                            // centred inside the core's or the bearing's cube
                            var cube = on == 1 ? coreBox : bearingBox;
                            (onCube[on - 1] ??= new()).Add(link);
                            share = SplitShare(link, cube);
                        }
                        else
                        {
                            int sh = on == 0 ? 0 : on - 2;   // keep[0] -> shell 0, keep[3 + mo] -> shell 1 + mo
                            (sitting[sh] ??= new()).Add(link);
                            share = shells[sh].ShareFor(link);
                        }
                        railSize[j] = Min3(railSize[j], Vector3.Scale(new Vector3(1.2f, 1f, railLen), share));
                    }

                // Armature spoke sun -> planet. Dock at the sun shell (r=46) instead of skewering
                // the core fly-in space; it runs on into the planet, so its links yield to the
                // planet's shell and core (FitLink). Sized here, laid last as before.
                int ns = (int)(R / 3.4f);
                var spokeRot = SpawnPoint.LookRotation(pc.normalized, Vector3.up);
                float spokeLen = Flush * R / ns;   // a straight chain: flush at exactly its step
                int i0 = Mathf.Max(2, Mathf.CeilToInt(ns * 50f / R));
                var spokeSize = new Vector3[ns];
                for (int i = i0; i < ns; i++)
                {
                    var at = SpokeLink(ri, ns, i);
                    int on = FitLink(at, spokeRot, 0.65f, 0.65f, 0.5f * spokeLen, keep, 2, out float half, out float w);
                    spokeSize[i] = Min3(new Vector3(1.3f * w, 1.3f * w, 2f * half),
                        Vector3.Scale(new Vector3(1.3f, 1.3f, spokeLen), _spokeShare[ri][i]));
                    if (on != 0) continue;
                    var link = Keepout.Prism(at, spokeRot, new Vector3(1.3f, 1.3f, spokeLen));
                    (sitting[0] ??= new()).Add(link);
                    spokeSize[i] = Min3(spokeSize[i], Vector3.Scale(new Vector3(1.3f, 1.3f, spokeLen), shells[0].ShareFor(link)));
                }

                // Saturn ring on the fourth planet: the chips clear the moons orbiting through the
                // ring and the spoke that docks across it - the spoke as one long bar, since its
                // links tile its line.
                Vector3[] chipAt = null, chipSize = null;
                if (ri == 3)
                {
                    var chipKeep = new Keepout[1 + moons];
                    chipKeep[0] = SpokeBar(ri);
                    for (int mo = 0; mo < moons; mo++) chipKeep[1 + mo] = keep[3 + mo];
                    chipAt = new Vector3[240];
                    chipSize = new Vector3[240];
                    for (int i = 0; i < 240; i++)
                    {
                        float a = 2f * Mathf.PI * i / 240f;
                        float rr = pr + 6f + 3f * Hash01(i * 7);
                        var rot = ChipRot(i);
                        chipAt[i] = pc + new Vector3(rr * Mathf.Cos(a), 0.15f * rr * Mathf.Sin(a * 3f), rr * Mathf.Sin(a));
                        // Neighbours sit at random radii, so each chip is held inside its own
                        // 1.5-degree wedge: the flush chord at ITS inner edge (FlushArc).
                        float len = FlushArc(rr, 240, 0.8f);
                        int on = FitLink(chipAt[i], rot, 0.8f, 0.25f, 0.5f * len, chipKeep, chipKeep.Length, out float half, out float w);
                        chipSize[i] = new Vector3(1.6f * w, 0.5f * w, 2f * half);
                        if (on < 1) continue;
                        var link = Keepout.Prism(chipAt[i], rot, new Vector3(1.6f, 0.5f, len));
                        (sitting[on] ??= new()).Add(link);
                        chipSize[i] = Min3(chipSize[i], Vector3.Scale(new Vector3(1.6f, 0.5f, len), shells[on].ShareFor(link)));
                    }
                }

                // ── Lay, in the original order ──
                for (int i = 0; i < n; i++)
                    for (int pair = -1; pair <= 1; pair += 2)
                    {
                        RailLink(ri, n, i, pair, out var at, out var rot);
                        Emit(at, rot, railSize[2 * i + (pair + 1) / 2], dom);
                    }

                // Jewel bearing anchor (shielded).
                Emit(bearing, Quaternion.identity, Vector3.Scale(new Vector3(2.4f, 2.4f, 2.4f), ShareWith(bearingBox, onCube[1])),
                    Domains.Ruby, PrismKind.Shielded);

                // Planet (phyllotaxis shell) + shielded core.
                Emit(pc, Quaternion.identity, Vector3.Scale(new Vector3(2.6f, 2.6f, 2.6f), ShareWith(coreBox, onCube[0])),
                    Domains.Gold, PrismKind.Shielded);
                for (int i = 0; i < np; i++)
                {
                    var d = SpherePoint(i, np);
                    var share = sitting[0] == null ? Vector3.one : ShareWith(shells[0].PaneBox(i), sitting[0]);
                    Emit(pc + d * pr, ShellRot(d, i),
                        Vector3.Scale(new Vector3(pane, pane, Jit(new Vector3(2.6f, 2.6f, 0.7f), 0.2f).z), share),
                        PlanetDoms[ri]);
                }

                // Moons for the outer planets.
                for (int mo = 0; mo < moons; mo++)
                {
                    var mc = shells[1 + mo].C;
                    for (int i = 0; i < 150; i++)
                    {
                        var d = SpherePoint(i, 150);
                        var share = sitting[1 + mo] == null ? Vector3.one : ShareWith(shells[1 + mo].PaneBox(i), sitting[1 + mo]);
                        Emit(mc + d * 4.2f, ShellRot(d, i), Vector3.Scale(new Vector3(moonPane, moonPane, 0.6f), share),
                            Domains.Blue);
                    }
                }

                // Saturn ring on the fourth planet.
                if (ri == 3)
                    for (int i = 0; i < 240; i++)
                        Emit(chipAt[i], ChipRot(i), chipSize[i], Domains.Gold);

                // Armature spoke sun -> planet.
                for (int i = i0; i < ns; i++)
                    Emit(SpokeLink(ri, ns, i), spokeRot, spokeSize[i], i % 2 != 0 ? Domains.Gold : Domains.Blue);
            }
        }

        static void RailLink(int ri, int n, int i, int pair, out Vector3 at, out Quaternion rot)
        {
            var (R, tiltDeg, _) = RingDefs[ri];
            float tr = tiltDeg * Mathf.Deg2Rad;
            float a = 2f * Mathf.PI * i / n;
            float x = R * Mathf.Cos(a), zz = R * Mathf.Sin(a);
            var tangent = new Vector3(-Mathf.Sin(a), Mathf.Cos(a) * Mathf.Sin(tr), Mathf.Cos(a) * Mathf.Cos(tr));
            rot = SpawnPoint.LookRotation(tangent, Vector3.up);
            at = new Vector3(x, zz * Mathf.Sin(tr) + pair * 1.9f * Mathf.Cos(tr), zz * Mathf.Cos(tr));
        }

        static Vector3 SpokeLink(int ri, int ns, int i)
        {
            var (R, tiltDeg, _) = RingDefs[ri];
            float tr = tiltDeg * Mathf.Deg2Rad, pa = ri * GoldenAngle * 4.1f;
            float t = i / (float)ns;
            float x = R * t * Mathf.Cos(pa), zz = R * t * Mathf.Sin(pa);
            return new Vector3(x, zz * Mathf.Sin(tr), zz * Mathf.Cos(tr));
        }

        static Quaternion ChipRot(int i)
        {
            float a = 2f * Mathf.PI * i / 240f;
            return SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)), Vector3.up);
        }

        static Vector3 PlanetCentre(int ri)
        {
            var (R, tiltDeg, _) = RingDefs[ri];
            float tr = tiltDeg * Mathf.Deg2Rad, pa = ri * GoldenAngle * 4.1f;
            float px = R * Mathf.Cos(pa), pz0 = R * Mathf.Sin(pa);
            return new Vector3(px, pz0 * Mathf.Sin(tr), pz0 * Mathf.Cos(tr));
        }

        /// <summary>Ring <paramref name="ri"/>'s armature spoke (origin to planet) as one bar, for the
        /// structures it passes.</summary>
        static Keepout SpokeBar(int ri)
        {
            var pc = PlanetCentre(ri);
            return Keepout.Bar(0.5f * pc, SpawnPoint.LookRotation(pc.normalized, Vector3.up), 0.65f, 0.65f, 0.5f * pc.magnitude);
        }

        static Vector3 MoonCentre(Vector3 pc, float pr, int ri, int mo)
        {
            float ma = mo * 2.4f + ri;
            return pc + new Vector3(Mathf.Cos(ma), 0.4f * Mathf.Sin(ma * 2f), Mathf.Sin(ma)) * (pr + 9f + mo * 6f);
        }

        /// <summary>Gate <paramref name="k"/>'s polygon sides as bars (its links tile each side
        /// vertex to vertex), written into <paramref name="keep"/> from <paramref name="at"/>.</summary>
        static int ZodiacSides(int k, Keepout[] keep, int at)
        {
            float a = 2f * Mathf.PI * k / 12f;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var cx = new Vector3(420f * ca, 0f, 420f * sa);
            int sides = 3 + k % 4;
            for (int sd = 0; sd < sides; sd++)
            {
                float a0 = 2f * Mathf.PI * sd / sides, a1 = 2f * Mathf.PI * (sd + 1) / sides;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                var sideDir = new Vector3(-sa * (c1 - c0), s1 - s0, ca * (c1 - c0));
                float lx = 9f * (c0 + c1), ly = 9f * (s0 + s1);
                keep[at++] = Keepout.Bar(cx + new Vector3(-sa * lx, ly, ca * lx),
                    SpawnPoint.LookRotation(sideDir, new Vector3(ca, 0f, sa)), 0.8f, 0.8f, 18f * Mathf.Sin(Mathf.PI / sides));
            }
            return at;
        }

        /// <summary>Link <paramref name="i"/> of gate <paramref name="k"/>'s glyph (16 per side).</summary>
        static void ZodiacSideLink(int k, int j, out Vector3 at, out Quaternion rot, out Vector3 size)
        {
            float a = 2f * Mathf.PI * k / 12f;
            float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            var cx = new Vector3(420f * ca, 0f, 420f * sa);
            int sides = 3 + k % 4, sd = j / 16, i = j % 16;
            float a0 = 2f * Mathf.PI * sd / sides, a1 = 2f * Mathf.PI * (sd + 1) / sides;
            float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
            // Straight CHORDS (lerped positions, not lerped angles) so triangle /
            // square / pentagon / hexagon glyphs actually render as polygons.
            var sideDir = new Vector3(-sa * (c1 - c0), s1 - s0, ca * (c1 - c0));
            // A side is a straight chain, flush at its own step (the links were 4.2 on a
            // 1.1-2.0 step). At a vertex the two end links meet at the polygon's interior
            // angle and their INNER corners cross the corner's bisector first: an end
            // link stays on its side of it when len + width * cot(angle / 2) <= step, so
            // it is the largest such box - full width where the corner allows it,
            // otherwise the even split (len = step / 2).
            float step = Flush * 36f * Mathf.Sin(Mathf.PI / sides) / 16f;
            float cot = 1f / Mathf.Tan(0.5f * Mathf.PI * (sides - 2) / sides);
            float endW = Mathf.Min(1.6f, 0.5f * step / cot), endLen = step - endW * cot;
            float t = (i + 0.5f) / 16f;
            float lx = 18f * Mathf.Lerp(c0, c1, t), ly = 18f * Mathf.Lerp(s0, s1, t);
            at = cx + new Vector3(-sa * lx, ly, ca * lx);
            rot = SpawnPoint.LookRotation(sideDir, new Vector3(ca, 0f, sa));
            size = i == 0 || i == 15 ? new Vector3(endW, 1.6f, endLen) : new Vector3(1.6f, 1.6f, step);
        }

        static void ZodiacBandLink(int k, int i, out Vector3 at, out Quaternion rot)
        {
            float aa = 2f * Mathf.PI * k / 12f + (i + 1) / 45f * (2f * Mathf.PI / 12f);
            rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(aa), 0f, Mathf.Cos(aa)), Vector3.up);
            at = new Vector3(420f * Mathf.Cos(aa), 3f * Mathf.Sin(aa * 9f), 420f * Mathf.Sin(aa));
        }

        void BuildZodiac()
        {
            // The band threads each gate and the next through their centres, so it crosses each
            // glyph's outline twice: its strands yield to the two glyphs' sides (FitLink, each
            // side one bar). A strand whose centre sits ON an outline parts from the side links
            // it sits among by SplitShare - both sides give a little, nobody moves - so the sides
            // are sized only once every strand is known.
            var sideSize = new Vector3[12][];
            var sitting = new System.Collections.Generic.List<Keepout>[12];
            var bandSize = new Vector3[12, 44];
            var keep = new Keepout[12];
            for (int k = 0; k < 12; k++)
            {
                int sides = 3 + k % 4, next = (k + 1) % 12;
                int nk = ZodiacSides(k, keep, 0);
                nk = ZodiacSides(next, keep, nk);
                for (int i = 0; i < 44; i++)
                {
                    ZodiacBandLink(k, i, out var at, out var rot);
                    int on = FitLink(at, rot, 0.6f, 0.45f, 2.3f, keep, nk, out float half, out float w);
                    bandSize[k, i] = new Vector3(1.2f * w, 0.9f * w, 2f * half);
                    if (on < 0) continue;
                    int gate = on < sides ? k : next;
                    var link = Keepout.Prism(at, rot, new Vector3(1.2f, 0.9f, 4.6f));
                    (sitting[gate] ??= new()).Add(link);
                    var share = Vector3.one;
                    for (int j = 0; j < 16 * (3 + gate % 4); j++)
                    {
                        ZodiacSideLink(gate, j, out var sp, out var sr, out var ss);
                        share = Min3(share, SplitShare(link, Keepout.Prism(sp, sr, ss)));
                    }
                    bandSize[k, i] = Min3(bandSize[k, i], Vector3.Scale(new Vector3(1.2f, 0.9f, 4.6f), share));
                }
            }
            for (int k = 0; k < 12; k++)
            {
                sideSize[k] = new Vector3[16 * (3 + k % 4)];
                for (int j = 0; j < sideSize[k].Length; j++)
                {
                    ZodiacSideLink(k, j, out var sp, out var sr, out var ss);
                    sideSize[k][j] = Vector3.Scale(ss, ShareWith(Keepout.Prism(sp, sr, ss), sitting[k]));
                }
            }

            for (int k = 0; k < 12; k++)
            {
                Domains dom = (k % 4) switch { 0 => Domains.Jade, 1 => Domains.Ruby, 2 => Domains.Gold, _ => Domains.Blue };
                for (int j = 0; j < sideSize[k].Length; j++)
                {
                    ZodiacSideLink(k, j, out var sp, out var sr, out _);
                    Emit(sp, sr, sideSize[k][j], dom);
                }
                for (int i = 0; i < 44; i++)
                {
                    ZodiacBandLink(k, i, out var at, out var rot);
                    Emit(at, rot, bandSize[k, i], Domains.Blue);
                }
            }
        }

        void BuildComet()
        {
            const int n = 330;
            Vector3 Orbit(float t) => new Vector3(430f * Mathf.Cos(t) - 160f, 60f * Mathf.Sin(t * 2f), 260f * Mathf.Sin(t));
            Vector3 prev = Orbit(-0.02f);
            // The tail springs from a head ON the orbit and first runs back along it, so the
            // orbit links near the head yield to the sparks (FitLink, each spark its own block).
            var hd = Orbit(0.9f);
            var sparks = new Keepout[140];
            for (int i = 0; i < 140; i++)
                sparks[i] = Keepout.Block(hd + TailOffset(i), 0.5f * TailSpark(i, new Vector3(1.2f, 1.2f, 2.6f * (1f - i / 140f * 0.5f))));
            for (int i = 0; i < n; i++)
            {
                float t = 2f * Mathf.PI * i / n;
                var p = Orbit(t);
                var rot = SpawnPoint.LookRotation(p - prev, Vector3.up);
                float half = 2.1f, w = 1f;
                if ((p - hd).sqrMagnitude < 45f * 45f)
                    FitLink(p, rot, 0.75f, 0.6f, 2.1f, sparks, sparks.Length, out half, out w);
                Emit(p, rot, new Vector3(1.5f * w, 1.2f * w, 2f * half), Domains.Blue);
                prev = p;
            }
            // Head + spark tail (the machine's only danger, clearly comet-shaped).
            //
            // 140 sparks along ~35 units of tail: a spark is ~0.25 from the next, and the sparks
            // used to be 1.2 x 1.2 x 2.6 blocks, each through a dozen others. They stay unrotated
            // blocks with their full 1.2 face, but each is a CARD - thin along the axis the tail
            // is travelling on there, exactly as thin as its gap to the sparks either side
            // (TailSpark) - so the tail reads as the same streak and every spark is flush.
            for (int i = 0; i < 140; i++)
            {
                float t = i / 140f;
                Emit(hd + TailOffset(i), Quaternion.identity, TailSpark(i, new Vector3(1.2f, 1.2f, 2.6f * (1f - t * 0.5f))),
                    Domains.Ruby, i % 2 == 0 ? PrismKind.Danger : PrismKind.Plain);
            }
        }

        /// <summary>Spark <paramref name="i"/>'s offset from the comet head along the spiral tail.</summary>
        const int TailReach = 4;

        static Vector3 TailOffset(int i)
        {
            float t = i / 140f;
            return new Vector3(28f * t * Mathf.Cos(0.9f + t * 0.8f), 10f * Mathf.Sin(t * 7f) * t,
                -28f * t * Mathf.Sin(0.9f + t * 0.8f));
        }

        /// <summary>
        /// A tail spark's flush size. For every spark within <see cref="TailReach"/> places of it,
        /// the pair's offset has a dominant axis, and on that axis this spark is no thicker than
        /// the offset - so each such pair shares an axis on which both fit inside their gap (a
        /// spark where the tail turns from one axis to another is thin on both). Sparks further
        /// apart are more than a spark's face apart. Unrotated blocks, so this is exact.
        /// </summary>
        static Vector3 TailSpark(int i, Vector3 full)
        {
            var here = TailOffset(i);
            var size = full;
            for (int j = Mathf.Max(0, i - TailReach); j <= Mathf.Min(139, i + TailReach); j++)
            {
                if (j == i) continue;
                var step = TailOffset(j) - here;
                int axis = Mathf.Abs(step.x) >= Mathf.Abs(step.y) && Mathf.Abs(step.x) >= Mathf.Abs(step.z) ? 0
                    : Mathf.Abs(step.y) >= Mathf.Abs(step.z) ? 1 : 2;
                size[axis] = Mathf.Min(size[axis], Flush * Mathf.Abs(step[axis]));
            }
            return size;
        }

        void BuildGears()
        {
            // Six meshed PAIRS (same plane, centre distance 48: tooth arms interleave, rims
            // clear) with the odd gear half-tooth phased - a legible gear train, not medallions.
            //
            // The train crosses itself - each gear's rim (r = 22) runs straight through its own
            // arms' seventh link (r = 21.6, inside the rim), and the partner's arm tips reach over
            // the rim and between this gear's arms - with each centre inside the other prism, so
            // neither can step aside. Every crossing pair parts by SplitShare (the rim gives up
            // radial width, the arm link radial length, about evenly), worked out over the whole
            // pair before either gear is laid; and where an armature spoke passes through the
            // train its links and the gear's part the same way (FitSpokesThroughGears).
            const int per = GearLinks;
            var at = new Vector3[2 * per];
            var rot = new Quaternion[2 * per];
            var size = new Vector3[2 * per];
            var box = new Keepout[2 * per];
            var near = new int[16];
            for (int pair = 0; pair < 6; pair++)
            {
                for (int g = 0; g < 2; g++)
                    for (int j = 0; j < per; j++)
                    {
                        GearLink(2 * pair + g, j, out at[g * per + j], out rot[g * per + j], out size[g * per + j]);
                        box[g * per + j] = Keepout.Prism(at[g * per + j], rot[g * per + j], size[g * per + j]);
                    }
                for (int a = 0; a < 2 * per; a++)
                {
                    // Only the links its polar position in each gear says can reach it.
                    var share = Vector3.one;
                    for (int g = 0; g < 2; g++)
                    {
                        int n = GearNeighbours(2 * pair + g, box[a].C, near);
                        for (int q = 0; q < n; q++)
                        {
                            int b = g * per + near[q];
                            if (b != a) share = Min3(share, SplitShare(box[a], box[b]));
                        }
                    }
                    size[a] = Vector3.Scale(size[a], Min3(share, _gearShare[2 * pair * per + a]));
                }
                for (int a = 0; a < 2 * per; a++)
                    Emit(at[a], rot[a], size[a], (a % per) < 99 ? Domains.Gold : Domains.Ruby);
            }
        }

        const int GearLinks = 11 * 9 + 26;

        Vector3[][] _spokeShare;
        Vector3[] _gearShare;

        /// <summary>
        /// The armature spokes run from the sun to the planets straight through the gear train
        /// (ring 6's spoke crosses gear 7's teeth). Where a spoke link and a gear link cross, the
        /// pair parts by SplitShare - worked out here, before either is laid, for both.
        /// </summary>
        void FitSpokesThroughGears()
        {
            _spokeShare = new Vector3[RingDefs.Length][];
            _gearShare = new Vector3[12 * GearLinks];
            for (int g = 0; g < _gearShare.Length; g++) _gearShare[g] = Vector3.one;
            for (int ri = 0; ri < RingDefs.Length; ri++)
            {
                float R = RingDefs[ri].R;
                int ns = (int)(R / 3.4f);
                _spokeShare[ri] = new Vector3[ns];
                for (int i = 0; i < ns; i++) _spokeShare[ri][i] = Vector3.one;
                var pc = PlanetCentre(ri);
                var spokeRot = SpawnPoint.LookRotation(pc.normalized, Vector3.up);
                float spokeLen = Flush * R / ns;
                int i0 = Mathf.Max(2, Mathf.CeilToInt(ns * 50f / R));
                for (int k = 0; k < 12; k++)
                {
                    // Only a gear the spoke's line passes within reach of.
                    var gc = GearCentre(k);
                    float along = Mathf.Clamp(Vector3.Dot(gc, pc) / pc.sqrMagnitude, 0f, 1f);
                    if ((gc - along * pc).sqrMagnitude > 32f * 32f) continue;
                    for (int i = i0; i < ns; i++)
                    {
                        var spoke = Keepout.Prism(SpokeLink(ri, ns, i), spokeRot, new Vector3(1.3f, 1.3f, spokeLen));
                        if ((spoke.C - gc).sqrMagnitude > 32f * 32f) continue;
                        for (int j = 0; j < GearLinks; j++)
                        {
                            GearLink(k, j, out var gp, out var gr, out var gs);
                            var gear = Keepout.Prism(gp, gr, gs);
                            float reach = gear.H.magnitude + spoke.H.magnitude;
                            if ((spoke.C - gear.C).sqrMagnitude > reach * reach || Apart(gear, spoke)) continue;
                            _gearShare[k * GearLinks + j] = Min3(_gearShare[k * GearLinks + j], SplitShare(gear, spoke));
                            _spokeShare[ri][i] = Min3(_spokeShare[ri][i], SplitShare(spoke, gear));
                        }
                    }
                }
            }
        }

        /// <summary>The links of gear <paramref name="k"/> that can reach a point, from its polar
        /// position about the gear: the nearest two arms' nearest links, and the nearest rim links
        /// when it is near the rim. Written into <paramref name="into"/>; returns the count.</summary>
        static int GearNeighbours(int k, Vector3 p, int[] into)
        {
            var d = p - GearCentre(k);
            float rho = Mathf.Sqrt(d.x * d.x + d.z * d.z);
            if (rho > 6f + 8f * 2.6f + 4f || Mathf.Abs(d.y) > 3f) return 0;
            float theta = Mathf.Atan2(d.z, d.x);
            int count = 0;
            float armStep = 2f * Mathf.PI / 11f, armPhase = (k % 2) * (Mathf.PI / 11f);
            int tt0 = Mathf.FloorToInt((theta - armPhase) / armStep);
            int i0 = Mathf.RoundToInt((rho - 6f) / 2.6f);
            for (int tt = tt0; tt <= tt0 + 1; tt++)
                for (int i = Mathf.Max(0, i0 - 1); i <= Mathf.Min(8, i0 + 1); i++)
                    into[count++] = ((tt % 11) + 11) % 11 * 9 + i;
            if (Mathf.Abs(rho - 22f) < 4f)
            {
                float rimStep = 2f * Mathf.PI / 26f, rimPhase = (k % 2) * (Mathf.PI / 26f);
                int r0 = Mathf.RoundToInt((theta - rimPhase) / rimStep);
                for (int r = r0 - 1; r <= r0 + 1; r++) into[count++] = 99 + ((r % 26) + 26) % 26;
            }
            return count;
        }

        /// <summary>Link <paramref name="j"/> of gear <paramref name="k"/>: 11 tooth arms of nine
        /// 2 x 0.8 x 1.6 links (r = 6 .. 26.8), then the 26-link rim at r = 22.</summary>
        static void GearLink(int k, int j, out Vector3 at, out Quaternion rot, out Vector3 size)
        {
            var cx = GearCentre(k);
            if (j < 99)
            {
                float ta = 2f * Mathf.PI * (j / 9) / 11f + (k % 2) * (Mathf.PI / 11f);
                var radial = new Vector3(Mathf.Cos(ta), 0f, Mathf.Sin(ta));
                float r2 = 6f + (j % 9) * 2.6f;
                at = cx + new Vector3(r2 * radial.x, 0.4f * Mathf.Sin(ta * 11f), r2 * radial.z);
                rot = SpawnPoint.LookRotation(radial, Vector3.up);
                size = new Vector3(2f, 0.8f, 1.6f);
                return;
            }
            float tr = 2f * Mathf.PI * (j - 99) / 26f + (k % 2) * (Mathf.PI / 26f);
            at = cx + new Vector3(22f * Mathf.Cos(tr), 0f, 22f * Mathf.Sin(tr));
            rot = SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(tr), 0f, Mathf.Cos(tr)), Vector3.up);
            size = new Vector3(1.6f, 0.9f, 2.6f);
        }

        static Vector3 GearCentre(int k)
        {
            int pair = k / 2;
            float a = pair * GoldenAngle * 2.9f;
            float rr = 125f + 45f * (pair % 3);
            var cx = new Vector3(rr * Mathf.Cos(a), (pair % 2 * 2 - 1) * 36f, rr * Mathf.Sin(a));
            if (k % 2 != 0)
                cx += new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * 48f;
            return cx;
        }

        void BuildPendulums()
        {
            FitHoop();
            for (int k = 0; k < 9; k++)
            {
                float a = 2f * Mathf.PI * k / 9f;
                var top = new Vector3(120f * Mathf.Cos(a), 300f, 120f * Mathf.Sin(a));
                float swing = Mathf.Sin(k * 2.1f) * 38f;
                var swingDir = new Vector3(Mathf.Cos(a + 1.5f), 0f, Mathf.Sin(a + 1.5f));
                var swingSide = new Vector3(-Mathf.Sin(a + 1.5f), 0f, Mathf.Cos(a + 1.5f));
                for (int i = 0; i < 26; i++)
                {
                    float t = i / 25f;
                    // Links chain along the swing curve's tangent (same prism volume).
                    var tangentDir = swingDir * (2f * swing * t) + Vector3.down * 150f;
                    Emit(new Vector3(top.x + swing * t * t * swingDir.x, top.y - t * 150f,
                            top.z + swing * t * t * swingDir.z),
                        SpawnPoint.LookRotation(tangentDir, swingSide), i == 0 ? _topSize[k] : new Vector3(1f, 1f, 3.2f),
                        Domains.Blue);
                }
                var bob = new Vector3(top.x + swing * Mathf.Cos(a + 1.5f), top.y - 152f, top.z + swing * Mathf.Sin(a + 1.5f));
                float bobPane = FlushPane(5f, 60, 0.625f);
                for (int i = 0; i < 60; i++)
                {
                    var d = SpherePoint(i, 60);
                    Emit(bob + d * 5f, ShellRot(d, i), new Vector3(bobPane, bobPane, 0.7f), Domains.Gold);
                }
            }
        }

        Vector3[] _topSize, _hoopAt, _hoopSize;

        /// <summary>
        /// Each pendulum's top link is centred ON the suspension hoop (it hangs from it). The hoop
        /// links yield to the nine top links (FitLink); a hoop link whose centre sits inside a top
        /// link parts from it by SplitShare (both give a little), and the one whose centre IS the
        /// top link's centre - one centre on another, which no size can part - slides along the
        /// hoop within its own cell (SlideLink). Worked out before either is laid.
        /// </summary>
        void FitHoop()
        {
            var tops = new Keepout[9];
            _topSize = new Vector3[9];
            for (int k = 0; k < 9; k++)
            {
                float a = 2f * Mathf.PI * k / 9f;
                var swingSide = new Vector3(-Mathf.Sin(a + 1.5f), 0f, Mathf.Cos(a + 1.5f));
                tops[k] = Keepout.Prism(new Vector3(120f * Mathf.Cos(a), 300f, 120f * Mathf.Sin(a)),
                    SpawnPoint.LookRotation(Vector3.down, swingSide), new Vector3(1f, 1f, 3.2f));
                _topSize[k] = new Vector3(1f, 1f, 3.2f);
            }
            int n = (int)(2f * Mathf.PI * 120f / 3.4f);
            float len = FlushArc(120f, n, RailHalfDiag);
            _hoopAt = new Vector3[n];
            _hoopSize = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float t = 2f * Mathf.PI * i / n;
                var rot = HoopRot(i, n);
                var at = new Vector3(120f * Mathf.Cos(t), 300f, 120f * Mathf.Sin(t));
                int on = FitLink(at, rot, 0.6f, 0.5f, 0.5f * len, tops, tops.Length, out float half, out float w);
                var size = new Vector3(1.2f * w, 1f * w, 2f * half);
                if (on >= 0)
                {
                    if ((tops[on].C - at).sqrMagnitude < 1e-6f)
                    {
                        SlideLink(ref at, rot, 0.6f, 0.5f, 0.5f * len, 0.5f * len, tops[on], out half);
                        size = new Vector3(1.2f, 1f, 2f * half);
                    }
                    else
                    {
                        var link = Keepout.Prism(at, rot, new Vector3(1.2f, 1f, len));
                        size = Min3(size, Vector3.Scale(new Vector3(1.2f, 1f, len), SplitShare(link, tops[on])));
                        _topSize[on] = Min3(_topSize[on], Vector3.Scale(new Vector3(1f, 1f, 3.2f), SplitShare(tops[on], link)));
                    }
                }
                _hoopAt[i] = at;
                _hoopSize[i] = size;
            }
        }

        static Quaternion HoopRot(int i, int n)
        {
            float t = 2f * Mathf.PI * i / n;
            return SpawnPoint.LookRotation(new Vector3(-Mathf.Sin(t), 0f, Mathf.Cos(t)), Vector3.up);
        }

        /// <summary>The canopy frame the pendulums hang FROM - a gold suspension hoop through
        /// the tops, reusing the orbit-rail vocabulary; also the machine's high skim line. Its
        /// links are the flush chords of the hoop, fitted round the top links (FitHoop).</summary>
        void BuildSuspensionHoop()
        {
            for (int i = 0; i < _hoopAt.Length; i++)
                Emit(_hoopAt[i], HoopRot(i, _hoopAt.Length), _hoopSize[i], Domains.Gold);
        }

        void BuildStars()
        {
            int n = Scaled(6400);
            for (int i = 0; i < n; i++)
            {
                if (Hash01(i * 17 + _noiseSeed) < 0.38f) continue;
                var d = SpherePoint(i, n);
                var p = d * (445f + 35f * Hash01(i * 23));
                Emit(p, Quaternion.Euler(0f, Hash01(i * 29) * 360f, 0f), new Vector3(1.2f, 1.2f, 1.2f),
                    i % 5 != 0 ? Domains.Blue : Domains.Gold);
            }
        }
    }
}
