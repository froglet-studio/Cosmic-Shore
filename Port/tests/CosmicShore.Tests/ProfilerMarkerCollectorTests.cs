using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CosmicShore.Engine.Profiling;
using CosmicShore.Engine.Profiling.LowLevel.Unsafe;
using Xunit;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The marker collector behind ProfilerMarker / ProfilerRecorder: the path the game's own
    /// <c>diag</c> (MarkerBudgetRecorder) reads, so a marker the game times reads a real number here.
    /// </summary>
    public class ProfilerMarkerCollectorTests
    {
        static void FreshWindow()
        {
            MarkerCollector.LoopThreadId = Environment.CurrentManagedThreadId;
            MarkerCollector.EndFrame(); // drop whatever an earlier test left in the open frame
            MarkerCollector.ResetTotals();
        }

        static MarkerCollector.Summary Find(string name) => MarkerCollector.Summarize().Single(m => m.Name == name);

        [Fact]
        public void AutoScope_RecordsCallsAndTimePerFrame()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Auto");
            for (int frame = 0; frame < 4; frame++)
            {
                for (int call = 0; call < 3; call++)
                    using (marker.Auto()) Thread.SpinWait(2000);
                MarkerCollector.EndFrame();
            }

            var s = Find("Test.Collector.Auto");
            Assert.Equal(4, s.ActiveFrames);
            Assert.Equal(3.0, s.CallsPerFrame, 3);
            Assert.True(s.AvgMsPerFrame > 0, "time was recorded");
            Assert.True(s.MaxMs >= s.P50Ms);
        }

        [Fact]
        public void AverageIsOverEveryFrame_PercentilesOverActiveFrames()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Sparse");
            for (int frame = 0; frame < 10; frame++)
            {
                if (frame == 0) using (marker.Auto()) Thread.SpinWait(2000);
                MarkerCollector.EndFrame();
            }

            var s = Find("Test.Collector.Sparse");
            Assert.Equal(1, s.ActiveFrames);
            Assert.Equal(10, s.Frames);
            Assert.Equal(0.1, s.CallsPerFrame, 3);
        }

        [Fact]
        public void AllocationsInsideTheScopeAreAttributed()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Alloc");
            var quiet = new ProfilerMarker("Test.Collector.NoAlloc");
            object keep = null;
            for (int frame = 0; frame < 5; frame++)
            {
                using (marker.Auto()) keep = new byte[64 * 1024];
                using (quiet.Auto()) Thread.SpinWait(10);
                MarkerCollector.EndFrame();
            }
            GC.KeepAlive(keep);

            Assert.True(Find("Test.Collector.Alloc").KBPerFrame >= 64, "a 64 KB array per frame shows as >= 64 KB/frame");
            Assert.Equal(0, Find("Test.Collector.NoAlloc").KBPerFrame);
        }

        [Fact]
        public void NestedMarkers_AreInclusive()
        {
            FreshWindow();
            var outer = new ProfilerMarker("Test.Collector.Outer");
            var inner = new ProfilerMarker("Test.Collector.Inner");
            for (int frame = 0; frame < 3; frame++)
            {
                using (outer.Auto())
                using (inner.Auto())
                    Thread.SpinWait(5000);
                MarkerCollector.EndFrame();
            }
            Assert.True(Find("Test.Collector.Outer").AvgMsPerFrame >= Find("Test.Collector.Inner").AvgMsPerFrame);
        }

        [Fact]
        public void OtherThreads_AreNotRecorded()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Worker");
            var worker = new Thread(() => { using (marker.Auto()) Thread.SpinWait(100); });
            worker.Start();
            worker.Join();
            MarkerCollector.EndFrame();
            Assert.DoesNotContain(MarkerCollector.Summarize(), m => m.Name == "Test.Collector.Worker");
        }

        [Fact]
        public void BeginWithoutEnd_IsDroppedAtFrameEnd()
        {
            FreshWindow();
            var leaked = new ProfilerMarker("Test.Collector.Leaked");
            var after = new ProfilerMarker("Test.Collector.After");
            leaked.Begin();
            MarkerCollector.EndFrame();
            using (after.Auto()) { }
            leaked.End(); // its Begin was dropped with the frame: nothing to close
            MarkerCollector.EndFrame();

            Assert.DoesNotContain(MarkerCollector.Summarize(), m => m.Name == "Test.Collector.Leaked");
            Assert.Equal(1, Find("Test.Collector.After").ActiveFrames);
        }

        [Fact]
        public void HandleRecorder_ReadsOneSamplePerFrame_LikeDiag()
        {
            FreshWindow();
            var marker = new ProfilerMarker(ProfilerCategory.Ai, "Test.Collector.Diag");

            // diag's lookup: enumerate handles, match the name, record by handle.
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var handle = handles.Single(h => ProfilerRecorderHandle.GetDescription(h).Name == "Test.Collector.Diag");
            var description = ProfilerRecorderHandle.GetDescription(handle);
            Assert.Equal("Ai", description.Category.Name);
            Assert.Equal(ProfilerMarkerDataUnit.TimeNanoseconds, description.UnitType);

            var recorder = new ProfilerRecorder(handle, 8,
                ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            recorder.Start();
            for (int frame = 0; frame < 3; frame++)
            {
                for (int call = 0; call <= frame; call++)
                    using (marker.Auto()) Thread.SpinWait(1000);
                MarkerCollector.EndFrame();
            }
            MarkerCollector.EndFrame(); // a frame the marker did not run adds no sample

            var samples = new List<ProfilerRecorderSample>();
            recorder.CopyTo(samples);
            Assert.Equal(3, samples.Count);
            Assert.Equal(new long[] { 1, 2, 3 }, samples.Select(s => s.Count).ToArray());
            Assert.All(samples, s => Assert.True(s.Value > 0));
            recorder.Dispose();
        }

        [Fact]
        public void Reset_StopsTheRecorder_AndCopiesShareState()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Reset");
            var recorder = ProfilerRecorder.StartNew(marker, 4);
            var copy = recorder; // diag keeps recorders in a struct list and resets copies

            using (marker.Auto()) { }
            MarkerCollector.EndFrame();
            Assert.Equal(1, recorder.Count);

            copy.Reset(); // Unity: clears AND stops
            using (marker.Auto()) { }
            MarkerCollector.EndFrame();
            Assert.Equal(0, recorder.Count);
            Assert.False(recorder.IsRunning);

            copy.Start();
            using (marker.Auto()) { }
            MarkerCollector.EndFrame();
            Assert.Equal(1, recorder.Count);
            recorder.Dispose();
        }

        [Fact]
        public void Ring_KeepsTheLatestFrames_AndReportsWrap()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Ring");
            var recorder = ProfilerRecorder.StartNew(marker, 2);
            for (int frame = 1; frame <= 3; frame++)
            {
                for (int call = 0; call < frame; call++) using (marker.Auto()) { }
                MarkerCollector.EndFrame();
            }
            Assert.True(recorder.WrappedAround);
            Assert.Equal(2, recorder.Count);
            Assert.Equal(2, recorder.GetSample(0).Count);
            Assert.Equal(3, recorder.GetSample(1).Count);
            recorder.Dispose();
        }

        [Fact]
        public void Counters_StayCounters()
        {
            FreshWindow();
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            Assert.True(drawCalls.Valid);
            Assert.Equal(0, drawCalls.LastValue);

            var alloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            MarkerCollector.EndFrame();
            GC.KeepAlive(new byte[32 * 1024]);
            MarkerCollector.EndFrame();
            Assert.True(alloc.LastValue >= 32 * 1024, "the loop thread's bytes in the last frame");
        }

        [Fact]
        public void Disabled_IsANoOp()
        {
            FreshWindow();
            var marker = new ProfilerMarker("Test.Collector.Disabled");
            MarkerCollector.Enabled = false;
            try
            {
                using (marker.Auto()) { }
                MarkerCollector.EndFrame();
            }
            finally { MarkerCollector.Enabled = true; }
            MarkerCollector.EndFrame();
            Assert.DoesNotContain(MarkerCollector.Summarize(), m => m.Name == "Test.Collector.Disabled");
        }
    }
}
