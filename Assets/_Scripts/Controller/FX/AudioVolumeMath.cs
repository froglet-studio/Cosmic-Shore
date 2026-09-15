using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// The ONE mapping from the player's settings to the linear volume an FMOD instance is given.
    /// Pure and static so it is unit-testable and so the five emitters that used to each carry
    /// their own copy of this arithmetic cannot drift apart.
    ///
    /// <para><b>Two application modes.</b> Today every FMOD instance the code creates is given
    /// <c>slider × baseMultiplier</c> per instance (<paramref name="vcaDrivesLevel"/> = false),
    /// because the FMOD project's <c>vca:/SFX</c> and <c>vca:/Music</c> VCAs control no bus yet. Once
    /// the audio project assigns the buses to those VCAs, <see cref="CosmicShore.Core.AudioSystem"/>
    /// drives the VCAs and every per-instance caller passes <paramref name="vcaDrivesLevel"/> = true,
    /// which yields the base multiplier alone - applying the slider in both places would square it.
    /// Mute short-circuits to 0 in BOTH modes, so a muted slider creates no voice at all.</para>
    /// </summary>
    public static class AudioVolumeMath
    {
        public const float MaxBaseMultiplier = 2f;

        /// <summary>
        /// Linear volume for one instance.
        /// </summary>
        /// <param name="enabled">The channel's on/off toggle (SFXEnabled / MusicEnabled).</param>
        /// <param name="sliderLevel">The channel's 0..1 slider.</param>
        /// <param name="baseMultiplier">Per-emitter trim (0..2). 1 = no change.</param>
        /// <param name="vcaDrivesLevel">True when the slider is already applied on the FMOD VCA.</param>
        public static float InstanceVolume(bool enabled, float sliderLevel, float baseMultiplier, bool vcaDrivesLevel)
        {
            if (!enabled) return 0f;

            float trim = Mathf.Clamp(baseMultiplier, 0f, MaxBaseMultiplier);
            float slider = Mathf.Clamp01(sliderLevel);
            if (slider <= 0f) return 0f;

            return vcaDrivesLevel ? trim : Mathf.Clamp(slider * trim, 0f, MaxBaseMultiplier);
        }

        /// <summary>Linear volume for the channel's VCA: the slider itself, or 0 when the channel is off.</summary>
        public static float VcaVolume(bool enabled, float sliderLevel)
            => enabled ? Mathf.Clamp01(sliderLevel) : 0f;
    }
}
