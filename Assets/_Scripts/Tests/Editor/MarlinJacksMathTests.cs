using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Marlin and Jacks' arithmetic (<c>R_VesselActions/MARLIN_JACKS.md</c>), held to the Swarm Vessel Lab's own formulas
    /// (<c>Docs/Studios/SwarmVesselLab/src/shepherd.js</c>, <c>SEL</c>). Each test names the decision it guards
    /// (Garrett, 2026-10-20 and 2026-10-21), so a failure says which one moved.
    /// </summary>
    [TestFixture]
    public class MarlinJacksMathTests
    {
        const float Eps = 1e-3f;

        [Test]
        public void RingFloor_IsLargerThanABigThumb_LabNumbers()
        {
            // the lab's floorU at its shipped settings: 22 mm thumb, 1.25 margin, 1800 u across the short side
            Assert.AreEqual(399.8077f, MarlinJacksMath.FloorUnits(22f, 1.25f, 1800f), Eps);
            // the game's view is the lab's x 0.4, so the floor scales with it
            Assert.AreEqual(399.8077f * 0.4f, MarlinJacksMath.FloorUnits(22f, 1.25f, 720f), Eps);
            // a margin above 1 keeps the ring wider than the thumb's own disc
            Assert.Greater(MarlinJacksMath.FloorUnits(22f, 1.25f, 1800f), MarlinJacksMath.FloorUnits(22f, 1f, 1800f));
        }

        [Test]
        public void Ring_IsTheFloorAtSpaceMinus5_AndNeverBelowIt()
        {
            Assert.AreEqual(100f, MarlinJacksMath.Ring(100f, -5f), Eps);
            Assert.AreEqual(100f, MarlinJacksMath.Ring(100f, -12f), Eps);
            Assert.AreEqual(130f, MarlinJacksMath.Ring(100f, 0f), Eps);
            Assert.AreEqual(220f, MarlinJacksMath.Ring(100f, 15f), Eps);
        }

        [Test]
        public void Time_SetsTheHerdSpeed_FlooredAt40Percent()
        {
            Assert.AreEqual(1f, MarlinJacksMath.SpeedMultiplier(0f), Eps);
            Assert.AreEqual(0.7f, MarlinJacksMath.SpeedMultiplier(-5f), Eps);
            Assert.AreEqual(1.9f, MarlinJacksMath.SpeedMultiplier(15f), Eps);
            Assert.AreEqual(0.4f, MarlinJacksMath.SpeedMultiplier(-20f), Eps);
        }

        [Test]
        public void Mass_SetsTheBirthRate_TheSpeciesOwnAtMinus5_AThirdAt15()
        {
            Assert.AreEqual(1f, MarlinJacksMath.FeedsPerOffspringScale(-5f), Eps);
            Assert.AreEqual(0.5f, MarlinJacksMath.FeedsPerOffspringScale(5f), Eps);
            Assert.AreEqual(1f / 3f, MarlinJacksMath.FeedsPerOffspringScale(15f), Eps);
        }

        [Test]
        public void Charge_SetsTheDangerShare_NoneAtMinus5_AllAt15()
        {
            Assert.AreEqual(0f, MarlinJacksMath.DangerShare(-5f), Eps);
            Assert.AreEqual(0.5f, MarlinJacksMath.DangerShare(5f), Eps);
            Assert.AreEqual(1f, MarlinJacksMath.DangerShare(15f), Eps);
            Assert.IsFalse(MarlinJacksMath.IsDanger(0f, -5f), "at -5 no creature is danger");
            Assert.IsTrue(MarlinJacksMath.IsDanger(0.999f, 15f), "at 15 every creature is danger");
        }

        [Test]
        public void Roll01_IsStableAndInRange()
        {
            for (int id = -50; id < 50; id++)
            {
                float r = MarlinJacksMath.Roll01(id);
                Assert.That(r, Is.InRange(0f, 1f));
                Assert.AreEqual(r, MarlinJacksMath.Roll01(id));
            }
        }

        [Test]
        public void Repel_PushesOnlyInsideTheRing_AlongThePlane()
        {
            var up = Vector3.up;
            var push = MarlinJacksMath.Repel(new Vector3(50f, 30f, 0f), Vector3.zero, 100f, up, out float panic);
            Assert.AreEqual(0.5f, panic, Eps);
            Assert.AreEqual(1.6f, push.x, Eps);    // 3.2 x (1 - 50/100), height ignored: a cylinder read from above
            Assert.AreEqual(0f, push.y, Eps);
            Assert.AreEqual(Vector3.zero, MarlinJacksMath.Repel(new Vector3(150f, 0f, 0f), Vector3.zero, 100f, up, out panic));
            Assert.AreEqual(0f, panic, Eps);
        }

        [Test]
        public void Call_PullsTowardTheMarlin_BeyondItsDeadZone()
        {
            var pull = MarlinJacksMath.CallPull(new Vector3(100f, 0f, 0f), Vector3.zero, Vector3.up, 12f);
            Assert.AreEqual(-2.6f, pull.x, Eps);
            Assert.AreEqual(Vector3.zero, MarlinJacksMath.CallPull(new Vector3(5f, 0f, 0f), Vector3.zero, Vector3.up, 12f));
        }

        [Test]
        public void PlaneSpeed_DrawsTheHerdBackToThePlane()
        {
            Assert.Less(MarlinJacksMath.PlaneSpeed(50f, 0f, 1.6f, 1000f), 0f, "above the plane it heads down");
            Assert.Greater(MarlinJacksMath.PlaneSpeed(-50f, 0f, 1.6f, 1000f), 0f, "below the plane it heads up");
            Assert.AreEqual(-20f, MarlinJacksMath.PlaneSpeed(500f, 0f, 1.6f, 20f), Eps, "capped at its top speed");
        }

        [Test]
        public void StickRun_HasTheLabsDeadZoneAndCurve()
        {
            Assert.AreEqual(Vector2.zero, MarlinJacksMath.StickRun(new Vector2(0.1f, 0.1f), 100f, 1f));
            var full = MarlinJacksMath.StickRun(new Vector2(0f, 1f), 100f, 0.5f);
            Assert.AreEqual(50f, full.y, Eps);
            var half = MarlinJacksMath.StickRun(new Vector2(0f, 0.575f), 100f, 1f);   // 0.5 after the dead zone
            Assert.AreEqual(100f * Mathf.Pow(0.5f, 1.4f), half.y, Eps);
        }

        [Test]
        public void SteadyView_PansOnlyOutsideTheSlackBox()
        {
            Assert.AreEqual(Vector2.zero, MarlinJacksMath.SteadyPan(40f, -40f, 50f, 50f, 0.1f));
            var pan = MarlinJacksMath.SteadyPan(150f, 0f, 50f, 50f, 0.25f);
            Assert.AreEqual(100f * 0.4f, pan.x, Eps);
        }

        [Test]
        public void JacksSeat_FramesTheHerdAlone_SoTheMarlinNeverPansTheView()
        {
            // Garrett, 2026-10-21: "the marlin makes the jacks player sick with its movement"
            var herd = new Vector3(10f, 0f, 0f);
            var marlin = new Vector3(500f, 0f, 0f);
            Assert.AreEqual(herd, MarlinJacksMath.FrameGoal(herd, 5, marlin, 0f, Vector3.zero));
            Assert.AreEqual(new Vector3(-3f, 0f, 0f), MarlinJacksMath.FrameGoal(herd, 0, marlin, 0f, new Vector3(-3f, 0f, 0f)),
                            "no herd: the given fallback (the jacks), not the marlin");
        }

        [Test]
        public void PlaneEase_ZeroHoldsTheBoardStill()
        {
            Assert.AreEqual(0f, MarlinJacksMath.Ease(0f, 0.016f));
            Assert.Less(MarlinJacksMath.Ease(0.25f, 0.016f), MarlinJacksMath.Ease(3f, 0.016f));
        }

        [Test]
        public void SteadyView_NeverTurnsWithTheMarlinsHeading()
        {
            var f = MarlinJacksMath.SteadyForward(Vector3.forward, Vector3.up, Vector3.right);
            Assert.AreEqual(Vector3.forward, f);
        }
    }
}
