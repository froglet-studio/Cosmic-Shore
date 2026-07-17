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

        #region Feel doctrine (playtest-locked): sparse haptics, two hero feels

        [Test]
        public void Doctrine_SkimPulse_IsTheStrongestJuiciestHaptic()
        {
            var skim = AudioHapticsBakedDefaults.SkimPulse();

            Assert.IsNotNull(skim);
            Assert.GreaterOrEqual(skim.gain, 0.9f, "the skim pulse is the hero — full strength");
            Assert.LessOrEqual(skim.cooldownSeconds, 0.05f,
                "hitting many prisms in sequence must chain into a rapid pulse train");
            Assert.LessOrEqual(HapticPatternBuilder.Duration(skim.envelope), 0.12f,
                "each pulse must be a discrete tick, not a smear — the train reads pulse-by-pulse");

            bool hasEmphasis = false;
            foreach (var point in skim.envelope)
                if (point.Emphasize) hasEmphasis = true;
            Assert.IsTrue(hasEmphasis, "the pulse needs a crisp transient snap");
        }

        [Test]
        public void Doctrine_SkimPulse_IsMemoized()
        {
            Assert.AreSame(AudioHapticsBakedDefaults.SkimPulse(), AudioHapticsBakedDefaults.SkimPulse());
        }

        [Test]
        public void Doctrine_PrismPunish_IsLowFrequencyAndCutsThroughTheTrain()
        {
            var punish = AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.VesselImpact);
            var skim = AudioHapticsBakedDefaults.SkimPulse();

            Assert.GreaterOrEqual(punish.gain, 0.7f, "hitting a prism should feel distinctly punishing");
            Assert.LessOrEqual(punish.envelope[0].Frequency, 0.3f,
                "punish = a LOW thud (heavy motor / soft deep vibration), opposite in character to the bright skim");
            Assert.Greater(punish.priority, skim.priority,
                "a crash must cut through the reward train");
            Assert.AreEqual(0f, punish.audioEventGain,
                "the punish fires only from the local-player-gated effect SO, never from the audio hook (an AI crashing nearby must not thud this device)");
        }

        [Test]
        public void Doctrine_MenuHaptics_AreAllSilent()
        {
            foreach (MenuAudioCategory category in Enum.GetValues(typeof(MenuAudioCategory)))
                Assert.AreEqual(0f, AudioHapticsBakedDefaults.ForMenu(category).gain,
                    $"menu category {category} must default silent — a click is not worth a buzz");
        }

        [Test]
        public void Doctrine_GameplayHaptics_AreSparse()
        {
            int audible = 0;
            foreach (GameplaySFXCategory category in Enum.GetValues(typeof(GameplaySFXCategory)))
                if (AudioHapticsBakedDefaults.ForGameplay(category).gain > 0f)
                    audible++;

            Assert.LessOrEqual(audible, 5,
                "haptics are sparse signals, not a soundtrack — only events worth interrupting the hand keep a gain");
        }

        [Test]
        public void Doctrine_ChatterCategories_AreSilent()
        {
            var mustBeSilent = new[]
            {
                GameplaySFXCategory.BlockDestroy,   // fires en masse on trail breaks
                GameplaySFXCategory.CrystalSkim,    // superseded by the dedicated skim pulse
                GameplaySFXCategory.DriftStart,
                GameplaySFXCategory.DriftEnd,
                GameplaySFXCategory.BoostActivate,
                GameplaySFXCategory.GunFire,
                GameplaySFXCategory.ShieldActivate,
                GameplaySFXCategory.EnergyGain,
            };

            foreach (var category in mustBeSilent)
                Assert.AreEqual(0f, AudioHapticsBakedDefaults.ForGameplay(category).gain,
                    $"{category} must default silent");
        }

        [Test]
        public void Doctrine_VesselAttributedCategories_OptOutOfTheAudioHook()
        {
            // These have local-player-gated effect-SO paths; the automatic
            // audio hook (any actor, anywhere) must not fire them.
            Assert.AreEqual(0f, AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.VesselImpact).audioEventGain);
            Assert.AreEqual(0f, AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.TrackImpact).audioEventGain);
            Assert.AreEqual(0f, AudioHapticsBakedDefaults.ForGameplay(GameplaySFXCategory.CrystalCollect).audioEventGain);
        }

        #endregion

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
