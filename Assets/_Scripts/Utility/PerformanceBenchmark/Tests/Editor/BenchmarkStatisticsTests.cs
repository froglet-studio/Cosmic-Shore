using System.Collections.Generic;
using NUnit.Framework;

namespace CosmicShore.Utility.PerformanceBenchmark.Tests
{
    [TestFixture]
    public class BenchmarkStatisticsTests
    {
        #region Empty / Null Input

        [Test]
        public void Compute_NullList_ReturnsDefaultStatistics()
        {
            var stats = BenchmarkStatistics.Compute(null, 10f);

            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.totalFrames);
        }

        [Test]
        public void Compute_EmptyList_ReturnsDefaultStatistics()
        {
            var stats = BenchmarkStatistics.Compute(new List<FrameSnapshot>(), 10f);

            Assert.IsNotNull(stats);
            Assert.AreEqual(0, stats.totalFrames);
        }

        #endregion

        #region Single Frame

        [Test]
        public void Compute_SingleFrame_SetsAllStatsToThatFrame()
        {
            var snapshots = new List<FrameSnapshot>
            {
                new FrameSnapshot { deltaTimeMs = 16.67f, fps = 60f, drawCalls = 100, batches = 50 }
            };

            var stats = BenchmarkStatistics.Compute(snapshots, 1f);

            Assert.AreEqual(1, stats.totalFrames);
            Assert.AreEqual(1f, stats.durationSeconds, 0.001f);
            Assert.AreEqual(16.67f, stats.avgFrameTimeMs, 0.01f);
            Assert.AreEqual(60f, stats.avgFps, 0.01f);
            Assert.AreEqual(16.67f, stats.minFrameTimeMs, 0.01f);
            Assert.AreEqual(16.67f, stats.maxFrameTimeMs, 0.01f);
            Assert.AreEqual(0f, stats.stdDevFrameTimeMs, 0.01f);
        }

        #endregion

        #region Multiple Frames

        [Test]
        public void Compute_MultipleFrames_CalculatesCorrectAverage()
        {
            var snapshots = new List<FrameSnapshot>
            {
                new FrameSnapshot { deltaTimeMs = 10f, fps = 100f },
                new FrameSnapshot { deltaTimeMs = 20f, fps = 50f },
                new FrameSnapshot { deltaTimeMs = 30f, fps = 33.33f },
            };

            var stats = BenchmarkStatistics.Compute(snapshots, 3f);

            Assert.AreEqual(3, stats.totalFrames);
            Assert.AreEqual(20f, stats.avgFrameTimeMs, 0.01f);
            Assert.AreEqual(10f, stats.minFrameTimeMs, 0.01f);
            Assert.AreEqual(30f, stats.maxFrameTimeMs, 0.01f);
        }

        [Test]
        public void Compute_MultipleFrames_CalculatesMedianCorrectly()
        {
            var snapshots = new List<FrameSnapshot>
            {
                new FrameSnapshot { deltaTimeMs = 10f, fps = 100f },
                new FrameSnapshot { deltaTimeMs = 20f, fps = 50f },
                new FrameSnapshot { deltaTimeMs = 30f, fps = 33.33f },
            };

            var stats = BenchmarkStatistics.Compute(snapshots, 3f);

            Assert.AreEqual(20f, stats.medianFrameTimeMs, 0.01f);
        }

        [Test]
        public void Compute_MultipleFrames_CalculatesStdDev()
        {
            // 10, 20, 30 => mean=20, variance=((10-20)^2+(20-20)^2+(30-20)^2)/3 = 200/3, stddev=sqrt(66.67)=8.165
            var snapshots = new List<FrameSnapshot>
            {
                new FrameSnapshot { deltaTimeMs = 10f, fps = 100f },
                new FrameSnapshot { deltaTimeMs = 20f, fps = 50f },
                new FrameSnapshot { deltaTimeMs = 30f, fps = 33.33f },
            };

            var stats = BenchmarkStatistics.Compute(snapshots, 3f);

            Assert.AreEqual(8.165f, stats.stdDevFrameTimeMs, 0.01f);
        }

        #endregion

        #region Rendering Stats

        [Test]
        public void Compute_RenderingStats_AveragesCorrectly()
        {
            var snapshots = new List<FrameSnapshot>
            {
                new FrameSnapshot { deltaTimeMs = 16f, fps = 60f, drawCalls = 100, batches = 40, setPassCalls = 10, triangles = 5000, vertices = 3000 },
                new FrameSnapshot { deltaTimeMs = 16f, fps = 60f, drawCalls = 200, batches = 60, setPassCalls = 20, triangles = 7000, vertices = 5000 },
            };

            var stats = BenchmarkStatistics.Compute(snapshots, 2f);

            Assert.AreEqual(150f, stats.avgDrawCalls, 0.01f);
            Assert.AreEqual(50f, stats.avgBatches, 0.01f);
            Assert.AreEqual(15f, stats.avgSetPassCalls, 0.01f);
            Assert.AreEqual(6000f, stats.avgTriangles, 0.01f);
            Assert.AreEqual(4000f, stats.avgVertices, 0.01f);
        }

        #endregion

        #region Memory Stats

        [Test]
        public void Compute_Memory_TracksPeakAndGcTotal()
        {
            var snapshots = new List<FrameSnapshot>
            {
                new FrameSnapshot { deltaTimeMs = 16f, fps = 60f, totalAllocatedMemory = 100_000_000, gcAllocatedPerFrame = 1024 },
                new FrameSnapshot { deltaTimeMs = 16f, fps = 60f, totalAllocatedMemory = 200_000_000, gcAllocatedPerFrame = 2048 },
                new FrameSnapshot { deltaTimeMs = 16f, fps = 60f, totalAllocatedMemory = 150_000_000, gcAllocatedPerFrame = 512 },
            };

            var stats = BenchmarkStatistics.Compute(snapshots, 3f);

            Assert.AreEqual(200_000_000, stats.peakAllocatedMemory);
            Assert.AreEqual(1024 + 2048 + 512, stats.totalGcAllocated);
        }

        #endregion

        #region Percentiles

        [Test]
        public void Compute_P95FrameTime_HigherThanMedian()
        {
            // Create 100 frames: 95 at 16ms, 5 at 50ms
            var snapshots = new List<FrameSnapshot>();
            for (int i = 0; i < 95; i++)
                snapshots.Add(new FrameSnapshot { deltaTimeMs = 16f, fps = 60f });
            for (int i = 0; i < 5; i++)
                snapshots.Add(new FrameSnapshot { deltaTimeMs = 50f, fps = 20f });

            var stats = BenchmarkStatistics.Compute(snapshots, 10f);

            Assert.Greater(stats.p95FrameTimeMs, stats.medianFrameTimeMs);
            Assert.Greater(stats.p99FrameTimeMs, stats.p95FrameTimeMs);
        }

        [Test]
        public void Compute_P1Fps_LowerThanMedian()
        {
            var snapshots = new List<FrameSnapshot>();
            for (int i = 0; i < 95; i++)
                snapshots.Add(new FrameSnapshot { deltaTimeMs = 16f, fps = 60f });
            for (int i = 0; i < 5; i++)
                snapshots.Add(new FrameSnapshot { deltaTimeMs = 50f, fps = 20f });

            var stats = BenchmarkStatistics.Compute(snapshots, 10f);

            Assert.Less(stats.p1Fps, stats.medianFps);
            Assert.Less(stats.p5Fps, stats.medianFps);
        }

        #endregion
    }
}
