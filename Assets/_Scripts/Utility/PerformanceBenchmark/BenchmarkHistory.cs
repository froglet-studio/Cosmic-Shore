using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Manages the on-disk history of benchmark reports. Provides indexed access,
    /// tagging, and quick-lookup so every run is automatically saved and retrievable
    /// for comparison.
    ///
    /// Reports are stored as individual JSON files in Application.persistentDataPath/{folder}.
    /// A lightweight index file (benchmark_index.json) caches metadata for fast listing
    /// without deserializing every report.
    /// </summary>
    public static class BenchmarkHistory
    {
        const string IndexFileName = "benchmark_index.json";

        [Serializable]
        public class IndexEntry
        {
            public string reportId;
            public string filePath;
            public string label;
            public string sceneName;
            public string gitBranch;
            public string gitCommitHash;
            public string timestamp;
            public string tag;
            public float avgFps;
            public float p1Fps;
            public float avgFrameTimeMs;
            public float p99FrameTimeMs;
            public int totalFrames;
            public string deviceModel;
        }

        [Serializable]
        class IndexFile
        {
            public List<IndexEntry> entries = new();
        }

        /// <summary>
        /// Adds a completed report to the history index. Call this after SaveToFile.
        /// </summary>
        public static IndexEntry AddToHistory(BenchmarkReport report, string filePath, string folder)
        {
            var index = LoadIndex(folder);

            // Prevent duplicates
            if (index.entries.Any(e => e.reportId == report.reportId))
                return index.entries.First(e => e.reportId == report.reportId);

            var entry = new IndexEntry
            {
                reportId = report.reportId,
                filePath = filePath,
                label = report.label,
                sceneName = report.sceneName,
                gitBranch = report.gitBranch,
                gitCommitHash = report.gitCommitHash,
                timestamp = report.timestamp,
                tag = "",
                deviceModel = report.deviceModel,
                avgFps = report.statistics?.avgFps ?? 0,
                p1Fps = report.statistics?.p1Fps ?? 0,
                avgFrameTimeMs = report.statistics?.avgFrameTimeMs ?? 0,
                p99FrameTimeMs = report.statistics?.p99FrameTimeMs ?? 0,
                totalFrames = report.statistics?.totalFrames ?? 0,
            };

            index.entries.Insert(0, entry); // newest first
            SaveIndex(index, folder);
            return entry;
        }

        /// <summary>
        /// Tag a report for easy identification (e.g., "baseline", "pre-optimization", "GDC-build").
        /// </summary>
        public static void TagReport(string reportId, string tag, string folder)
        {
            var index = LoadIndex(folder);
            var entry = index.entries.FirstOrDefault(e => e.reportId == reportId);
            if (entry != null)
            {
                entry.tag = tag;
                SaveIndex(index, folder);
            }
        }

        /// <summary>Returns all indexed entries, newest first.</summary>
        public static List<IndexEntry> GetAll(string folder)
        {
            return LoadIndex(folder).entries;
        }

        /// <summary>Returns entries matching a specific tag.</summary>
        public static List<IndexEntry> GetByTag(string tag, string folder)
        {
            return LoadIndex(folder).entries.Where(e => e.tag == tag).ToList();
        }

        /// <summary>Returns the most recent entry, or null.</summary>
        public static IndexEntry GetLatest(string folder)
        {
            var entries = LoadIndex(folder).entries;
            return entries.Count > 0 ? entries[0] : null;
        }

        /// <summary>Returns entries for a specific scene.</summary>
        public static List<IndexEntry> GetByScene(string sceneName, string folder)
        {
            return LoadIndex(folder).entries
                .Where(e => e.sceneName == sceneName)
                .ToList();
        }

        /// <summary>Loads the full report from disk for a given index entry.</summary>
        public static BenchmarkReport LoadReport(IndexEntry entry)
        {
            return BenchmarkReport.LoadFromFile(entry.filePath);
        }

        /// <summary>
        /// Removes an entry from the index and optionally deletes the JSON file.
        /// </summary>
        public static void RemoveEntry(string reportId, string folder, bool deleteFile = true)
        {
            var index = LoadIndex(folder);
            var entry = index.entries.FirstOrDefault(e => e.reportId == reportId);
            if (entry == null) return;

            if (deleteFile && File.Exists(entry.filePath))
                File.Delete(entry.filePath);

            index.entries.Remove(entry);
            SaveIndex(index, folder);
        }

        /// <summary>
        /// Rebuilds the index by scanning the folder for JSON report files.
        /// Useful if files were manually added/removed or the index got corrupted.
        /// </summary>
        public static int RebuildIndex(string folder)
        {
            string dir = Path.Combine(Application.persistentDataPath, folder);
            if (!Directory.Exists(dir)) return 0;

            var index = new IndexFile();
            var files = Directory.GetFiles(dir, "*.json")
                .Where(f => Path.GetFileName(f) != IndexFileName)
                .OrderByDescending(File.GetLastWriteTimeUtc);

            foreach (var file in files)
            {
                try
                {
                    var report = BenchmarkReport.LoadFromFile(file);
                    if (report == null) continue;

                    index.entries.Add(new IndexEntry
                    {
                        reportId = report.reportId ?? Path.GetFileNameWithoutExtension(file),
                        filePath = file,
                        label = report.label,
                        sceneName = report.sceneName,
                        gitBranch = report.gitBranch,
                        gitCommitHash = report.gitCommitHash,
                        timestamp = report.timestamp,
                        tag = "",
                        deviceModel = report.deviceModel,
                        avgFps = report.statistics?.avgFps ?? 0,
                        p1Fps = report.statistics?.p1Fps ?? 0,
                        avgFrameTimeMs = report.statistics?.avgFrameTimeMs ?? 0,
                        p99FrameTimeMs = report.statistics?.p99FrameTimeMs ?? 0,
                        totalFrames = report.statistics?.totalFrames ?? 0,
                    });
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[BenchmarkHistory] Skipping {file}: {e.Message}");
                }
            }

            SaveIndex(index, folder);
            return index.entries.Count;
        }

        /// <summary>
        /// Generates a quick text summary comparing the last N runs for a scene.
        /// Useful for spotting trends at a glance.
        /// </summary>
        public static string GetTrendSummary(string sceneName, string folder, int count = 5)
        {
            var entries = GetByScene(sceneName, folder);
            if (entries.Count == 0) return "No benchmark data for this scene.";

            var recent = entries.Take(count).ToList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Performance trend for '{sceneName}' (last {recent.Count} runs):");
            sb.AppendLine($"{"Date",-22} {"Label",-16} {"Avg FPS",8} {"P1 FPS",8} {"Avg ms",8} {"P99 ms",8} {"Frames",7}  Tag");
            sb.AppendLine(new string('─', 100));

            foreach (var e in recent)
            {
                string date = e.timestamp?.Length > 19 ? e.timestamp[..19] : e.timestamp ?? "?";
                string label = e.label?.Length > 15 ? e.label[..15] : e.label ?? "";
                string tag = string.IsNullOrEmpty(e.tag) ? "" : $"[{e.tag}]";
                sb.AppendLine($"{date,-22} {label,-16} {e.avgFps,8:F1} {e.p1Fps,8:F1} {e.avgFrameTimeMs,8:F2} {e.p99FrameTimeMs,8:F2} {e.totalFrames,7}  {tag}");
            }

            if (recent.Count >= 2)
            {
                var newest = recent[0];
                var oldest = recent[^1];
                float fpsDelta = newest.avgFps - oldest.avgFps;
                string direction = fpsDelta > 1 ? "IMPROVING" : fpsDelta < -1 ? "REGRESSING" : "STABLE";
                sb.AppendLine();
                sb.AppendLine($"Trend: {direction} (FPS delta: {(fpsDelta >= 0 ? "+" : "")}{fpsDelta:F1} over {recent.Count} runs)");
            }

            return sb.ToString();
        }

        // ── Internal helpers ──────────────────────────────

        static string GetIndexPath(string folder)
        {
            return Path.Combine(Application.persistentDataPath, folder, IndexFileName);
        }

        static IndexFile LoadIndex(string folder)
        {
            string path = GetIndexPath(folder);
            if (!File.Exists(path)) return new IndexFile();

            try
            {
                string json = File.ReadAllText(path);
                return JsonUtility.FromJson<IndexFile>(json) ?? new IndexFile();
            }
            catch
            {
                return new IndexFile();
            }
        }

        static void SaveIndex(IndexFile index, string folder)
        {
            string dir = Path.Combine(Application.persistentDataPath, folder);
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, IndexFileName);
            File.WriteAllText(path, JsonUtility.ToJson(index, true));
        }
    }
}
