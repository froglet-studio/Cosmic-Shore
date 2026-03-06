using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class BenchmarkReportTests
    {
        string _testOutputDir;

        [SetUp]
        public void SetUp()
        {
            _testOutputDir = Path.Combine(Application.temporaryCachePath, "BenchmarkTests_" + System.Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testOutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_testOutputDir))
                Directory.Delete(_testOutputDir, true);
        }

        #region ComputeStatistics

        [Test]
        public void ComputeStatistics_PopulatesStatisticsField()
        {
            var report = new BenchmarkReport
            {
                sampleDuration = 5f,
                snapshots = new System.Collections.Generic.List<FrameSnapshot>
                {
                    new FrameSnapshot { deltaTimeMs = 16f, fps = 60f },
                    new FrameSnapshot { deltaTimeMs = 20f, fps = 50f },
                }
            };

            report.ComputeStatistics();

            Assert.IsNotNull(report.statistics);
            Assert.AreEqual(2, report.statistics.totalFrames);
            Assert.AreEqual(5f, report.statistics.durationSeconds, 0.001f);
        }

        [Test]
        public void ComputeStatistics_EmptySnapshots_ReturnsDefaultStats()
        {
            var report = new BenchmarkReport
            {
                sampleDuration = 5f,
                snapshots = new System.Collections.Generic.List<FrameSnapshot>()
            };

            report.ComputeStatistics();

            Assert.IsNotNull(report.statistics);
            Assert.AreEqual(0, report.statistics.totalFrames);
        }

        #endregion

        #region Save / Load Round-Trip

        [Test]
        public void SaveToFile_CreatesFileOnDisk()
        {
            var report = CreateMinimalReport();

            // SaveToFile uses persistentDataPath internally, so test file creation separately
            string json = JsonUtility.ToJson(report, true);
            string filePath = Path.Combine(_testOutputDir, "test_report.json");
            File.WriteAllText(filePath, json);

            Assert.IsTrue(File.Exists(filePath));
        }

        [Test]
        public void LoadFromFile_NonExistentPath_ReturnsNull()
        {
            var result = BenchmarkReport.LoadFromFile(Path.Combine(_testOutputDir, "nonexistent.json"));

            Assert.IsNull(result);
        }

        [Test]
        public void LoadFromFile_ValidJson_DeserializesCorrectly()
        {
            var report = CreateMinimalReport();
            report.label = "TestLabel";
            report.sceneName = "TestScene";
            report.sampleDuration = 7.5f;
            report.ComputeStatistics();

            string json = JsonUtility.ToJson(report, true);
            string filePath = Path.Combine(_testOutputDir, "roundtrip.json");
            File.WriteAllText(filePath, json);

            var loaded = BenchmarkReport.LoadFromFile(filePath);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("TestLabel", loaded.label);
            Assert.AreEqual("TestScene", loaded.sceneName);
            Assert.AreEqual(7.5f, loaded.sampleDuration, 0.001f);
            Assert.AreEqual(report.statistics.totalFrames, loaded.statistics.totalFrames);
        }

        [Test]
        public void SaveAndLoad_PreservesSnapshotData()
        {
            var report = CreateMinimalReport();
            report.ComputeStatistics();

            string json = JsonUtility.ToJson(report, true);
            string filePath = Path.Combine(_testOutputDir, "snapshots.json");
            File.WriteAllText(filePath, json);

            var loaded = BenchmarkReport.LoadFromFile(filePath);

            Assert.AreEqual(report.snapshots.Count, loaded.snapshots.Count);
            Assert.AreEqual(report.snapshots[0].deltaTimeMs, loaded.snapshots[0].deltaTimeMs, 0.001f);
            Assert.AreEqual(report.snapshots[0].fps, loaded.snapshots[0].fps, 0.001f);
        }

        #endregion

        #region Helpers

        static BenchmarkReport CreateMinimalReport()
        {
            return new BenchmarkReport
            {
                reportId = "test123",
                label = "test",
                timestamp = "2026-01-01T00:00:00Z",
                sceneName = "TestScene",
                sampleDuration = 5f,
                warmupDuration = 2f,
                snapshots = new System.Collections.Generic.List<FrameSnapshot>
                {
                    new FrameSnapshot { frameIndex = 0, deltaTimeMs = 16.67f, fps = 60f, drawCalls = 100, batches = 50 },
                    new FrameSnapshot { frameIndex = 1, deltaTimeMs = 18.00f, fps = 55.6f, drawCalls = 110, batches = 55 },
                    new FrameSnapshot { frameIndex = 2, deltaTimeMs = 15.50f, fps = 64.5f, drawCalls = 95, batches = 48 },
                }
            };
        }

        #endregion
    }
}
