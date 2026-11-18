using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's and the Swordfish's ramp runs on fuel that ploughing and slicing earn (Garrett, 2026-11-18). The burn
    /// is the part a course is sized against: full at top power, nothing at cruise, proportional between, and nothing
    /// for a free ramp (no fuel slot, the old behaviour every other ramp asset keeps).
    /// </summary>
    public class RampFuelTests
    {
        static RampBoostActionSO Ramp(int fuelIndex, float burn = 0.1f, float max = 16.8f)
        {
            var so = ScriptableObject.CreateInstance<RampBoostActionSO>();
            Set(so, "maxBoostMultiplier", max);
            Set(so, "fuelResourceIndex", fuelIndex);
            Set(so, "fuelBurnPerSecond", burn);
            return so;
        }

        static void Set(object target, string field, object value) =>
            target.GetType()
                  .GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                  .SetValue(target, value);

        [Test]
        public void A_free_ramp_burns_nothing()
        {
            var so = Ramp(-1);
            Assert.IsFalse(so.UsesFuel);
            Assert.AreEqual(0f, so.FuelBurn(16.8f, 1f));
        }

        [Test]
        public void Full_power_burns_the_full_rate()
        {
            var so = Ramp(0, 0.1f);
            Assert.IsTrue(so.UsesFuel);
            Assert.AreEqual(0.1f, so.FuelBurn(16.8f, 1f), 1e-5f);
            Assert.AreEqual(0.05f, so.FuelBurn(16.8f, 0.5f), 1e-5f);
        }

        [Test]
        public void Cruise_burns_nothing_and_a_bent_line_burns_its_share()
        {
            var so = Ramp(0, 0.1f);
            Assert.AreEqual(0f, so.FuelBurn(1f, 1f), 1e-6f);
            float half = 1f + (16.8f - 1f) * 0.5f;
            Assert.AreEqual(0.05f, so.FuelBurn(half, 1f), 1e-5f);
        }

        [Test]
        public void An_empty_tank_runs_on_fumes_with_the_same_grading()
        {
            var so = Ramp(0);
            Set(so, "straightnessGraceBand", 1f);
            Assert.AreEqual(1f, so.EmptyTankMultiplierFor(0f), 1e-6f, "the default starves to plain cruise");
            Set(so, "emptyTankMaxBoostMultiplier", 4f);
            Assert.AreEqual(4f, so.EmptyTankMultiplierFor(0f), 1e-5f, "straight on fumes reaches the fumes top");
            Assert.AreEqual(1f, so.EmptyTankMultiplierFor(1f), 1e-5f, "stick hard over is plain cruise, as on a full tank");
            Set(so, "emptyTankMaxBoostMultiplier", 40f);
            Assert.AreEqual(16.8f, so.EmptyTankMaxBoostMultiplier, 1e-5f, "fumes never beat the full top");
        }

        [Test]
        public void No_time_no_burn()
        {
            var so = Ramp(0, 0.1f);
            Assert.AreEqual(0f, so.FuelBurn(16.8f, 0f));
        }
    }
}
