using System;

namespace CosmicShore.Engine
{
    /// <summary>4-component float vector (shader params, homogeneous coords).</summary>
    [Serializable]
    public struct Vector4 : IEquatable<Vector4>
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Vector4 zero => new(0f, 0f, 0f, 0f);
        public static Vector4 one => new(1f, 1f, 1f, 1f);

        public float magnitude => MathF.Sqrt(x * x + y * y + z * z + w * w);
        public float sqrMagnitude => x * x + y * y + z * z + w * w;

        public Vector4 normalized
        {
            get
            {
                float mag = magnitude;
                return mag > 1E-05f ? this / mag : zero;
            }
        }

        public static float Dot(Vector4 a, Vector4 b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector4(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
        }

        public static Vector4 operator +(Vector4 a, Vector4 b) => new(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
        public static Vector4 operator -(Vector4 a, Vector4 b) => new(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
        public static Vector4 operator *(Vector4 a, float d) => new(a.x * d, a.y * d, a.z * d, a.w * d);
        public static Vector4 operator /(Vector4 a, float d) => new(a.x / d, a.y / d, a.z / d, a.w / d);

        public static bool operator ==(Vector4 lhs, Vector4 rhs) => (lhs - rhs).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector4 lhs, Vector4 rhs) => !(lhs == rhs);

        public static implicit operator Vector4(Vector3 v) => new(v.x, v.y, v.z, 0f);
        public static implicit operator Vector3(Vector4 v) => new(v.x, v.y, v.z);

        public bool Equals(Vector4 other) => x == other.x && y == other.y && z == other.z && w == other.w;
        public override bool Equals(object obj) => obj is Vector4 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z, w);
        public override string ToString() => $"({x:F2}, {y:F2}, {z:F2}, {w:F2})";
    }
}
