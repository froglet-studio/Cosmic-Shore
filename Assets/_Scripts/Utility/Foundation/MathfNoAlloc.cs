namespace CosmicShore.Utility
{
    /// <summary>
    /// Three-value <c>Min</c>/<c>Max</c> without Unity's hidden allocation. <c>UnityEngine.Mathf</c> has
    /// no three-argument overload, so <c>Mathf.Max(a, b, c)</c> binds to <c>Max(params float[])</c> and
    /// allocates a <c>float[]</c> on EVERY call. In the Skim Race AI's planner that was 840 allocations
    /// (36 KB) per frame with two AI seats, half the frame's garbage (measured 2026-10-06,
    /// <c>Docs/SKIM_RACE_AI.md</c> §8.0f).
    ///
    /// Same answer as the params form bit for bit, NaN and ties included: start from the first value
    /// and take a later one only when it is strictly greater (smaller) - Unity's own loop.
    /// <c>Tools/Build/check_mathf_params_alloc.py</c> keeps the params form out of runtime code.
    /// </summary>
    public static class MathfNoAlloc
    {
        public static float Max(float a, float b, float c)
        {
            float m = a;
            if (b > m) m = b;
            if (c > m) m = c;
            return m;
        }

        public static float Min(float a, float b, float c)
        {
            float m = a;
            if (b < m) m = b;
            if (c < m) m = c;
            return m;
        }
    }
}
