namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Single source of truth for the A–F health grade derived from a run's statistics.
    /// Runtime-safe so both the editor window and the multi-scene sweep grade identically.
    /// </summary>
    public static class BenchmarkGrade
    {
        public static string Evaluate(BenchmarkStatistics s, out string explanation)
        {
            if (s == null || s.totalFrames == 0)
            {
                explanation = "No data captured";
                return "-";
            }

            if (s.avgFps >= 55 && s.p99FrameTimeMs < 25 && s.stdDevFrameTimeMs < 5)
            {
                explanation = "Excellent - smooth and stable";
                return "A";
            }
            if (s.avgFps >= 45 && s.p99FrameTimeMs < 35)
            {
                explanation = "Good - playable with minor hitches";
                return "B";
            }
            if (s.avgFps >= 30 && s.p99FrameTimeMs < 50)
            {
                explanation = "Acceptable - noticeable frame drops";
                return "C";
            }
            if (s.avgFps >= 20)
            {
                explanation = "Poor - frequent stutters, needs optimization";
                return "D";
            }

            explanation = "Critical - not playable";
            return "F";
        }

        public static string Evaluate(BenchmarkStatistics s) => Evaluate(s, out _);
    }
}
