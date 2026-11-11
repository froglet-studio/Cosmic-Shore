using System.IO;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Manta's trail is shaped by speed and turning (Caleb, 2026-11-11, Manta Polish round "speed-shaped trail
    /// prisms", <c>Docs/Manta/POLISH.md</c>). The rule is the Manta Trail Lab's "Free curves"
    /// (<c>Docs/Studios/MantaTrailLab.html</c>, <c>shapeFor('free')</c>) at the numbers he recorded in the lab's
    /// decision log. Every expected value below was read from the lab page itself (Chromium, 2026-11-11), so a drift
    /// between the C# port and the lab fails here.
    ///
    /// <para>Also locks the prefab: the Manta authors the shape with those numbers, and the environment sizing it
    /// replaced (<c>ScoutTrailPrismScaler</c>) is no longer on the hull.</para>
    /// </summary>
    public class TrailSpeedShapeTests
    {
        const string MantaPrefabPath = "Assets/_Prefabs/Spacevessels/Manta.prefab";
        const string ScoutTrailPrismScalerGuid = "6b1f12eb3ed4ed94c9a10d3834dc7d4c";

        // Manta.prefab VesselPrismController: BaseScale 20 x 0.75 x 5.5, Gap 18, initialWavelength 10.
        const float BaseY = 0.75f, BaseZ = 5.5f, HalfGap = 9f, Wavelength = 10f;
        const float Tol = 1e-3f;

        static TrailSpeedShape Picked() => new() { Enabled = true };   // the class defaults ARE the pick

        static float LabSpacing(float speed) => Mathf.Max(Wavelength, speed / 60f);   // the lab at 60 fps

        static Vector3 Straight(float speed) =>
            Picked().Evaluate(BaseY, BaseZ, HalfGap, speed, 0f, 0f, 1, LabSpacing(speed));

        [TestCase(20f, 2.9228395f, 5.5f)]
        [TestCase(36f, 1.9873333f, 5.5f)]
        [TestCase(60f, 0.75f, 5.5f)]
        [TestCase(162f, 0.75f, 5.5f)]
        [TestCase(300f, 0.75f, 6.5586337f)]
        [TestCase(450f, 0.75f, 8.7781704f)]
        [TestCase(600f, 0.75f, 9f)]
        [TestCase(700f, 0.75f, 10f)]
        public void Straight_MatchesTheLab(float speed, float y, float z)
        {
            var s = Straight(speed);
            Assert.AreEqual(8f, s.x, Tol, "width is uniform");
            Assert.AreEqual(y, s.y, Tol, "height");
            Assert.AreEqual(z, s.z, Tol, "length");
        }

        [Test]
        public void Width_IsTheSameAtEverySpeedAndTurn()
        {
            foreach (float v in new[] { 4f, 36f, 162f, 650f })
            foreach (float turn in new[] { 0f, 1f })
            foreach (int side in new[] { -1, 1 })
                Assert.AreEqual(8f, Picked().Evaluate(BaseY, BaseZ, HalfGap, v, turn, turn * 1.047f, side, 10f).x, Tol);
        }

        [Test]
        public void YawsteryAtCruise_InsideShortensAndGrows_OutsideStretchesAndThins()
        {
            // Full right trigger at cruise: 60 deg/s (MantaAnalogTurnBoostExecutor.maxYawDegPerSec) = 1.047 rad/s.
            var inside = Picked().Evaluate(BaseY, BaseZ, HalfGap, 162f, 1f, 1.047f, +1, Wavelength);
            var outside = Picked().Evaluate(BaseY, BaseZ, HalfGap, 162f, 1f, 1.047f, -1, Wavelength);
            Assert.AreEqual(1.35f, inside.y, Tol);
            Assert.AreEqual(5.0378981f, inside.z, Tol);
            Assert.AreEqual(1.0125f, outside.y, Tol);
            Assert.AreEqual(5.9621019f, outside.z, Tol);
        }

        [Test]
        public void Length_NeverOverrunsTheSpacingCap()
        {
            var shape = Picked();
            for (float v = 4f; v <= 1200f; v += 7f)
            {
                float spacing = LabSpacing(v);
                Assert.LessOrEqual(shape.Evaluate(BaseY, BaseZ, HalfGap, v, 0f, 0f, 1, spacing).z,
                    spacing * shape.MaxLengthOfSpacing + Tol, $"at {v} u/s");
            }
        }

        [Test]
        public void OffByDefault()
        {
            Assert.IsFalse(new TrailSpeedShape().Enabled, "a vessel that never authors the shape lays what it did before");
        }

        [Test]
        public void MantaPrefab_AuthorsThePick_AndNoLongerSizesFromItsSurroundings()
        {
            string yaml = File.ReadAllText(MantaPrefabPath);
            StringAssert.DoesNotContain(ScoutTrailPrismScalerGuid, yaml, "ScoutTrailPrismScaler is retired from the Manta");
            int at = yaml.IndexOf("  speedShape:\n", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "Manta.prefab's VesselPrismController authors speedShape");
            string block = yaml.Substring(at, Mathf.Min(900, yaml.Length - at));
            foreach (string line in new[]
                     {
                         "    Enabled: 1", "    RailWidth: 8", "    ReferenceSpeed: 162", "    SoarSpeed: 600",
                         "    SoarLength: 10", "    MaxLengthOfSpacing: 0.9", "    FrameAwareSpacing: 1",
                         "    CrawlSpeed: 60", "    CrawlHeightMultiplier: 4", "    YawsteryHeightGain: 0.8",
                         "    OutsideThin: 0.25", "    MinHeight: 0.1", "    MaxHeight: 3.5", "    TurnGeometry: 1",
                     })
                StringAssert.Contains(line + "\n", block);
        }
    }
}
