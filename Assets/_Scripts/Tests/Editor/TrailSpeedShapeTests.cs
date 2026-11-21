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
    /// decision log. Every expected shape below was read from the lab page itself (Chromium, 2026-11-11; above Soar re-read 2026-11-12
    /// after the gap fix), so a drift
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

        static float LabSpacing(float speed) => Picked().LaySpacing(Wavelength, speed);   // the lab's laySpacing

        static Vector3 Straight(float speed) =>
            Picked().Evaluate(BaseY, BaseZ, HalfGap, speed, 0f, 0f, 1, LabSpacing(speed));

        [TestCase(20f, 2.9228395f, 5.5f)]
        [TestCase(36f, 1.9873333f, 5.5f)]
        [TestCase(60f, 0.75f, 5.5f)]
        [TestCase(162f, 0.75f, 5.5f)]
        [TestCase(300f, 0.75f, 6.5586337f)]
        [TestCase(450f, 0.75f, 8.7781704f)]
        [TestCase(600f, 0.75f, 9f)]
        [TestCase(700f, 0.7032f, 10.872f)]
        [TestCase(850f, 0.525f, 15f)]
        [TestCase(1080f, 0.3021024f, 19.95328f)]
        [TestCase(1100f, 0.3f, 20f)]
        [TestCase(1400f, 0.3f, 20f)]
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
        public void AboveSoar_TheTrailStretches_FurtherApartLongerThinner_NeverTouching()
        {
            // Caleb, 2026-11-12: "increase the distance between prisms at higher speed while thinning them in Y and
            // stretching them in Z ... the whole thing stretches at the highest speeds while production struggles."
            var shape = Picked();
            Assert.AreEqual(10f, shape.LaySpacing(Wavelength, 600f), Tol, "Soar keeps the wavelength");
            Assert.AreEqual(30f, shape.LaySpacing(Wavelength, 1100f), Tol, "Top Speed lays 3x apart");
            float lastSp = 0f, lastZ = 0f, lastY = float.MaxValue;
            for (float v = 600f; v <= 1100f; v += 25f)
            {
                float sp = shape.LaySpacing(Wavelength, v);
                var s = Straight(v);
                Assert.GreaterOrEqual(sp, lastSp - Tol, $"spacing grows at {v}");
                Assert.GreaterOrEqual(s.z, lastZ - Tol, $"length grows at {v}");
                Assert.LessOrEqual(s.y, lastY + Tol, $"height thins at {v}");
                Assert.Less(s.z, sp, $"a gap at {v}: never touching");
                lastSp = sp; lastZ = s.z; lastY = s.y;
            }
            // Production struggles: mass per metre at Top Speed is under a third of Soar's.
            var soar = Straight(600f); var top = Straight(1100f);
            float perMetreSoar = soar.x * soar.y * soar.z / LabSpacing(600f), perMetreTop = top.x * top.y * top.z / LabSpacing(1100f);
            Assert.Less(perMetreTop, perMetreSoar / 3f);
        }

        // Every prism exactly one spacing from the last, whatever the frame time does: steady frames, jitter, and
        // editor hitches of a tenth of a second (Caleb's fly test 3: prisms bunched after a long frame).
        [TestCase(162f, 10f)]
        [TestCase(400f, 10f)]
        [TestCase(610f, 10f)]
        [TestCase(1080f, 30f)]
        [TestCase(1080f, 10f)]
        public void StepLay_LaysEvenlyAtEverySpeed_ThroughJitterAndHitches(float speed, float spacing)
        {
            var rnd = new System.Random(7);
            float travelled = float.PositiveInfinity;
            double path = 0, lastLayAt = 0, firstLayAt = 0;   // double: 2000 frames at top speed fly 36 km
            int lays = 0;
            for (int frame = 0; frame < 2000; frame++)
            {
                float dt = frame % 97 == 0 ? 0.1f : (1f / 60f) * (0.8f + 0.4f * (float)rnd.NextDouble());
                float step = speed * dt;
                path += step;
                bool lay = TrailSpeedShape.StepLay(ref travelled, step, spacing, out float back);
                for (int n = 0; lay && n < 64; n++)   // the primitive owes every point; the controller caps at four
                {
                    Assert.GreaterOrEqual(back, 0f);
                    double layAt = path - back;
                    if (lays > 0)
                        Assert.AreEqual(spacing, (float)(layAt - lastLayAt), 1e-2f, $"lay {lays} at frame {frame}");
                    else
                        firstLayAt = layAt;
                    lastLayAt = layAt;
                    lays++;
                    lay = TrailSpeedShape.NextLay(ref travelled, spacing, out back);
                }
            }
            Assert.AreEqual((path - firstLayAt) / spacing + 1, lays, 1.0, "one prism per spacing flown, none dropped");
        }

        [Test]
        public void StepLay_LaysAtOnceWhenTheTrailStarts()
        {
            float travelled = float.PositiveInfinity;
            Assert.IsTrue(TrailSpeedShape.StepLay(ref travelled, 2f, Wavelength, out float back));
            Assert.AreEqual(0f, back, Tol);
            Assert.AreEqual(0f, travelled, Tol);
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
                         "    TopSpeed: 1100", "    TopSpacingMultiplier: 3", "    TopLength: 20", "    TopHeightMultiplier: 0.4",
                         "    CrawlSpeed: 60", "    CrawlHeightMultiplier: 4", "    YawsteryHeightGain: 0.8",
                         "    OutsideThin: 0.25", "    MinHeight: 0.1", "    MaxHeight: 3.5", "    TurnGeometry: 1",
                     })
                StringAssert.Contains(line + "\n", block);
        }
    }
}
