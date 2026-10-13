// The slice of UnityEngine that DepthRatchet, CrystalPoseJitter and their suite touch: value-type maths only.
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 up => new(0f, 1f, 0f);
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);
        public override string ToString() => $"({x:F4}, {y:F4}, {z:F4})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new(0f, 0f, 0f, 1f);
        public static Quaternion Inverse(Quaternion q) => new(-q.x, -q.y, -q.z, q.w);
        // Unity's q * v: rotate v by the unit quaternion q.
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var u = new Vector3(q.x, q.y, q.z);
            return u * (2f * Vector3.Dot(u, v)) + v * (q.w * q.w - Vector3.Dot(u, u)) + Vector3.Cross(u, v) * (2f * q.w);
        }
    }
}
