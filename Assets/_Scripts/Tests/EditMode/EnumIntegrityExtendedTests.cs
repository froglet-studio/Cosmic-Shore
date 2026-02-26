using System;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Extended Enum Integrity Tests — Covers additional enums not in the original suite.
    ///
    /// WHY THIS MATTERS:
    /// CaptainLevel maps directly to PlayFab product content types for in-app purchases.
    /// If the integer values shift, players who purchased Upgrade3 could see Upgrade2
    /// applied to their account — a monetization-breaking bug. CSLogLevel controls
    /// runtime log filtering, so its values matter for configuration persistence.
    /// </summary>
    [TestFixture]
    public class EnumIntegrityExtendedTests
    {
        #region CaptainLevel

        [Test]
        public void CaptainLevel_HasSixLevels()
        {
            Assert.AreEqual(6, Enum.GetValues(typeof(CaptainLevel)).Length,
                "CaptainLevel should have 6 levels (Upgrade0 through Upgrade5).");
        }

        [Test]
        [TestCase(CaptainLevel.Upgrade0, 0)]
        [TestCase(CaptainLevel.Upgrade1, 1)]
        [TestCase(CaptainLevel.Upgrade2, 2)]
        [TestCase(CaptainLevel.Upgrade3, 3)]
        [TestCase(CaptainLevel.Upgrade4, 4)]
        [TestCase(CaptainLevel.Upgrade5, 5)]
        public void CaptainLevel_HasCorrectIntegerValue(CaptainLevel level, int expected)
        {
            Assert.AreEqual(expected, (int)level,
                $"CaptainLevel.{level} must be {expected}. Changing this breaks PlayFab purchase records.");
        }

        [Test]
        public void CaptainLevel_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(CaptainLevel)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates);
        }

        [Test]
        public void CaptainLevel_AllValuesAreSequential()
        {
            // Upgrade levels should form a continuous 0..N sequence for array indexing.
            var values = Enum.GetValues(typeof(CaptainLevel)).Cast<int>().OrderBy(v => v).ToList();

            for (int i = 0; i < values.Count; i++)
            {
                Assert.AreEqual(i, values[i],
                    $"CaptainLevel values should be sequential. Expected {i}, got {values[i]}.");
            }
        }

        [Test]
        public void CaptainLevel_AllValuesAreNonNegative()
        {
            foreach (CaptainLevel level in Enum.GetValues(typeof(CaptainLevel)))
            {
                Assert.GreaterOrEqual((int)level, 0,
                    $"CaptainLevel.{level} should not be negative.");
            }
        }

        [Test]
        public void CaptainLevel_MaxLevel_IsFive()
        {
            var max = Enum.GetValues(typeof(CaptainLevel)).Cast<int>().Max();
            Assert.AreEqual(5, max,
                "Maximum captain upgrade level should be 5.");
        }

        #endregion

        #region CSLogLevel

        [Test]
        public void CSLogLevel_HasThreeLevels()
        {
            Assert.AreEqual(3, Enum.GetValues(typeof(CSLogLevel)).Length);
        }

        [Test]
        [TestCase(CSLogLevel.All, 0)]
        [TestCase(CSLogLevel.WarningsAndErrors, 1)]
        [TestCase(CSLogLevel.Off, 2)]
        public void CSLogLevel_HasCorrectIntegerValue(CSLogLevel level, int expected)
        {
            Assert.AreEqual(expected, (int)level);
        }

        [Test]
        public void CSLogLevel_AllValuesAreUnique()
        {
            var values = Enum.GetValues(typeof(CSLogLevel)).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates);
        }

        [Test]
        public void CSLogLevel_OrderIsCorrect_AllIsLeastRestrictive()
        {
            // All < WarningsAndErrors < Off (higher = more restrictive)
            Assert.Less((int)CSLogLevel.All, (int)CSLogLevel.WarningsAndErrors);
            Assert.Less((int)CSLogLevel.WarningsAndErrors, (int)CSLogLevel.Off);
        }

        #endregion

        #region InputEvents

        [Test]
        public void InputEvents_AllValuesAreUnique()
        {
            var type = typeof(InputEvents);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in InputEvents.");
        }

        #endregion

        #region ImpactEffects

        [Test]
        public void ImpactEffects_AllValuesAreUnique()
        {
            var type = typeof(ImpactEffects);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in ImpactEffects.");
        }

        #endregion

        #region PassiveAbilities

        [Test]
        public void PassiveAbilities_AllValuesAreUnique()
        {
            var type = typeof(PassiveAbilities);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in PassiveAbilities.");
        }

        #endregion

        #region CrystalImpactEffects

        [Test]
        public void CrystalImpactEffects_AllValuesAreUnique()
        {
            var type = typeof(CrystalImpactEffects);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in CrystalImpactEffects.");
        }

        #endregion

        #region TrailBlockImpactEffects

        [Test]
        public void TrailBlockImpactEffects_AllValuesAreUnique()
        {
            var type = typeof(TrailBlockImpactEffects);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in TrailBlockImpactEffects.");
        }

        #endregion

        #region SkimmerStayEffects

        [Test]
        public void SkimmerStayEffects_AllValuesAreUnique()
        {
            var type = typeof(SkimmerStayEffects);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in SkimmerStayEffects.");
        }

        #endregion

        #region VesselImpactEffects

        [Test]
        public void VesselImpactEffects_AllValuesAreUnique()
        {
            var type = typeof(VesselImpactEffects);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in VesselImpactEffects.");
        }

        #endregion

        #region UserActionType

        [Test]
        public void UserActionType_AllValuesAreUnique()
        {
            var type = typeof(UserActionType);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in UserActionType.");
        }

        #endregion

        #region CallToActionTargetType

        [Test]
        public void CallToActionTargetType_AllValuesAreUnique()
        {
            var type = typeof(CallToActionTargetType);
            var values = Enum.GetValues(type).Cast<int>().ToList();
            var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(duplicates, "Duplicate integer values found in CallToActionTargetType.");
        }

        #endregion
    }
}
