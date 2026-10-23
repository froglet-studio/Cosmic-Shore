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
        /// The trail's hills, Tiny Wings' (Garrett, 2026-10-22: "the reference for the pistol is the game tiny wings"): ONE
        /// train of smooth hills, <see cref="Height"/> high and <see cref="Wavelength"/> from crest to crest, whose heights
        /// swell and fall by <see cref="Swell"/> over <see cref="SwellWavelength"/> - runs of big hills and runs of small
        /// ones, like the game's islands, and every hill a clean slope to dive down and a clean crest to fly off. Laid along
        /// the Goby's up. (Rolls plus a fine zigzag, the shape before, left no slope a landing could be smooth on.)
        /// </summary>
        public struct Hills
        {
            public float Height, Wavelength, Swell, SwellWavelength;

            public Hills(float height, float wavelength, float swell, float swellWavelength)
            {
                Height = height; Wavelength = wavelength; Swell = swell; SwellWavelength = swellWavelength;
            }

            public bool IsFlat => Height == 0f;
        }

        /// <summary>Whole half-waves of <paramref name="wavelength"/> that fit a stretch <paramref name="length"/> long (at
        /// least one), so a hill of that scale is zero at both perches without fading: every dart, short or long, keeps
        /// its hills right up to each perch.</summary>
        public static int HalfWaves(float length, float wavelength) => Mathf.Max(1, Mathf.RoundToInt(2f * length / Mathf.Max(1f, wavelength)));

        /// <summary>The steepest the trail's hills may climb or fall, dHill/ds. Under cot(slope) the ground never folds
        /// back on the Pistol's screen.</summary>
        public const float MaxHillSlope = 1.5f;

        /// <summary>The hill at <paramref name="s"/> along a line <paramref name="length"/> long: whole half-waves
        /// (<see cref="HalfWaves"/>), so the ground meets both perches, under the swell's envelope (1 - swell at each perch,
        /// up to 1 between). A dart so short that its hills would pass <see cref="MaxHillSlope"/> gets lower hills.</summary>
        public static float Hill(float s, float length, in Hills h)
        {
            if (length <= 1e-4f || h.IsFlat || s <= 0f || s >= length) return 0f;
            float q = Mathf.PI * s / length;
            int m = HalfWaves(length, h.Wavelength), n = HalfWaves(length, h.SwellWavelength);
            return Calm(length, h, n, m) * h.Height * Envelope(h.Swell, n * q) * Mathf.Sin(m * q);
        }

        /// <summary>dHill/ds.</summary>
        public static float HillSlope(float s, float length, in Hills h)
        {
            if (length <= 1e-4f || h.IsFlat || s < 0f || s > length) return 0f;
            float k = Mathf.PI / length, q = k * s;
            int m = HalfWaves(length, h.Wavelength), n = HalfWaves(length, h.SwellWavelength);
            float dEnv = 0.5f * Mathf.Clamp01(h.Swell) * n * k * Mathf.Sin(n * q);
            return Calm(length, h, n, m) * h.Height * (dEnv * Mathf.Sin(m * q) + Envelope(h.Swell, n * q) * m * k * Mathf.Cos(m * q));
        }

        /// <summary>The swell: 1 - swell at each perch (the island starts on small hills, as Tiny Wings' do, so nothing flies
        /// off the first crest), rising to 1 between.</summary>
        static float Envelope(float swell, float phase) => 1f - 0.5f * Mathf.Clamp01(swell) * (1f + Mathf.Cos(phase));

        /// <summary>1, or less where the hills would climb steeper than <see cref="MaxHillSlope"/>.</summary>
        static float Calm(float length, in Hills h, int n, int m)
        {
            float steepest = Mathf.PI / length * Mathf.Abs(h.Height) * (m + 0.5f * Mathf.Clamp01(h.Swell) * n);
            return steepest > MaxHillSlope ? MaxHillSlope / steepest : 1f;
        }

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
