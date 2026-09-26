// A Unity shim: just enough of UnityEngine for the two PURE course files to compile and RUN
// outside the editor. Nothing here is a reimplementation of gameplay - Vector3 and Mathf are
// specified arithmetic, and the whole point is that the SHIPPED WaystationCourse.cs is the code
// under test rather than a transcription of it.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized
        {
            get { float m = magnitude; return m > 1e-9f ? new Vector3(x / m, y / m, z / m) : zero; }
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => a * s;
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        public override string ToString() => $"({x:R},{y:R},{z:R})";
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Infinity = float.PositiveInfinity;

        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Asin(float v) => (float)Math.Asin(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Abs(float v) => Math.Abs(v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
    }
}
