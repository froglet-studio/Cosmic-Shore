#if UNITY_EDITOR
using System;
using CosmicShore.Core;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// AudioHapticsBakedDefaults Tests - Validates the measured-envelope tables
    /// that back every audio category with zero asset wiring.
    ///
    /// WHY THIS MATTERS:
    /// The defaults are the guaranteed floor of the haptic experience: when the
    /// config asset is missing (or misses a category), these tables ARE the
    /// haptics. Every enum value must resolve to a playable, well-formed
    /// envelope, and adding a new audio category must never silently ship
    /// without touch feedback.
    /// </summary>
    [TestFixture]
    public class AudioHapticsBakedDefaultsTests
    {
        [Test]
        public void ForGameplay_EveryCategory_HasPlayableSpec()
        {
            foreach (GameplaySFXCategory category in Enum.GetValues(typeof(GameplaySFXCategory)))
            {
                var spec = AudioHapticsBakedDefaults.ForGameplay(category);

                Assert.IsNotNull(spec, $"{category} has no baked default");
                Assert.IsTrue(spec.HasEnvelope, $"{category} default has an empty envelope");
                Assert.That(spec.gain, Is.InRange(0f, 1f), $"{category} gain out of range");
                Assert.That(spec.priority, Is.InRange(0, 100), $"{category} priority out of range");
                Assert.GreaterOrEqual(spec.cooldownSeconds, 0f, $"{category} negative cooldown");
            }
        }

        [Test]
        public void ForMenu_EveryCategory_HasPlayableSpec()
        {
            foreach (MenuAudioCategory category in Enum.GetValues(typeof(MenuAudioCategory)))
            {
                var spec = AudioHapticsBakedDefaults.ForMenu(category);

                Assert.IsNotNull(spec, $"{category} has no baked default");
                Assert.IsTrue(spec.HasEnvelope, $"{category} default has an empty envelope");
            }
        }

        [Test]
        public void AllEnvelopes_AreStrictlyTimeOrdered_AndNormalized()
        {
            foreach (GameplaySFXCategory category in Enum.GetValues(typeof(GameplaySFXCategory)))
                AssertWellFormed(AudioHapticsBakedDefaults.ForGameplay(category).envelope, category.ToString());
            foreach (MenuAudioCategory category in Enum.GetValues(typeof(MenuAudioCategory)))
                AssertWellFormed(AudioHapticsBakedDefaults.ForMenu(category).envelope, category.ToString());
        }

        [Test]
        public void AllEnvelopes_EndSilent_SoClipsReleaseTheActuatorCleanly()
        {
            foreach (GameplaySFXCategory category in Enum.GetValues(typeof(GameplaySFXCategory)))
            {
                var envelope = AudioHapticsBakedDefaults.ForGameplay(category).envelope;
                Assert.AreEqual(0f, envelope[envelope.Count - 1].Amplitude, 1e-4f,
                    $"{category} envelope must end at zero amplitude");
            }
        }

        [Test]
        public void ImpactCategories_CarryEmphasisTransients()
        {
            // The signature "snap" moments must have at least one emphasis point
            // (iOS Core Haptics transient) — that's what makes impacts feel crisp.
            var mustSnap = new[]
            {
                GameplaySFXCategory.BlockDestroy,
                GameplaySFXCategory.VesselImpact,
                GameplaySFXCategory.TrackImpact,
                GameplaySFXCategory.CrystalCollect,
            };

            foreach (var category in mustSnap)
            {
                bool hasEmphasis = false;
                foreach (var point in AudioHapticsBakedDefaults.ForGameplay(category).envelope)
                    if (point.Emphasize) hasEmphasis = true;
                Assert.IsTrue(hasEmphasis, $"{category} should carry an emphasis transient");
            }
        }

        [Test]
        public void BigMoments_OutrankChatter()
        {
            int explosion = AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.MineExplode).priority;
            int impact = AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.VesselImpact).priority;
            int blockBreak = AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.BlockDestroy).priority;
            int skim = AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.CrystalSkim).priority;

            Assert.Greater(explosion, blockBreak);
            Assert.Greater(explosion, skim);
            Assert.Greater(impact, blockBreak);
        }

        [Test]
        public void ChatterCategories_HaveCooldowns()
        {
            Assert.Greater(AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.BlockDestroy).cooldownSeconds, 0.05f);
            Assert.Greater(AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.CrystalSkim).cooldownSeconds, 0.05f);
        }

        [Test]
        public void AllDefaults_CompileToValidPatterns()
        {
            foreach (GameplaySFXCategory category in Enum.GetValues(typeof(GameplaySFXCategory)))
            {
                var spec = AudioHapticsBakedDefaults.ForGameplay(category);
                var json = HapticPatternBuilder.RenderJson(spec.envelope);
                var rumble = HapticPatternBuilder.RenderRumble(spec.envelope);

                StringAssert.Contains("\"amplitude\":[", json);
                Assert.IsTrue(rumble.IsValid(), $"{category} produces invalid gamepad rumble");
                Assert.Greater(HapticPatternBuilder.Duration(spec.envelope), 0f);
            }
        }

        static void AssertWellFormed(System.Collections.Generic.List<HapticBreakpoint> envelope, string label)
        {
            Assert.IsNotEmpty(envelope, label);
            for (int i = 0; i < envelope.Count; i++)
            {
                var p = envelope[i];
                Assert.That(p.Amplitude, Is.InRange(0f, 1f), $"{label}[{i}] amplitude");
                Assert.That(p.Frequency, Is.InRange(0f, 1f), $"{label}[{i}] frequency");
                if (i > 0)
                    Assert.Greater(p.Time, envelope[i - 1].Time, $"{label}[{i}] time must increase");
            }
        }
    }
}
#endif
