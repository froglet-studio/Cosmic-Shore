#if UNITY_EDITOR
using System.Linq;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B36 (Docs/PartySystem/BUGS.md): Unity Relay drops a host it has not heard from for ~10 s, and the
    /// transport only pings from the main thread, so a frame that freezes for seconds ends the session.
    /// The watch records such a frame while online, and a drop names the freeze it followed.
    /// </summary>
    [TestFixture]
    public class FrameStallWatchTests
    {
        double _clock;
        bool _online;

        [SetUp]
        public void SetUp()
        {
            _clock = 500;
            _online = true;
            FrameStallWatch.Now = () => _clock;
            FrameStallWatch.OnlineProvider = () => _online;
            FrameStallWatch.Reset();
            StallFrameMarkers.Sample = () => _markers;
            NetSessionRecorder.Now = () => _clock;
            NetSessionRecorder.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FrameStallWatch.Now = () => Time.realtimeSinceStartupAsDouble;
            // The real provider, not "offline": the editor keeps statics into the next Play when there is no domain
            // reload, and a provider left at false switched the stall record off in that Play (2026-11-21).
            FrameStallWatch.OnlineProvider = FrameStallWatch.NetworkIsListening;
            FrameStallWatch.Reset();
            StallFrameMarkers.Sample = null;
            StallFrameMarkers.Stop();
            NetSessionRecorder.Now = () => Time.realtimeSinceStartupAsDouble;
            NetSessionRecorder.Reset();
        }

        System.Collections.Generic.KeyValuePair<string, long>[] _markers =
            System.Array.Empty<System.Collections.Generic.KeyValuePair<string, long>>();

        static System.Collections.Generic.KeyValuePair<string, long> Ms(string name, double ms) =>
            new(name, (long)(ms * 1e6));

        string[] FrameMarks() => NetSessionRecorder.BuildRecord().lifecycle
            .Where(e => e.e == "stallFrame").Select(e => e.detail).ToArray();

        [Test]
        public void AStall_IsNamedByTheMarkersItSpentItsTimeIn_OneFrameLater()
        {
            Frame(0.02);
            // The frame that ended at the stall held the scene load; the next one finished the destroy.
            _markers = new[] { Ms("PlayerLoop", 8000), Ms("Loading.UpdatePreloading", 6000), Ms("Destroy", 900), Ms("Instantiate", 5) };
            Frame(9.0, "Menu_Main");
            Assert.IsEmpty(FrameMarks(), "the stall's own frame is not complete when it is noticed");

            _markers = new[] { Ms("PlayerLoop", 30), Ms("Destroy", 2500) };
            Frame(0.03, "Menu_Main");
            CollectionAssert.AreEqual(
                new[] { "PlayerLoop 8030 ms; Loading.UpdatePreloading 6000 ms; Destroy 3400 ms" },
                FrameMarks(), "the two frames the stall spans, added, longest first, small ones left out");

            Frame(0.02);
            Assert.AreEqual(1, FrameMarks().Length, "one mark per stall");
        }

        [Test]
        public void NoStall_ReadsNoMarkers()
        {
            int reads = 0;
            StallFrameMarkers.Sample = () => { reads++; return _markers; };
            for (int i = 0; i < 5; i++) Frame(0.5);
            Assert.AreEqual(0, reads, "ordinary frames cost nothing");
            Assert.IsEmpty(FrameMarks());
        }

        double Frame(double seconds, string scene = "Menu_Main")
        {
            _clock += seconds;
            return FrameStallWatch.ObserveFrame(scene);
        }

        string[] Stalls() => NetSessionRecorder.BuildRecord().lifecycle
            .Where(e => e.e == "stall").Select(e => e.detail).ToArray();

        [Test]
        public void AFrozenFrameOnline_IsRecordedWithItsScene()
        {
            Frame(0, "MinigameBloomrush");
            Assert.AreEqual(0, Frame(0.29, "MinigameBloomrush"), "a slow frame (Bloomrush ran at 290 ms) is not a stall");
            Assert.AreEqual(12.5, Frame(12.5, "MinigameBloomrush"), 1e-9);
            CollectionAssert.AreEqual(new[] { "12.5s in MinigameBloomrush" }, Stalls());
            Assert.AreEqual(12.5, FrameStallWatch.LongestStallSeconds, 1e-9);
        }

        [Test]
        public void AStall_IsFollowedByWhatTheMachineWasDoing()
        {
            FrameStallWatch.ContextProvider = () => "focused no; async prism clones waiting 1";
            try
            {
                Frame(0);
                Frame(39.6);
                var context = NetSessionRecorder.BuildRecord().lifecycle.Where(e => e.e == "stallContext").Select(e => e.detail).ToArray();
                CollectionAssert.AreEqual(new[] { "focused no; async prism clones waiting 1" }, context,
                    "the 30 Oct freezes said only '39.6s in Menu_Main'");
                Frame(0.016);
                Assert.AreEqual(1, NetSessionRecorder.BuildRecord().lifecycle.Count(e => e.e == "stallContext"), "only stalls carry it");
            }
            finally { FrameStallWatch.ContextProvider = FrameStallWatch.DescribeContext; }
        }

        [Test]
        public void AsyncClonesInFlight_AreCountedAndNeverGoNegative()
        {
            FrameStallWatch.BeginAsyncClone();
            FrameStallWatch.BeginAsyncClone();
            FrameStallWatch.EndAsyncClone();
            StringAssert.Contains("async prism clones waiting 1", FrameStallWatch.DescribeContext());
            FrameStallWatch.EndAsyncClone();
            FrameStallWatch.EndAsyncClone();
            StringAssert.Contains("async prism clones waiting 0", FrameStallWatch.DescribeContext());
        }

        [Test]
        public void AFreezeAcrossASceneChange_NamesTheSceneItStartedIn()
        {
            Frame(0, "MinigameBloomrush");
            Frame(11.0, "Menu_Main");
            CollectionAssert.AreEqual(new[] { "11.0s in MinigameBloomrush, ending in Menu_Main" }, Stalls(),
                "the teardown froze, not the menu it led to");
        }

        [Test]
        public void AFreezeOffline_IsNotRecorded()
        {
            Frame(0);
            _online = false;
            Assert.AreEqual(0, Frame(20), "no Relay to lose (negative control)");
            Assert.IsEmpty(Stalls());
        }

        [Test]
        public void TheFirstFrameOfAPlay_IsNotAStall()
        {
            _clock += 100;
            Assert.AreEqual(0, FrameStallWatch.ObserveFrame("Bootstrap"), "no previous frame to measure from");
        }

        [Test]
        public void ADrop_NamesTheFreezeItFollowed_ForThirtySeconds()
        {
            Frame(0);
            Assert.AreEqual("no freeze in the last 30 s", FrameStallWatch.DescribeRecentStall());
            Frame(11.0, "Menu_Main");
            _clock += 2.9;
            Assert.AreEqual("after a 11.0 s freeze 2.9 s earlier", FrameStallWatch.DescribeRecentStall(),
                "the owner's drops came ~3 s after the teardown");
            _clock += 40;
            Assert.AreEqual("no freeze in the last 30 s", FrameStallWatch.DescribeRecentStall(), "an old freeze is not blamed");
        }

        [Test]
        public void TheRelayRiskLine_SitsUnderTheRelayTimeout()
        {
            Assert.Less(FrameStallWatch.RELAY_RISK_SECONDS, 10.0, "Unity Relay drops after ~10 s of silence");
            Assert.Less(FrameStallWatch.STALL_SECONDS, FrameStallWatch.RELAY_RISK_SECONDS);
        }
    }
}
#endif
