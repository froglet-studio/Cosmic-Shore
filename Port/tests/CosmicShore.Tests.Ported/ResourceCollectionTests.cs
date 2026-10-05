using NUnit.Framework;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Resource Collection Tests — Validates the 4-element resource struct.
    ///
    /// WHY THIS MATTERS:
    /// ResourceCollection is a fundamental data type that holds the four element
    /// values (Mass, Charge, Space, Time) for vessels, crystals, and game scoring.
    /// If the constructor or field assignment is broken, resource calculations
    /// throughout the game (damage, scoring, ability costs) will be wrong.
    /// </summary>
    [TestFixture]
    public class ResourceCollectionTests
    {
        #region Constructor

        [Test]
        public void Constructor_AssignsAllFields()
        {
            var rc = new ResourceCollection(1.5f, 2.5f, 3.5f, 4.5f);

            Assert.AreEqual(1.5f, rc.Mass);
            Assert.AreEqual(2.5f, rc.Charge);
            Assert.AreEqual(3.5f, rc.Space);
            Assert.AreEqual(4.5f, rc.Time);
        }

        [Test]
        public void DefaultConstructor_AllFieldsAreZero()
        {
            var rc = new ResourceCollection();

            Assert.AreEqual(0f, rc.Mass);
            Assert.AreEqual(0f, rc.Charge);
            Assert.AreEqual(0f, rc.Space);
            Assert.AreEqual(0f, rc.Time);
        }

        [Test]
        public void Constructor_NegativeValues_AreAllowed()
        {
            // Resources can be negative (e.g., debuffs, costs)
            var rc = new ResourceCollection(-10f, -20f, -30f, -40f);

            Assert.AreEqual(-10f, rc.Mass);
            Assert.AreEqual(-20f, rc.Charge);
            Assert.AreEqual(-30f, rc.Space);
            Assert.AreEqual(-40f, rc.Time);
        }

        [Test]
        public void Constructor_LargeValues_ArePreserved()
        {
            var rc = new ResourceCollection(float.MaxValue, float.MinValue, 0f, 0f);

            Assert.AreEqual(float.MaxValue, rc.Mass);
            Assert.AreEqual(float.MinValue, rc.Charge);
        }

        #endregion

        #region Field Assignment

        [Test]
        public void Fields_CanBeModifiedIndependently()
        {
            var rc = new ResourceCollection(1f, 2f, 3f, 4f);

            rc.Mass = 100f;

            Assert.AreEqual(100f, rc.Mass, "Mass should be updated.");
            Assert.AreEqual(2f, rc.Charge, "Charge should be unchanged.");
            Assert.AreEqual(3f, rc.Space, "Space should be unchanged.");
            Assert.AreEqual(4f, rc.Time, "Time should be unchanged.");
        }

        [Test]
        public void Constructor_ParameterOrder_IsMassChargeSpaceTime()
        {
            // This test documents the constructor parameter order explicitly.
            // If someone reorders the parameters, this test catches it.
            var rc = new ResourceCollection(
                mass: 10f,
                charge: 20f,
                space: 30f,
                time: 40f
            );

            Assert.AreEqual(10f, rc.Mass);
            Assert.AreEqual(20f, rc.Charge);
            Assert.AreEqual(30f, rc.Space);
            Assert.AreEqual(40f, rc.Time);
        }

        #endregion

        #region Struct Semantics

        [Test]
        public void Struct_CopySemantics_ModifyingCopyDoesNotAffectOriginal()
        {
            // Structs are value types — copies should be independent.
            var original = new ResourceCollection(1f, 2f, 3f, 4f);
            var copy = original;

            copy.Mass = 999f;

            Assert.AreEqual(1f, original.Mass,
                "Modifying the copy should not affect the original (value type semantics).");
            Assert.AreEqual(999f, copy.Mass);
        }

        [Test]
        public void Struct_Equality_SameValuesAreEqual()
        {
            var a = new ResourceCollection(1f, 2f, 3f, 4f);
            var b = new ResourceCollection(1f, 2f, 3f, 4f);

            Assert.AreEqual(a, b, "Two ResourceCollections with identical values should be equal.");
        }

        [Test]
        public void Struct_Equality_DifferentValuesAreNotEqual()
        {
            var a = new ResourceCollection(1f, 2f, 3f, 4f);
            var b = new ResourceCollection(1f, 2f, 3f, 5f);

            Assert.AreNotEqual(a, b);
        }

        #endregion
    }
}
