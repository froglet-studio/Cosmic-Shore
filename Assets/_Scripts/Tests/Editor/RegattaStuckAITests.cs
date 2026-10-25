using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Two AIs that sat out whole Regatta races in people's flights (2026-10-25): a Dolphin whose commit drift began at
    /// the start line and held 0 u/s for 60 s (<see cref="AIPilot.CanCommitAt"/>), and an Urchin that latched a rival's
    /// lane and crawled it for 105 s (<see cref="UrchinRailAssessment.DryCrawlTooLong"/>,
    /// <see cref="RegattaController.OwnLaneApproach"/>). Pure rules - no vessel.
    /// </summary>
    public class RegattaStuckAITests
    {
        [Test]
        public void Commit_NeverFromAStandstill()
        {
            Assert.IsFalse(AIPilot.CanCommitAt(0f, 20f), "a drift begun at rest");
            Assert.IsFalse(AIPilot.CanCommitAt(19.9f, 20f));
            Assert.IsTrue(AIPilot.CanCommitAt(68f, 20f));
        }

        [Test]
        public void Commit_OnASpeedHoldingDrift_WaitsUntilUpToSpeed()
        {
            // The Dolphin accelerating off the line toward its 68 u/s cruise: a drift at 30 would hold 30.
            Assert.IsFalse(AIPilot.CanCommitAt(30f, 20f, holdsSpeed: true, throttleTarget: 68f, upToSpeedShare: 0.9f));
            Assert.IsTrue(AIPilot.CanCommitAt(62f, 20f, holdsSpeed: true, throttleTarget: 68f, upToSpeedShare: 0.9f));
            // At a discharge's peak the speed meets the falling target: the re-drift is never delayed.
            Assert.IsTrue(AIPilot.CanCommitAt(347f, 20f, holdsSpeed: true, throttleTarget: 347f, upToSpeedShare: 0.9f));
            // A legacy drift (throttle keeps driving speed) only asks the floor.
            Assert.IsTrue(AIPilot.CanCommitAt(30f, 20f, holdsSpeed: false, throttleTarget: 68f, upToSpeedShare: 0.9f));
        }

        [Test]
        public void DryCrawl_IsLeftAfterTheCap_OnlyWhenDry()
        {
            Assert.IsFalse(UrchinRailAssessment.DryCrawlTooLong(true, 10f, 13f, 4f), "3 s in: keep crawling to the ring");
            Assert.IsTrue(UrchinRailAssessment.DryCrawlTooLong(true, 10f, 14.5f, 4f), "4.5 s in: leave");
            Assert.IsFalse(UrchinRailAssessment.DryCrawlTooLong(false, 10f, 60f, 4f), "its own lane is never a crawl");
            Assert.IsFalse(UrchinRailAssessment.DryCrawlTooLong(true, -1f, 60f, 4f), "no crawl clock yet");
        }

        [Test]
        public void OwnLane_IsApproachedAwayFromTheRivalLaneBesideIt()
        {
            var own = new Vector3(0f, 0f, 22f);     // own lane point, 22 u from the rival lane at the origin
            var rival = Vector3.zero;
            var from = new Vector3(0f, 0f, 3f);     // just slipped off the rival lane
            var aim = RegattaController.OwnLaneApproach(own, from, rival, 3f, 19f, 30f, 20f);
            Assert.AreEqual(42f, aim.z, 1e-3f, "20 u past its own lane, straight away from the rival point");

            // Nearer its own lane than the rival's, or clear of every rival lane: the plain aim.
            Assert.AreEqual(own, RegattaController.OwnLaneApproach(own, from, rival, 15f, 7f, 30f, 20f));
            Assert.AreEqual(own, RegattaController.OwnLaneApproach(own, from, rival, 40f, 60f, 30f, 20f));
        }
    }
}
