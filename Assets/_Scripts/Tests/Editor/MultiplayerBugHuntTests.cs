#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated half of the 2026-11 multiplayer bug hunt (Docs/PresenceSystem/MULTIPLAYER_TEST_CHECKLIST.md
    /// is the manual half). Pure-logic pieces of the four playtest bugs: the ghost row (watched poll cadence,
    /// absence strikes), the slow join (post-join converge timing) and the stale AI on a guest's card (the
    /// replicated roster dropping a kicked bot).
    /// </summary>
    public class MultiplayerBugHuntTests
    {
        [SetUp, TearDown]
        public void ResetDemand() => PresenceWatchDemand.ResetForPlay();

        // ── PresenceWatchDemand ─────────────────────────────────────────────

        [Test]
        public void WatchDemand_IsCounted_AndNeverGoesNegative()
        {
            Assert.IsFalse(PresenceWatchDemand.IsWatched);
            PresenceWatchDemand.Acquire();
            PresenceWatchDemand.Acquire();
            PresenceWatchDemand.Release();
            Assert.IsTrue(PresenceWatchDemand.IsWatched, "one holder left");
            PresenceWatchDemand.Release();
            PresenceWatchDemand.Release();   // an unpaired release must not make the next Acquire a no-op
            Assert.AreEqual(0, PresenceWatchDemand.Holders);
            PresenceWatchDemand.Acquire();
            Assert.IsTrue(PresenceWatchDemand.IsWatched);
        }

        // ── LobbyRefreshScheduler ───────────────────────────────────────────

        static float SecondsUntilFire(LobbyRefreshScheduler s, float step = 0.1f, float cap = 120f)
        {
            for (float t = step; t <= cap; t += step)
                if (s.ShouldFireNow(step)) return t;
            return float.PositiveInfinity;
        }

        [Test]
        public void Scheduler_UnwatchedUsesSafetyInterval()
        {
            var s = new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS);
            Assert.AreEqual(LobbyRefreshScheduler.SAFETY_POLL_SECONDS, s.CurrentBaseInterval, 1e-4f);
            float t = SecondsUntilFire(s);
            float j = LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION;
            Assert.That(t, Is.InRange(LobbyRefreshScheduler.SAFETY_POLL_SECONDS * (1 - j) - 0.2f,
                                       LobbyRefreshScheduler.SAFETY_POLL_SECONDS * (1 + j) + 0.2f));
        }

        [Test]
        public void Scheduler_WatchedPollsEveryEightSeconds()
        {
            var s = new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS);
            PresenceWatchDemand.Acquire();
            Assert.AreEqual(LobbyRefreshScheduler.WATCHED_POLL_SECONDS, s.CurrentBaseInterval, 1e-4f);
            float j = LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION;
            for (int i = 0; i < 3; i++)
                Assert.LessOrEqual(SecondsUntilFire(s), LobbyRefreshScheduler.WATCHED_POLL_SECONDS * (1 + j) + 0.2f);
        }

        [Test]
        public void Scheduler_OpeningThePanelShortensAnAlreadyRolledInterval()
        {
            var s = new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS);
            Assert.IsFalse(s.ShouldFireNow(0.1f));            // rolls a ~20 s interval
            PresenceWatchDemand.Acquire();                      // friends panel opens mid-cycle
            float j = LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION;
            Assert.LessOrEqual(SecondsUntilFire(s), LobbyRefreshScheduler.WATCHED_POLL_SECONDS * (1 + j) + 0.2f);
        }

        [Test]
        public void Scheduler_FireAfter_FiresOnTime_ForBothPostJoinPolls()
        {
            var s = new LobbyRefreshScheduler(LobbyRefreshScheduler.SAFETY_POLL_SECONDS);
            s.FireAfter(LobbyRefreshScheduler.FIRST_POLL_AFTER_JOIN_SECONDS);
            Assert.AreEqual(LobbyRefreshScheduler.FIRST_POLL_AFTER_JOIN_SECONDS, SecondsUntilFire(s), 0.25f);
            s.FireAfter(LobbyRefreshScheduler.FOLLOW_UP_POLL_AFTER_JOIN_SECONDS);   // what Update does after the first
            Assert.AreEqual(LobbyRefreshScheduler.FOLLOW_UP_POLL_AFTER_JOIN_SECONDS, SecondsUntilFire(s), 0.25f);
        }

        [Test]
        public void Scheduler_FollowUpLandsWellBeforeTheSafetyPoll()
        {
            Assert.Less(LobbyRefreshScheduler.FIRST_POLL_AFTER_JOIN_SECONDS + LobbyRefreshScheduler.FOLLOW_UP_POLL_AFTER_JOIN_SECONDS,
                        LobbyRefreshScheduler.SAFETY_POLL_SECONDS * (1 - LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION));
        }

        // ── AbsenceStrikes: how long a ghost row can survive ────────────────

        [Test]
        public void Strikes_GhostRemovedOnSecondServerRead_NotOnLocalReads()
        {
            var a = new AbsenceStrikes();
            Assert.IsFalse(a.Absent("p", departureStated: false, serverRead: true));
            Assert.AreEqual(1, a.StrikesFor("p"));
            for (int i = 0; i < 5; i++)
                Assert.IsFalse(a.Absent("p", false, serverRead: false), "a local (notification) read never strikes");
            Assert.IsTrue(a.Absent("p", false, serverRead: true));
            Assert.AreEqual(0, a.StrikesFor("p"));
        }

        [Test]
        public void Strikes_StatedDepartureRemovesAtOnce_AndSeenForgives()
        {
            var a = new AbsenceStrikes();
            a.Absent("p", false, true);
            a.Seen("p");
            Assert.AreEqual(0, a.StrikesFor("p"), "a blip followed by a sighting is not a departure");
            Assert.IsTrue(a.Absent("q", departureStated: true, serverRead: false));
        }

        [Test]
        public void Strikes_WatchedWorstCaseGhostLifetime_IsUnderHalfAMinutePastReap()
        {
            // Two server reads at the watched cadence, plus one interval of phase.
            float worst = LobbyRefreshScheduler.WATCHED_POLL_SECONDS * (1 + LobbyRefreshScheduler.INTERVAL_JITTER_FRACTION)
                          * AbsenceStrikes.ReadsBeforeRemoval;
            Assert.Less(worst, 30f);
        }

        // ── LobbySnapshot: a kicked AI leaves the replicated roster ─────────

        [Test]
        public void Snapshot_KickRoundTrip_DropsTheSeat_DownToZero()
        {
            var snap = new ArcadeConfigSyncManager.LobbySnapshot { IsOpen = true, Generation = 3, PlayerCount = 4, HumanCount = 2 };
            snap.SetPlacedAiDomains(new[] { 2, 3 });
            var before = snap;

            snap.SetPlacedAiDomains(new[] { 3 });            // host kicked AI 0
            snap.PlayerCount = 3;
            Assert.IsFalse(before.Equals(snap), "a kick must change the value or the NetworkVariable never writes");
            Assert.IsFalse(before.SameAi(snap));
            CollectionAssert.AreEqual(new[] { 3 }, snap.PlacedAiDomains());

            var one = snap;
            snap.SetPlacedAiDomains(new int[0]);              // and the last one
            snap.PlayerCount = 2;
            Assert.AreEqual(0, snap.AiCount);
            Assert.IsEmpty(snap.PlacedAiDomains());
            Assert.IsFalse(one.SameAi(snap));
            StringAssert.Contains("ai=0", snap.ToString());
        }

        [Test]
        public void Snapshot_NullPlacementClearsEverySlot()
        {
            var snap = new ArcadeConfigSyncManager.LobbySnapshot();
            snap.SetPlacedAiDomains(new[] { 1, 2, 3, 4, 5, 6 });
            snap.SetPlacedAiDomains(null);
            Assert.AreEqual(0, snap.AiCount);
            Assert.AreEqual(new ArcadeConfigSyncManager.LobbySnapshot().Ai5, snap.Ai5, "a stale slot would compare unequal forever");
        }
    }
}
#endif
