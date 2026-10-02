// UnityEngine stubs, transcribed against exactly the members FloraElementalForm.cs and
// FloraReproductionRules.cs use. A harness, not Unity: every member here is float
// arithmetic matching Unity's own (Mathf is a thin wrapper over System.Math with a float
// cast), so a value the harness prints is the value the engine computes.
using System;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0f, 0f, 0f);
        public static Vector3 one => new(1f, 1f, 1f);
        public override string ToString() => $"({x}, {y}, {z})";
    }

    public static class Mathf
    {
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        // Unity's own tolerance, verbatim.
        public static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)),
                                       1.121173e-44f * 8f);
    }
}
