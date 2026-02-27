using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class BenchmarkHistoryTests
    {
        string _testFolder;

        [SetUp]
        public void SetUp()
        {
            // Use a unique subfolder per test run to avoid collisions
            _testFolder = "BenchmarkHistoryTest_" + System.Guid.NewGuid().ToString("N")[..8];
            string dir = Path.Combine(Application.persistentDataPath, _testFolder);
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            string dir = Path.Combine(Application.persistentDataPath, _testFolder);
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        #region AddToHistory

        [Test]
        public void AddToHistory_CreatesIndexEntry()
        {
            var report = CreateReport("test-run", "TestScene");
            string filePath = report.SaveToFile(_testFolder);

            var entry = BenchmarkHistory.AddToHistory(report, filePath, _testFolder);

            Assert.IsNotNull(entry);
            Assert.AreEqual(report.reportId, entry.reportId);
            Assert.AreEqual("test-run", entry.label);
            Assert.AreEqual("TestScene", entry.sceneName);
        }

        [Test]
        public void AddToHistory_PreventsDuplicates()
        {
            var report = CreateReport("test-run", "TestScene");
            string filePath = report.SaveToFile(_testFolder);

            BenchmarkHistory.AddToHistory(report, filePath, _testFolder);
            BenchmarkHistory.AddToHistory(report, filePath, _testFolder);

            var all = BenchmarkHistory.GetAll(_testFolder);
            Assert.AreEqual(1, all.Count);
        }

        [Test]
        public void AddToHistory_NewestFirst()
        {
            var report1 = CreateReport("first", "Scene1");
            report1.reportId = "aaa";
            string path1 = report1.SaveToFile(_testFolder);
            BenchmarkHistory.AddToHistory(report1, path1, _testFolder);

            var report2 = CreateReport("second", "Scene2");
            report2.reportId = "bbb";
            string path2 = report2.SaveToFile(_testFolder);
            BenchmarkHistory.AddToHistory(report2, path2, _testFolder);

            var all = BenchmarkHistory.GetAll(_testFolder);
            Assert.AreEqual(2, all.Count);
            Assert.AreEqual("bbb", all[0].reportId);
            Assert.AreEqual("aaa", all[1].reportId);
        }

        #endregion

        #region GetAll / GetLatest

        [Test]
        public void GetAll_EmptyFolder_ReturnsEmptyList()
        {
            var all = BenchmarkHistory.GetAll(_testFolder);
            Assert.IsNotNull(all);
            Assert.AreEqual(0, all.Count);
        }

        [Test]
        public void GetLatest_EmptyFolder_ReturnsNull()
        {
            var latest = BenchmarkHistory.GetLatest(_testFolder);
            Assert.IsNull(latest);
        }

        [Test]
        public void GetLatest_ReturnsNewestEntry()
        {
            var report1 = CreateReport("old", "Scene");
            report1.reportId = "old1";
            BenchmarkHistory.AddToHistory(report1, report1.SaveToFile(_testFolder), _testFolder);

            var report2 = CreateReport("new", "Scene");
            report2.reportId = "new2";
            BenchmarkHistory.AddToHistory(report2, report2.SaveToFile(_testFolder), _testFolder);

            var latest = BenchmarkHistory.GetLatest(_testFolder);
            Assert.AreEqual("new2", latest.reportId);
        }

        #endregion

        #region Tagging

        [Test]
        public void TagReport_SetsTag()
        {
            var report = CreateReport("run", "Scene");
            BenchmarkHistory.AddToHistory(report, report.SaveToFile(_testFolder), _testFolder);

            BenchmarkHistory.TagReport(report.reportId, "baseline", _testFolder);

            var all = BenchmarkHistory.GetAll(_testFolder);
            Assert.AreEqual("baseline", all[0].tag);
        }

        [Test]
        public void GetByTag_FindsTaggedEntries()
        {
            var report1 = CreateReport("run1", "Scene");
            report1.reportId = "r1";
            BenchmarkHistory.AddToHistory(report1, report1.SaveToFile(_testFolder), _testFolder);
            BenchmarkHistory.TagReport("r1", "baseline", _testFolder);

            var report2 = CreateReport("run2", "Scene");
            report2.reportId = "r2";
            BenchmarkHistory.AddToHistory(report2, report2.SaveToFile(_testFolder), _testFolder);

            var tagged = BenchmarkHistory.GetByTag("baseline", _testFolder);
            Assert.AreEqual(1, tagged.Count);
            Assert.AreEqual("r1", tagged[0].reportId);
        }

        #endregion

        #region GetByScene

        [Test]
        public void GetByScene_FiltersCorrectly()
        {
            var report1 = CreateReport("run1", "SceneA");
            report1.reportId = "r1";
            BenchmarkHistory.AddToHistory(report1, report1.SaveToFile(_testFolder), _testFolder);

            var report2 = CreateReport("run2", "SceneB");
            report2.reportId = "r2";
            BenchmarkHistory.AddToHistory(report2, report2.SaveToFile(_testFolder), _testFolder);

            var sceneA = BenchmarkHistory.GetByScene("SceneA", _testFolder);
            Assert.AreEqual(1, sceneA.Count);
            Assert.AreEqual("r1", sceneA[0].reportId);
        }

        #endregion

        #region RemoveEntry

        [Test]
        public void RemoveEntry_RemovesFromIndex()
        {
            var report = CreateReport("run", "Scene");
            string path = report.SaveToFile(_testFolder);
            BenchmarkHistory.AddToHistory(report, path, _testFolder);

            BenchmarkHistory.RemoveEntry(report.reportId, _testFolder, deleteFile: true);

            var all = BenchmarkHistory.GetAll(_testFolder);
            Assert.AreEqual(0, all.Count);
            Assert.IsFalse(File.Exists(path));
        }

        #endregion

        #region RebuildIndex

        [Test]
        public void RebuildIndex_DiscoversReportFiles()
        {
            // Save reports without indexing them
            var report1 = CreateReport("orphan1", "Scene");
            report1.SaveToFile(_testFolder);
            var report2 = CreateReport("orphan2", "Scene");
            report2.SaveToFile(_testFolder);

            int count = BenchmarkHistory.RebuildIndex(_testFolder);

            Assert.AreEqual(2, count);
            var all = BenchmarkHistory.GetAll(_testFolder);
            Assert.AreEqual(2, all.Count);
        }

        #endregion

        #region GetTrendSummary

        [Test]
        public void GetTrendSummary_NoData_ReturnsMessage()
        {
            string summary = BenchmarkHistory.GetTrendSummary("MissingScene", _testFolder);
            Assert.IsTrue(summary.Contains("No benchmark data"));
        }

        [Test]
        public void GetTrendSummary_WithData_ContainsSceneName()
        {
            var report = CreateReport("run", "TestScene");
            BenchmarkHistory.AddToHistory(report, report.SaveToFile(_testFolder), _testFolder);

            string summary = BenchmarkHistory.GetTrendSummary("TestScene", _testFolder);
            Assert.IsTrue(summary.Contains("TestScene"));
        }

        #endregion

        #region LoadReport

        [Test]
        public void LoadReport_ReturnsFullReport()
        {
            var report = CreateReport("full-data", "TestScene");
            report.ComputeStatistics();
            string path = report.SaveToFile(_testFolder);
            var entry = BenchmarkHistory.AddToHistory(report, path, _testFolder);

            var loaded = BenchmarkHistory.LoadReport(entry);

            Assert.IsNotNull(loaded);
            Assert.AreEqual("full-data", loaded.label);
            Assert.AreEqual(report.snapshots.Count, loaded.snapshots.Count);
        }

        #endregion

        #region Helpers

        static BenchmarkReport CreateReport(string label, string sceneName)
        {
            var report = new BenchmarkReport
            {
                reportId = System.Guid.NewGuid().ToString("N")[..12],
                label = label,
                sceneName = sceneName,
                timestamp = System.DateTime.UtcNow.ToString("o"),
                gitBranch = "test",
                gitCommitHash = "abc123",
                warmupDuration = 2f,
                sampleDuration = 5f,
                snapshots = new List<FrameSnapshot>
                {
                    new FrameSnapshot { frameIndex = 0, deltaTimeMs = 16.67f, fps = 60f, drawCalls = 100, batches = 50, totalAllocatedMemory = 50_000_000, gcAllocatedPerFrame = 1024 },
                    new FrameSnapshot { frameIndex = 1, deltaTimeMs = 18.00f, fps = 55.6f, drawCalls = 110, batches = 55, totalAllocatedMemory = 51_000_000, gcAllocatedPerFrame = 2048 },
                    new FrameSnapshot { frameIndex = 2, deltaTimeMs = 15.50f, fps = 64.5f, drawCalls = 95, batches = 48, totalAllocatedMemory = 49_000_000, gcAllocatedPerFrame = 512 },
                },
                statistics = new BenchmarkStatistics
                {
                    totalFrames = 3,
                    durationSeconds = 5f,
                    avgFps = 60f,
                    p1Fps = 55f,
                    avgFrameTimeMs = 16.72f,
                    p99FrameTimeMs = 18f,
                }
            };
            return report;
        }

        #endregion
    }
}
