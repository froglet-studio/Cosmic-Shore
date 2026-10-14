using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Manta autopilot drive (<see cref="MantaAnalogTurnBoostExecutor.AutopilotTriggers"/>):
    /// an AIPilot has no triggers, so the executor derives both from the stick it last wrote.
    /// Two promises are under test. First, below the Yastri band the drive is EXACTLY the
    /// band-only drive that shipped with Redline (both triggers at the boost intent, no net), so
    /// straights and ordinary corners fly as before. Second, a full yaw deflection is one trigger
    /// flat and the other released - the 82 u pivot the Redline course model assumes
    /// (<see cref="RedlineCourse.CornerRadiusAtBoost"/> at boost 0).
    /// </summary>
    [TestFixture]
    public class MantaAutopilotDriveTests
    {
        const float BoostBand = 0.35f;    // MantaAnalogTurnBoostExecutor.aiBoostStickBand default
        const float YastriBand = 0.75f;   // MantaAnalogTurnBoostExecutor.aiYastriStickBand default
        const float Eps = 1e-5f;

        /// <summary>The drive as it shipped before the pivot, verbatim: both triggers equal.</summary>
        static float BandOnlyIntent(float x, float y, float band)
        {
            if (band <= 0f) return 0f;
            float stick = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
            if (stick <= band) return 1f;
            return Mathf.Clamp01(1f - (stick - band) / Mathf.Max(1e-4f, 1f - band));
        }

        static void Drive(float x, float y, float yastriBand, out float lt, out float rt) =>
            MantaAnalogTurnBoostExecutor.AutopilotTriggers(
                x, Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)), BoostBand, yastriBand, out lt, out rt);

        [Test]
        public void InsideYastriBand_MatchesBandOnlyDrive()
        {
            for (int i = -75; i <= 75; i++)
            for (int j = -20; j <= 20; j++)
            {
                float x = i / 100f, y = j / 20f;
                Drive(x, y, YastriBand, out float lt, out float rt);
                float expected = BandOnlyIntent(x, y, BoostBand);
                Assert.AreEqual(expected, lt, Eps, $"lt at x={x} y={y}");
                Assert.AreEqual(expected, rt, Eps, $"rt at x={x} y={y}");
            }
        }

        [Test]
        public void Straight_IsFullSoarNoTurn()
        {
            Drive(0f, 0f, YastriBand, out float lt, out float rt);
            Assert.AreEqual(1f, lt, Eps);
            Assert.AreEqual(1f, rt, Eps);
        }

        [Test]
        public void PastYastriBand_BoostIsUnchanged_AndNetFollowsTheStick()
        {
            for (int i = -100; i <= 100; i++)
            for (int j = -10; j <= 10; j++)
            {
                float x = i / 100f, y = j / 10f;
                Drive(x, y, YastriBand, out float lt, out float rt);
                Assert.AreEqual(BandOnlyIntent(x, y, BoostBand), Mathf.Min(lt, rt), Eps,
                    $"the overlap (boost) moved at x={x} y={y}");
                Assert.That(lt, Is.InRange(0f, 1f));
                Assert.That(rt, Is.InRange(0f, 1f));

                float net = rt - lt;
                if (Mathf.Abs(x) <= YastriBand) Assert.AreEqual(0f, net, Eps, $"net inside band, x={x}");
                else if (x > 0f) Assert.That(net, Is.GreaterThan(0f), $"right stick, x={x}");
                else Assert.That(net, Is.LessThan(0f), $"left stick, x={x}");
            }
        }

        [Test]
        public void FullYaw_IsTheOneTriggerPivot()
        {
            Drive(1f, 0f, YastriBand, out float lt, out float rt);
            Assert.AreEqual(0f, lt, Eps);
            Assert.AreEqual(1f, rt, Eps);

            Drive(-1f, 0f, YastriBand, out lt, out rt);
            Assert.AreEqual(1f, lt, Eps);
            Assert.AreEqual(0f, rt, Eps);

            // The course model's "one trigger flat, the other at b": boost b = min, net = 1 - b.
            float boost = Mathf.Min(lt, rt);
            Assert.AreEqual(1f - boost, Mathf.Abs(rt - lt), Eps);
            Assert.AreEqual(82f, RedlineCourse.CornerRadiusAtBoost(boost), 1f);
        }

        [Test]
        public void PitchOnlyDeflection_NeverPivots()
        {
            Drive(0f, 1f, YastriBand, out float lt, out float rt);
            Assert.AreEqual(0f, lt, Eps);
            Assert.AreEqual(0f, rt, Eps);
        }

        [Test]
        public void NetIsContinuousAtTheBandEdge()
        {
            Drive(YastriBand + 1e-4f, 0f, YastriBand, out float lt, out float rt);
            Assert.AreEqual(0f, rt - lt, 1e-3f);
        }

        [Test]
        public void YastriBandOfOne_DisablesThePivot()
        {
            for (int i = -100; i <= 100; i++)
            {
                float x = i / 100f;
                Drive(x, 0f, 1f, out float lt, out float rt);
                Assert.AreEqual(BandOnlyIntent(x, 0f, BoostBand), lt, Eps, $"x={x}");
                Assert.AreEqual(lt, rt, Eps, $"x={x}");
            }
        }

        [Test]
        public void BoostBandOfZero_StillDisablesTheSoar()
        {
            MantaAnalogTurnBoostExecutor.AutopilotTriggers(0f, 0f, 0f, YastriBand, out float lt, out float rt);
            Assert.AreEqual(0f, lt, Eps);
            Assert.AreEqual(0f, rt, Eps);
        }
    }
}
