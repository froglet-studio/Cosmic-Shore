using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Caleb's fleet fly test after the distance lay (2026-11-18; <c>Docs/Manta/POLISH.md</c>, round "fleet fly test"):
    /// the Dolphin's drift line, the Urchin's ride over odd prisms, and the Thresher's Mass Mode trail.
    /// </summary>
    public class TrailFlyTestFixesTests
    {
        const float Tol = 1e-4f;

        // ---- Dolphin: drift prisms keep the nose's angle and the drift trail's length (the trail tells the story),
        // but are slid back so none of them lies ahead of the nose, where the hull flies when the drift ends.

        [Test]
        public void Dolphin_DriftKeepsNoseClear()
        {
            string yaml = File.ReadAllText("Assets/_Prefabs/Spacevessels/Dolphin.prefab");
            StringAssert.Contains("\n  driftKeepsNoseClear: 1\n", yaml);
            StringAssert.Contains("\n  driftNoseClearance: 2\n", yaml);
        }

        [Test]
        public void DriftNoseClear_SidewaysDrift_PutsTheWholePrismBehindTheNose()
        {
            // A 48 u drift prism along the nose (+z) while the hull travels sideways (+x).
            Vector3 shift = VesselPrismController.DriftNoseClearShift(Vector3.forward, Vector3.right, 48f, 2f);
            Assert.AreEqual(-26f, shift.z, Tol);
            Assert.AreEqual(0f, shift.x, Tol, "only along the prism's own axis: its angle and length are untouched");
            float frontEnd = shift.z + 24f;
            Assert.LessOrEqual(frontEnd, -2f + Tol, "the front end sits the clearance behind the nose");
        }

        [Test]
        public void DriftNoseClear_IsFullUntilTheNoseTurnsBackward_ThenFadesOut()
        {
            Vector3 aligned = VesselPrismController.DriftNoseClearShift(Vector3.forward, Vector3.forward, 8f, 2f);
            Assert.AreEqual(-6f, aligned.z, Tol);
            Vector3 broadside = VesselPrismController.DriftNoseClearShift(Vector3.forward, Vector3.right, 8f, 2f);
            Assert.AreEqual(-6f, broadside.z, Tol);
            Vector3 backward = VesselPrismController.DriftNoseClearShift(Vector3.forward, -Vector3.forward, 8f, 2f);
            Assert.AreEqual(0f, backward.magnitude, Tol, "flying backwards it would be slid into the path ahead");
        }

        // ---- Urchin: the ride's surface offset is capped, whatever the prism.

        [Test]
        public void RideSurface_SquarePrism_IsItsHalfHeightPlusClearance()
        {
            float d = GunVesselTransformer.RideSurfaceDistance(new Vector3(1f, 1.25f, 2f), Vector3.up,
                Vector3.right, Vector3.up, Vector3.forward, 1.5f, 8f);
            Assert.AreEqual(2.75f, d, Tol);
        }

        [Test]
        public void RideSurface_LongPrismYawedOffTheRibbon_IsCapped()
        {
            // A 48 u drift block lying across the ribbon: its support along the radial is half its length.
            Vector3 radial = Vector3.right;
            float d = GunVesselTransformer.RideSurfaceDistance(new Vector3(0.5f, 0.75f, 24f), radial,
                Vector3.forward, Vector3.up, Vector3.right, 1.5f, 8f);
            Assert.AreEqual(8f, d, Tol, "was 25.5 u, which threw the hull and camera off the trail");
        }

        [Test]
        public void RideSurface_NeverBelowTheClearance()
        {
            float d = GunVesselTransformer.RideSurfaceDistance(Vector3.one * 50f, Vector3.up,
                Vector3.right, Vector3.up, Vector3.forward, 1.5f, 0f);
            Assert.AreEqual(1.5f, d, Tol);
        }

        // ---- Thresher: the ball trail's tiles keep one roll, and the hull stops laying in Mass Mode.

        [Test]
        public void BallTrailUp_SeedsTowardTheShip()
        {
            Vector3 up = ThresherExecutor.BallTrailUp(Vector3.zero, Vector3.forward, new Vector3(0f, 5f, -3f));
            Assert.AreEqual(1f, Vector3.Dot(up, Vector3.up), Tol);
        }

        [Test]
        public void BallTrailUp_IsCarriedSmoothly_WhileTheShipSwingsAround()
        {
            // The ball's path bends gently while the ship circles it fast: the old rule (up = toward the ship)
            // spun each tile with the ship; the carried up turns no more than the path does.
            Vector3 up = Vector3.zero;
            Vector3 lastDir = Vector3.forward;
            for (int i = 0; i < 200; i++)
            {
                Vector3 dir = Quaternion.AngleAxis(i * 0.5f, Vector3.up) * Vector3.forward;
                Vector3 towardShip = Quaternion.AngleAxis(i * 23f, dir) * Vector3.up;
                Vector3 next = ThresherExecutor.BallTrailUp(up, dir, towardShip);
                Assert.AreEqual(0f, Vector3.Dot(next, dir), 1e-3f, "up stays across the path");
                Assert.AreEqual(1f, next.magnitude, 1e-3f);
                if (i > 0)
                    Assert.LessOrEqual(Vector3.Angle(up, next), Vector3.Angle(lastDir, dir) + 0.01f, $"lay {i}");
                up = next;
                lastDir = dir;
            }
        }

        [Test]
        public void BallTrailUp_ADeliberateRollStillTwistsTheRibbon()
        {
            // The hull rolls 30 degrees about the path between two lays: the carried up turns with it.
            Vector3 dir = Vector3.forward;
            Vector3 up = Vector3.up;
            Quaternion roll = Quaternion.AngleAxis(30f, dir);
            Vector3 next = ThresherExecutor.BallTrailUp(roll * up, dir, Vector3.up);
            Assert.AreEqual(30f, Vector3.Angle(up, next), 1e-2f);
        }

        [Test]
        public void Thresher_LaysNothingWithBothTriggersHeld_AndTheHullNothingInMassMode()
        {
            string src = File.ReadAllText("Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ThresherExecutor.cs");
            StringAssert.Contains("bool TrailPenWanted(bool launchView) => launchView || _massMode || BothTriggersHeld;", src);
            StringAssert.Contains("HoldTrailPen(TrailPenWanted(up));", src);
            StringAssert.Contains("HoldTrailPen(TrailPenWanted(LaunchViewUp));", src);
            Assert.AreEqual(2, Regex.Matches(src, @"if \(_massMode && !BothTriggersHeld\) LayBallTrail\(").Count,
                "both ball-trail lays are gated");
        }
    }
}
