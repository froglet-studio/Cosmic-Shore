using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Lantern Towers' hop plan (<see cref="WaystationHopPlan"/>, Docs/Studios/TIGER_MOTH_TIME.md §6): the grade a landing
    /// earns, the streak, and where the autopilot aims. <c>Tools/Build/waystation_tower_course.py</c> mirrors it over 200
    /// seeds (`aibudget`) and races it (`--sweep`); these pin the properties on the shipped C# itself.
    /// </summary>
    [TestFixture]
    public class WaystationHopPlanTests
    {
        const int Clusters = 5;

        /// <summary>A 30 fps frame of a 900 u/s fall released on the nearest frame, plus the carry a slowed or quickened
        /// hull lands with: what the autopilot's landing may stray by.</summary>
        const float Stray = 15f + 8f;

        static WaystationCourseSettings Course(int intensity)
        {
            var s = WaystationCourseSettings.ForIntensity(intensity);
            s.RingTarget = Clusters * s.RingsPerCluster;
            s.InnerRadius = 478f;
            s.OuterRadius = 1080f;
            s.FirstClusterDirection = Vector3.up;
            return s;
        }

        [Test]
        public void TheAutopilotTargetWithItsFrameAndCarryStaysBetweenThePlateAndTheFarEdge()
        {
            int checkedHops = 0;
            for (int intensity = 1; intensity <= 4; intensity++)
            for (int seed = 1; seed <= 40; seed++)
            {
                var laid = WaystationTower.Build(seed, Course(intensity), intensity, out _);
                var t = WaystationTowerSettings.ForIntensity(intensity);
                for (int i = 1; i < laid.Count; i++)
                {
                    if (!WaystationTower.IsTowerRing(laid[i], t)) continue;
                    for (int seat = 0; seat < 4; seat++)
                    {
                        float target = WaystationHopPlan.AiTargetDepth(laid, i, t, seat);
                        string at = $"i{intensity} seed {seed} ring {i} seat {seat}";
                        if (WaystationTower.IsPlunge(laid, i, t))
                        {
                            Assert.GreaterOrEqual(target - Stray, t.WindowLow + WaystationTower.PlungeOf(laid, i, t) + 2f - 1e-3f,
                                $"{at}: a plunge landing can fall short of the plunge ring");
                            continue;
                        }
                        checkedHops++;
                        Assert.GreaterOrEqual(target - Stray, t.WindowLow + 2f - 1e-3f, $"{at}: the landing can hit the plate");
                        Assert.LessOrEqual(target + Stray, WaystationTower.FarEdge(laid, i, t) - 2f + 1e-3f,
                            $"{at}: the landing can pass the far edge and miss the next ring");
                    }
                }
            }
            Assert.Greater(checkedHops, 400, "too few hops to say anything");
        }

        [Test]
        public void TheAutopilotAimsDeeperOnALooseHopThanOnATightOne()
        {
            var laid = WaystationTower.Build(7, Course(2), 2, out _);
            var t = WaystationTowerSettings.ForIntensity(2);
            float loose = float.NaN, tight = float.NaN;
            for (int i = 1; i < laid.Count; i++)
            {
                if (!WaystationTower.HasShelf(laid, i, t)) continue;
                float aim = WaystationHopPlan.AiTargetDepth(laid, i, t, 0) - WaystationHopPlan.AiReleaseError(0, i, t);
                if (WaystationTower.LaidDepth(laid, i, t) >= t.WindowHigh - 0.5f) loose = aim; else tight = aim;
            }
            if (float.IsNaN(loose) || float.IsNaN(tight)) Assert.Inconclusive("seed 7 at I2 has no tight and loose hop");
            Assert.Greater(loose, tight, "the autopilot does not read the lanterns");
        }

        [Test]
        public void AiReleaseErrorIsTheSameOnEveryPeerAndDiffersBySeat()
        {
            var t = WaystationTowerSettings.ForIntensity(3);
            int differ = 0;
            for (int gate = 1; gate < 60; gate++)
            {
                float a = WaystationHopPlan.AiReleaseError(0, gate, t);
                Assert.AreEqual(a, WaystationHopPlan.AiReleaseError(0, gate, t), "the error is not a pure function");
                Assert.LessOrEqual(Mathf.Abs(a), t.AiErrorU, "the error leaves its band");
                if (Mathf.Abs(a - WaystationHopPlan.AiReleaseError(1, gate, t)) > 0.5f) differ++;
            }
            Assert.Greater(differ, 40, "two seats land alike: the rivals would fly as one");
        }

        [Test]
        public void GradesFollowTheLanterns()
        {
            var t = WaystationTowerSettings.ForIntensity(2);
            float shelf = 200f, far = 230f;
            float foot = shelf - WaystationTower.LanternBack;
            Assert.AreEqual(HopGrade.Short, WaystationHopPlan.Grade(t.WindowLow - 1f, shelf, far, t));
            Assert.AreEqual(HopGrade.Clean, WaystationHopPlan.Grade(t.WindowLow + 1f, shelf, far, t));
            Assert.AreEqual(HopGrade.Clean, WaystationHopPlan.Grade(foot - t.DeepBand - 1f, shelf, far, t));
            Assert.AreEqual(HopGrade.Deep, WaystationHopPlan.Grade(foot - t.DeepBand + 1f, shelf, far, t));
            Assert.AreEqual(HopGrade.Deep, WaystationHopPlan.Grade(far - 1f, shelf, far, t),
                "past the shelf but inside the far edge still threads: the fast line");
            Assert.AreEqual(HopGrade.Past, WaystationHopPlan.Grade(far + 1f, shelf, far, t));
        }

        [Test]
        public void EveryDeepBandIsAtLeastTwoFramesOfFallWide()
        {
            for (int intensity = 1; intensity <= 4; intensity++)
                Assert.GreaterOrEqual(WaystationTowerSettings.ForIntensity(intensity).DeepBand + WaystationTower.LanternBack,
                    2f * WaystationTower.DropSpeed / 60f, $"i{intensity}: the deep band is narrower than two 60 fps frames");
        }

        [Test]
        public void TheStreakIsAPureFunctionOfItsGrades()
        {
            var streak = default(WaystationHopPlan.Streak);
            var grades = new[] { HopGrade.Deep, HopGrade.Deep, HopGrade.Clean, HopGrade.Deep };
            foreach (var g in grades) streak = WaystationHopPlan.Step(streak, g, missedRing: false);
            Assert.AreEqual(3, streak.Count, "a clean landing should hold the streak, a deep one add to it");
            Assert.AreEqual(1, streak.Tier);
            for (int k = 0; k < 7; k++) streak = WaystationHopPlan.Step(streak, HopGrade.Deep, missedRing: false);
            Assert.AreEqual(3, streak.Tier, "ten deep landings in a row is tier 3");
            Assert.AreEqual(0, WaystationHopPlan.Step(streak, HopGrade.Deep, missedRing: true).Count, "a missed ring resets");
            Assert.AreEqual(0, WaystationHopPlan.Step(streak, HopGrade.Short, missedRing: false).Count, "a short landing resets");
            Assert.AreEqual(0, WaystationHopPlan.Step(streak, HopGrade.Past, missedRing: false).Count, "a past landing resets");
        }

        [Test]
        public void HoldForTravelAtLiveSpeedMatchesTheDropMath()
        {
            foreach (float cruise in new[] { 40f, 67f, 120f })
            foreach (float travel in new[] { 250f, 400f, 900f })
            {
                float hold = WaystationTower.HoldForTravel(travel, cruise);
                float executor = TigerMothDropMath.HoldSecondsFor(travel, WaystationTower.DropSpeed,
                    WaystationTower.DropEaseSeconds, WaystationTower.LandSeconds, cruise);
                Assert.AreEqual(executor, hold, 0.0005f, $"cruise {cruise}, travel {travel}");
            }
            Assert.AreEqual(WaystationTower.HoldForTravel(200f), WaystationTower.HoldForTravel(200f, WaystationTower.Cruise),
                1e-6f, "the one-argument hold is not the shipped cruise's");
        }
    }
}
