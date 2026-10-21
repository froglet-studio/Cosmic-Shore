// The slice of UnityEngine.Mathf that SilenceVignetteMask.cs calls, in float32 like Unity's.
namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)System.Math.PI;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Abs(float v) => System.Math.Abs(v);
        public static float Sqrt(float v) => (float)System.Math.Sqrt(v);
        public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);
        public static float Cos(float v) => (float)System.Math.Cos(v);
        public static float Floor(float v) => (float)System.Math.Floor(v);
    }
}
