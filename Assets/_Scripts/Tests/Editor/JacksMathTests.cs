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

        [Test]
        public void Net_OpensWithTheSticksApart_AndReadsBackFromTheFish()
        {
            // Garrett, 2026-10-24: "a net that opens wider with xdiff"
            Assert.AreEqual(0f, JacksMath.NetOpenFromSticks(Vector2.zero, Vector2.zero), Eps);
            Assert.AreEqual(1f, JacksMath.NetOpenFromSticks(Vector2.left, Vector2.right), Eps);
            Assert.AreEqual(0f, JacksMath.NetOpenFromSticks(Vector2.right, Vector2.left), Eps);   // together: closed
            Assert.AreEqual(0.5f, JacksMath.NetOpenFromSticks(new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f)), Eps);
            Assert.AreEqual(0f, JacksMath.NetOpenFromGap(28f, 28f, 128f), Eps);
            Assert.AreEqual(0.5f, JacksMath.NetOpenFromGap(78f, 28f, 128f), Eps);
            Assert.AreEqual(1f, JacksMath.NetOpenFromGap(500f, 28f, 128f), Eps);
        }

        [Test]
        public void Net_HoldsWhatIsBetweenTheFishAndBehindTheMouth()
        {
            // the fish 40 apart across the hull's x, flying +z: the bag hangs back toward -z
            var net = JacksMath.NetFrame.Between(new Vector3(-20f, 0f, 0f), new Vector3(20f, 0f, 0f), Vector3.forward,
                                                Vector3.up, 20f, 24f);
            Assert.Less(Vector3.Distance(net.Normal, Vector3.forward), Eps);
            Assert.IsTrue(net.Contains(new Vector3(0f, 0f, -10f), 0f, out float s, out float t, out float w));
            Assert.AreEqual(0.5f, s, Eps);
            Assert.AreEqual(0f, t, Eps);
            Assert.AreEqual(10f / 24f, w, Eps);
            Assert.IsFalse(net.Contains(new Vector3(0f, 0f, 5f), 0f, out _, out _, out _), "ahead of the mouth");
            Assert.IsFalse(net.Contains(new Vector3(0f, 0f, -30f), 0f, out _, out _, out _), "past the bag");
            Assert.IsFalse(net.Contains(new Vector3(30f, 0f, -1f), 0f, out _, out _, out _), "outside the fish");
            Assert.IsFalse(net.Contains(new Vector3(0f, 15f, -5f), 0f, out _, out _, out _), "above the mouth");
            // a point's (s, t, w) maps back to itself
            var p = net.Point(0.3f, -0.4f, 0.6f);
            Assert.IsTrue(net.Contains(p, 0.01f, out s, out t, out w));
            Assert.AreEqual(0.3f, s, Eps);
            Assert.AreEqual(-0.4f, t, Eps);
            Assert.AreEqual(0.6f, w, Eps);
        }

        [Test]
        public void ChaseCamera_BacksOffUntilTheNetIsInView()
        {
            float tan = Mathf.Tan(30f * Mathf.Deg2Rad);
            // a fish 50 to the side: at fill 1 the camera sits 50 / tan30 back
            Assert.AreEqual(50f / tan, JacksMath.ChaseDistanceFor(new Vector3(50f, 0f, 0f), tan, tan, 1f), 0.01f);
            // the bag behind the hull needs the camera further back by as much
            Assert.AreEqual(50f / tan + 20f, JacksMath.ChaseDistanceFor(new Vector3(50f, 0f, -20f), tan, tan, 1f), 0.01f);
            // a smaller share of the view pushes it back
            Assert.Greater(JacksMath.ChaseDistanceFor(new Vector3(50f, 0f, 0f), tan, tan, 0.5f),
                           JacksMath.ChaseDistanceFor(new Vector3(50f, 0f, 0f), tan, tan, 1f));
        }

        [Test]
        public void Net_SwingsOutAgainstTheTurn_InEveryDirection_WhileTheTurnLasts()
        {
            // Garrett, 2026-10-25: "i would expect this to work in any direction (including combinations like 30 degrees vs 70
            // degrees) and it should work whether changing or maintaining directions"
            var f = Vector3.forward;
            Assert.Less(Vector3.Distance(JacksMath.SwingBag(f, Vector3.zero, 0.6f, 1.2f), Vector3.back), Eps, "straight: back");
            foreach (float deg in new[] { 0f, 30f, 70f, 90f, 180f, 250f })
            {
                // the nose turning toward `dir` at 1 rad/s: the bag leans the other way, the same every frame it lasts
                var dir = Quaternion.AngleAxis(deg, f) * Vector3.up;
                var bag = JacksMath.SwingBag(f, dir, 0.6f, 1.2f);
                Assert.Less(Vector3.Dot(bag, f), 0f, $"{deg}: behind");
                var lean = Vector3.ProjectOnPlane(bag, f).normalized;
                Assert.Less(Vector3.Distance(lean, -dir), Eps, $"{deg}: out against the turn");
            }
            // turning up swings it down; turning left swings it right; capped
            var up = JacksMath.SwingBag(f, Vector3.up * 10f, 0.6f, 1.2f);
            Assert.Less(up.y, 0f);
            Assert.AreEqual(Mathf.Atan(1.2f), Vector3.Angle(up, Vector3.back) * Mathf.Deg2Rad, 1e-3f);
            Assert.Greater(JacksMath.SwingBag(f, Vector3.left, 0.6f, 1.2f).x, 0f);
        }

        [Test]
        public void Net_ALeaningBag_HoldsWhatHangsInIt()
        {
            // the bag swung sideways, along the fish line: a point it holds maps back to itself, and the mouth stays between the fish
            var bag = new Vector3(0.6f, 0f, -0.8f);
            var net = JacksMath.NetFrame.Between(new Vector3(-20f, 0f, 0f), new Vector3(20f, 0f, 0f), Vector3.forward, Vector3.up,
                                                bag, 20f, 24f);
            Assert.Less(Vector3.Distance(net.Bag, bag.normalized), Eps);
            var p = net.Point(0.4f, 0.3f, 0.7f);
            Assert.IsTrue(net.Contains(p, 0.01f, out float s, out float t, out float w));
            Assert.AreEqual(0.4f, s, Eps);
            Assert.AreEqual(0.3f, t, Eps);
            Assert.AreEqual(0.7f, w, Eps);
            Assert.IsFalse(net.Contains(new Vector3(0f, 0f, 3f), 0f, out _, out _, out _), "ahead of the mouth");
            // a bag laid flat against the mouth is stood back up
            var flat = JacksMath.NetFrame.Between(new Vector3(-20f, 0f, 0f), new Vector3(20f, 0f, 0f), Vector3.forward, Vector3.up,
                                                 Vector3.right, 20f, 24f);
            Assert.GreaterOrEqual(-Vector3.Dot(flat.Bag, flat.Normal), 0.29f);
        }

        [Test]
        public void Net_ASurfacePoint_LiesAcrossTheMesh_FacingTheMouth()
        {
            // what is stuck in the net lies flat against it (2026-10-25): along the fish line at the bottom of the bag, standing
            // out toward the mouth, and the normal is square to both directions of the mesh everywhere
            var net = JacksMath.NetFrame.Between(new Vector3(-20f, 0f, 0f), new Vector3(20f, 0f, 0f), Vector3.forward, Vector3.up,
                                                20f, 24f);
            net.SurfaceAt(0.5f, 0f, 1f, out var along, out var normal);
            Assert.Less(Vector3.Distance(along, Vector3.right), Eps, "across the bottom of the bag: along the fish line");
            Assert.Less(Vector3.Distance(normal, Vector3.forward), Eps, "facing the mouth");
            foreach (var (s, t) in new[] { (0.2f, 0.5f), (0.8f, -0.6f), (0.5f, 0.9f), (0.05f, 0f) })
            {
                net.SurfaceAt(s, t, 1f, out along, out normal);
                const float h = 1e-3f;
                var ds = net.Point(s + h, t, 1f) - net.Point(s - h, t, 1f);
                var dt = net.Point(s, t + h, 1f) - net.Point(s, t - h, 1f);
                Assert.Less(Mathf.Abs(Vector3.Dot(normal, ds.normalized)), 1e-2f, $"square to the mesh along s at ({s}, {t})");
                Assert.Less(Mathf.Abs(Vector3.Dot(normal, dt.normalized)), 1e-2f, $"square to the mesh along t at ({s}, {t})");
                Assert.Greater(Vector3.Dot(normal, net.Normal), 0f, "toward the mouth");
            }
        }
    }
}
