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
            NetSessionRecorder.Now = () => _clock;
            NetSessionRecorder.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FrameStallWatch.Now = () => Time.realtimeSinceStartupAsDouble;
            FrameStallWatch.OnlineProvider = () => false;
            FrameStallWatch.Reset();
            NetSessionRecorder.Now = () => Time.realtimeSinceStartupAsDouble;
            NetSessionRecorder.Reset();
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
