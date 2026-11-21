// ─────────────────────────────────────────────────────────────────────────────
// NetSessionRecorderTests.cs
// Covers NetSessionRecorder and the NetSessionRecord schema.
//
// These run with no PlayerLoop, no UGS and no NetworkManager because the recorder takes its
// clock and its role/offline answers through assignable providers. That is the point of the
// indirection, so it is what these tests exercise.
// ─────────────────────────────────────────────────────────────────────────────
using NUnit.Framework;

namespace CosmicShore.Utility
{
    [TestFixture]
    public class NetSessionRecorderTests
    {
        double _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = 0;
            NetSessionRecorder.Now = () => _clock;
            NetSessionRecorder.Reset();
            UgsRequestTelemetry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            NetSessionRecorder.Now = () => UnityEngine.Time.realtimeSinceStartupAsDouble;
            NetSessionRecorder.RoleProvider = () => "unknown";
            NetSessionRecorder.OfflineProvider = () => false;
            NetSessionRecorder.Reset();
            UgsRequestTelemetry.Reset();
        }

        [Test]
        public void Mark_RecordsInOrder_WithElapsedTimes()
        {
            NetSessionRecorder.Mark("signedIn");
            _clock = 1.5;
            NetSessionRecorder.Mark("presenceJoined", "lobby-7");

            var r = NetSessionRecorder.BuildRecord();

            Assert.AreEqual(2, r.lifecycle.Count);
            Assert.AreEqual("signedIn", r.lifecycle[0].e);
            Assert.AreEqual(0f, r.lifecycle[0].t, 1e-4f, "the first mark starts the clock");
            Assert.AreEqual("presenceJoined", r.lifecycle[1].e);
            Assert.AreEqual(1.5f, r.lifecycle[1].t, 1e-4f);
            Assert.AreEqual("lobby-7", r.lifecycle[1].detail);
        }

        [Test]
        public void Mark_IgnoresEmptyVerb_SoATypoCannotFillTheTimeline()
        {
            NetSessionRecorder.Mark(null);
            NetSessionRecorder.Mark("");
            Assert.AreEqual(0, NetSessionRecorder.EventCount);
        }

        [Test]
        public void Mark_BeyondCap_DropsOldestAndRecordsTheCount()
        {
            for (int i = 0; i < NetSessionRecorder.MaxEvents + 25; i++)
                NetSessionRecorder.Mark("e", i.ToString());

            var r = NetSessionRecorder.BuildRecord();

            Assert.AreEqual(NetSessionRecorder.MaxEvents, r.lifecycle.Count, "capped");
            Assert.AreEqual(25, r.lifecycleDropped, "a truncated record must not read as a complete one");
            Assert.AreEqual("25", r.lifecycle[0].detail, "the OLDEST are dropped - the end of a session is where the failure is");
        }

        [Test]
        public void LobbyReadsPerSecond_IsZero_NotInfinity_AtZeroDuration()
        {
            UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);
            NetSessionRecorder.Mark("signedIn");   // duration still ~0

            var r = NetSessionRecorder.BuildRecord();

            Assert.AreEqual(0f, r.counters.lobbyReadsPerSecond,
                "an Infinity here serialises and then poisons every later average");
            Assert.IsFalse(float.IsInfinity(r.counters.lobbyReadsPerSecond));
            Assert.IsFalse(float.IsNaN(r.counters.lobbyReadsPerSecond));
        }

        [Test]
        public void LobbyReadsPerSecond_IsTheRate_OnceTimeHasPassed()
        {
            NetSessionRecorder.Mark("start");
            for (int i = 0; i < 13; i++) UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);
            _clock = 10.0;

            var r = NetSessionRecorder.BuildRecord();

            Assert.AreEqual(1.3f, r.counters.lobbyReadsPerSecond, 1e-3f);
        }

        [Test]
        public void Verdict_IsClean_WhenNothingWentWrong()
        {
            NetSessionRecorder.Mark("start");
            UgsRequestTelemetry.Count(UgsRequestCounter.Requests);
            UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);

            Assert.IsTrue(NetSessionRecorder.BuildRecord().verdict.IsClean,
                "requests and reads are normal traffic, not faults");
        }

        [Test]
        public void Verdict_IsDirty_OnARateLimit()
        {
            NetSessionRecorder.Mark("start");
            UgsRequestTelemetry.CountFailure(UgsFailureClass.RateLimited);

            var v = NetSessionRecorder.BuildRecord().verdict;

            Assert.AreEqual(1, v.rateLimited);
            Assert.IsFalse(v.IsClean);
        }

        [Test]
        public void OfflineFallback_CountsOnlyWhenTheDeviceWasOnline()
        {
            NetSessionRecorder.Mark("start");
            NetSessionRecorder.MarkOfflineFallback(deviceWasOnline: false);

            var v = NetSessionRecorder.BuildRecord().verdict;
            Assert.AreEqual(0, v.offlineFallbacksWhileOnline,
                "a player with no wifi going offline is correct behaviour, not a defect");
            Assert.IsTrue(v.IsClean);

            NetSessionRecorder.MarkOfflineFallback(deviceWasOnline: true);

            v = NetSessionRecorder.BuildRecord().verdict;
            Assert.AreEqual(1, v.offlineFallbacksWhileOnline, "this is B24's shape and it is a defect");
            Assert.IsFalse(v.IsClean);
        }

        [Test]
        public void Reset_ClearsTheTimeline_ButNotTheCounters()
        {
            NetSessionRecorder.Mark("start");
            UgsRequestTelemetry.Count(UgsRequestCounter.LobbyReads);

            NetSessionRecorder.Reset();
            var r = NetSessionRecorder.BuildRecord();

            Assert.AreEqual(0, r.lifecycle.Count);
            Assert.AreEqual(1, r.counters.lobbyReads,
                "UgsRequestTelemetry owns the counters; two owners of one piece of state is the bug this avoids");
        }

        [Test]
        public void Providers_ThatThrow_DoNotTakeTheRecordDown()
        {
            NetSessionRecorder.RoleProvider = () => throw new System.InvalidOperationException("scene unloaded");
            NetSessionRecorder.OfflineProvider = () => throw new System.InvalidOperationException("scene unloaded");
            NetSessionRecorder.Mark("start");

            NetSessionRecord r = null;
            Assert.DoesNotThrow(() => r = NetSessionRecorder.BuildRecord(),
                "a diagnostic that takes the session down with it is worse than useless");
            Assert.AreEqual("unknown", r.role);
            Assert.IsFalse(r.isOfflineSession);
        }

        [Test]
        public void Schema_IsPinned_SoAReaderCanTellVersionsApart()
        {
            Assert.AreEqual("cosmicshore.netsession.v1", new NetSessionRecord().schema);
        }
    }
}
