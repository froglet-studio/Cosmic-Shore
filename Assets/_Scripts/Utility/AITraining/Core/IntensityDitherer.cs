using System;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Turns ONE flawless trained pilot into four difficulty levels, without ever
    /// touching the genome. Difficulty degrades along two honest axes:
    ///
    ///   INPUT (per frame, applied by TrainingModulator.LateUpdate): reaction
    ///   delay, decision dropout, gaussian steering noise, throttle scaling —
    ///   the same imperfections a human hand has, applied to what the pilot's
    ///   own steering wrote this frame.
    ///
    ///   TEMPO (applied once at genome-apply time): a skill factor multiplied
    ///   into the pilot's skill dial and a cooldown factor stretched over its
    ///   ability cadence — a lower-intensity AI thinks and acts less often, it
    ///   is never lobotomised.
    ///
    /// Intensity 4 must remain the identity transform: the trained ceiling,
    /// untouched. AITrainingCoreTests holds that invariant.
    /// </summary>
    [Serializable]
    public class IntensityDitherer
    {
        /// <summary>One frame of steering input as the AI wrote it.</summary>
        public struct InputFrame
        {
            public float XSum;
            public float YSum;
            public float XDiff;
            public float YDiff;
            public Vector2 EasedLeft;
        }

        [Serializable]
        public struct LevelSettings
        {
            [Tooltip("Probability per frame that the fresh decision is dropped and the last applied frame repeats.")]
            public float DropoutChance;
            [Tooltip("Std-dev of gaussian noise added to steering channels.")]
            public float NoiseAmplitude;
            [Tooltip("Input is sampled from this many seconds ago — simulated reaction time.")]
            public float ReactionDelaySeconds;
            [Tooltip("Multiplier on the throttle channel (dual-stick XDiff).")]
            public float ThrottleScale;
            [Tooltip("Multiplied into the pilot's ability cooldown scale at apply time — lower intensities use abilities less often.")]
            public float AbilityCooldownFactor;
            [Tooltip("Multiplied into the pilot's skill dial at apply time.")]
            public float SkillFactor;
        }

        // Index = intensity - 1. Intensity 4 is the identity by definition.
        public LevelSettings[] LevelsByIntensity =
        {
            new() { DropoutChance = 0.30f, NoiseAmplitude = 0.35f, ReactionDelaySeconds = 0.22f, ThrottleScale = 0.75f, AbilityCooldownFactor = 2.0f,  SkillFactor = 0.55f },
            new() { DropoutChance = 0.15f, NoiseAmplitude = 0.20f, ReactionDelaySeconds = 0.12f, ThrottleScale = 0.85f, AbilityCooldownFactor = 1.5f,  SkillFactor = 0.70f },
            new() { DropoutChance = 0.05f, NoiseAmplitude = 0.10f, ReactionDelaySeconds = 0.05f, ThrottleScale = 0.95f, AbilityCooldownFactor = 1.2f,  SkillFactor = 0.85f },
            new() { DropoutChance = 0.00f, NoiseAmplitude = 0.00f, ReactionDelaySeconds = 0.00f, ThrottleScale = 1.00f, AbilityCooldownFactor = 1.0f,  SkillFactor = 1.00f },
        };

        InputFrame _lastApplied;
        bool _hasLastApplied;

        struct DelayedSample { public float TimeStamp; public InputFrame Frame; }
        readonly DelayedSample[] _history = new DelayedSample[30];
        int _historyHead;

        public LevelSettings GetSettings(int intensity)
        {
            int idx = Mathf.Clamp(intensity - 1, 0, LevelsByIntensity.Length - 1);
            return LevelsByIntensity[idx];
        }

        public void Reset()
        {
            _hasLastApplied = false;
            _historyHead = 0;
            for (int i = 0; i < _history.Length; i++) _history[i] = default;
        }

        /// <summary>
        /// Degrades one frame of input per the intensity's settings. Intensity 4
        /// returns the frame untouched (and skips history bookkeeping entirely).
        /// </summary>
        public InputFrame Apply(int intensity, InputFrame frame, float now)
        {
            var s = GetSettings(intensity);
            bool identity = s.DropoutChance <= 0f && s.NoiseAmplitude <= 0f
                         && s.ReactionDelaySeconds <= 0f && s.ThrottleScale >= 1f;
            if (identity) return frame;

            // Record for reaction-delay sampling.
            _history[_historyHead] = new DelayedSample { TimeStamp = now, Frame = frame };
            _historyHead = (_historyHead + 1) % _history.Length;

            // Reaction delay: replace with the closest historical sample at (now - delay).
            if (s.ReactionDelaySeconds > 0f)
            {
                float target = now - s.ReactionDelaySeconds;
                float bestErr = float.PositiveInfinity;
                for (int i = 0; i < _history.Length; i++)
                {
                    var h = _history[i];
                    if (h.TimeStamp <= 0f) continue;
                    float err = Mathf.Abs(h.TimeStamp - target);
                    if (err < bestErr) { bestErr = err; frame = h.Frame; }
                }
            }

            // Dropout: hold the previous applied frame — "the hand didn't move this frame".
            if (s.DropoutChance > 0f && _hasLastApplied && UnityEngine.Random.value < s.DropoutChance)
                frame = _lastApplied;

            // Steering noise on every steering channel.
            if (s.NoiseAmplitude > 0f)
            {
                frame.XSum = Mathf.Clamp(frame.XSum + Gaussian() * s.NoiseAmplitude, -1f, 1f);
                frame.YSum = Mathf.Clamp(frame.YSum + Gaussian() * s.NoiseAmplitude, -1f, 1f);
                frame.YDiff = Mathf.Clamp(frame.YDiff + Gaussian() * s.NoiseAmplitude, -1f, 1f);
                frame.EasedLeft = new Vector2(
                    Mathf.Clamp(frame.EasedLeft.x + Gaussian() * s.NoiseAmplitude, -1f, 1f),
                    Mathf.Clamp(frame.EasedLeft.y + Gaussian() * s.NoiseAmplitude, -1f, 1f));
            }

            // Throttle scaling. XDiff is the dual-stick throttle channel (AIPilot writes
            // throttle there); single-stick vessels carry no throttle in these channels.
            frame.XDiff = Mathf.Clamp(frame.XDiff * s.ThrottleScale, -1f, 1f);

            _lastApplied = frame;
            _hasLastApplied = true;
            return frame;
        }

        static float Gaussian()
        {
            float u1 = Mathf.Max(UnityEngine.Random.value, 1e-6f);
            float u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
