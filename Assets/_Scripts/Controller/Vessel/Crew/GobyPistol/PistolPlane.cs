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
        public Vector3 Ground(float s, in PistolPlane.Hills hills) => A + S * s + N * PistolPlane.Hill(s, L, hills);
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
            public Hills H;
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
        /// The trail's two scales of hill, the two the lab's Pistol rode (<c>goby.html</c> rounds 12-13, Garrett: "lower
        /// amplitude and higher frequency wiggles"): the ROLLS, which the Goby AI's weave in height laid there (18 u at
        /// 0.55 Hz behind a 160 u/s Goby: about 290 u from crest to crest), the long hills a Tiny Wings dive and release
        /// is played on; and the FINE zigzag of the trail's alternating hinges (every second 16 u block, 10 u up then
        /// down: 64 u), the texture under the Pistol. A dart is straight, so neither comes from its flight any more; both
        /// are laid along the Goby's up.
        /// </summary>
        public struct Hills
        {
            public float RollAmplitude, RollWavelength, FineAmplitude, FineWavelength;

            public Hills(float rollAmplitude, float rollWavelength, float fineAmplitude, float fineWavelength)
            {
                RollAmplitude = rollAmplitude; RollWavelength = rollWavelength; FineAmplitude = fineAmplitude; FineWavelength = fineWavelength;
            }

            public bool IsFlat => RollAmplitude == 0f && FineAmplitude == 0f;
        }

        /// <summary>Whole half-waves of <paramref name="wavelength"/> that fit a stretch <paramref name="length"/> long (at
        /// least one), so a hill of that scale is zero at both perches without fading: every dart, short or long, keeps
        /// its wiggles right up to each perch.</summary>
        public static int HalfWaves(float length, float wavelength) => Mathf.Max(1, Mathf.RoundToInt(2f * length / Mathf.Max(1f, wavelength)));

        /// <summary>The steepest the trail's hills may climb or fall, dHill/ds. Under cot(slope) (2.5 at 22 deg) the ground
        /// never folds back on the Pistol's screen; the two scales at full size reach about 1.4.</summary>
        public const float MaxHillSlope = 1.5f;

        /// <summary>The hill at <paramref name="s"/> along a line <paramref name="length"/> long: the rolls plus the fine
        /// zigzag, each a whole number of half-waves (<see cref="HalfWaves"/>), so the ground meets both perches. A dart so
        /// short that its hills would pass <see cref="MaxHillSlope"/> gets lower hills.</summary>
        public static float Hill(float s, float length, in Hills h)
        {
            if (length <= 1e-4f || h.IsFlat || s <= 0f || s >= length) return 0f;
            float q = Mathf.PI * s / length;
            int n = HalfWaves(length, h.RollWavelength), m = HalfWaves(length, h.FineWavelength);
            return Calm(length, h, n, m) * (h.RollAmplitude * Mathf.Sin(n * q) + h.FineAmplitude * Mathf.Sin(m * q));
        }

        /// <summary>dHill/ds.</summary>
        public static float HillSlope(float s, float length, in Hills h)
        {
            if (length <= 1e-4f || h.IsFlat || s < 0f || s > length) return 0f;
            float k = Mathf.PI / length, q = k * s;
            int n = HalfWaves(length, h.RollWavelength), m = HalfWaves(length, h.FineWavelength);
            return Calm(length, h, n, m) * k * (h.RollAmplitude * n * Mathf.Cos(n * q) + h.FineAmplitude * m * Mathf.Cos(m * q));
        }

        /// <summary>1, or less where both scales together would climb steeper than <see cref="MaxHillSlope"/>.</summary>
        static float Calm(float length, in Hills h, int n, int m)
        {
            float steepest = Mathf.PI / length * (Mathf.Abs(h.RollAmplitude) * n + Mathf.Abs(h.FineAmplitude) * m);
            return steepest > MaxHillSlope ? MaxHillSlope / steepest : 1f;
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
        /// (-1, from B to A), leaning the travel <paramref name="slopeDegrees"/> downhill.</summary>
        public static Frame For(in PlaneSegment seg, int dir, float slopeDegrees, in Hills hills)
        {
            float a = Mathf.Clamp(slopeDegrees, 0f, 75f) * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            Vector3 t = dir >= 0 ? seg.S : -seg.S;
            var f = new Frame
            {
                Dir = dir >= 0 ? 1 : -1, Seg = seg, L = seg.L, Cos = c, Sin = sn, H = hills,
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
            // x = u cos + h(u) sin: solve for u by Newton's method. Its derivative cos + h'(u) sin stays positive while the
            // hills are under MaxHillSlope and the lean under 33 deg (cot > 1.5), so x never folds and the root is unique;
            // the fixed-point guess starts it close.
            float cs = Mathf.Max(1e-4f, f.Cos), u = (x - HillAt(f, x / cs) * f.Sin) / cs;
            for (int i = 0; i < 8; i++)
            {
                float d = cs + HillSlopeAt(f, u) * f.Sin;
                u -= (u * cs + HillAt(f, u) * f.Sin - x) / (d > 0.05f ? d : 0.05f);
            }
            float h = HillAt(f, u), dh = HillSlopeAt(f, u);
            float dx = f.Cos + dh * f.Sin, dy = -f.Sin + dh * f.Cos;
            slope = dy / (Mathf.Abs(dx) < 1e-4f ? 1e-4f : dx);
            return -u * f.Sin + h * f.Cos;
        }

        public static float Ground(in Frame f, float x) => Ground(f, x, out _);

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
            return Hill(LineS(f, u), f.L, f.H);
        }

        static float HillSlopeAt(in Frame f, float u)
        {
            if (u <= 0f || u >= f.L) return 0f;
            return HillSlope(LineS(f, u), f.L, f.H) * f.Dir;
        }
    }
}
