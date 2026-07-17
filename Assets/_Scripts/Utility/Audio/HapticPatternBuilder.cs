using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Lofelt.NiceVibrations;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One breakpoint of a haptic envelope. Time is seconds from clip start;
    /// Amplitude/Frequency are the normalized 0..1 values of the Lofelt
    /// <c>.haptic</c> format. A breakpoint may additionally carry an emphasis —
    /// a sharp transient (iOS Core Haptics transient / short max-amplitude
    /// burst elsewhere) layered on top of the continuous envelope.
    /// </summary>
    [Serializable]
    public struct HapticBreakpoint
    {
        public float Time;
        [Range(0f, 1f)] public float Amplitude;
        [Range(0f, 1f)] public float Frequency;
        public bool Emphasize;
        [Range(0f, 1f)] public float EmphasisAmplitude;
        [Range(0f, 1f)] public float EmphasisFrequency;

        public HapticBreakpoint(float time, float amplitude, float frequency)
        {
            Time = time;
            Amplitude = amplitude;
            Frequency = frequency;
            Emphasize = false;
            EmphasisAmplitude = 0f;
            EmphasisFrequency = 0f;
        }

        public HapticBreakpoint(float time, float amplitude, float frequency, float emphasisAmplitude, float emphasisFrequency)
        {
            Time = time;
            Amplitude = amplitude;
            Frequency = frequency;
            Emphasize = true;
            EmphasisAmplitude = emphasisAmplitude;
            EmphasisFrequency = emphasisFrequency;
        }
    }

    /// <summary>
    /// Renders haptic envelopes into the two runtime representations the
    /// NiceVibrations stack plays: Lofelt <c>.haptic</c> v1.0.0 JSON (iOS /
    /// Android) and <see cref="GamepadRumble"/> (desktop / editor gamepads).
    /// Pure math + string building — no Unity object access — so it is
    /// edit-mode testable and callable from both runtime and editor bakers.
    /// </summary>
    public static class HapticPatternBuilder
    {
        /// <summary>Gamepad rumble resample step. 50 ms preserves envelope shape while staying well above GamepadRumbler's 16 ms floor.</summary>
        public const int RumbleStepMs = 50;

        const float MinBreakpointSpacing = 0.001f;

        /// <summary>
        /// Sanitizes an envelope in place semantics-wise (returns a new list):
        /// clamps values, enforces strictly increasing time, guarantees at
        /// least two points (a lone point gets a zero-amplitude tail).
        /// </summary>
        public static List<HapticBreakpoint> Sanitize(IReadOnlyList<HapticBreakpoint> points)
        {
            var result = new List<HapticBreakpoint>(points?.Count ?? 0);
            if (points != null)
            {
                float lastTime = float.NegativeInfinity;
                for (int i = 0; i < points.Count; i++)
                {
                    var p = points[i];
                    if (float.IsNaN(p.Time) || float.IsInfinity(p.Time)) continue;
                    p.Time = Mathf.Max(0f, p.Time);
                    if (p.Time <= lastTime + MinBreakpointSpacing)
                    {
                        if (result.Count == 0) p.Time = 0f;
                        else continue;
                    }
                    p.Amplitude = Mathf.Clamp01(p.Amplitude);
                    p.Frequency = Mathf.Clamp01(p.Frequency);
                    p.EmphasisAmplitude = Mathf.Clamp01(p.EmphasisAmplitude);
                    p.EmphasisFrequency = Mathf.Clamp01(p.EmphasisFrequency);
                    lastTime = p.Time;
                    result.Add(p);
                }
            }

            if (result.Count == 0)
                result.Add(new HapticBreakpoint(0f, 0f, 0.5f));
            if (result.Count == 1)
            {
                var only = result[0];
                result.Add(new HapticBreakpoint(only.Time + 0.05f, 0f, only.Frequency));
            }
            return result;
        }

        /// <summary>Clip duration = time of the last breakpoint (after sanitize).</summary>
        public static float Duration(IReadOnlyList<HapticBreakpoint> points)
        {
            if (points == null || points.Count == 0) return 0f;
            float max = 0f;
            for (int i = 0; i < points.Count; i++)
                max = Mathf.Max(max, points[i].Time);
            return max;
        }

        /// <summary>
        /// Renders a Lofelt <c>.haptic</c> v1.0.0 JSON string with amplitude +
        /// frequency envelopes and per-breakpoint emphasis, matching the format
        /// of the plugin's own templates (see
        /// Assets/NiceVibrations/Scripts/Components/Resources/nv-*.txt).
        /// </summary>
        public static string RenderJson(IReadOnlyList<HapticBreakpoint> envelope)
        {
            var points = Sanitize(envelope);
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder(1024);

            sb.Append("{\"version\":{\"major\":1,\"minor\":0,\"patch\":0},");
            sb.Append("\"signals\":{\"continuous\":{\"envelopes\":{");

            sb.Append("\"amplitude\":[");
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"time\":").Append(p.Time.ToString("0.######", inv));
                sb.Append(",\"amplitude\":").Append(p.Amplitude.ToString("0.######", inv));
                if (p.Emphasize)
                {
                    sb.Append(",\"emphasis\":{\"amplitude\":").Append(p.EmphasisAmplitude.ToString("0.######", inv));
                    sb.Append(",\"frequency\":").Append(p.EmphasisFrequency.ToString("0.######", inv));
                    sb.Append('}');
                }
                sb.Append('}');
            }
            sb.Append("],");

            sb.Append("\"frequency\":[");
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"time\":").Append(p.Time.ToString("0.######", inv));
                sb.Append(",\"frequency\":").Append(p.Frequency.ToString("0.######", inv));
                sb.Append('}');
            }
            sb.Append(']');

            sb.Append("}}}}");
            return sb.ToString();
        }

        /// <summary>UTF-8 bytes of <see cref="RenderJson"/> — the payload <c>Lofelt.NiceVibrations.HapticController.Load</c> expects.</summary>
        public static byte[] RenderJsonBytes(IReadOnlyList<HapticBreakpoint> envelope)
            => Encoding.UTF8.GetBytes(RenderJson(envelope));

        /// <summary>
        /// Converts an envelope to a <see cref="GamepadRumble"/> by resampling
        /// at a fixed step (linear interpolation between breakpoints), so sparse
        /// breakpoints still produce smooth ramps on the motors. The haptic
        /// frequency steers the low/high motor balance: bassy content leans on
        /// the low-frequency (heavy) motor, bright content on the high-frequency
        /// (light) motor. Emphasis breakpoints slam their sample to the emphasis
        /// amplitude so transients still snap on rumble hardware.
        /// </summary>
        public static GamepadRumble RenderRumble(IReadOnlyList<HapticBreakpoint> envelope, int stepMs = RumbleStepMs)
        {
            var points = Sanitize(envelope);
            var rumble = new GamepadRumble();

            stepMs = Mathf.Max(16, stepMs);
            float duration = points[points.Count - 1].Time - points[0].Time;
            int entryCount = Mathf.Max(1, Mathf.CeilToInt(duration * 1000f / stepMs));

            rumble.durationsMs = new int[entryCount];
            rumble.lowFrequencyMotorSpeeds = new float[entryCount];
            rumble.highFrequencyMotorSpeeds = new float[entryCount];
            rumble.totalDurationMs = 0;

            float t0 = points[0].Time;
            for (int i = 0; i < entryCount; i++)
            {
                float sampleTime = t0 + (i + 0.5f) * stepMs * 0.001f;
                SampleEnvelope(points, sampleTime, out float amp, out float freq);

                // Emphasis inside this sample window snaps the window to the transient.
                for (int p = 0; p < points.Count; p++)
                {
                    var bp = points[p];
                    if (!bp.Emphasize) continue;
                    float windowStart = t0 + i * stepMs * 0.001f;
                    float windowEnd = windowStart + stepMs * 0.001f;
                    if (bp.Time >= windowStart && bp.Time < windowEnd)
                    {
                        amp = Mathf.Max(amp, bp.EmphasisAmplitude);
                        freq = bp.EmphasisFrequency;
                    }
                }

                rumble.durationsMs[i] = stepMs;
                rumble.lowFrequencyMotorSpeeds[i] = Mathf.Clamp01(amp * Mathf.Clamp01(1.25f - freq));
                rumble.highFrequencyMotorSpeeds[i] = Mathf.Clamp01(amp * Mathf.Clamp01(0.25f + freq));
                rumble.totalDurationMs += stepMs;
            }

            return rumble;
        }

        /// <summary>Linear-interpolated amplitude/frequency of an envelope at <paramref name="time"/> (points must be sanitized/sorted).</summary>
        public static void SampleEnvelope(IReadOnlyList<HapticBreakpoint> points, float time, out float amplitude, out float frequency)
        {
            if (points == null || points.Count == 0)
            {
                amplitude = 0f;
                frequency = 0.5f;
                return;
            }

            if (time <= points[0].Time)
            {
                amplitude = points[0].Amplitude;
                frequency = points[0].Frequency;
                return;
            }

            for (int i = 1; i < points.Count; i++)
            {
                if (time > points[i].Time) continue;
                var a = points[i - 1];
                var b = points[i];
                float span = b.Time - a.Time;
                float f = span > 0f ? (time - a.Time) / span : 1f;
                amplitude = Mathf.Lerp(a.Amplitude, b.Amplitude, f);
                frequency = Mathf.Lerp(a.Frequency, b.Frequency, f);
                return;
            }

            var last = points[points.Count - 1];
            amplitude = last.Amplitude;
            frequency = last.Frequency;
        }

        /// <summary>
        /// Trims an envelope to <paramref name="maxDuration"/>: points beyond the
        /// cut are dropped and a final breakpoint is inserted at the cut with the
        /// interpolated amplitude ramped to zero, so the clip ends cleanly instead
        /// of stopping mid-vibration.
        /// </summary>
        public static List<HapticBreakpoint> Trim(IReadOnlyList<HapticBreakpoint> envelope, float maxDuration)
        {
            var points = Sanitize(envelope);
            if (maxDuration <= 0f || points[points.Count - 1].Time <= maxDuration)
                return points;

            var result = new List<HapticBreakpoint>(points.Count);
            for (int i = 0; i < points.Count && points[i].Time < maxDuration; i++)
                result.Add(points[i]);

            result.Add(new HapticBreakpoint(maxDuration, 0f, result.Count > 0 ? result[result.Count - 1].Frequency : 0.5f));
            return Sanitize(result);
        }

        /// <summary>
        /// Procedural fallback envelope for content with no baked data: a sharp
        /// emphasized attack decaying exponentially — a classic impact shape.
        /// </summary>
        public static List<HapticBreakpoint> BuildImpact(float duration, float amplitude, float frequency)
        {
            duration = Mathf.Max(0.05f, duration);
            amplitude = Mathf.Clamp01(amplitude);
            frequency = Mathf.Clamp01(frequency);
            return new List<HapticBreakpoint>
            {
                new HapticBreakpoint(0f, amplitude, frequency, amplitude, frequency),
                new HapticBreakpoint(duration * 0.25f, amplitude * 0.45f, frequency),
                new HapticBreakpoint(duration * 0.55f, amplitude * 0.18f, Mathf.Clamp01(frequency * 0.8f)),
                new HapticBreakpoint(duration, 0f, Mathf.Clamp01(frequency * 0.7f)),
            };
        }

        /// <summary>
        /// The continuous "bed" clip the orchestrator modulates in real time:
        /// constant full amplitude at mid frequency so <c>clipLevel</c> and
        /// <c>clipFrequencyShift</c> have full authority in both directions.
        /// </summary>
        public static List<HapticBreakpoint> BuildConstantBed(float duration)
        {
            duration = Mathf.Max(0.05f, duration);
            return new List<HapticBreakpoint>
            {
                new HapticBreakpoint(0f, 1f, 0.5f),
                new HapticBreakpoint(duration, 1f, 0.5f),
            };
        }
    }
}
