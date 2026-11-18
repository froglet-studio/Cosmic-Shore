using System.IO;
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

        // ---- Dolphin: a drift lays along the path, so its long drift prisms no longer stack across it.

        [Test]
        public void Dolphin_DriftLaysAlongCourse()
        {
            string yaml = File.ReadAllText("Assets/_Prefabs/Spacevessels/Dolphin.prefab");
            StringAssert.Contains("\n  driftLaysAlongCourse: 1\n", yaml);
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
        public void Thresher_HullPenIsUpInMassMode()
        {
            string src = File.ReadAllText("Assets/_Scripts/Controller/Vessel/R_VesselActions/Executors/ThresherExecutor.cs");
            StringAssert.Contains("HoldTrailPen(up || _massMode);", src);
            StringAssert.Contains("HoldTrailPen(LaunchViewUp || _massMode);", src);
        }
    }
}
