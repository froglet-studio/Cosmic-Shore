#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// HapticWaveformAnalyzer Tests - Validates audio-to-haptic envelope
    /// extraction on synthetic signals with known shapes.
    ///
    /// WHY THIS MATTERS:
    /// This analyzer is what makes haptics MATCH the audio: the editor baker
    /// runs it over the FMOD project's source wavs and the runtime runs it over
    /// legacy AudioClips. If envelope extraction drifts (wrong normalization,
    /// missed transients, unbounded breakpoint counts), every derived haptic
    /// clip degrades at once.
    /// </summary>
    [TestFixture]
    public class HapticWaveformAnalyzerTests
    {
        const int SampleRate = 44100;

        static float[] Silence(float seconds) => new float[(int)(seconds * SampleRate)];

        static float[] SineBurst(float seconds, float frequencyHz, float amplitude, float totalSeconds)
        {
            var samples = Silence(totalSeconds);
            int count = (int)(seconds * SampleRate);
            for (int i = 0; i < count && i < samples.Length; i++)
                samples[i] = amplitude * Mathf.Sin(2f * Mathf.PI * frequencyHz * i / SampleRate);
            return samples;
        }

        [Test]
        public void Analyze_Silence_ReturnsEmptyEnvelope()
        {
            var envelope = HapticWaveformAnalyzer.Analyze(Silence(0.5f), SampleRate, HapticWaveformAnalyzer.Settings.Default);
            Assert.IsEmpty(envelope);
        }

        [Test]
        public void Analyze_NullOrEmpty_ReturnsEmptyEnvelope()
        {
            Assert.IsEmpty(HapticWaveformAnalyzer.Analyze(null, SampleRate, HapticWaveformAnalyzer.Settings.Default));
            Assert.IsEmpty(HapticWaveformAnalyzer.Analyze(new float[0], SampleRate, HapticWaveformAnalyzer.Settings.Default));
        }

        [Test]
        public void Analyze_SineBurst_PeaksNearFullAmplitude()
        {
            var samples = SineBurst(0.2f, 440f, 0.8f, 0.5f);
            var envelope = HapticWaveformAnalyzer.Analyze(samples, SampleRate, HapticWaveformAnalyzer.Settings.Default);

            Assert.IsNotEmpty(envelope);
            float peak = 0f;
            foreach (var p in envelope) peak = Mathf.Max(peak, p.Amplitude);
            Assert.Greater(peak, 0.95f, "envelope normalizes so the loudest window reaches ~1");
        }

        [Test]
        public void Analyze_SineBurst_TrimsTrailingSilence()
        {
            var samples = SineBurst(0.2f, 440f, 0.8f, 2f); // 1.8s of trailing silence
            var envelope = HapticWaveformAnalyzer.Analyze(samples, SampleRate, HapticWaveformAnalyzer.Settings.Default);

            float duration = HapticPatternBuilder.Duration(envelope);
            Assert.Less(duration, 0.4f, "trailing silence must not inflate the haptic clip");
        }

        [Test]
        public void Analyze_RespectsBreakpointBudget()
        {
            var settings = HapticWaveformAnalyzer.Settings.Default;
            settings.MaxBreakpoints = 8;
            settings.MaxEmphasisPoints = 0;

            // Noisy content that would otherwise produce a breakpoint per window.
            var samples = new float[SampleRate];
            var random = new System.Random(1234);
            for (int i = 0; i < samples.Length; i++)
                samples[i] = (float)(random.NextDouble() * 2.0 - 1.0) * (0.2f + 0.8f * Mathf.PingPong(i / 2000f, 1f));

            var envelope = HapticWaveformAnalyzer.Analyze(samples, SampleRate, settings);

            Assert.LessOrEqual(envelope.Count, 8);
        }

        [Test]
        public void Analyze_SharpAttack_ProducesEmphasis()
        {
            // Hard onset: silence then instant full-scale content.
            var samples = Silence(1f);
            int onset = (int)(0.3f * SampleRate);
            for (int i = onset; i < onset + SampleRate / 10; i++)
                samples[i] = Mathf.Sin(2f * Mathf.PI * 200f * (i - onset) / SampleRate);

            var envelope = HapticWaveformAnalyzer.Analyze(samples, SampleRate, HapticWaveformAnalyzer.Settings.Default);

            bool foundEmphasis = false;
            foreach (var p in envelope)
                if (p.Emphasize && Mathf.Abs(p.Time - 0.3f) < 0.05f)
                    foundEmphasis = true;
            Assert.IsTrue(foundEmphasis, "the hard onset should register as an emphasis breakpoint near t=0.3");
        }

        [Test]
        public void Analyze_LowVsHighFrequency_MapsToHapticFrequency()
        {
            var settings = HapticWaveformAnalyzer.Settings.Default;

            var low = HapticWaveformAnalyzer.Analyze(SineBurst(0.3f, 90f, 0.8f, 0.3f), SampleRate, settings);
            var high = HapticWaveformAnalyzer.Analyze(SineBurst(0.3f, 3000f, 0.8f, 0.3f), SampleRate, settings);

            Assert.IsNotEmpty(low);
            Assert.IsNotEmpty(high);
            Assert.Less(low[0].Frequency, 0.35f, "a 90 Hz tone should read as a low haptic frequency");
            Assert.Greater(high[0].Frequency, 0.75f, "a 3 kHz tone should read as a high haptic frequency");
        }

        [Test]
        public void Analyze_TimesAreStrictlyIncreasing()
        {
            var samples = SineBurst(0.5f, 440f, 0.8f, 0.5f);
            var envelope = HapticWaveformAnalyzer.Analyze(samples, SampleRate, HapticWaveformAnalyzer.Settings.Default);

            for (int i = 1; i < envelope.Count; i++)
                Assert.Greater(envelope[i].Time, envelope[i - 1].Time);
        }

        [Test]
        public void InterleavedToMono_AveragesChannels()
        {
            var stereo = new float[] { 1f, 0f, 0.5f, 0.5f, -1f, 1f };
            var mono = HapticWaveformAnalyzer.InterleavedToMono(stereo, 2);

            Assert.AreEqual(3, mono.Length);
            Assert.AreEqual(0.5f, mono[0], 1e-5f);
            Assert.AreEqual(0.5f, mono[1], 1e-5f);
            Assert.AreEqual(0f, mono[2], 1e-5f);
        }

        [Test]
        public void InterleavedToMono_MonoInput_PassesThrough()
        {
            var mono = new float[] { 0.1f, 0.2f };
            Assert.AreSame(mono, HapticWaveformAnalyzer.InterleavedToMono(mono, 1));
        }
    }
}
#endif
