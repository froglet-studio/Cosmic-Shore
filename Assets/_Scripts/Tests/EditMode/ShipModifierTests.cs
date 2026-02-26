using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Ship Modifier Tests — Validates throttle and velocity modifier structs.
    ///
    /// WHY THIS MATTERS:
    /// ShipThrottleModifier and ShipVelocityModifier are applied to vessels every
    /// frame during gameplay to create boosts, slowdowns, and directional pushes.
    /// If the constructor misassigns fields (e.g., duration gets initialValue),
    /// boost effects will be wrong — a 2-second boost could last 500 frames, or
    /// a speed buff could apply a duration instead of a velocity.
    /// </summary>
    [TestFixture]
    public class ShipModifierTests
    {
        #region ShipThrottleModifier

        [Test]
        public void ThrottleModifier_Constructor_AssignsAllFields()
        {
            var mod = new ShipThrottleModifier(
                initialValue: 2.5f,
                duration: 3.0f,
                elapsedTime: 0.5f
            );

            Assert.AreEqual(2.5f, mod.initialValue, 0.001f);
            Assert.AreEqual(3.0f, mod.duration, 0.001f);
            Assert.AreEqual(0.5f, mod.elapsedTime, 0.001f);
        }

        [Test]
        public void ThrottleModifier_DefaultConstructor_AllZero()
        {
            var mod = new ShipThrottleModifier();

            Assert.AreEqual(0f, mod.initialValue);
            Assert.AreEqual(0f, mod.duration);
            Assert.AreEqual(0f, mod.elapsedTime);
        }

        [Test]
        public void ThrottleModifier_NegativeInitialValue_IsAllowed()
        {
            // Negative throttle = reverse/brake effect
            var mod = new ShipThrottleModifier(-1.0f, 2.0f, 0f);

            Assert.AreEqual(-1.0f, mod.initialValue, 0.001f);
        }

        [Test]
        public void ThrottleModifier_StructCopy_IsIndependent()
        {
            var original = new ShipThrottleModifier(1f, 2f, 0f);
            var copy = original;

            copy.elapsedTime = 99f;

            Assert.AreEqual(0f, original.elapsedTime,
                "Modifying copy should not affect original (value type).");
        }

        [Test]
        public void ThrottleModifier_ElapsedTimeCanExceedDuration()
        {
            // This should not crash — game code checks the ratio.
            var mod = new ShipThrottleModifier(1f, 2f, 5f);

            Assert.AreEqual(5f, mod.elapsedTime,
                "Elapsed time exceeding duration is valid (modifier has expired).");
        }

        #endregion

        #region ShipVelocityModifier

        [Test]
        public void VelocityModifier_Constructor_AssignsAllFields()
        {
            var velocity = new Vector3(10, 20, 30);
            var mod = new ShipVelocityModifier(
                initialValue: velocity,
                duration: 5.0f,
                elapsedTime: 1.0f
            );

            Assert.AreEqual(velocity, mod.initialValue);
            Assert.AreEqual(5.0f, mod.duration, 0.001f);
            Assert.AreEqual(1.0f, mod.elapsedTime, 0.001f);
        }

        [Test]
        public void VelocityModifier_DefaultConstructor_AllZero()
        {
            var mod = new ShipVelocityModifier();

            Assert.AreEqual(Vector3.zero, mod.initialValue);
            Assert.AreEqual(0f, mod.duration);
            Assert.AreEqual(0f, mod.elapsedTime);
        }

        [Test]
        public void VelocityModifier_StructCopy_IsIndependent()
        {
            var original = new ShipVelocityModifier(Vector3.one, 2f, 0f);
            var copy = original;

            copy.initialValue = Vector3.zero;

            Assert.AreEqual(Vector3.one, original.initialValue,
                "Modifying copy should not affect original (value type).");
        }

        [Test]
        public void VelocityModifier_LargeVelocity_IsPreserved()
        {
            var bigVelocity = new Vector3(9999f, -9999f, 0f);
            var mod = new ShipVelocityModifier(bigVelocity, 0.1f, 0f);

            Assert.AreEqual(bigVelocity, mod.initialValue);
        }

        [Test]
        public void VelocityModifier_ZeroDuration_IsValid()
        {
            // Instantaneous velocity impulse
            var mod = new ShipVelocityModifier(Vector3.forward * 100f, 0f, 0f);

            Assert.AreEqual(0f, mod.duration, "Zero duration = instant impulse.");
        }

        #endregion

        #region Cross-Modifier Consistency

        [Test]
        public void BothModifiers_ShareSameFieldNames()
        {
            // Both structs should have duration and elapsedTime for consistent
            // handling in the ShipTransformer's modifier processing loop.
            var throttle = new ShipThrottleModifier(1f, 2f, 0.5f);
            var velocity = new ShipVelocityModifier(Vector3.one, 2f, 0.5f);

            Assert.AreEqual(throttle.duration, velocity.duration,
                "Both modifier types use the same duration.");
            Assert.AreEqual(throttle.elapsedTime, velocity.elapsedTime,
                "Both modifier types use the same elapsed time.");
        }

        #endregion
    }
}
