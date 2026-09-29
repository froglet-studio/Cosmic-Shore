using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Engine stand-in for <c>UnityEngine.Random</c> — a single global, seedable xorshift128
    /// generator (the original's algorithm family) whose whole state is a value
    /// (<see cref="State"/>) so the "borrow the global RNG" pattern (save <see cref="state"/>,
    /// <see cref="InitState"/>, draw, restore) works exactly. Same contracts as the original:
    /// int <see cref="Range(int,int)"/> is max-exclusive, float <see cref="Range(float,float)"/>
    /// is max-inclusive. Main-thread only, like the original.
    /// </summary>
    public static partial class Random
    {
        [Serializable]
        public struct State
        {
            public uint s0, s1, s2, s3;
        }

        static State s_State = Seeded(Environment.TickCount);

        static State Seeded(int seed)
        {
            uint s0 = (uint)seed;
            uint s1 = s0 * 1812433253u + 1u;
            uint s2 = s1 * 1812433253u + 1u;
            uint s3 = s2 * 1812433253u + 1u;
            return new State { s0 = s0, s1 = s1, s2 = s2, s3 = s3 };
        }

        /// <summary>The full generator state (save/restore to borrow the global RNG).</summary>
        public static State state
        {
            get => s_State;
            set => s_State = value;
        }

        /// <summary>Reseeds the global state (deterministic sequence per seed).</summary>
        public static void InitState(int seed) => s_State = Seeded(seed);

        static uint NextUInt()
        {
            uint t = s_State.s0 ^ (s_State.s0 << 11);
            s_State.s0 = s_State.s1; s_State.s1 = s_State.s2; s_State.s2 = s_State.s3;
            s_State.s3 = s_State.s3 ^ (s_State.s3 >> 19) ^ t ^ (t >> 8);
            return s_State.s3;
        }

        /// <summary>Random float in [0, 1] (inclusive, like the original).</summary>
        public static float value => (NextUInt() & 0x7FFFFFu) / 8388607f;

        /// <summary>Random int in [minInclusive, maxExclusive). Returns minInclusive when the range is empty.</summary>
        public static int Range(int minInclusive, int maxExclusive)
        {
            if (minInclusive == maxExclusive) return minInclusive;
            if (minInclusive > maxExclusive)
                return maxExclusive + 1 + (int)(NextUInt() % (uint)(minInclusive - maxExclusive)); // original: swapped, (max, min]
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }

        /// <summary>Random float in [minInclusive, maxInclusive].</summary>
        public static float Range(float minInclusive, float maxInclusive)
            => minInclusive + value * (maxInclusive - minInclusive);

        /// <summary>Random point on the surface of a unit sphere (uniform — Marsaglia rejection).</summary>
        public static Vector3 onUnitSphere
        {
            get
            {
                while (true)
                {
                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                    float sqr = p.sqrMagnitude;
                    if (sqr > 1e-6f && sqr <= 1f) return p / Mathf.Sqrt(sqr);
                }
            }
        }

        /// <summary>Uniformly random rotation (axis from the unit sphere, angle in [0, 360)).</summary>
        public static Quaternion rotation
            => Quaternion.AngleAxis(Range(0f, 360f), onUnitSphere);

        /// <summary>Uniformly distributed rotation (Shoemake's method — same distribution as the original).</summary>
        public static Quaternion rotationUniform
        {
            get
            {
                float u1 = value, u2 = value * 2f * MathF.PI, u3 = value * 2f * MathF.PI;
                float a = MathF.Sqrt(1f - u1), b = MathF.Sqrt(u1);
                return new Quaternion(a * MathF.Sin(u2), a * MathF.Cos(u2), b * MathF.Sin(u3), b * MathF.Cos(u3));
            }
        }

        /// <summary>Random point inside (or on) a unit sphere.</summary>
        public static Vector3 insideUnitSphere
        {
            get
            {
                while (true)
                {
                    var p = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                    if (p.sqrMagnitude <= 1f) return p;
                }
            }
        }

        /// <summary>Random point inside (or on) a unit circle.</summary>
        public static Vector2 insideUnitCircle
        {
            get
            {
                while (true)
                {
                    var p = new Vector2(Range(-1f, 1f), Range(-1f, 1f));
                    if (p.sqrMagnitude <= 1f) return p;
                }
            }
        }

        public static Color ColorHSV() => ColorHSV(0f, 1f, 0f, 1f, 0f, 1f, 1f, 1f);
        public static Color ColorHSV(float hueMin, float hueMax) => ColorHSV(hueMin, hueMax, 0f, 1f, 0f, 1f, 1f, 1f);
        public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax)
            => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, 0f, 1f, 1f, 1f);
        public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax)
            => ColorHSV(hueMin, hueMax, saturationMin, saturationMax, valueMin, valueMax, 1f, 1f);
        public static Color ColorHSV(float hueMin, float hueMax, float saturationMin, float saturationMax, float valueMin, float valueMax, float alphaMin, float alphaMax)
        {
            var c = Color.HSVToRGB(Mathf.Lerp(hueMin, hueMax, value), Mathf.Lerp(saturationMin, saturationMax, value), Mathf.Lerp(valueMin, valueMax, value), true);
            c.a = Mathf.Lerp(alphaMin, alphaMax, value);
            return c;
        }
    }
}
