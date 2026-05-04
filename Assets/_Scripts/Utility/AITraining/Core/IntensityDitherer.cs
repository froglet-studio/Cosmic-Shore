using System;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Runtime input-degradation layer that turns one "flawless" trained genome into
    /// a family of four difficulty levels.
    ///
    /// Intensity 4 is the trained genome verbatim. Intensities 1-3 inject reaction
    /// delay, gaussian noise, ability skipping, and decision dropouts.
    ///
    /// All dithering happens AFTER the policies have decided — we never alter the
    /// genome itself. The ditherer is stateless across calls except for a small
    /// rolling input history needed for reaction delay.
    /// </summary>
    [Serializable]
    public class IntensityDitherer
    {
        // Per-intensity dithering settings. Intensity 4 must be (0,0,0,0) — flawless.
        [Serializable]
        public struct LevelSettings
        {
            public float DropoutChance;        // 0..1, probability per frame to skip the new decision
            public float NoiseAmplitude;       // 0..1, std dev added to steering
            public float ReactionDelaySeconds; // 0..0.5
            public float AbilitySkipChance;    // 0..1
            public float ThrottleScale;        // 0..1, multiplier on chosen throttle
        }

        // Defaults chosen so L1 feels like a casual player, L4 is the trained ceiling.
        public LevelSettings[] LevelsByIntensity =
        {
            new() { DropoutChance = 0.30f, NoiseAmplitude = 0.40f, ReactionDelaySeconds = 0.20f, AbilitySkipChance = 0.50f, ThrottleScale = 0.75f },
            new() { DropoutChance = 0.15f, NoiseAmplitude = 0.20f, ReactionDelaySeconds = 0.10f, AbilitySkipChance = 0.25f, ThrottleScale = 0.85f },
            new() { DropoutChance = 0.05f, NoiseAmplitude = 0.10f, ReactionDelaySeconds = 0.05f, AbilitySkipChance = 0.10f, ThrottleScale = 0.95f },
            new() { DropoutChance = 0.00f, NoiseAmplitude = 0.00f, ReactionDelaySeconds = 0.00f, AbilitySkipChance = 0.00f, ThrottleScale = 1.00f },
        };

        DecisionOutput _lastApplied;
        bool _hasLastApplied;

        // Tiny ring buffer for reaction-delay simulation. 30 entries at 60fps = 0.5s history.
        struct DelayedSample { public float TimeStamp; public DecisionOutput Output; }
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
            for (int i = 0; i < _history.Length; i++)
                _history[i] = default;
        }

        /// <summary>
        /// Records the current decision and returns whatever decision should actually be
        /// applied this frame given the configured intensity.
        /// </summary>
        public DecisionOutput Apply(int intensity, DecisionOutput decision, float now)
        {
            var s = GetSettings(intensity);

            // 1) Push current decision into history for delay sampling.
            _history[_historyHead] = new DelayedSample { TimeStamp = now, Output = decision };
            _historyHead = (_historyHead + 1) % _history.Length;

            // 2) If reaction delay > 0, replace `decision` with the closest historical
            //    sample at (now - delay).
            if (s.ReactionDelaySeconds > 0f)
            {
                float target = now - s.ReactionDelaySeconds;
                float bestErr = float.PositiveInfinity;
                DecisionOutput pick = decision;
                for (int i = 0; i < _history.Length; i++)
                {
                    var h = _history[i];
                    if (h.TimeStamp <= 0f) continue;
                    float err = Mathf.Abs(h.TimeStamp - target);
                    if (err < bestErr) { bestErr = err; pick = h.Output; }
                }
                decision = pick;
            }

            // 3) Decision dropout — re-use the last applied decision, simulating "no input change".
            if (s.DropoutChance > 0f && _hasLastApplied && UnityEngine.Random.value < s.DropoutChance)
                decision = _lastApplied;

            // 4) Inject steering / roll noise.
            if (s.NoiseAmplitude > 0f)
            {
                decision.SteerLocal += new Vector2(SampleGaussian(), SampleGaussian()) * s.NoiseAmplitude;
                decision.SteerLocal.x = Mathf.Clamp(decision.SteerLocal.x, -1f, 1f);
                decision.SteerLocal.y = Mathf.Clamp(decision.SteerLocal.y, -1f, 1f);
                decision.Roll = Mathf.Clamp(decision.Roll + SampleGaussian() * s.NoiseAmplitude, -1f, 1f);
            }

            // 5) Throttle scaling — slower difficulty levels move slower so players can keep up.
            decision.Throttle = Mathf.Clamp01(decision.Throttle * s.ThrottleScale);

            // 6) Ability skip — drop one start request if rolled. Stops are never dropped to
            //    avoid leaving abilities stuck on.
            if (s.AbilitySkipChance > 0f
                && decision.RequestActionsStart != null
                && decision.RequestActionsStart.Count > 0
                && UnityEngine.Random.value < s.AbilitySkipChance)
            {
                decision.RequestActionsStart.RemoveAt(decision.RequestActionsStart.Count - 1);
            }

            _lastApplied = decision;
            _hasLastApplied = true;
            return decision;
        }

        static float SampleGaussian()
        {
            float u1 = Mathf.Max(UnityEngine.Random.value, 1e-6f);
            float u2 = UnityEngine.Random.value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Cos(2f * Mathf.PI * u2);
        }
    }
}
