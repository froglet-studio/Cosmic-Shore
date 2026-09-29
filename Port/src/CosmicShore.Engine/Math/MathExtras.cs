using System;

namespace CosmicShore.Engine
{
    public partial struct Color
    {
        /// <summary>sRGB → linear conversion of the RGB channels (original: Color.linear).</summary>
        public Color linear => new(Mathf.GammaToLinearSpace(r), Mathf.GammaToLinearSpace(g), Mathf.GammaToLinearSpace(b), a);

        /// <summary>Linear → sRGB conversion of the RGB channels (original: Color.gamma).</summary>
        public Color gamma => new(Mathf.LinearToGammaSpace(r), Mathf.LinearToGammaSpace(g), Mathf.LinearToGammaSpace(b), a);

        public float maxColorComponent => MathF.Max(MathF.Max(r, g), b);
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;

        public static Color LerpUnclamped(Color a, Color b, float t)
            => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);

        public static Color operator /(Color a, float d) => new(a.r / d, a.g / d, a.b / d, a.a / d);

        public float this[int index]
        {
            get => index switch { 0 => r, 1 => g, 2 => b, 3 => a, _ => throw new IndexOutOfRangeException() };
            set { switch (index) { case 0: r = value; break; case 1: g = value; break; case 2: b = value; break; case 3: a = value; break; default: throw new IndexOutOfRangeException(); } }
        }

        public static void RGBToHSV(Color rgb, out float H, out float S, out float V)
        {
            float max = MathF.Max(rgb.r, MathF.Max(rgb.g, rgb.b)), min = MathF.Min(rgb.r, MathF.Min(rgb.g, rgb.b));
            V = max;
            float d = max - min;
            S = max > 0f ? d / max : 0f;
            if (d <= 0f) { H = 0f; return; }
            float h;
            if (max == rgb.r) h = (rgb.g - rgb.b) / d + (rgb.g < rgb.b ? 6f : 0f);
            else if (max == rgb.g) h = (rgb.b - rgb.r) / d + 2f;
            else h = (rgb.r - rgb.g) / d + 4f;
            H = h / 6f;
        }

        public static Color HSVToRGB(float H, float S, float V) => HSVToRGB(H, S, V, true);

        public static Color HSVToRGB(float H, float S, float V, bool hdr)
        {
            if (S <= 0f) return new Color(V, V, V, 1f);
            H = Mathf.Repeat(H, 1f) * 6f;
            int i = (int)MathF.Floor(H);
            float f = H - i, p = V * (1f - S), q = V * (1f - S * f), t = V * (1f - S * (1f - f));
            var c = (i % 6) switch
            {
                0 => new Color(V, t, p, 1f), 1 => new Color(q, V, p, 1f), 2 => new Color(p, V, t, 1f),
                3 => new Color(p, q, V, 1f), 4 => new Color(t, p, V, 1f), _ => new Color(V, p, q, 1f),
            };
            if (!hdr) { c.r = Mathf.Clamp01(c.r); c.g = Mathf.Clamp01(c.g); c.b = Mathf.Clamp01(c.b); }
            return c;
        }
    }

    public partial struct Vector2
    {
        public static float SqrMagnitude(Vector2 v) => v.x * v.x + v.y * v.y;
        public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new(a.x / b.x, a.y / b.y);
        public static implicit operator Vector4(Vector2 v) => new(v.x, v.y, 0f, 0f);
        public static implicit operator Vector2(Vector4 v) => new(v.x, v.y);
    }

    public partial struct Vector4
    {
        public static float SqrMagnitude(Vector4 v) => v.x * v.x + v.y * v.y + v.z * v.z + v.w * v.w;
    }

    public partial struct Quaternion
    {
        /// <summary>Decompose into an angle (degrees) and a unit axis (original contract).</summary>
        public void ToAngleAxis(out float angle, out Vector3 axis)
        {
            var q = normalized;
            if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            float s = MathF.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z);
            angle = 2f * MathF.Atan2(s, q.w) * Mathf.Rad2Deg;
            axis = s > 1e-8f ? new Vector3(q.x / s, q.y / s, q.z / s) : Vector3.right;
        }

        public void SetLookRotation(Vector3 view) => this = LookRotation(view);
        public void SetLookRotation(Vector3 view, Vector3 up) => this = LookRotation(view, up);
        public void SetFromToRotation(Vector3 fromDirection, Vector3 toDirection) => this = FromToRotation(fromDirection, toDirection);
        public void Set(float newX, float newY, float newZ, float newW) { x = newX; y = newY; z = newZ; w = newW; }
        public static Quaternion Normalize(Quaternion q) => q.normalized;
        public static Quaternion EulerAngles(Vector3 euler) => Euler(euler * Mathf.Rad2Deg);
        public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            if (Dot(a, b) < 0f) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
        }

        public float this[int index]
        {
            get => index switch { 0 => x, 1 => y, 2 => z, 3 => w, _ => throw new IndexOutOfRangeException() };
            set { switch (index) { case 0: x = value; break; case 1: y = value; break; case 2: z = value; break; case 3: w = value; break; default: throw new IndexOutOfRangeException(); } }
        }
    }

    public partial struct Keyframe
    {
        public WeightedMode weightedMode;
        public float inWeight;
        public float outWeight;

        public Keyframe(float time, float value, float inTangent, float outTangent, float inWeight, float outWeight)
            : this(time, value, inTangent, outTangent)
        {
            this.inWeight = inWeight;
            this.outWeight = outWeight;
            weightedMode = WeightedMode.None;
        }
    }

    public enum WeightedMode { None = 0, In = 1, Out = 2, Both = 3 }

    public static partial class Mathf
    {
        /// <summary>sRGB transfer → linear (exact piecewise curve).</summary>
        public static float GammaToLinearSpace(float value)
            => value <= 0.04045f ? value / 12.92f : MathF.Pow((value + 0.055f) / 1.055f, 2.4f);

        public static float LinearToGammaSpace(float value)
            => value <= 0.0031308f ? value * 12.92f : 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;

        public static bool IsPowerOfTwo(int value) => value > 0 && (value & (value - 1)) == 0;
        public static int ClosestPowerOfTwo(int value)
        {
            int next = NextPowerOfTwo(value), prev = next >> 1;
            return value - prev < next - value ? prev : next;
        }

        public static float CorrelatedColorTemperatureToRGBChannel(float t) => 1f;
        public static ushort FloatToHalf(float val) => BitConverter.HalfToUInt16Bits((Half)val);
        public static float HalfToFloat(ushort val) => (float)BitConverter.UInt16BitsToHalf(val);
    }
}
