using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Jacks' arithmetic (<c>R_VesselActions/JACKS.md</c>), held to the Swarm Vessel Lab's own formulas
    /// (<c>Docs/Studios/SwarmVesselLab/src/shepherd.js</c>, <c>SEL</c>). Each test names the decision it guards
    /// (Garrett, 2026-10-20 and 2026-10-21), so a failure says which one moved.
    /// </summary>
    [TestFixture]
    public class JacksMathTests
    {
        const float Eps = 1e-3f;

        [Test]
        public void RingFloor_IsLargerThanABigThumb_LabNumbers()
        {
            // the lab's floorU at its shipped settings: 22 mm thumb, 1.25 margin, 1800 u across the short side
            Assert.AreEqual(399.8077f, JacksMath.FloorUnits(22f, 1.25f, 1800f), Eps);
            // the game's view is the lab's x 0.4, so the floor scales with it
            Assert.AreEqual(399.8077f * 0.4f, JacksMath.FloorUnits(22f, 1.25f, 720f), Eps);
            // a margin above 1 keeps the ring wider than the thumb's own disc
            Assert.Greater(JacksMath.FloorUnits(22f, 1.25f, 1800f), JacksMath.FloorUnits(22f, 1f, 1800f));
        }

        [Test]
        public void JacksView_ZoomsOutUntilABigThumbBarelyCoversTheSmallestRing()
        {
            // Garrett, 2026-10-21: "zoomed out enough that on a full size mobile device an above average sized thumb barely
            // covers the smallest jack repulsion radius". The shipped floor (159.9 u), a 22 mm thumb, 0.9 cover, a 71 mm phone.
            float floor = JacksMath.FloorUnits(22f, 1.25f, 720f);
            float span = JacksMath.ThumbViewSpan(floor, 22f, 0.9f, 71f);
            Assert.AreEqual(2f * floor * 71f / (22f * 0.9f), span, Eps);
            // the ring's diameter on that phone's screen is the cover times the thumb
            Assert.AreEqual(22f * 0.9f, 2f * floor / span * 71f, Eps);
            // wider than the lab's 720 u view (where the ring was 1.25 thumbs on a smaller phone): the view zoomed out
            Assert.Greater(span, 720f);
            // a lower cover zooms out further; the ring's world size is the caller's and does not move
            Assert.Greater(JacksMath.ThumbViewSpan(floor, 22f, 0.7f, 71f), span);
        }

        [Test]
        public void Ring_IsTheFloorAtSpaceMinus5_AndNeverBelowIt()
        {
            Assert.AreEqual(100f, JacksMath.Ring(100f, -5f), Eps);
            Assert.AreEqual(100f, JacksMath.Ring(100f, -12f), Eps);
            Assert.AreEqual(130f, JacksMath.Ring(100f, 0f), Eps);
            Assert.AreEqual(220f, JacksMath.Ring(100f, 15f), Eps);
        }

        [Test]
        public void Time_SetsTheHerdSpeed_FlooredAt40Percent()
        {
            Assert.AreEqual(1f, JacksMath.SpeedMultiplier(0f), Eps);
            Assert.AreEqual(0.7f, JacksMath.SpeedMultiplier(-5f), Eps);
            Assert.AreEqual(1.9f, JacksMath.SpeedMultiplier(15f), Eps);
            Assert.AreEqual(0.4f, JacksMath.SpeedMultiplier(-20f), Eps);
        }

        [Test]
        public void Mass_SetsTheBirthRate_TheSpeciesOwnAtMinus5_AThirdAt15()
        {
            Assert.AreEqual(1f, JacksMath.FeedsPerOffspringScale(-5f), Eps);
            Assert.AreEqual(0.5f, JacksMath.FeedsPerOffspringScale(5f), Eps);
            Assert.AreEqual(1f / 3f, JacksMath.FeedsPerOffspringScale(15f), Eps);
        }

        [Test]
        public void Charge_SetsTheDangerShare_NoneAtMinus5_AllAt15()
        {
            Assert.AreEqual(0f, JacksMath.DangerShare(-5f), Eps);
            Assert.AreEqual(0.5f, JacksMath.DangerShare(5f), Eps);
            Assert.AreEqual(1f, JacksMath.DangerShare(15f), Eps);
            Assert.IsFalse(JacksMath.IsDanger(0f, -5f), "at -5 no creature is danger");
            Assert.IsTrue(JacksMath.IsDanger(0.999f, 15f), "at 15 every creature is danger");
        }

        [Test]
        public void Roll01_IsStableAndInRange()
        {
            for (int id = -50; id < 50; id++)
            {
                float r = JacksMath.Roll01(id);
                Assert.That(r, Is.InRange(0f, 1f));
                Assert.AreEqual(r, JacksMath.Roll01(id));
            }
        }

        [Test]
        public void Repel_PushesOnlyInsideTheRing_AlongThePlane()
        {
            var up = Vector3.up;
            var push = JacksMath.Repel(new Vector3(50f, 30f, 0f), Vector3.zero, 100f, up, out float panic);
            Assert.AreEqual(0.5f, panic, Eps);
            Assert.AreEqual(1.6f, push.x, Eps);    // 3.2 x (1 - 50/100), height ignored: a cylinder read from above
            Assert.AreEqual(0f, push.y, Eps);
            Assert.AreEqual(Vector3.zero, JacksMath.Repel(new Vector3(150f, 0f, 0f), Vector3.zero, 100f, up, out panic));
            Assert.AreEqual(0f, panic, Eps);
        }

        [Test]
        public void Call_PullsTowardTheJacks_BeyondItsDeadZone()
        {
            var pull = JacksMath.CallPull(new Vector3(100f, 0f, 0f), Vector3.zero, Vector3.up, 12f);
            Assert.AreEqual(-2.6f, pull.x, Eps);
            Assert.AreEqual(Vector3.zero, JacksMath.CallPull(new Vector3(5f, 0f, 0f), Vector3.zero, Vector3.up, 12f));
        }

        [Test]
        public void PlaneSpeed_DrawsTheHerdBackToThePlane()
        {
            Assert.Less(JacksMath.PlaneSpeed(50f, 0f, 1.6f, 1000f), 0f, "above the plane it heads down");
            Assert.Greater(JacksMath.PlaneSpeed(-50f, 0f, 1.6f, 1000f), 0f, "below the plane it heads up");
            Assert.AreEqual(-20f, JacksMath.PlaneSpeed(500f, 0f, 1.6f, 20f), Eps, "capped at its top speed");
        }

        [Test]
        public void StickRun_HasTheLabsDeadZoneAndCurve()
        {
            Assert.AreEqual(Vector2.zero, JacksMath.StickRun(new Vector2(0.1f, 0.1f), 100f, 1f));
            var full = JacksMath.StickRun(new Vector2(0f, 1f), 100f, 0.5f);
            Assert.AreEqual(50f, full.y, Eps);
            var half = JacksMath.StickRun(new Vector2(0f, 0.575f), 100f, 1f);   // 0.5 after the dead zone
            Assert.AreEqual(100f * Mathf.Pow(0.5f, 1.4f), half.y, Eps);
        }

        [Test]
        public void SteadyView_PansOnlyOutsideTheSlackBox()
        {
            Assert.AreEqual(Vector2.zero, JacksMath.SteadyPan(40f, -40f, 50f, 50f, 0.1f));
            var pan = JacksMath.SteadyPan(150f, 0f, 50f, 50f, 0.25f);
            Assert.AreEqual(100f * 0.4f, pan.x, Eps);
        }

        [Test]
        public void Herding_FramesTheHerdAlone_SoTheHullNeverPansTheView()
        {
            // Garrett, 2026-10-21: "make things as stable for the jack player as we can"
            var herd = new Vector3(10f, 0f, 0f);
            var hull = new Vector3(500f, 0f, 0f);
            Assert.AreEqual(herd, JacksMath.FrameGoal(herd, 5, hull, 0f, Vector3.zero));
            Assert.AreEqual(new Vector3(-3f, 0f, 0f), JacksMath.FrameGoal(herd, 0, hull, 0f, new Vector3(-3f, 0f, 0f)),
                            "no herd: the given fallback (the jacks), not the hull");
        }

        [Test]
        public void Flying_EachFishSitsUnderItsThumb()
        {
            // Garrett, 2026-10-21: "move one fish up and the other down to roll, or move both together for pitch and yaw,
            // and bring them apart to speed up, bring them together to slow down"
            Assert.AreEqual(new Vector3(-14f, 0f, 0f), JacksMath.FlightOffset(-1f, Vector2.zero, 14f, 10f), "at rest: left of centre");
            Assert.AreEqual(new Vector3(14f, 0f, 0f), JacksMath.FlightOffset(1f, Vector2.zero, 14f, 10f), "at rest: right of centre");
            // a roll: left thumb up, right thumb down
            Assert.AreEqual(10f, JacksMath.FlightOffset(-1f, Vector2.up, 14f, 10f).y, Eps);
            Assert.AreEqual(-10f, JacksMath.FlightOffset(1f, Vector2.down, 14f, 10f).y, Eps);
            // apart: further from each other
            Assert.AreEqual(-24f, JacksMath.FlightOffset(-1f, Vector2.left, 14f, 10f).x, Eps);
            Assert.AreEqual(24f, JacksMath.FlightOffset(1f, Vector2.right, 14f, 10f).x, Eps);
            // together never crosses while reach < gap, and a corner stick is clamped to the unit disc
            Assert.Less(JacksMath.FlightOffset(-1f, Vector2.right, 14f, 10f).x, 0f);
            Assert.AreEqual(10f, (JacksMath.FlightOffset(1f, new Vector2(1f, 1f), 14f, 10f) - new Vector3(14f, 0f, 0f)).magnitude, Eps);
            Assert.AreEqual(0f, JacksMath.FlightOffset(1f, Vector2.one, 14f, 10f).z);
        }

        [Test]
        public void PlaneEase_ZeroHoldsTheBoardStill()
        {
            Assert.AreEqual(0f, JacksMath.Ease(0f, 0.016f));
            Assert.Less(JacksMath.Ease(0.25f, 0.016f), JacksMath.Ease(3f, 0.016f));
        }

        [Test]
        public void SteadyView_NeverTurnsWithTheHullsHeading()
        {
            var f = JacksMath.SteadyForward(Vector3.forward, Vector3.up, Vector3.right);
            Assert.AreEqual(Vector3.forward, f);
        }
    }
}
