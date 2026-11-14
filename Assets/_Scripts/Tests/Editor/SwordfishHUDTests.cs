using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The Swordfish's own Charge card (2026-11-12): the bill turns with the side swing and is foreshortened
    /// only while it points ahead (specs/swordfish.py bill_pose).</summary>
    public class SwordfishHUDTests
    {
        [Test]
        public void TheBillIsForeshortenedAheadAndFullOutToASideOrRaised()
        {
            var rest = SwordfishVesselHUDView.BillPose(0f, 0f, 55f, 0.65f);
            Assert.AreEqual(0f, rest.AngleZ, 1e-4f);
            Assert.AreEqual(0.65f, rest.Length01, 1e-4f);

            var right = SwordfishVesselHUDView.BillPose(1f, 0f, 55f, 0.65f);
            Assert.AreEqual(-55f, right.AngleZ, 1e-4f, "a swing right turns it clockwise");
            Assert.AreEqual(1f, right.Length01, 1e-4f);

            var raised = SwordfishVesselHUDView.BillPose(0f, 1f, 55f, 0.65f);
            Assert.AreEqual(0f, raised.AngleZ, 1e-4f);
            Assert.AreEqual(1f, raised.Length01, 1e-4f);
        }

        [Test]
        public void TheReachPrismSitsOnTheStraightBillsLine()
        {
            var c = SwordfishVesselHUDView.ReachPrismCentre(new Vector2(20f, 112f), 52f, 68f, 13.12f, 0f);
            float a = 52f * Mathf.Deg2Rad;
            Assert.AreEqual(20f + Mathf.Sin(a) * 81.12f, c.x, 1e-3f);
            Assert.AreEqual(112f - Mathf.Cos(a) * 81.12f, c.y, 1e-3f);
        }
    }
}
