using System;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Tuning for <see cref="HapticEnvelopeFollower"/>. Serializable so the
    /// values live in AudioHapticsConfigSO per the config-separation rule.
    /// </summary>
    [Serializable]
    public struct HapticFollowerSettings
    {
        [Tooltip("Input gain applied before everything else. The raw bus RMS is divided by the reference level (see AudioHapticsConfigSO.bedInputReference) upstream; this is a final trim.")]
        public float InputGain;

        [Tooltip("Time constant (seconds) for the level to rise toward a louder input. Small = punchy response to attacks.")]
        public float AttackSeconds;

        [Tooltip("Time constant (seconds) for the level to fall toward a quieter input. Larger than attack so tails feel natural instead of stuttery.")]
        public float ReleaseSeconds;

        [Tooltip("Smoothed level at which the gate opens and the bed starts vibrating.")]
        public float GateOpen;

        [Tooltip("Smoothed level below which the gate closes again. Keep below GateOpen for hysteresis so the bed doesn't flutter at the threshold.")]
        public float GateClose;

        [Tooltip("Perceptual curve on the gated level. <1 boosts quiet detail (vibration motors have a high sensation threshold).")]
        public float Gamma;

        [Tooltip("Ceiling on the output level. Keeps the bed from saturating the actuator so transients can still punch above it.")]
        public float MaxLevel;

        public static HapticFollowerSettings Default => new HapticFollowerSettings
        {
            InputGain = 1f,
            AttackSeconds = 0.035f,
            ReleaseSeconds = 0.18f,
            GateOpen = 0.04f,
            GateClose = 0.025f,
            Gamma = 0.7f,
            MaxLevel = 0.85f,
        };
    }

    /// <summary>
    /// Attack/release envelope follower with a hysteresis noise gate and a
    /// perceptual gamma — the DSP core of the continuous haptic bed. Feed it the
    /// normalized loudness of the SFX bus each frame; it returns the haptic
    /// amplitude the actuator should sit at. Pure math struct: edit-mode
    /// testable, no Unity object access (Mathf only).
    /// </summary>
    public struct HapticEnvelopeFollower
    {
        float _smoothed;
        bool _gateOpen;

        /// <summary>The smoothed pre-gamma level (0..1). Exposed for tests and debug HUDs.</summary>
        public float SmoothedLevel => _smoothed;

        /// <summary>Whether the noise gate is currently open.</summary>
        public bool GateOpen => _gateOpen;

        /// <summary>
        /// Advances the follower by <paramref name="deltaSeconds"/> with the
        /// given normalized input level, returning the shaped output level
        /// (0..MaxLevel; exactly 0 while the gate is closed).
        /// </summary>
        public float Step(float input, float deltaSeconds, in HapticFollowerSettings settings)
        {
            input = Mathf.Clamp01(input * Mathf.Max(0f, settings.InputGain));
            deltaSeconds = Mathf.Max(0f, deltaSeconds);

            float tau = input > _smoothed ? settings.AttackSeconds : settings.ReleaseSeconds;
            float coefficient = tau <= 0f ? 1f : 1f - Mathf.Exp(-deltaSeconds / tau);
            _smoothed += (input - _smoothed) * coefficient;

            if (_gateOpen)
            {
                if (_smoothed < settings.GateClose) _gateOpen = false;
            }
            else
            {
                if (_smoothed >= settings.GateOpen) _gateOpen = true;
            }

            if (!_gateOpen) return 0f;

            float shaped = Mathf.Pow(Mathf.Clamp01(_smoothed), Mathf.Max(0.01f, settings.Gamma));
            return Mathf.Min(shaped, Mathf.Clamp01(settings.MaxLevel));
        }

        /// <summary>Hard reset (e.g. on scene transitions or when haptics are toggled off).</summary>
        public void Reset()
        {
            _smoothed = 0f;
            _gateOpen = false;
        }

        /// <summary>
        /// Rate-limited approach of <paramref name="current"/> toward
        /// <paramref name="target"/> — used to slew the bed's haptic frequency so
        /// the spectral centroid jumping between mix moments doesn't buzz.
        /// </summary>
        public static float SlewTowards(float current, float target, float maxDeltaPerSecond, float deltaSeconds)
        {
            float maxStep = Mathf.Max(0f, maxDeltaPerSecond) * Mathf.Max(0f, deltaSeconds);
            return Mathf.MoveTowards(current, target, maxStep);
        }

        /// <summary>Log-scale map of a frequency in Hz into the 0..1 haptic frequency range.</summary>
        public static float NormalizeFrequency(float hz, float minHz, float maxHz)
        {
            if (hz <= 0f) return 0.5f;
            minHz = Mathf.Max(1f, minHz);
            maxHz = Mathf.Max(minHz + 1f, maxHz);
            float logMin = Mathf.Log(minHz);
            float logMax = Mathf.Log(maxHz);
            return Mathf.Clamp01((Mathf.Log(Mathf.Max(1f, hz)) - logMin) / (logMax - logMin));
        }
    }
}
