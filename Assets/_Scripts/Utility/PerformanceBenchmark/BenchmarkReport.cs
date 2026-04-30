using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Complete result of a single benchmark run. Serializable to JSON for
    /// persistent storage and cross-commit comparison.
    /// </summary>
    [Serializable]
    public class BenchmarkReport
    {
        // ── Identity ────────────────────────────────────
        public string reportId;
        public string label;
        public string timestamp;
        public string sceneName;

        // ── Environment ─────────────────────────────────
        public string gitCommitHash;
        public string gitBranch;
        public string deviceModel;
        public string operatingSystem;
        public string graphicsDeviceName;
        public int graphicsMemorySize;
        public int systemMemorySize;
        public int processorCount;
        public string processorType;
        public int screenWidth;
        public int screenHeight;
        public int targetFrameRate;
        public int vSyncCount;
        public string qualityLevel;

        // ── Config ──────────────────────────────────────
        public float warmupDuration;
        public float sampleDuration;

        // ── Data ────────────────────────────────────────
        public List<FrameSnapshot> snapshots = new();
        public BenchmarkStatistics statistics;

        public void PopulateEnvironment()
        {
            reportId = Guid.NewGuid().ToString("N")[..12];
            timestamp = DateTime.UtcNow.ToString("o");
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            deviceModel = SystemInfo.deviceModel;
            operatingSystem = SystemInfo.operatingSystem;
            graphicsDeviceName = SystemInfo.graphicsDeviceName;
            graphicsMemorySize = SystemInfo.graphicsMemorySize;
            systemMemorySize = SystemInfo.systemMemorySize;
            processorCount = SystemInfo.processorCount;
            processorType = SystemInfo.processorType;
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            targetFrameRate = Application.targetFrameRate;
            vSyncCount = QualitySettings.vSyncCount;
            qualityLevel = QualitySettings.names[QualitySettings.GetQualityLevel()];

            PopulateGitInfo();
        }

        void PopulateGitInfo()
        {
            gitCommitHash = TryRunGit("rev-parse --short HEAD");
            gitBranch = TryRunGit("rev-parse --abbrev-ref HEAD");
        }

        static string TryRunGit(string arguments)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(2000);
                return process.ExitCode == 0 ? output : "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        public void ComputeStatistics()
        {
            statistics = BenchmarkStatistics.Compute(snapshots, sampleDuration);
        }

        /// <summary>
        /// Serializes the report to JSON and writes to disk.
        /// Returns the full file path.
        /// </summary>
        public string SaveToFile(string outputFolder)
        {
            string dir = Path.Combine(Application.persistentDataPath, outputFolder);
            Directory.CreateDirectory(dir);

            string safeName = string.IsNullOrEmpty(label) ? "benchmark" : SanitizeFileName(label);
            string fileName = $"{safeName}_{timestamp.Replace(":", "-").Replace(".", "-")}_{reportId}.json";
            string filePath = Path.Combine(dir, fileName);

            string json = JsonUtility.ToJson(this, true);
            File.WriteAllText(filePath, json);

            return filePath;
        }

        public static BenchmarkReport LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            string json = File.ReadAllText(filePath);
            return JsonUtility.FromJson<BenchmarkReport>(json);
        }

        static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
