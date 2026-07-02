using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Float math utilities with the API surface ported code expects.
    /// Semantics match the original engine behavior (Approximately epsilon scaling,
    /// Repeat/PingPong wrapping, SmoothDamp critically-damped spring).
    /// </summary>
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = 1.17549435E-38f;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;

        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int i) => Math.Abs(i);

        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;

        public static float Min(params float[] values)
        {
            if (values.Length == 0) return 0f;
            float m = values[0];
            for (int i = 1; i < values.Length; i++) if (values[i] < m) m = values[i];
            return m;
        }

        public static float Max(params float[] values)
        {
            if (values.Length == 0) return 0f;
            float m = values[0];
            for (int i = 1; i < values.Length; i++) if (values[i] > m) m = values[i];
            return m;
        }

        public static float Sqrt(float f) => MathF.Sqrt(f);
        public static float Pow(float f, float p) => MathF.Pow(f, p);
        public static float Exp(float power) => MathF.Exp(power);
        public static float Log(float f) => MathF.Log(f);
        public static float Log(float f, float p) => MathF.Log(f, p);
        public static float Log10(float f) => MathF.Log10(f);

        public static float Sin(float f) => MathF.Sin(f);
        public static float Cos(float f) => MathF.Cos(f);
        public static float Tan(float f) => MathF.Tan(f);
        public static float Asin(float f) => MathF.Asin(f);
        public static float Acos(float f) => MathF.Acos(f);
        public static float Atan(float f) => MathF.Atan(f);
        public static float Atan2(float y, float x) => MathF.Atan2(y, x);

        public static float Ceil(float f) => MathF.Ceiling(f);
        public static float Floor(float f) => MathF.Floor(f);
        public static float Round(float f) => MathF.Round(f, MidpointRounding.ToEven);
        public static int CeilToInt(float f) => (int)MathF.Ceiling(f);
        public static int FloorToInt(float f) => (int)MathF.Floor(f);
        public static int RoundToInt(float f) => (int)MathF.Round(f, MidpointRounding.ToEven);

        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        /// <summary>Smallest power of two ≥ <paramref name="value"/> (0 → 0; original engine contract).</summary>
        public static int NextPowerOfTwo(int value)
        {
            if (value <= 0) return 0;
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }

        public static float Clamp(float value, float min, float max)
            => value < min ? min : value > max ? max : value;

        public static int Clamp(int value, int min, int max)
            => value < min ? min : value > max ? max : value;

        public static float Clamp01(float value)
            => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

        public static float LerpAngle(float a, float b, float t)
        {
            float delta = Repeat(b - a, 360f);
            if (delta > 180f) delta -= 360f;
            return a + delta * Clamp01(t);
        }

        public static float InverseLerp(float a, float b, float value)
            => a != b ? Clamp01((value - a) / (b - a)) : 0f;

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Abs(target - current) <= maxDelta) return target;
            return current + Sign(target - current) * maxDelta;
        }

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float deltaAngle = DeltaAngle(current, target);
            if (-maxDelta < deltaAngle && deltaAngle < maxDelta) return target;
            return MoveTowards(current, current + deltaAngle, maxDelta);
        }

        public static float Repeat(float t, float length)
            => Clamp(t - Floor(t / length) * length, 0f, length);

        public static float PingPong(float t, float length)
        {
            t = Repeat(t, length * 2f);
            return length - Abs(t - length);
        }

        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }

        public static bool Approximately(float a, float b)
            => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }

        /// <summary>
        /// Critically damped spring smoothing (Game Programming Gems 4, ch. 1.10) —
        /// matches the reference engine implementation including overshoot clamping.
        /// </summary>
        public static float SmoothDamp(float current, float target, ref float currentVelocity,
            float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;

            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;

            float maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            target = current - change;

            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;

            if (originalTo - current > 0f == output > originalTo)
            {
                output = originalTo;
                currentVelocity = (output - originalTo) / deltaTime;
            }

            return output;
        }

        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime)
            => SmoothDamp(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime);

        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed)
            => SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);

        public static float SmoothDampAngle(float current, float target, ref float currentVelocity,
            float smoothTime, float maxSpeed, float deltaTime)
        {
            target = current + DeltaAngle(current, target);
            return SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, deltaTime);
        }

        public static float SmoothDampAngle(float current, float target, ref float currentVelocity, float smoothTime)
            => SmoothDampAngle(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime);

        // ── Perlin noise (original-contract: deterministic 2D gradient noise, ~[0,1]) ──
        // Classic Perlin with the reference permutation table. Matches the original
        // engine's usage profile (smooth, deterministic across runs) — consumers
        // (camera shake, drift wobble) only need smooth continuity, not bit-identical
        // values to the original implementation.

        static readonly int[] Perm = BuildPerm();

        static int[] BuildPerm()
        {
            // Ken Perlin's reference permutation, doubled to avoid index wrapping.
            int[] p =
            {
                151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,
                190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,88,237,149,56,87,174,20,
                125,136,171,168,68,175,74,165,71,134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,
                105,92,41,55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,18,169,200,196,
                135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,250,124,123,5,202,38,147,118,126,255,
                82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,
                153,101,155,167,43,172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,228,
                251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,107,49,192,214,31,181,199,106,
                157,184,84,204,176,115,121,50,45,127,4,150,254,138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,
                66,215,61,156,180
            };
            var perm = new int[512];
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
            return perm;
        }

        static float PerlinFade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        static float PerlinGrad(int hash, float x, float y)
        {
            // 8 gradient directions over the 2D lattice.
            switch (hash & 7)
            {
                case 0: return x + y;
                case 1: return x - y;
                case 2: return -x + y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>2D Perlin noise, deterministic, output ≈ [0, 1] (clamped).</summary>
        public static float PerlinNoise(float x, float y)
        {
            int xi = FloorToInt(x) & 255;
            int yi = FloorToInt(y) & 255;
            float xf = x - FloorToInt(x);
            float yf = y - FloorToInt(y);

            float u = PerlinFade(xf);
            float v = PerlinFade(yf);

            int aa = Perm[Perm[xi] + yi];
            int ab = Perm[Perm[xi] + yi + 1];
            int ba = Perm[Perm[xi + 1] + yi];
            int bb = Perm[Perm[xi + 1] + yi + 1];

            float x1 = Lerp(PerlinGrad(aa, xf, yf), PerlinGrad(ba, xf - 1f, yf), u);
            float x2 = Lerp(PerlinGrad(ab, xf, yf - 1f), PerlinGrad(bb, xf - 1f, yf - 1f), u);
            float value = Lerp(x1, x2, v);

            // Map from ~[-1, 1] to [0, 1] like the original contract; clamp for safety.
            return Clamp01(value * 0.5f + 0.5f);
        }
    }
}
