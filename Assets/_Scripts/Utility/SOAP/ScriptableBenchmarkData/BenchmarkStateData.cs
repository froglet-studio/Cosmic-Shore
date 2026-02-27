using UnityEngine;

namespace CosmicShore.Soap
{
    /// <summary>
    /// Immutable snapshot of benchmark progress/results broadcast via SOAP events.
    /// Lightweight payload — carries the key summary, not raw frame data.
    /// </summary>
    [System.Serializable]
    public struct BenchmarkStateData
    {
        [SerializeField] private string label;
        [SerializeField] private string sceneName;
        [SerializeField] private string gitCommitHash;
        [SerializeField] private float progress;
        [SerializeField] private int framesCaptured;
        [SerializeField] private float avgFps;
        [SerializeField] private float avgFrameTimeMs;
        [SerializeField] private float p99FrameTimeMs;
        [SerializeField] private string reportFilePath;

        public string Label => label;
        public string SceneName => sceneName;
        public string GitCommitHash => gitCommitHash;
        public float Progress => progress;
        public int FramesCaptured => framesCaptured;
        public float AvgFps => avgFps;
        public float AvgFrameTimeMs => avgFrameTimeMs;
        public float P99FrameTimeMs => p99FrameTimeMs;
        public string ReportFilePath => reportFilePath;

        public BenchmarkStateData(
            string label, string sceneName, string gitCommitHash,
            float progress, int framesCaptured,
            float avgFps, float avgFrameTimeMs, float p99FrameTimeMs,
            string reportFilePath)
        {
            this.label = label;
            this.sceneName = sceneName;
            this.gitCommitHash = gitCommitHash;
            this.progress = progress;
            this.framesCaptured = framesCaptured;
            this.avgFps = avgFps;
            this.avgFrameTimeMs = avgFrameTimeMs;
            this.p99FrameTimeMs = p99FrameTimeMs;
            this.reportFilePath = reportFilePath;
        }

        public override bool Equals(object obj)
        {
            if (obj is not BenchmarkStateData other) return false;
            return reportFilePath == other.reportFilePath;
        }

        public override int GetHashCode() => reportFilePath?.GetHashCode() ?? 0;
    }
}
