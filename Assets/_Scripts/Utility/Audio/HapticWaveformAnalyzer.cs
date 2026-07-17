using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Converts an audio sample buffer into a haptic envelope
    /// (<see cref="HapticBreakpoint"/> list) so haptics can be derived from the
    /// actual waveform of a sound instead of hand-authored approximations.
    ///
    /// Used by the editor baker (Tools &gt; Cosmic Shore &gt; Audio Haptics) against
    /// the FMOD Studio project's source wavs, and at runtime by the legacy
    /// AudioClip path in <c>AudioSystem.PlaySFXClip</c>.
    ///
    /// The math is deliberately cheap — no FFT:
    ///  - amplitude  = per-window peak (matches the punchy character of game SFX
    ///    better than RMS),
    ///  - frequency  = zero-crossing rate mapped log-scale into the 0..1 haptic
    ///    frequency range (a crude but effective spectral-centroid proxy),
    ///  - emphasis   = positive peak flux between adjacent windows (attack
    ///    transients become Core Haptics transients on iOS).
    /// Breakpoints are then reduced to a budget by greedy max-deviation
    /// selection so clips stay tiny.
    /// </summary>
    public static class HapticWaveformAnalyzer
    {
        public struct Settings
        {
            public float WindowSeconds;          // analysis window length
            public float MaxDurationSeconds;     // truncate clips longer than this
            public float AmplitudeGamma;         // <1 boosts quiet detail after normalization
            public float AmplitudeFloor;         // normalized amplitudes below this clamp to 0
            public float MinFrequencyHz;         // ZCR mapped to haptic frequency 0
            public float MaxFrequencyHz;         // ZCR mapped to haptic frequency 1
            public int MaxBreakpoints;           // envelope budget after reduction
            public float EmphasisFluxThreshold;  // normalized peak-flux that marks a transient
            public float EmphasisMinGapSeconds;  // minimum spacing between transients
            public int MaxEmphasisPoints;        // strongest N transients kept

            public static Settings Default => new Settings
            {
                WindowSeconds = 0.025f,
                MaxDurationSeconds = 3f,
                AmplitudeGamma = 0.65f,
                AmplitudeFloor = 0.03f,
                MinFrequencyHz = 65f,
                MaxFrequencyHz = 3500f,
                MaxBreakpoints = 14,
                EmphasisFluxThreshold = 0.35f,
                EmphasisMinGapSeconds = 0.09f,
                MaxEmphasisPoints = 3,
            };
        }

        /// <summary>
        /// Analyzes mono samples into a reduced haptic envelope. Returns an
        /// empty list when there is nothing usable (null/empty/silent input).
        /// </summary>
        public static List<HapticBreakpoint> Analyze(float[] monoSamples, int sampleRate, Settings settings)
        {
            var result = new List<HapticBreakpoint>();
            if (monoSamples == null || monoSamples.Length == 0 || sampleRate <= 0)
                return result;

            int maxSamples = Mathf.Min(monoSamples.Length, Mathf.RoundToInt(settings.MaxDurationSeconds * sampleRate));
            int windowSamples = Mathf.Max(1, Mathf.RoundToInt(settings.WindowSeconds * sampleRate));
            int windowCount = Mathf.Max(1, maxSamples / windowSamples);
            if (maxSamples <= 0) return result;

            // ---- Pass 1: windowed peak, zero-crossing rate, peak flux ----
            var times = new float[windowCount];
            var peaks = new float[windowCount];
            var zcrs = new float[windowCount];
            var fluxes = new float[windowCount];

            float peakAll = 0f;
            float prevPeak = 0f;
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
                    float a = s >= 0f ? s : -s;
                    if (a > windowPeak) windowPeak = a;
                    if ((prev >= 0f) != (s >= 0f)) crossings++;
                    prev = s;
                }

                float seconds = (end - start) / (float)sampleRate;
                times[w] = start / (float)sampleRate;
                peaks[w] = windowPeak;
                zcrs[w] = seconds > 0f ? (crossings * 0.5f) / seconds : 0f;
                fluxes[w] = Mathf.Max(0f, windowPeak - prevPeak);
                prevPeak = windowPeak;
                if (windowPeak > peakAll) peakAll = windowPeak;
            }

            if (peakAll <= 1e-6f) return result;

            // ---- Pass 2: normalize + shape into candidate breakpoints ----
            float invPeak = 1f / peakAll;
            float gamma = Mathf.Max(0.01f, settings.AmplitudeGamma);
            var candidates = new List<HapticBreakpoint>(windowCount);
            int lastAudible = -1;
            for (int w = 0; w < windowCount; w++)
            {
                float amp = Mathf.Pow(Mathf.Clamp01(peaks[w] * invPeak), gamma);
                if (amp < settings.AmplitudeFloor) amp = 0f;
                if (amp > 0f) lastAudible = w;
                candidates.Add(new HapticBreakpoint(times[w], amp, FrequencyFromZcr(zcrs[w], settings)));
            }
            if (lastAudible < 0) return result;

            // Trim trailing silence, keeping one zero point to end the clip cleanly.
            int keepCount = Mathf.Min(candidates.Count, lastAudible + 2);
            candidates.RemoveRange(keepCount, candidates.Count - keepCount);

            // Sounds still loud at the buffer end (or the MaxDuration cut) have no
            // silent window to end on — append one so the clip always releases the
            // actuator instead of stopping mid-vibration.
            var lastCandidate = candidates[candidates.Count - 1];
            if (lastCandidate.Amplitude > 0f)
                candidates.Add(new HapticBreakpoint(lastCandidate.Time + settings.WindowSeconds, 0f, lastCandidate.Frequency));

            // ---- Pass 3: reduce to breakpoint budget ----
            result = Reduce(candidates, settings.MaxBreakpoints);

            // ---- Pass 4: transient detection -> emphasis ----
            MarkEmphasis(result, times, peaks, fluxes, invPeak, settings);

            return result;
        }

        /// <summary>Averages interleaved multi-channel samples into mono. Returns the input array when already mono.</summary>
        public static float[] InterleavedToMono(float[] interleaved, int channels)
        {
            if (interleaved == null || channels <= 1) return interleaved;
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

        /// <summary>Log-scale map of a zero-crossing rate (Hz) into the 0..1 haptic frequency range.</summary>
        public static float FrequencyFromZcr(float zcrHz, Settings settings)
        {
            if (zcrHz <= 0f) return 0.5f;
            float logMin = Mathf.Log(Mathf.Max(1f, settings.MinFrequencyHz));
            float logMax = Mathf.Log(Mathf.Max(settings.MinFrequencyHz + 1f, settings.MaxFrequencyHz));
            return Mathf.Clamp01((Mathf.Log(Mathf.Max(1f, zcrHz)) - logMin) / (logMax - logMin));
        }

        /// <summary>
        /// Greedy breakpoint reduction (Douglas-Peucker flavored): always keeps
        /// the first, last, and loudest points, then repeatedly keeps the point
        /// with the largest amplitude deviation from the current piecewise-linear
        /// envelope until the budget is met.
        /// </summary>
        static List<HapticBreakpoint> Reduce(List<HapticBreakpoint> points, int maxPoints)
        {
            maxPoints = Mathf.Max(3, maxPoints);
            if (points.Count <= maxPoints) return new List<HapticBreakpoint>(points);

            var keep = new SortedSet<int> { 0, points.Count - 1 };
            int loudest = 0;
            for (int i = 1; i < points.Count; i++)
                if (points[i].Amplitude > points[loudest].Amplitude)
                    loudest = i;
            keep.Add(loudest);

            while (keep.Count < maxPoints)
            {
                float bestDeviation = -1f;
                int bestIndex = -1;
                int prev = -1;
                foreach (int idx in keep)
                {
                    if (prev >= 0 && idx - prev > 1)
                    {
                        var a = points[prev];
                        var b = points[idx];
                        float span = b.Time - a.Time;
                        for (int i = prev + 1; i < idx; i++)
                        {
                            float f = span > 0f ? (points[i].Time - a.Time) / span : 0f;
                            float interp = Mathf.Lerp(a.Amplitude, b.Amplitude, f);
                            float deviation = Mathf.Abs(points[i].Amplitude - interp);
                            if (deviation > bestDeviation)
                            {
                                bestDeviation = deviation;
                                bestIndex = i;
                            }
                        }
                    }
                    prev = idx;
                }

                if (bestIndex < 0) break;
                keep.Add(bestIndex);
            }

            var result = new List<HapticBreakpoint>(keep.Count);
            foreach (int idx in keep) result.Add(points[idx]);
            return result;
        }

        /// <summary>
        /// Marks the strongest attack transients as emphasis on the nearest kept
        /// breakpoint (window-sized tolerance), inserting a breakpoint when no
        /// kept point is close enough.
        /// </summary>
        static void MarkEmphasis(List<HapticBreakpoint> envelope, float[] times, float[] peaks, float[] fluxes, float invPeak, Settings settings)
        {
            var transients = new List<(float time, float amplitude, float strength)>();
            float lastTime = float.NegativeInfinity;
            for (int w = 0; w < times.Length; w++)
            {
                float flux = fluxes[w] * invPeak;
                if (flux < settings.EmphasisFluxThreshold) continue;
                if (times[w] - lastTime < settings.EmphasisMinGapSeconds) continue;
                lastTime = times[w];
                transients.Add((times[w], Mathf.Clamp01(peaks[w] * invPeak), flux));
            }
            if (transients.Count == 0) return;

            transients.Sort((a, b) => b.strength.CompareTo(a.strength));
            int count = Mathf.Min(transients.Count, Mathf.Max(0, settings.MaxEmphasisPoints));

            for (int t = 0; t < count; t++)
            {
                var (time, amplitude, _) = transients[t];

                int nearest = -1;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < envelope.Count; i++)
                {
                    float d = Mathf.Abs(envelope[i].Time - time);
                    if (d < nearestDistance)
                    {
                        nearestDistance = d;
                        nearest = i;
                    }
                }

                if (nearest >= 0 && nearestDistance <= settings.WindowSeconds)
                {
                    var p = envelope[nearest];
                    p.Emphasize = true;
                    p.EmphasisAmplitude = Mathf.Max(p.EmphasisAmplitude, amplitude);
                    p.EmphasisFrequency = p.Frequency;
                    envelope[nearest] = p;
                }
                else
                {
                    HapticPatternBuilder.SampleEnvelope(envelope, time, out float amp, out float freq);
                    var inserted = new HapticBreakpoint(time, amp, freq, amplitude, freq);
                    int insertAt = envelope.Count;
                    for (int i = 0; i < envelope.Count; i++)
                    {
                        if (envelope[i].Time > time)
                        {
                            insertAt = i;
                            break;
                        }
                    }
                    envelope.Insert(insertAt, inserted);
                }
            }
        }
    }
}
