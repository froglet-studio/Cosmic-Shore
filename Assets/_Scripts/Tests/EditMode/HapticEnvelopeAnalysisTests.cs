using System;
using NUnit.Framework;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Verifies the analysis math used by HapticClipBaker.
    /// Synthesizes known waveforms, runs Analyze, and checks invariants:
    ///   * breakpoint count matches windowing
    ///   * peak amplitude renormalized to ~1.0
    ///   * silent input produces no haptic energy
    ///   * frequency mapping produces values in [0,1]
    ///   * higher zero-crossing rate maps to higher haptic frequency
    ///   * rendered JSON is well-formed (matches Lofelt v1.0.0 schema keys)
    /// </summary>
    [TestFixture]
    public class HapticEnvelopeAnalysisTests
    {
        const int SampleRate = 44100;

        static float[] Sine(float freqHz, float seconds, float amplitude = 1f)
        {
            int n = (int)(seconds * SampleRate);
            var s = new float[n];
            float w = 2f * Mathf.PI * freqHz / SampleRate;
            for (int i = 0; i < n; i++) s[i] = amplitude * Mathf.Sin(w * i);
            return s;
        }

        [Test]
        public void Analyze_EmptyInput_ReturnsEmptyEnvelope()
        {
            var env = HapticEnvelopeAnalysis.Analyze(new float[0], SampleRate, HapticEnvelopeAnalysis.Settings.Default);
            Assert.AreEqual(0, env.Points.Count);
            Assert.AreEqual(0f, env.Duration);
        }

        [Test]
        public void Analyze_SilentInput_ProducesZeroAmplitudes()
        {
            var samples = new float[SampleRate]; // 1 second of silence
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, HapticEnvelopeAnalysis.Settings.Default);

            Assert.IsNotEmpty(env.Points);
            foreach (var p in env.Points)
                Assert.AreEqual(0f, p.Amplitude, 1e-5f, $"silent sample produced amplitude at t={p.Time}");
        }

        [Test]
        public void Analyze_PeakNormalizedToOne()
        {
            // a 0.1-amplitude sine should still normalize to peak 1.0
            var samples = Sine(440f, 0.5f, 0.1f);
            var settings = HapticEnvelopeAnalysis.Settings.Default;
            settings.AmplitudeGamma = 1f; // disable gamma so we can compare directly
            settings.AmplitudeFloor = 0f;
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, settings);

            float maxAmp = 0f;
            foreach (var p in env.Points) if (p.Amplitude > maxAmp) maxAmp = p.Amplitude;
            Assert.AreEqual(1f, maxAmp, 0.05f);
        }

        [Test]
        public void Analyze_BreakpointCountMatchesWindowing()
        {
            // 1 second at 30ms windows = 33 windows
            var samples = Sine(440f, 1f);
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, HapticEnvelopeAnalysis.Settings.Default);
            Assert.AreEqual(33, env.Points.Count);
        }

        [Test]
        public void Analyze_FrequencyAlwaysInUnitRange()
        {
            var samples = Sine(2000f, 0.3f);
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, HapticEnvelopeAnalysis.Settings.Default);
            foreach (var p in env.Points)
            {
                Assert.GreaterOrEqual(p.Frequency, 0f);
                Assert.LessOrEqual(p.Frequency, 1f);
            }
        }

        [Test]
        public void Analyze_HigherPitchProducesHigherHapticFrequency()
        {
            var lowPitch = HapticEnvelopeAnalysis.Analyze(Sine(200f, 0.3f), SampleRate, HapticEnvelopeAnalysis.Settings.Default);
            var highPitch = HapticEnvelopeAnalysis.Analyze(Sine(3000f, 0.3f), SampleRate, HapticEnvelopeAnalysis.Settings.Default);

            float lowAvg = AverageFreq(lowPitch);
            float highAvg = AverageFreq(highPitch);
            Assert.Greater(highAvg, lowAvg, "3000Hz tone should map to higher haptic freq than 200Hz tone");
        }

        [Test]
        public void Analyze_TruncatesAtMaxDuration()
        {
            var samples = Sine(440f, 30f); // 30s clip
            var settings = HapticEnvelopeAnalysis.Settings.Default;
            settings.MaxDurationSeconds = 5f;
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, settings);
            Assert.LessOrEqual(env.Duration, 5.001f);
        }

        [Test]
        public void RenderHapticJson_HasLofeltV1Schema()
        {
            var samples = Sine(440f, 0.2f);
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, HapticEnvelopeAnalysis.Settings.Default);
            string json = HapticEnvelopeAnalysis.RenderHapticJson(env, "TestClip");

            StringAssert.Contains("\"version\":{\"major\":1,\"minor\":0,\"patch\":0}", json);
            StringAssert.Contains("\"signals\":{\"continuous\":{\"envelopes\":{", json);
            StringAssert.Contains("\"amplitude\":[", json);
            StringAssert.Contains("\"frequency\":[", json);
            StringAssert.Contains("\"source\":\"TestClip\"", json);
            // valid trailing braces
            Assert.AreEqual('}', json[json.Length - 1]);
        }

        [Test]
        public void RenderHapticJson_BreakpointCountsMatchEnvelope()
        {
            var samples = Sine(440f, 0.3f);
            var env = HapticEnvelopeAnalysis.Analyze(samples, SampleRate, HapticEnvelopeAnalysis.Settings.Default);
            string json = HapticEnvelopeAnalysis.RenderHapticJson(env, "TestClip");

            // Each breakpoint emits one {"time": …} token in the amplitude array and one in
            // the frequency array, so the total count should be 2× the envelope's point count.
            int timeTokens = CountSubstring(json, "{\"time\":");
            Assert.AreEqual(env.Points.Count * 2, timeTokens);
        }

        [Test]
        public void InterleavedToMono_AveragesChannels()
        {
            var stereo = new[] { 1f, 0f, 1f, 0f, 1f, 0f }; // L=1, R=0
            var mono = HapticEnvelopeAnalysis.InterleavedToMono(stereo, 2);
            Assert.AreEqual(3, mono.Length);
            foreach (var s in mono) Assert.AreEqual(0.5f, s, 1e-6f);
        }

        [Test]
        public void InterleavedToMono_SingleChannelReturnsSameArray()
        {
            var input = new[] { 0.1f, 0.2f, 0.3f };
            var output = HapticEnvelopeAnalysis.InterleavedToMono(input, 1);
            Assert.AreSame(input, output);
        }

        static float AverageFreq(HapticEnvelopeAnalysis.Envelope env)
        {
            float sum = 0f;
            foreach (var p in env.Points) sum += p.Frequency;
            return env.Points.Count > 0 ? sum / env.Points.Count : 0f;
        }

        static int CountSubstring(string s, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = s.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) { count++; idx += needle.Length; }
            return count;
        }
    }
}
