using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One stretch of the Pistol's world: the straight dart from one Goby perch to the next, and the FLAT plane through it
    /// that the Pistol plays in (Garrett, 2026-10-22: "the pistol's plane ... no longer needs to bend at all"). The plane
    /// holds the dart's line <see cref="S"/> and the Goby's up at the dart (<see cref="N"/>, made square to the line).
    /// The ground the Pistol rides is the dart's own trail: the line with hills along <see cref="N"/>
    /// (<see cref="Hill"/>), which come to nothing at both perches so the ground meets each one.
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
        public Vector3 Ground(float s, float amplitude, float wavelength) => A + S * s + N * PistolPlane.Hill(s, L, amplitude, wavelength);
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
            public float L, Cos, Sin, Amp, Wave;
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

        /// <summary>The hill at <paramref name="s"/> along a line <paramref name="length"/> long: a wave of
        /// <paramref name="wavelength"/>, faded to zero at both ends so the ground meets both perches.</summary>
        public static float Hill(float s, float length, float amplitude, float wavelength)
        {
            if (length <= 1e-4f || amplitude == 0f) return 0f;
            float w = Mathf.Max(1f, wavelength);
            return amplitude * Mathf.Sin(2f * Mathf.PI * s / w) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(s / length));
        }

        /// <summary>dHill/ds.</summary>
        public static float HillSlope(float s, float length, float amplitude, float wavelength)
        {
            if (length <= 1e-4f || amplitude == 0f) return 0f;
            float w = Mathf.Max(1f, wavelength), k = 2f * Mathf.PI / w, u = Mathf.Clamp01(s / length), q = Mathf.PI / length;
            float slope = Mathf.Cos(k * s) * k * Mathf.Sin(Mathf.PI * u);
            if (s > 0f && s < length) slope += Mathf.Sin(k * s) * Mathf.Cos(Mathf.PI * u) * q;
            return amplitude * slope;
        }

        /// <summary>The frame for riding <paramref name="seg"/> forward (<paramref name="dir"/> +1, from A to B) or back
        /// (-1, from B to A), leaning the travel <paramref name="slopeDegrees"/> downhill.</summary>
        public static Frame For(in PlaneSegment seg, int dir, float slopeDegrees, float amplitude, float wavelength)
        {
            float a = Mathf.Clamp(slopeDegrees, 0f, 75f) * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            Vector3 t = dir >= 0 ? seg.S : -seg.S;
            var f = new Frame
            {
                Dir = dir >= 0 ? 1 : -1, Seg = seg, L = seg.L, Cos = c, Sin = sn, Amp = amplitude, Wave = wavelength,
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
            // x = u cos + h(u) sin: solve for u (the hills' slope times tan(angle) stays well under 1, so this converges)
            float u = x / Mathf.Max(1e-4f, f.Cos);
            for (int i = 0; i < 6; i++)
                u = (x - HillAt(f, u) * f.Sin) / Mathf.Max(1e-4f, f.Cos);
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
            return Hill(LineS(f, u), f.L, f.Amp, f.Wave);
        }

        static float HillSlopeAt(in Frame f, float u)
        {
            if (u <= 0f || u >= f.L) return 0f;
            return HillSlope(LineS(f, u), f.L, f.Amp, f.Wave) * f.Dir;
        }
    }
}
