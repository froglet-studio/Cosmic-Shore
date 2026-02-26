using NUnit.Framework;
using CosmicShore.Core;

namespace CosmicShore.Tests
{
    /// <summary>
    /// XpData Tests — Validates the experience points data structure.
    ///
    /// WHY THIS MATTERS:
    /// XpData is the struct that stores per-element XP (Space, Time, Mass, Charge)
    /// for each captain/vessel class. It gets serialized to JSON and sent to PlayFab.
    /// If the struct fields are reordered or the constructor mapping is wrong,
    /// players will see their XP assigned to the wrong element — a data corruption bug.
    /// </summary>
    [TestFixture]
    public class XpDataTests
    {
        #region Constructor

        [Test]
        public void Constructor_AssignsAllFields()
        {
            var xp = new XpData(space: 10, time: 20, mass: 30, charge: 40);

            Assert.AreEqual(10, xp.Space);
            Assert.AreEqual(20, xp.Time);
            Assert.AreEqual(30, xp.Mass);
            Assert.AreEqual(40, xp.Charge);
        }

        [Test]
        public void DefaultConstructor_AllFieldsAreZero()
        {
            var xp = new XpData();

            Assert.AreEqual(0, xp.Space);
            Assert.AreEqual(0, xp.Time);
            Assert.AreEqual(0, xp.Mass);
            Assert.AreEqual(0, xp.Charge);
        }

        [Test]
        public void Constructor_NegativeValues_AreAllowed()
        {
            // XP could potentially be negative in edge cases (penalties).
            var xp = new XpData(-5, -10, -15, -20);

            Assert.AreEqual(-5, xp.Space);
            Assert.AreEqual(-10, xp.Time);
            Assert.AreEqual(-15, xp.Mass);
            Assert.AreEqual(-20, xp.Charge);
        }

        #endregion

        #region Struct Semantics

        [Test]
        public void Struct_CopySemantics_ModifyingCopyDoesNotAffectOriginal()
        {
            var original = new XpData(10, 20, 30, 40);
            var copy = original;

            copy.Space = 999;

            Assert.AreEqual(10, original.Space,
                "Modifying the copy should not change the original (value type).");
        }

        [Test]
        public void Struct_Equality_SameValuesAreEqual()
        {
            var a = new XpData(1, 2, 3, 4);
            var b = new XpData(1, 2, 3, 4);

            Assert.AreEqual(a, b);
        }

        [Test]
        public void Struct_FieldAssignment_WorksIndependently()
        {
            var xp = new XpData(0, 0, 0, 0);
            xp.Space = 100;

            Assert.AreEqual(100, xp.Space);
            Assert.AreEqual(0, xp.Time, "Other fields should be unaffected.");
            Assert.AreEqual(0, xp.Mass, "Other fields should be unaffected.");
            Assert.AreEqual(0, xp.Charge, "Other fields should be unaffected.");
        }

        #endregion

        #region Parameter Order

        [Test]
        public void Constructor_ParameterOrder_IsSpaceTimeMassCharge()
        {
            // The constructor signature is: (int space, int time, int mass, int charge)
            // This test documents and locks the order. If someone swaps parameters
            // in the constructor, the named argument test will still pass but the
            // positional test will fail, catching the regression.
            var xp = new XpData(1, 2, 3, 4);

            Assert.AreEqual(1, xp.Space, "First parameter should be Space.");
            Assert.AreEqual(2, xp.Time, "Second parameter should be Time.");
            Assert.AreEqual(3, xp.Mass, "Third parameter should be Mass.");
            Assert.AreEqual(4, xp.Charge, "Fourth parameter should be Charge.");
        }

        #endregion
    }
}
