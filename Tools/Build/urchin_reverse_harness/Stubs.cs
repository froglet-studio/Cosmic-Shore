// REAL math (not the type-check stubs): MinimumThrottleBrake is pure arithmetic over Mathf,
// so a faithful Mathf lets the SHIPPED file be executed rather than transcribed.
namespace UnityEngine
{
    public static class Mathf
    {
        public static float Abs(float f) => f < 0 ? -f : f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);   // Unity clamps t
        public static float MoveTowards(float a, float b, float d)
            => Abs(b - a) <= d ? b : a + (b > a ? d : -d);
        public static bool Approximately(float a, float b)
            => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), 1.1E-44f * 8f);
    }
}
