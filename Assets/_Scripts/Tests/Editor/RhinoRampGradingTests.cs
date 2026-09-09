using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Rhino's ramp boost as a CURVE rather than a latch (RHINO_RAMP_BOOST.md).
    ///
    /// <para>Before it was graded, the ability had exactly two operating points — full ramp at
    /// dead straight, nothing at all past the gesture's threshold — so a corner was a binary
    /// classification and a lap was the same lap every time. The grading is one lerp, and
    /// everything a pilot can now practise falls out of composing it with two formulas that were
    /// already there: turn rate is linear in stick, and the max turn rate grows with speed. These
    /// tests assert the composition still has the shape the mode is cut against, because a course
    /// generator two files away is sized against it.</para>
    /// </summary>
    public class RhinoRampGradingTests
    {
        static RampBoostActionSO Ramp(float max = 24f, float band = 1f)
        {
            var so = ScriptableObject.CreateInstance<RampBoostActionSO>();
            Set(so, "maxBoostMultiplier", max);
            Set(so, "straightnessGraceBand", band);
            return so;
        }

        static void Set(object target, string field, float value) =>
            target.GetType()
                  .GetField(field, System.Reflection.BindingFlags.Instance |
                                   System.Reflection.BindingFlags.NonPublic)
                  .SetValue(target, value);

        // ── The lerp ─────────────────────────────────────────────────────────

        [Test]
        public void Full_power_across_the_plateau()
        {
            var so = Ramp();
            foreach (float d in new[] { 0f, 0.1f, 0.29f, StraightLineGesture.EngageThreshold })
                Assert.AreEqual(24f, so.MultiplierFor(d), 1e-3f,
                    $"deviation {d} is inside the gesture and must cost nothing");
        }

        [Test]
        public void Plain_cruise_at_the_band_edge_so_disengaging_is_not_a_step()
        {
            var so = Ramp();
            Assert.AreEqual(1f, so.MultiplierFor(1f), 1e-3f);
            Assert.AreEqual(1f, so.MultiplierFor(2f), 1e-3f, "and stays there past it");

            // 1 rather than the vessel's resting BoostMultiplier on purpose: CurrentBoostAmount
            // applies 1 when IsBoosting is false, so the graded ramp hands off to disengagement
            // at exactly the same speed rather than dropping a step on the way out.
        }

        [Test]
        public void Monotone_between_the_two()
        {
            var so = Ramp();
            float previous = float.MaxValue;
            for (float d = 0.30f; d <= 1.0001f; d += 0.02f)
            {
                float m = so.MultiplierFor(d);
                Assert.LessOrEqual(m, previous + 1e-4f, $"multiplier rose at deviation {d}");
                previous = m;
            }
        }

        [Test]
        public void A_band_at_the_threshold_restores_the_old_binary_latch()
        {
            // The safety valve: this asset field is the whole difference between the shipped
            // curve and the behaviour that shipped before it, so authoring it away must be exact
            // rather than approximate.
            var so = Ramp(band: StraightLineGesture.EngageThreshold);
            Assert.AreEqual(24f, so.MultiplierFor(0.29f), 1e-3f);
            Assert.AreEqual(1f, so.MultiplierFor(0.30f), 1e-3f);
            Assert.AreEqual(1f, so.MultiplierFor(0.90f), 1e-3f);
        }

        [Test]
        public void A_band_authored_below_the_threshold_degrades_rather_than_dividing_by_zero()
        {
            var so = Ramp(band: 0f);
            Assert.AreEqual(StraightLineGesture.EngageThreshold, so.StraightnessGraceBand, 1e-4f);
            Assert.AreEqual(24f, so.MultiplierFor(0f), 1e-3f);
            Assert.AreEqual(1f, so.MultiplierFor(0.5f), 1e-3f);
        }

        // ── The curve the course is cut against ──────────────────────────────

        [Test]
        public void The_settings_curve_matches_the_shipped_asset()
        {
            // HeadlongCircuitSettings restates the Rhino's numbers as compile-time constants so
            // the generator stays pure. That is a COPY, and this is the test that stops it
            // drifting from the ability it copies.
            var so = Ramp();
            for (float d = 0f; d <= 1.0001f; d += 0.05f)
            {
                float fromAsset = HeadlongCircuitSettings.RhinoThrottleScaler * so.MultiplierFor(d)
                                  + HeadlongCircuitSettings.RhinoMinimumSpeed;
                Assert.AreEqual(fromAsset, HeadlongCircuitSettings.SpeedAtStick(d), 0.5f,
                    $"the course generator and the ability disagree at deviation {d}");
            }
        }

        [Test]
        public void Speed_and_radius_both_fall_as_the_pilot_steers_harder()
        {
            // This monotonicity is what makes a corner a continuous optimisation rather than a
            // classification — and FastestSpeedForCorner bisects on it, so it is a correctness
            // requirement and not only a design one.
            float previousSpeed = float.MaxValue, previousRadius = float.MaxValue;
            for (float s = 0.30f; s <= 1.0001f; s += 0.01f)
            {
                float v = HeadlongCircuitSettings.SpeedAtStick(s);
                float r = HeadlongCircuitSettings.CornerRadiusAtStick(s);
                Assert.LessOrEqual(v, previousSpeed + 1e-3f, $"speed rose at stick {s}");
                Assert.LessOrEqual(r, previousRadius + 1e-3f, $"radius rose at stick {s}");
                previousSpeed = v;
                previousRadius = r;
            }
        }

        [Test]
        public void The_curve_spans_a_range_worth_learning()
        {
            // A curve whose ends are close together is a latch with extra steps. The mode is
            // built on there being a real, gradual trade between the two.
            float wide = HeadlongCircuitSettings.CornerRadiusAtStick(
                HeadlongCircuitSettings.BoostPlateauDeviation);
            float tight = HeadlongCircuitSettings.CornerRadiusAtStick(1f);

            Assert.Greater(wide / tight, 8f,
                "a Rhino should be able to fly a circle an order of magnitude tighter than its " +
                "flat-out one by paying for it in speed");
            Assert.AreEqual(HeadlongCircuitSettings.RhinoTopSpeed,
                HeadlongCircuitSettings.SpeedAtStick(HeadlongCircuitSettings.BoostPlateauDeviation), 0.5f);
        }

        [Test]
        public void Inverting_the_curve_round_trips()
        {
            for (float s = 0.31f; s <= 0.99f; s += 0.02f)
            {
                float r = HeadlongCircuitSettings.CornerRadiusAtStick(s);
                float v = HeadlongCircuitSettings.FastestSpeedForCorner(r);
                Assert.AreEqual(HeadlongCircuitSettings.SpeedAtStick(s), v, 12f,
                    $"FastestSpeedForCorner disagrees with the curve it inverts at stick {s}");
            }

            Assert.AreEqual(HeadlongCircuitSettings.RhinoTopSpeed,
                HeadlongCircuitSettings.FastestSpeedForCorner(10000f), 0.5f,
                "a straight costs nothing");
        }
    }
}
