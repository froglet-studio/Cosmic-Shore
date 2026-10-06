using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// RGBA color with 8-bit-per-channel byte components, matching UnityEngine.Color32.
    /// Implicitly converts to/from <see cref="Color"/> (float [0,1] ↔ byte [0,255]), which is
    /// how ported code reaches it (e.g. packing an accent colour into a material-cache key).
    /// </summary>
    [Serializable]
    public struct Color32 : IEquatable<Color32>
    {
        public byte r;
        public byte g;
        public byte b;
        public byte a;

        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }

        static byte ToByte(float v) => (byte)(Mathf.Clamp01(v) * 255f + 0.5f);

        public static implicit operator Color32(Color c) => new(ToByte(c.r), ToByte(c.g), ToByte(c.b), ToByte(c.a));
        public static implicit operator Color(Color32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        public bool Equals(Color32 other) => r == other.r && g == other.g && b == other.b && a == other.a;
        public override bool Equals(object obj) => obj is Color32 o && Equals(o);
        public override int GetHashCode() => (r << 24) | (g << 16) | (b << 8) | a;
        public override string ToString() => $"RGBA({r}, {g}, {b}, {a})";
    }
}
