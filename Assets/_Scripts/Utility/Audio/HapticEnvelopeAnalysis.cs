using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Pure analysis math for converting an audio sample buffer into a haptic envelope
    /// and rendering it as Lofelt v1.0.0 .haptic JSON. Lives in the runtime assembly so
    /// it can be unit-tested in edit-mode without referencing UnityEditor.
    ///
    /// The math is deliberately simple: peak-per-window for amplitude (matches the
    /// punchy character of game SFX better than RMS) and zero-crossing rate as a cheap
    /// spectral-centroid proxy (no FFT dependency).
    /// </summary>
    public static class HapticEnvelopeAnalysis
    {
        public struct Settings
        {
            public float WindowMs;          // analysis window length in ms
            public float MinFreqHz;         // audio ZCR (Hz) that maps to haptic freq 0
            public float MaxFreqHz;         // audio ZCR (Hz) that maps to haptic freq 1
            public float AmplitudeGamma;    // gamma curve on renormalized amplitude (1=linear, <1 boosts quiet)
            public float AmplitudeFloor;    // post-normalization amplitudes below this clamp to 0
            public float MaxDurationSeconds; // truncate clips longer than this

            public static Settings Default => new Settings
            {
                WindowMs = 30f,
                MinFreqHz = 80f,
                MaxFreqHz = 4000f,
                AmplitudeGamma = 0.6f,
                AmplitudeFloor = 0.02f,
                MaxDurationSeconds = 10f,
            };
        }

        public struct Breakpoint
        {
            public float Time;
            public float Amplitude;
            public float Frequency;
        }

        public struct Envelope
        {
            public List<Breakpoint> Points;
            public int SampleRate;
            public float Duration;
        }

        public static Envelope Analyze(float[] monoSamples, int sampleRate, Settings settings)
        {
            if (monoSamples == null || monoSamples.Length == 0)
                return new Envelope { Points = new List<Breakpoint>(), SampleRate = sampleRate, Duration = 0f };

            int maxSamples = Mathf.Min(monoSamples.Length, Mathf.RoundToInt(settings.MaxDurationSeconds * sampleRate));
            int windowSamples = Mathf.Max(1, Mathf.RoundToInt(settings.WindowMs * 0.001f * sampleRate));
            int windowCount = Mathf.Max(1, maxSamples / windowSamples);

            var raw = new List<(float time, float peak, float zcr)>(windowCount);
            float peakAmp = 0f;

            for (int w = 0; w < windowCount; w++)
            {
                int start = w * windowSamples;
                int end = Mathf.Min(start + windowSamples, maxSamples);
                if (end <= start) break;

                float windowPeak = 0f;
                int crossings = 0;
                float prev = monoSamples[start];

                for (int i = start; i < end; i++)
                {
                    float s = monoSamples[i];
                    float a = Mathf.Abs(s);
                    if (a > windowPeak) windowPeak = a;
                    if ((prev >= 0f) != (s >= 0f)) crossings++;
                    prev = s;
                }

                float windowSeconds = (end - start) / (float)sampleRate;
                float zcr = windowSeconds > 0f ? (crossings / 2f) / windowSeconds : 0f;
                float t = start / (float)sampleRate;

                raw.Add((t, windowPeak, zcr));
                if (windowPeak > peakAmp) peakAmp = windowPeak;
            }

            var points = new List<Breakpoint>(raw.Count);
            float invPeak = peakAmp > 1e-6f ? 1f / peakAmp : 0f;
            float gamma = Mathf.Max(0.01f, settings.AmplitudeGamma);

            float logMin = Mathf.Log(Mathf.Max(1f, settings.MinFreqHz));
            float logMax = Mathf.Log(Mathf.Max(settings.MinFreqHz + 1f, settings.MaxFreqHz));
            float logRange = logMax - logMin;

            foreach (var (time, peak, zcr) in raw)
            {
                float normAmp = peak * invPeak;
                normAmp = Mathf.Pow(Mathf.Clamp01(normAmp), gamma);
                if (normAmp < settings.AmplitudeFloor) normAmp = 0f;

                float freq = 0.5f;
                if (zcr > 0f)
                {
                    float logZ = Mathf.Log(Mathf.Max(1f, zcr));
                    freq = Mathf.Clamp01((logZ - logMin) / logRange);
                }

                points.Add(new Breakpoint { Time = time, Amplitude = normAmp, Frequency = freq });
            }

            return new Envelope
            {
                Points = points,
                SampleRate = sampleRate,
                Duration = maxSamples / (float)sampleRate,
            };
        }

        /// <summary>
        /// Render an envelope as a Lofelt v1.0.0 .haptic JSON string.
        /// </summary>
        public static string RenderHapticJson(Envelope env, string sourceName, string project = null)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(2048);
            sb.Append('{');
            sb.Append("\"version\":{\"major\":1,\"minor\":0,\"patch\":0},");
            sb.Append("\"metadata\":{");
            sb.AppendFormat(inv, "\"editor\":\"CosmicShore.HapticClipBaker\",");
            sb.AppendFormat(inv, "\"source\":\"{0}\",", JsonEscape(sourceName));
            sb.AppendFormat(inv, "\"project\":\"{0}\",", JsonEscape(project ?? sourceName));
            sb.Append("\"description\":\"\",\"tags\":[]");
            sb.Append("},");
            sb.Append("\"signals\":{\"continuous\":{\"envelopes\":{");

            sb.Append("\"amplitude\":[");
            for (int i = 0; i < env.Points.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = env.Points[i];
                sb.AppendFormat(inv, "{{\"time\":{0:0.######},\"amplitude\":{1:0.######}}}", p.Time, p.Amplitude);
            }
            sb.Append("],");

            sb.Append("\"frequency\":[");
            for (int i = 0; i < env.Points.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var p = env.Points[i];
                sb.AppendFormat(inv, "{{\"time\":{0:0.######},\"frequency\":{1:0.######}}}", p.Time, p.Frequency);
            }
            sb.Append(']');

            sb.Append("}}}}");
            return sb.ToString();
        }

        public static float[] InterleavedToMono(float[] interleaved, int channels)
        {
            if (channels <= 1) return interleaved;
            int frames = interleaved.Length / channels;
            var mono = new float[frames];
            float invCh = 1f / channels;
            for (int f = 0; f < frames; f++)
            {
                float sum = 0f;
                int baseIdx = f * channels;
                for (int c = 0; c < channels; c++) sum += interleaved[baseIdx + c];
                mono[f] = sum * invCh;
            }
            return mono;
        }

        static string JsonEscape(string s) =>
            s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
