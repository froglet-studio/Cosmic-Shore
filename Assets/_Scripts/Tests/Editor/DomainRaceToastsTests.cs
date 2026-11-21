using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Locks the race-beat thresholds of <see cref="DomainRaceToasts.Evaluate"/>, and in particular
    /// that a TEAM-SUMMED race (Regatta) measures a team against its pilots' combined courses
    /// rather than one pilot's. Numbers are Regatta's: a 24-gate course (three 8-ring laps), the
    /// gate race's 3-gate home stretch, the final lap from gate 16.
    /// </summary>
    public class DomainRaceToastsTests
    {
        const int Course = 24, Home = 3, FinalLapAt = 16;

        [Test]
        public void OnePilot_HalfwayIsHalfTheCourse()
        {
            Assert.IsFalse(DomainRaceToasts.Evaluate(11, Course, Home, FinalLapAt).Half);
            Assert.IsTrue(DomainRaceToasts.Evaluate(12, Course, Home, FinalLapAt).Half);
            Assert.AreEqual(Course, DomainRaceToasts.Evaluate(12, Course, Home, FinalLapAt).Target);
        }

        [Test]
        public void TwoPilots_EachAQuarterRoundIsNotHalfway()
        {
            // The defect: 6 + 6 summed gates against a 24-gate course read as "halfway".
            var b = DomainRaceToasts.Evaluate(12, Course, Home, FinalLapAt, pilots: 2);
            Assert.IsTrue(b.Quarter);
            Assert.IsFalse(b.Half);
            Assert.AreEqual(48, b.Target);
            Assert.IsTrue(DomainRaceToasts.Evaluate(24, Course, Home, FinalLapAt, pilots: 2).Half);
        }

        [Test]
        public void TwoPilots_FinalLapAndHomeStretchScaleWithTheTeam()
        {
            Assert.IsFalse(DomainRaceToasts.Evaluate(16, Course, Home, FinalLapAt, 2).FinalLap);
            Assert.IsTrue(DomainRaceToasts.Evaluate(32, Course, Home, FinalLapAt, 2).FinalLap);

            Assert.IsFalse(DomainRaceToasts.Evaluate(21, Course, Home, FinalLapAt, 2).Home);
            Assert.IsFalse(DomainRaceToasts.Evaluate(41, Course, Home, FinalLapAt, 2).Home);
            Assert.IsTrue(DomainRaceToasts.Evaluate(42, Course, Home, FinalLapAt, 2).Home);
            Assert.IsFalse(DomainRaceToasts.Evaluate(48, Course, Home, FinalLapAt, 2).Home, "finished is not a stretch");
        }

        [Test]
        public void ThreePilots_ScaleByThree()
        {
            var b = DomainRaceToasts.Evaluate(35, Course, Home, FinalLapAt, 3);
            Assert.IsFalse(b.Half);
            Assert.AreEqual(72, b.Target);
            Assert.IsTrue(DomainRaceToasts.Evaluate(36, Course, Home, FinalLapAt, 3).Half);
        }

        [Test]
        public void NoPilotCount_IsOneCourse()
        {
            // Every other mode passes no counter (1); a count of 0 (stats not replicated yet) must
            // not zero the target and fire every beat at once.
            for (int best = 0; best <= Course; best++)
            {
                var one = DomainRaceToasts.Evaluate(best, Course, Home, FinalLapAt, 1);
                var zero = DomainRaceToasts.Evaluate(best, Course, Home, FinalLapAt, 0);
                Assert.AreEqual(one.Half, zero.Half);
                Assert.AreEqual(one.Home, zero.Home);
                Assert.AreEqual(one.FinalLap, zero.FinalLap);
                Assert.AreEqual(Course, zero.Target);
            }
        }

        [Test]
        public void OnePilot_MatchesTheUnscaledThresholds()
        {
            // The arithmetic every non-summed gate race shipped with, restated: unchanged.
            for (int best = 0; best <= Course + 2; best++)
            {
                var b = DomainRaceToasts.Evaluate(best, Course, Home, FinalLapAt);
                Assert.AreEqual(best >= 0.25f * Course, b.Quarter, $"quarter @ {best}");
                Assert.AreEqual(best >= 0.5f * Course, b.Half, $"half @ {best}");
                Assert.AreEqual(Course > Home * 2 && best >= Course - Home && best < Course, b.Home, $"home @ {best}");
                Assert.AreEqual(FinalLapAt < Course && best >= FinalLapAt && best < Course, b.FinalLap, $"final lap @ {best}");
            }
        }
    }
}
