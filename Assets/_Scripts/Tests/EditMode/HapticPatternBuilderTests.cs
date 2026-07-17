#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// HapticPatternBuilder Tests - Validates the .haptic JSON / GamepadRumble
    /// rendering that all audio-matched haptics play through.
    ///
    /// WHY THIS MATTERS:
    /// Every haptic in the game — baked envelopes, legacy-clip analysis, the
    /// continuous bed — flows through this renderer into the Lofelt runtime.
    /// Malformed JSON silently loads as "no haptics" on device, and malformed
    /// rumble data throws inside GamepadRumbler; neither is visible in the
    /// editor, so the format contract must be locked by tests.
    /// </summary>
    [TestFixture]
    public class HapticPatternBuilderTests
    {
        static List<HapticBreakpoint> SimpleEnvelope() => new()
        {
            new HapticBreakpoint(0f, 1f, 0.8f, 0.9f, 0.8f),
            new HapticBreakpoint(0.1f, 0.5f, 0.6f),
            new HapticBreakpoint(0.3f, 0f, 0.4f),
        };

        #region JSON rendering

        [Test]
        public void RenderJson_ContainsVersionHeader()
        {
            string json = HapticPatternBuilder.RenderJson(SimpleEnvelope());
            StringAssert.Contains("\"version\":{\"major\":1,\"minor\":0,\"patch\":0}", json);
        }

        [Test]
        public void RenderJson_ContainsAmplitudeAndFrequencyEnvelopes()
        {
            string json = HapticPatternBuilder.RenderJson(SimpleEnvelope());
            StringAssert.Contains("\"amplitude\":[", json);
            StringAssert.Contains("\"frequency\":[", json);
        }

        [Test]
        public void RenderJson_EmphasizedBreakpoint_EmitsEmphasisObject()
        {
            string json = HapticPatternBuilder.RenderJson(SimpleEnvelope());
            StringAssert.Contains("\"emphasis\":{\"amplitude\":0.9", json);
        }

        [Test]
        public void RenderJson_PlainBreakpoint_HasNoEmphasisObject()
        {
            var envelope = new List<HapticBreakpoint> { new(0f, 0.5f, 0.5f), new(0.2f, 0f, 0.5f) };
            string json = HapticPatternBuilder.RenderJson(envelope);
            StringAssert.DoesNotContain("emphasis", json);
        }

        [Test]
        public void RenderJson_UsesInvariantDecimalSeparator()
        {
            string json = HapticPatternBuilder.RenderJson(SimpleEnvelope());
            // In JSON objects a comma is always followed by '"' or '{', so ",5"
            // can only appear if a comma decimal separator leaked in (e.g. de-DE
            // culture without InvariantCulture).
            StringAssert.DoesNotContain(",5", json);
            StringAssert.Contains("0.5", json);
        }

        #endregion

        #region Sanitize

        [Test]
        public void Sanitize_OutOfOrderTimes_DropsNonMonotonicPoints()
        {
            var messy = new List<HapticBreakpoint>
            {
                new(0f, 1f, 0.5f),
                new(0.2f, 0.8f, 0.5f),
                new(0.1f, 0.6f, 0.5f), // regresses — must be dropped
                new(0.3f, 0f, 0.5f),
            };

            var clean = HapticPatternBuilder.Sanitize(messy);

            Assert.AreEqual(3, clean.Count);
            for (int i = 1; i < clean.Count; i++)
                Assert.Greater(clean[i].Time, clean[i - 1].Time);
        }

        [Test]
        public void Sanitize_ClampsAmplitudeAndFrequency()
        {
            var wild = new List<HapticBreakpoint>
            {
                new(0f, 5f, -2f),
                new(0.1f, -1f, 3f),
            };

            var clean = HapticPatternBuilder.Sanitize(wild);

            Assert.AreEqual(1f, clean[0].Amplitude);
            Assert.AreEqual(0f, clean[0].Frequency);
            Assert.AreEqual(0f, clean[1].Amplitude);
            Assert.AreEqual(1f, clean[1].Frequency);
        }

        [Test]
        public void Sanitize_EmptyInput_ProducesPlayableTwoPointEnvelope()
        {
            var clean = HapticPatternBuilder.Sanitize(new List<HapticBreakpoint>());
            Assert.GreaterOrEqual(clean.Count, 2);
        }

        #endregion

        #region Duration + Trim

        [Test]
        public void Duration_ReturnsLastBreakpointTime()
        {
            Assert.AreEqual(0.3f, HapticPatternBuilder.Duration(SimpleEnvelope()), 1e-5f);
        }

        [Test]
        public void Trim_CutsLongEnvelope_AndEndsAtZeroAmplitude()
        {
            var trimmed = HapticPatternBuilder.Trim(SimpleEnvelope(), 0.15f);

            Assert.AreEqual(0.15f, HapticPatternBuilder.Duration(trimmed), 1e-5f);
            Assert.AreEqual(0f, trimmed[trimmed.Count - 1].Amplitude);
        }

        [Test]
        public void Trim_ShorterThanCap_IsUnchanged()
        {
            var trimmed = HapticPatternBuilder.Trim(SimpleEnvelope(), 5f);
            Assert.AreEqual(0.3f, HapticPatternBuilder.Duration(trimmed), 1e-5f);
        }

        #endregion

        #region Gamepad rumble

        [Test]
        public void RenderRumble_ProducesValidParallelArrays()
        {
            var rumble = HapticPatternBuilder.RenderRumble(SimpleEnvelope());

            Assert.IsTrue(rumble.IsValid());
            Assert.AreEqual(rumble.durationsMs.Length, rumble.lowFrequencyMotorSpeeds.Length);
            Assert.AreEqual(rumble.durationsMs.Length, rumble.highFrequencyMotorSpeeds.Length);
        }

        [Test]
        public void RenderRumble_TotalDurationMatchesEnvelope()
        {
            var rumble = HapticPatternBuilder.RenderRumble(SimpleEnvelope(), 50);

            // 0.3s envelope at 50ms steps = 6 entries = 300ms total.
            Assert.AreEqual(300, rumble.totalDurationMs);
        }

        [Test]
        public void RenderRumble_MotorSpeedsStayNormalized()
        {
            var rumble = HapticPatternBuilder.RenderRumble(SimpleEnvelope(), 16);

            foreach (var speed in rumble.lowFrequencyMotorSpeeds)
                Assert.That(speed, Is.InRange(0f, 1f));
            foreach (var speed in rumble.highFrequencyMotorSpeeds)
                Assert.That(speed, Is.InRange(0f, 1f));
        }

        [Test]
        public void RenderRumble_BassyContent_LeansOnLowFrequencyMotor()
        {
            var bassy = new List<HapticBreakpoint> { new(0f, 1f, 0f), new(0.2f, 1f, 0f) };
            var rumble = HapticPatternBuilder.RenderRumble(bassy);

            Assert.Greater(rumble.lowFrequencyMotorSpeeds[0], rumble.highFrequencyMotorSpeeds[0]);
        }

        [Test]
        public void RenderRumble_BrightContent_LeansOnHighFrequencyMotor()
        {
            var bright = new List<HapticBreakpoint> { new(0f, 1f, 1f), new(0.2f, 1f, 1f) };
            var rumble = HapticPatternBuilder.RenderRumble(bright);

            Assert.Greater(rumble.highFrequencyMotorSpeeds[0], rumble.lowFrequencyMotorSpeeds[0]);
        }

        #endregion

        #region SampleEnvelope

        [Test]
        public void SampleEnvelope_InterpolatesBetweenBreakpoints()
        {
            var envelope = HapticPatternBuilder.Sanitize(new List<HapticBreakpoint>
            {
                new(0f, 0f, 0f),
                new(1f, 1f, 1f),
            });

            HapticPatternBuilder.SampleEnvelope(envelope, 0.5f, out float amplitude, out float frequency);

            Assert.AreEqual(0.5f, amplitude, 1e-4f);
            Assert.AreEqual(0.5f, frequency, 1e-4f);
        }

        [Test]
        public void SampleEnvelope_BeforeStartAndAfterEnd_ClampsToEndpoints()
        {
            var envelope = HapticPatternBuilder.Sanitize(SimpleEnvelope());

            HapticPatternBuilder.SampleEnvelope(envelope, -1f, out float before, out _);
            HapticPatternBuilder.SampleEnvelope(envelope, 99f, out float after, out _);

            Assert.AreEqual(1f, before, 1e-4f);
            Assert.AreEqual(0f, after, 1e-4f);
        }

        #endregion

        #region Built-in generators

        [Test]
        public void BuildImpact_StartsEmphasized_EndsSilent()
        {
            var impact = HapticPatternBuilder.BuildImpact(0.3f, 0.9f, 0.7f);

            Assert.IsTrue(impact[0].Emphasize);
            Assert.AreEqual(0f, impact[impact.Count - 1].Amplitude);
        }

        [Test]
        public void BuildConstantBed_IsFullAmplitudeAtMidFrequency()
        {
            var bed = HapticPatternBuilder.BuildConstantBed(1f);

            Assert.AreEqual(2, bed.Count);
            Assert.AreEqual(1f, bed[0].Amplitude);
            Assert.AreEqual(0.5f, bed[0].Frequency);
            Assert.AreEqual(1f, HapticPatternBuilder.Duration(bed), 1e-5f);
        }

        #endregion
    }
}
#endif
