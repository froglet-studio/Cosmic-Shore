using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Broadside's point target is the only one on <see cref="EndConditionOverridesSO"/> that is a
    /// RATE rather than a total: it scales with how many pilots a side fields, because
    /// <c>VesselCombatHitLatch</c> admits one hit per (shooter, victim, class) window and a second
    /// pilot on the same victim therefore roughly doubles a domain's rate (BROADSIDE.md).
    ///
    /// <para>Worth a test because the arithmetic has a boundary (a team size of 0, which the live
    /// roster can legitimately produce before anyone has spawned) and a rounding, and because a
    /// target of 0 is not a small target - it is a target that is ALREADY REACHED, which would
    /// end the match on the countdown.</para>
    /// </summary>
    public class BroadsideTargetScalingTests
    {
        EndConditionOverridesSO _so;

        [SetUp]
        public void SetUp() => _so = ScriptableObject.CreateInstance<EndConditionOverridesSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_so);

        [Test]
        public void ShippedLadderIsTheDocumentedOne()
        {
            _so.broadsidePointsPerPilot = 100;
            Assert.AreEqual(100, _so.GetBroadsidePointTarget(1), "solo");
            Assert.AreEqual(160, _so.GetBroadsidePointTarget(2), "2v2 - the number the request named");
            Assert.AreEqual(220, _so.GetBroadsidePointTarget(3));
            Assert.AreEqual(280, _so.GetBroadsidePointTarget(4));
        }

        [Test]
        public void ATeamSizeBelowOneIsTreatedAsOne()
        {
            _so.broadsidePointsPerPilot = 100;
            // The live roster is empty until the first player spawns, and a 0 here would resolve to
            // a target of 0 - already reached, so the turn would end on the countdown.
            Assert.AreEqual(100, _so.GetBroadsidePointTarget(0));
            Assert.AreEqual(100, _so.GetBroadsidePointTarget(-3));
        }

        [Test]
        public void ZeroPerPilotFallsBackToTheDefaultRatherThanToZero()
        {
            _so.broadsidePointsPerPilot = 0;   // "auto", the asset's own sentinel
            Assert.AreEqual(EndConditionOverridesSO.DefaultBroadsidePointsPerPilot,
                            _so.GetBroadsidePointTarget(1));
        }

        [Test]
        public void TheExtraPilotFractionIsBelowOne()
        {
            // The design claim, asserted rather than trusted: at 1.0 the target would rise exactly
            // as fast as a team's rate and match length would be flat, which reads as a teammate
            // contributing nothing. Below 1, a fuller side finishes sooner.
            Assert.Less(EndConditionOverridesSO.BroadsideExtraPilotFraction, 1f);
            Assert.Greater(EndConditionOverridesSO.BroadsideExtraPilotFraction, 0f);
        }

        [Test]
        public void TargetIsStrictlyIncreasingInTeamSize()
        {
            _so.broadsidePointsPerPilot = 100;
            for (int n = 1; n < 8; n++)
                Assert.Greater(_so.GetBroadsidePointTarget(n + 1), _so.GetBroadsidePointTarget(n),
                               $"team of {n + 1} must race to more than a team of {n}");
        }
    }
}
