using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One stretch of the Pistol's world: the straight dart from one Goby perch to the next, and the FLAT plane through it
    /// that the Pistol plays in (Garrett, 2026-10-22: "the pistol's plane ... no longer needs to bend at all"). The plane
    /// holds the dart's line <see cref="S"/> and the Goby's up at the dart (<see cref="N"/>, made square to the line).
    /// The ground the Pistol rides is the dart's own trail: the line with hills along <see cref="N"/>
    /// (<see cref="PistolPlane.Hill"/>), which come to nothing at both perches so the ground meets each one.
    /// </summary>
    public readonly struct PlaneSegment
    {
        public readonly Vector3 A, B, S, N;
        public readonly float L;

        public PlaneSegment(Vector3 a, Vector3 b, Vector3 upHint)
        {
            A = a; B = b;
            Vector3 d = b - a;
            L = d.magnitude;
            S = L > 1e-4f ? d / L : Vector3.forward;
            Vector3 n = upHint - S * Vector3.Dot(upHint, S);
            if (n.sqrMagnitude < 1e-6f) n = Vector3.up - S * Vector3.Dot(Vector3.up, S);
            if (n.sqrMagnitude < 1e-6f) n = Vector3.right - S * Vector3.Dot(Vector3.right, S);
            N = n.normalized;
        }

        /// <summary>A point of the ground, <paramref name="s"/> along the line from <see cref="A"/>.</summary>
        public Vector3 Ground(float s, PistolPlane.HillTrack track) => A + S * s + N * (track?.Height(s) ?? 0f);
    }

    /// <summary>
    /// The Pistol's 2D world on a <see cref="PlaneSegment"/>: a frame in which travel from one perch to the other is
    /// DOWNHILL (Garrett, 2026-10-22: "the slope from one perch to another should be a fun downhill for the shrimp"). The
    /// frame's right <see cref="Frame.R"/> leans the travel direction down by the slope angle, so the straight line from
    /// the perch the Pistol left to the one it rides to falls by <c>L sin(slope)</c> whichever way it travels: going back
    /// to an old den is downhill too. x runs along <see cref="Frame.R"/>, y along <see cref="Frame.Up"/> (gravity is
    /// minus y), and d out of the plane along <see cref="Frame.M"/>. Pure: no scene, no time.
    /// </summary>
    public static class PistolPlane
    {
        public struct Frame
        {
            public Vector3 O, T, R, Up, M;
            public float L, Cos, Sin;
            public HillTrack Track;
            /// <summary>Which of the island's ground blocks still stand (null: all of them, the frame the Pistol waits in).</summary>
            public PistolGround Ground;
            public int Dir;
            public PlaneSegment Seg;

            /// <summary>Where the ground ends: the perch the Pistol rides toward (the hills are zero there).</summary>
            public float EndX => L * Cos;

            public Vector3 ToWorld(float x, float y) => O + R * x + Up * y;

            public Vector3 ToFrame(Vector3 world)
            {
                Vector3 q = world - O;
                return new Vector3(Vector3.Dot(q, R), Vector3.Dot(q, Up), Vector3.Dot(q, M));
            }
        }

        /// <summary>
        /// The shape of Tiny Wings' hills (Garrett, 2026-10-24: "you need to understand tiny wings better ... the right
        /// sinusoidal curve"; the study: <c>Docs/PISTOL_TINY_WINGS_STUDY.md</c>). The game's ground is a run of KEY POINTS,
        /// alternately a crest and a trough, each a random distance on and a random height up or down from the last, with
        /// a HALF-COSINE between every pair (the haqu remake's <c>generateHillKeyPoints</c>: dx 160-240 pt, dy 60-120 pt,
        /// for a 14 pt bird). So every hill is different, every crest and trough is level, and the steepest slope is
        /// pi dy / 2 dx. All lengths in u; the island's heights grow by <see cref="GrowthPerIsland"/> per island, as the
        /// game's later islands are taller, to <see cref="GrowthMax"/>.
        /// </summary>
        public struct HillSpec
        {
            public float HalfLengthMin, HalfLengthMax, HalfHeightMin, HalfHeightMax, Band, GrowthPerIsland, GrowthMax;

            public HillSpec(float halfLengthMin, float halfLengthMax, float halfHeightMin, float halfHeightMax, float band, float growthPerIsland = 0f, float growthMax = 1f)
            {
                HalfLengthMin = halfLengthMin; HalfLengthMax = halfLengthMax; HalfHeightMin = halfHeightMin; HalfHeightMax = halfHeightMax; Band = band;
                GrowthPerIsland = growthPerIsland; GrowthMax = growthMax;
            }

            /// <summary>The mean hill, crest to crest (two half-hills).</summary>
            public float MeanWavelength => HalfLengthMin + HalfLengthMax;

            /// <summary>The mean hill, trough to crest.</summary>
            public float MeanHeight => 0.5f * (HalfHeightMin + HalfHeightMax);

            public bool IsFlat => HalfHeightMax <= 0f;

            /// <summary>How much taller island <paramref name="island"/> (0 the first) is than the first.</summary>
            public float Growth(int island) => Mathf.Min(Mathf.Max(1f, GrowthMax), 1f + Mathf.Max(0f, GrowthPerIsland) * Mathf.Max(0, island));
        }

        /// <summary>
        /// One island's ground: the key points of a <see cref="HillSpec"/> laid along a dart <see cref="Length"/> long from a
        /// seed (the same seed on every machine: the rider's Pistol builds the same ground from the perches alone). The
        /// island starts on a crest at the perch it leaves (its first half-hill runs DOWN, the dive that starts the ride)
        /// and ends on a crest at the perch it rides to (its last half-hill climbs, the ramp that lands it there); both
        /// are at height 0, so the ground meets both perches. Heights stay within +-<see cref="HillSpec.Band"/> of the dart.
        /// </summary>
        public sealed class HillTrack
        {
            public readonly float Length;
            readonly float[] _s, _h;

            public int KeyCount => _s.Length;
            public float KeyS(int i) => _s[i];
            public float KeyH(int i) => _h[i];

            HillTrack(float length, float[] s, float[] h) { Length = length; _s = s; _h = h; }

            /// <summary>A flat track (no hills): the frame the Pistol waits in.</summary>
            public static HillTrack Flat(float length) => new(length, new[] { 0f, Mathf.Max(1f, length) }, new[] { 0f, 0f });

            /// <summary>Lay the island. <paramref name="island"/> is its index from the run's first (for the growth).</summary>
            public static HillTrack Build(float length, uint seed, in HillSpec spec, int island = 0)
            {
                length = Mathf.Max(1f, length);
                if (spec.IsFlat || length < 2f) return Flat(length);
                var rng = new PistolAI.Mulberry32(seed);
                float grow = spec.Growth(island);
                float dxMin = Mathf.Max(1f, spec.HalfLengthMin), dxMax = Mathf.Max(dxMin, spec.HalfLengthMax);
                float dyMin = Mathf.Max(0f, spec.HalfHeightMin) * grow, dyMax = Mathf.Max(dyMin, spec.HalfHeightMax) * grow, band = Mathf.Max(dyMax, spec.Band);
                var ss = new System.Collections.Generic.List<float> { 0f };
                var hs = new System.Collections.Generic.List<float> { 0f };
                float s = 0f, h = 0f, sign = -1f;   // the first half-hill runs down: the perch is a crest
                while (true)
                {
                    float dx = Mathf.Lerp(dxMin, dxMax, rng.Next()), dy = Mathf.Lerp(dyMin, dyMax, rng.Next());
                    if (s + dx > length - dxMin) break;
                    s += dx;
                    h = Mathf.Clamp(h + dy * sign, -band, band);
                    sign = -sign;
                    ss.Add(s); hs.Add(h);
                }
                // the island ends on a climb to the perch: the key before the end is a trough. If the last step went up,
                // put a trough between it and the end when there is room, else drop it
                int n = ss.Count;
                if (n >= 2 && hs[n - 1] > hs[n - 2])
                {
                    if (length - ss[n - 1] >= 1.6f * dxMin) { ss.Add(0.5f * (ss[n - 1] + length)); hs.Add(Mathf.Clamp(hs[n - 1] - Mathf.Lerp(dyMin, dyMax, rng.Next()), -band, band)); }
                    else { ss.RemoveAt(n - 1); hs.RemoveAt(n - 1); }
                }
                if (ss.Count == 1) { ss.Add(0.5f * length); hs.Add(-Mathf.Lerp(dyMin, dyMax, rng.Next())); }   // one valley
                ss.Add(length); hs.Add(0f);
                return new HillTrack(length, ss.ToArray(), hs.ToArray());
            }

            int Piece(float s)
            {
                int lo = 0, hi = _s.Length - 2;
                while (lo < hi) { int mid = (lo + hi + 1) >> 1; if (_s[mid] <= s) lo = mid; else hi = mid - 1; }
                return lo;
            }

            /// <summary>The ground's height at <paramref name="s"/> along the dart (0 past either end: the perches).</summary>
            public float Height(float s)
            {
                if (s <= 0f || s >= Length) return 0f;
                int i = Piece(s);
                float s0 = _s[i], s1 = _s[i + 1], h0 = _h[i], h1 = _h[i + 1];
                float t = (s - s0) / Mathf.Max(1e-4f, s1 - s0);
                return 0.5f * (h0 + h1) + 0.5f * (h0 - h1) * Mathf.Cos(Mathf.PI * t);
            }

            /// <summary>dHeight/ds.</summary>
            public float Slope(float s)
            {
                if (s < 0f || s > Length) return 0f;
                int i = Piece(s);
                float s0 = _s[i], s1 = _s[i + 1], h0 = _h[i], h1 = _h[i + 1], w = Mathf.Max(1e-4f, s1 - s0);
                return -0.5f * (h0 - h1) * Mathf.PI / w * Mathf.Sin(Mathf.PI * (s - s0) / w);
            }

            /// <summary>The steepest the track climbs or falls, dHeight/ds.</summary>
            public float SteepestSlope()
            {
                float worst = 0f;
                for (int i = 0; i + 1 < _s.Length; i++) worst = Mathf.Max(worst, Mathf.Abs(_h[i] - _h[i + 1]) * Mathf.PI / (2f * Mathf.Max(1e-4f, _s[i + 1] - _s[i])));
                return worst;
            }
        }

        /// <summary>The seed an island's ground is built from: its two perches, quantised, so every machine that knows the
        /// perches builds the same hills.</summary>
        public static uint IslandSeed(Vector3 a, Vector3 b)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (float v in new[] { a.x, a.y, a.z, b.x, b.y, b.z }) { h ^= (uint)Mathf.RoundToInt(v * 4f); h *= 16777619u; }
                return h;
            }
        }

        /// <summary>Hills steeper than this would fold the ground back on a leaning screen (cot of the lean); the lean is
        /// capped at 33 deg so cot stays above it. Level, as Tiny Wings is, nothing folds.</summary>
        public const float MaxHillSlope = 1.5f;

        /// <summary>The ground's signed curvature under frame-x <paramref name="x"/> (negative over a crest), and its slope.
        /// The ride's liftoff reads it: a crest throws the Pistol when its speed needs more pull round the curve than
        /// gravity gives (<c>PistolBodyMath.Step</c>).</summary>
        public static float Curvature(in Frame f, float x, out float slope)
        {
            const float d = 0.25f;
            Ground(f, x - d, out float a); Ground(f, x + d, out float b); Ground(f, x, out slope);
            float ypp = (b - a) / (2f * d), w = 1f + slope * slope;
            return ypp / (w * Mathf.Sqrt(w));
        }

        /// <summary>
        /// Tiny Wings' camera on the Pistol's plane: the window of the plane the screen shows. The Pistol sits
        /// <paramref name="pistolAt"/> of the width from the centre (left of it), the ground near the bottom; the window
        /// widens with speed (<paramref name="speedWiden"/> u of width per u/s), and when the Pistol flies high the window
        /// widens further, up to <paramref name="maxWidth"/>, so the ground below it never leaves the screen; past that it
        /// rises with the Pistol. Frame (x, y) in, frame (x, y) of the window's centre and its width out.
        /// </summary>
        public static Vector2 TinyWingsView(in Frame f, float px, float py, float speed, float baseWidth, float maxWidth, float speedWiden,
            float pistolAt, float groundedAt, float headroom, float aspect, out float width)
        {
            aspect = Mathf.Max(0.2f, aspect);
            float lo = Mathf.Max(1f, baseWidth), hi = Mathf.Max(lo, maxWidth);
            float W = Mathf.Clamp(lo + Mathf.Max(0f, speedWiden) * Mathf.Max(0f, speed), lo, hi);
            float bottom = ViewBottom(f, px, W, aspect, pistolAt, groundedAt);
            for (int pass = 0; pass < 4; pass++)
            {
                float need = (py - bottom) / Mathf.Max(0.05f, 1f - headroom);   // the height that keeps the Pistol under the headroom
                if (need <= W / aspect || W >= hi) break;
                W = Mathf.Min(hi, need * aspect);
                bottom = ViewBottom(f, px, W, aspect, pistolAt, groundedAt);
            }
            float h = W / aspect;
            if (py > bottom + (1f - headroom) * h) bottom = py - (1f - headroom) * h;   // at the widest: rise with it
            width = W;
            return new Vector2(px - pistolAt * W, bottom + 0.5f * h);
        }

        /// <summary>The bottom of a <paramref name="W"/> wide window: the ground under the Pistol at
        /// <paramref name="groundedAt"/> of the height, or lower if the ground across the window falls further.</summary>
        static float ViewBottom(in Frame f, float px, float W, float aspect, float pistolAt, float groundedAt)
        {
            float H = W / aspect, left = px - pistolAt * W - 0.5f * W, lowest = float.MaxValue;
            for (int i = 0; i <= 12; i++) lowest = Mathf.Min(lowest, Ground(f, left + W * i / 12f));
            return Mathf.Min(Ground(f, px) - groundedAt * H, lowest - 0.04f * H);
        }

        /// <summary>
        /// The side camera's off-axis lens (the lab's <c>frustumOf</c>, and the first Unity side view's): a camera
        /// <paramref name="distance"/> from the plane shows a <paramref name="width"/> x <paramref name="height"/> window of
        /// it whose centre is <paramref name="window"/> from the camera's axis (in the camera's x, y), as the near-plane
        /// bounds <c>Matrix4x4.Frustum</c> takes. The camera stays beside the Pistol while the window looks ahead.
        /// </summary>
        public static void OffAxis(Vector2 window, float distance, float width, float height, float near,
            out float left, out float right, out float bottom, out float top)
        {
            float k = near / Mathf.Max(1e-3f, distance);
            left = (window.x - 0.5f * width) * k; right = (window.x + 0.5f * width) * k;
            bottom = (window.y - 0.5f * height) * k; top = (window.y + 0.5f * height) * k;
        }

        /// <summary>The frame for riding <paramref name="seg"/> forward (<paramref name="dir"/> +1, from A to B) or back
        /// (-1, from B to A), leaning the travel <paramref name="slopeDegrees"/> downhill. <paramref name="ground"/>: which of
        /// its blocks still stand (null: all).</summary>
        public static Frame For(in PlaneSegment seg, int dir, float slopeDegrees, HillTrack track, PistolGround ground = null)
        {
            float a = Mathf.Clamp(slopeDegrees, 0f, 75f) * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            Vector3 t = dir >= 0 ? seg.S : -seg.S;
            var f = new Frame
            {
                Dir = dir >= 0 ? 1 : -1, Seg = seg, L = seg.L, Cos = c, Sin = sn, Track = track ?? HillTrack.Flat(seg.L), Ground = ground,
                O = dir >= 0 ? seg.A : seg.B, T = t,
                R = (t * c + seg.N * sn).normalized,
                Up = (-t * sn + seg.N * c).normalized,
            };
            f.M = Vector3.Cross(f.R, f.Up);
            return f;
        }

        /// <summary>The ground under frame-x <paramref name="x"/> and its slope dy/dx. Past either end the ground runs on
        /// along the slope (the Pistol arrives at a perch there).</summary>
        public static float Ground(in Frame f, float x, out float slope)
        {
            float u = TravelAt(f, x);
            float h = HillAt(f, u), dh = HillSlopeAt(f, u);
            float dx = f.Cos + dh * f.Sin, dy = -f.Sin + dh * f.Cos;
            slope = dy / (Mathf.Abs(dx) < 1e-4f ? 1e-4f : dx);
            return -u * f.Sin + h * f.Cos;
        }

        public static float Ground(in Frame f, float x) => Ground(f, x, out _);

        /// <summary>The travel u from the frame's start of the ground under frame-x <paramref name="x"/>.</summary>
        static float TravelAt(in Frame f, float x)
        {
            // x = u cos + h(u) sin: solve for u by Newton's method. Its derivative cos + h'(u) sin stays positive while the
            // hills are under MaxHillSlope and the lean under 33 deg (cot > 1.5), so x never folds and the root is unique;
            // the fixed-point guess starts it close. Level (the shipped lean is 0, Tiny Wings') it is exact at once.
            float cs = Mathf.Max(1e-4f, f.Cos), u = (x - HillAt(f, x / cs) * f.Sin) / cs;
            for (int i = 0; i < 8; i++)
            {
                float d = cs + HillSlopeAt(f, u) * f.Sin;
                u -= (u * cs + HillAt(f, u) * f.Sin - x) / (d > 0.05f ? d : 0.05f);
            }
            return u;
        }

        /// <summary>Distance along the line (from A) of the ground under frame-x <paramref name="x"/>.</summary>
        public static float LineAt(in Frame f, float x) => LineS(f, TravelAt(f, x));

        /// <summary>Is there ground under frame-x <paramref name="x"/>? Not where the island's block there was destroyed
        /// (<see cref="PistolGround"/>): the Pistol must clear that hole, and falls through it if it does not. The perches
        /// at either end (and past them) always stand.</summary>
        public static bool SolidAt(in Frame f, float x)
        {
            if (f.Ground == null || !f.Ground.AnyGone) return true;
            float u = TravelAt(f, x);
            return u <= 0f || u >= f.L || f.Ground.SolidAt(LineS(f, u));
        }

        /// <summary>The ground's point <paramref name="u"/> along the travel, in the frame (x, y).</summary>
        public static Vector2 GroundPoint(in Frame f, float u)
        {
            float h = HillAt(f, u);
            return new Vector2(u * f.Cos + h * f.Sin, -u * f.Sin + h * f.Cos);
        }

        /// <summary>Distance along the line (from A) for travel <paramref name="u"/> from the frame's start.</summary>
        public static float LineS(in Frame f, float u) => f.Dir > 0 ? u : f.L - u;

        static float HillAt(in Frame f, float u)
        {
            if (u <= 0f || u >= f.L) return 0f;
            return f.Track.Height(LineS(f, u));
        }

        static float HillSlopeAt(in Frame f, float u)
        {
            if (u <= 0f || u >= f.L) return 0f;
            return f.Track.Slope(LineS(f, u)) * f.Dir;
        }
    }

    /// <summary>
    /// Which of an island's ground blocks still stand (Garrett, 2026-10-28: "when the goby's trail is destroyed the shrimp
    /// should have to clear the destroyed section. if they fall they should not hit phantom prisms"). The Goby lays a block
    /// every <see cref="Gap"/> along its dart (<c>trailBlockGap</c>); slot i covers [i Gap, (i + 1) Gap) along the line from
    /// A and is ground until its block is destroyed (the seat marks it, frame by frame, from the blocks themselves). The
    /// ride's hills are the same either way: a destroyed block is a HOLE in them, never ground that is no longer there.
    /// Pure, so the tests read it.
    /// </summary>
    public sealed class PistolGround
    {
        public readonly float Length, Gap;
        readonly bool[] _gone;
        int _goneCount;

        public PistolGround(float length, float gap)
        {
            Length = Mathf.Max(1f, length); Gap = Mathf.Max(1f, gap);
            _gone = new bool[Mathf.Max(1, Mathf.CeilToInt(Length / Gap))];
        }

        public int Count => _gone.Length;

        /// <summary>True while any block of the island is gone.</summary>
        public bool AnyGone => _goneCount > 0;

        /// <summary>The slot <paramref name="s"/> along the line lies in.</summary>
        public int SlotAt(float s) => Mathf.Clamp(Mathf.FloorToInt(s / Gap), 0, _gone.Length - 1);

        /// <summary>The slot's block's place along the line (where the Goby laid it).</summary>
        public float BlockS(int slot) => Gap * (slot + 0.5f);

        public bool Gone(int slot) => slot >= 0 && slot < _gone.Length && _gone[slot];

        /// <summary>Marks slot <paramref name="slot"/>'s block destroyed, or standing again (a restored block is ground again).</summary>
        public void SetGone(int slot, bool gone)
        {
            if (slot < 0 || slot >= _gone.Length || _gone[slot] == gone) return;
            _gone[slot] = gone;
            _goneCount += gone ? 1 : -1;
        }

        /// <summary>Ground at <paramref name="s"/> along the line: everywhere but a destroyed block's slot.</summary>
        public bool SolidAt(float s) => s <= 0f || s >= Length || !_gone[SlotAt(s)];

        /// <summary>The slots whose block lies on the stretch from <paramref name="s0"/> to <paramref name="s1"/> along the
        /// line (either way; a block exactly at the start was counted by the stretch before), added to <paramref name="into"/>
        /// in the order the stretch passes them.</summary>
        public void BlocksBetween(float s0, float s1, System.Collections.Generic.List<int> into)
        {
            bool up = s1 >= s0;
            float lo = up ? s0 : s1, hi = up ? s1 : s0;
            int first = Mathf.Max(0, Mathf.CeilToInt(lo / Gap - 0.5f)), last = Mathf.Min(_gone.Length - 1, Mathf.FloorToInt(hi / Gap - 0.5f));
            if (up) { for (int i = first; i <= last; i++) if (BlockAt(i, lo, hi, true)) into.Add(i); }
            else { for (int i = last; i >= first; i--) if (BlockAt(i, lo, hi, false)) into.Add(i); }
        }

        bool BlockAt(int i, float lo, float hi, bool up)
        {
            float b = BlockS(i);
            if (b >= Length) return false;
            return up ? b > lo && b <= hi : b >= lo && b < hi;
        }
    }

    /// <summary>
    /// Where the Pistol's islands end (Garrett, 2026-10-23: "the goby is barely cruising around at all ... lets start by
    /// tuning their movement"). The Goby cruises between perches; a dart's end becomes a PERCH, the end of an island, only
    /// once it is <c>islandMinLength</c> from the last perch, and a longer gap is laid as islands of at most
    /// <c>islandMaxLength</c>, so the Pistol always rides the island length its Tiny Wings ride is tuned for. Pure.
    /// </summary>
    public static class GobyIslands
    {
        /// <summary>True when a perch at <paramref name="at"/> ends an island begun at <paramref name="from"/>.</summary>
        public static bool Reaches(Vector3 from, Vector3 at, float minLength) => (at - from).sqrMagnitude >= minLength * minLength;

        /// <summary>How many islands the gap from <paramref name="from"/> to <paramref name="at"/> is laid as.</summary>
        public static int Pieces(Vector3 from, Vector3 at, float maxLength) =>
            Mathf.Max(1, Mathf.CeilToInt((at - from).magnitude / Mathf.Max(1f, maxLength) - 1e-4f));

        /// <summary>Islands laid ahead of the one the Pistol rides toward (or rests at): the Goby lays no new perch while
        /// <c>islandQueue</c> wait, so the two stay together.</summary>
        public static int Ahead(int perches, bool resting, int at, int segment, int dir) =>
            Mathf.Max(0, perches - 1 - (resting ? at : (dir > 0 ? segment + 1 : segment)));
    }
}

