// UnityEngine stubs, transcribed against exactly the members MandelbulbSurface.cs uses.
// A harness, not Unity. Everything here is float arithmetic that matches Unity's own
// (Mathf is a thin wrapper over System.Math with a float cast), so a value the harness
// prints is the value the engine computes.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }

        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public static Vector3 up => new(0f, 1f, 0f);
        public static Vector3 right => new(1f, 0f, 0f);
        public static Vector3 forward => new(0f, 0f, 1f);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new(a.x / s, a.y / s, a.z / s);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized
        {
            get { float m = magnitude; return m > 1e-9f ? new Vector3(x / m, y / m, z / m) : zero; }
        }

        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }
        public override string ToString() => $"({x:R}, {y:R}, {z:R})";
    }

    public static class Mathf
    {
        public const float PI = 3.14159265358979f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;

        public static float Abs(float v) => Math.Abs(v);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Log(float v) => (float)Math.Log(v);
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
    }
}
