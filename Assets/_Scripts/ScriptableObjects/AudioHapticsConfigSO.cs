using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// One transient haptic definition: an envelope (typically baked from the
    /// FMOD Studio project's source wav via Tools &gt; Cosmic Shore &gt; Audio
    /// Haptics) plus the mixing metadata the orchestrator needs to arbitrate the
    /// single haptic channel.
    /// </summary>
    [Serializable]
    public class HapticTransientSpec
    {
        [Tooltip("Multiplier on the event strength before playback. Use to keep chatty categories (block breaks, skims) gentle and big moments (mine explosions) at full punch.")]
        [Range(0f, 1f)] public float gain = 1f;

        [Tooltip("Channel arbitration priority — higher wins. There is exactly one haptic channel: a playing transient is only interrupted by an equal-or-higher priority request.")]
        [Range(0, 100)] public int priority = 50;

        [Tooltip("Minimum seconds between two haptics of this category. Guards burst categories from turning the actuator into mush.")]
        [Min(0f)] public float cooldownSeconds = 0.08f;

        [Tooltip("Extra multiplier applied ONLY when this haptic is triggered automatically by the audio one-shot hook in AudioSystem (any actor, anywhere). Explicit gameplay calls (HapticSpec / HapticController) ignore it. Set 0 for vessel-attributed feedback (impacts, pickups) that a local-player-gated effect SO already owns — otherwise every AI collision nearby would buzz the device.")]
        [Range(0f, 1f)] public float audioEventGain = 1f;

        [Tooltip("Envelope breakpoints. Time is seconds from the audio event start so the haptic tracks the sound's actual shape. Bake from source audio or author by hand.")]
        public List<HapticBreakpoint> envelope = new();

        [Tooltip("Optional editor note: which source audio file this envelope was baked from.")]
        public string bakedFrom;

        public bool HasEnvelope => envelope != null && envelope.Count > 0;

        /// <summary>
        /// Deep copy. The code defaults (AudioHapticsBakedDefaults) are memoized
        /// shared instances — anything that stores or edits a spec (the editor
        /// baker seeding the config asset) must clone rather than alias them.
        /// </summary>
        public HapticTransientSpec Clone()
        {
            var copy = new HapticTransientSpec
            {
                gain = gain,
                priority = priority,
                cooldownSeconds = cooldownSeconds,
                audioEventGain = audioEventGain,
                bakedFrom = bakedFrom,
            };
            if (envelope != null)
                copy.envelope.AddRange(envelope);
            return copy;
        }
    }

    /// <summary>
    /// Single source of truth for the audio-matched haptics system — bed
    /// tuning, per-category transient envelopes, spatial attenuation, and
    /// platform pacing all live here (per the Config Separation rule; the
    /// orchestrator MonoBehaviour holds no tunables). Loaded from
    /// Resources/AudioHapticsConfig by default; when the asset is absent the
    /// orchestrator falls back to code defaults baked from the FMOD project's
    /// source audio (AudioHapticsBakedDefaults), so the system is zero-wire.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioHapticsConfig", menuName = "ScriptableObjects/Audio/AudioHapticsConfig")]
    public class AudioHapticsConfigSO : ScriptableObject
    {
        [Serializable]
        public struct GameplayEntry
        {
            public GameplaySFXCategory category;
            public HapticTransientSpec spec;
        }

        [Serializable]
        public struct MenuEntry
        {
            public MenuAudioCategory category;
            public HapticTransientSpec spec;
        }

        public const string ResourcesPath = "AudioHapticsConfig";

        [Header("Master")]
        [Tooltip("Design-level master switch for the whole audio-matched haptics system (the user's haptics setting still gates on top of this).")]
        public bool systemEnabled = true;

        [Tooltip("Master gain on all transient haptics.")]
        [Range(0f, 1f)] public float transientMasterGain = 1f;

        [Header("Transients (event-synchronized envelopes)")]
        [Tooltip("Per gameplay-SFX-category haptic envelopes. Missing categories fall back to the baked code defaults.")]
        public List<GameplayEntry> gameplayTransients = new();

        [Tooltip("Per menu-audio-category haptic envelopes. Missing categories fall back to the baked code defaults.")]
        public List<MenuEntry> menuTransients = new();

        [Tooltip("THE hero haptic: the Squirrel skimmer's per-prism skim pulse. Each prism the skimmer passes fires one bright, snappy pulse — riding a dense trail turns them into a continuous rewarding train. Leave empty to use the designed code default.")]
        public HapticTransientSpec skimPulse = new();

        [Header("Transient arbitration (one haptic channel)")]
        [Tooltip("Requests weaker than this are dropped — below the actuator sensation threshold.")]
        [Range(0f, 0.2f)] public float transientMinStrength = 0.02f;

        [Tooltip("Global floor between any two transient starts, so same-frame event bursts don't thrash the haptic clip loader.")]
        [Range(0f, 0.2f)] public float transientGlobalMinInterval = 0.03f;

        [Tooltip("An equal-priority request must be at least this fraction of the playing transient's strength to steal the channel.")]
        [Range(0f, 1f)] public float transientEqualPriorityStealFactor = 0.8f;

        [Tooltip("When a playing transient has less than this left, any admissible request may take over.")]
        [Range(0f, 0.3f)] public float transientTailSeconds = 0.06f;

        [Header("Continuous bed (follows the measured SFX bus signal)")]
        [Tooltip("Drive a continuous haptic layer from real-time FMOD bus metering (engine hum, drift, boost swells, one-shot tails). OFF by default after playtest: a bed that follows the engine hum vibrates whenever the vessel moves — haptics that never stop numb the hand and mask the discrete pulses that matter. Enable only for experiments.")]
        public bool bedEnabled = false;

        [Tooltip("FMOD bus whose output the bed follows. Empty = AudioSystem's SFX bus path (default \"bus:/\"). Music runs on the legacy Unity path, so it never contaminates this signal.")]
        public string bedBusPathOverride = "";

        [Tooltip("Bus RMS treated as full-scale input (linear). Game SFX mixes rarely sustain above ~0.25 RMS; lower = hotter bed.")]
        [Range(0.01f, 1f)] public float bedInputReference = 0.25f;

        [Tooltip("Attack/release, noise gate and perceptual curve of the bed level.")]
        public HapticFollowerSettings bedFollower = HapticFollowerSettings.Default;

        [Tooltip("Also derive the bed's haptic FREQUENCY from the bus spectral centroid (one FMOD FFT DSP on the bus). Bassy rumble feels low and soft; bright zaps feel sharp. iOS modulates true frequency; gamepads shift the low/high motor balance on the next pattern.")]
        public bool bedTrackSpectralCentroid = true;

        [Tooltip("FFT window size for the spectral centroid DSP (power of two).")]
        public int bedFftWindowSize = 512;

        [Tooltip("Spectral centroid mapped to haptic frequency 0.")]
        public float bedMinFrequencyHz = 65f;

        [Tooltip("Spectral centroid mapped to haptic frequency 1.")]
        public float bedMaxFrequencyHz = 3500f;

        [Tooltip("Max change of the bed's 0..1 haptic frequency per second (slew limiting so the centroid jumping between mix moments doesn't buzz).")]
        [Range(0.1f, 20f)] public float bedFrequencySlewPerSecond = 3f;

        [Tooltip("After this long below the gate the bed playback is fully stopped (battery + leaves the channel clean). It restarts automatically when signal returns.")]
        [Min(0.1f)] public float bedIdleStopSeconds = 1f;

        [Tooltip("Length of the looping constant clip the bed modulates on platforms with real-time amplitude modulation (iOS, gamepads).")]
        [Min(0.1f)] public float bedLoopClipSeconds = 1f;

        [Header("Continuous bed - Android pacing")]
        [Tooltip("Android has no real-time amplitude modulation, so the bed re-issues a short constant clip at this cadence with the current level applied (each Play() picks up the new level).")]
        [Range(0.05f, 0.5f)] public float androidChunkIntervalSeconds = 0.12f;

        [Tooltip("Length of the Android bed chunk clip. Longer than the cadence so consecutive chunks overlap into a continuous vibration.")]
        [Range(0.05f, 1f)] public float androidChunkClipSeconds = 0.25f;

        [Header("Spatial attenuation (world-positioned events)")]
        [Tooltip("Events at or closer than this distance from the listener play at full haptic strength.")]
        [Min(0f)] public float spatialFullStrengthDistance = 30f;

        [Tooltip("Events beyond this distance produce no haptic. Falloff between the two distances is quadratic, mirroring audio attenuation.")]
        [Min(1f)] public float spatialCutoffDistance = 200f;

        [Header("Legacy AudioClip path")]
        [Tooltip("Analyze AudioClips played through the legacy PlaySFXClip path at runtime (waveform envelope, cached per clip) so even unmigrated sounds get matched haptics. OFF by default after playtest — UI stingers don't earn a buzz (haptics are sparse signals, not a soundtrack).")]
        public bool analyzeLegacyClips = false;

        [Tooltip("Envelope analysis settings used for legacy AudioClips (window, budget, transient detection). Fixed at code defaults; exposed here only for the master duration cap.")]
        [Min(0.2f)] public float legacyClipMaxSeconds = 1.5f;

        [Tooltip("Gain on legacy-AudioClip haptics (no per-category authoring exists for arbitrary clips).")]
        [Range(0f, 1f)] public float legacyClipGain = 0.6f;

        [Tooltip("Arbitration priority for legacy-AudioClip haptics.")]
        [Range(0, 100)] public int legacyClipPriority = 45;

        [Tooltip("Minimum seconds between two haptics of the same legacy AudioClip.")]
        [Min(0f)] public float legacyClipCooldown = 0.1f;

        HapticTransientArbiter.Settings _cachedArbiterSettings;
        bool _arbiterSettingsCached;

        public HapticTransientArbiter.Settings ArbiterSettings
        {
            get
            {
                if (!_arbiterSettingsCached)
                {
                    _cachedArbiterSettings = new HapticTransientArbiter.Settings
                    {
                        MinStrength = transientMinStrength,
                        GlobalMinIntervalSeconds = transientGlobalMinInterval,
                        EqualPriorityStealFactor = transientEqualPriorityStealFactor,
                        TailSeconds = transientTailSeconds,
                    };
                    _arbiterSettingsCached = true;
                }
                return _cachedArbiterSettings;
            }
        }

        void OnValidate()
        {
            _arbiterSettingsCached = false;
            bedFftWindowSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(bedFftWindowSize, 128, 4096));
            spatialCutoffDistance = Mathf.Max(spatialCutoffDistance, spatialFullStrengthDistance + 1f);
            // Chunks must outlive the cadence or the Android bed stutters with gaps.
            androidChunkClipSeconds = Mathf.Max(androidChunkClipSeconds, androidChunkIntervalSeconds + 0.05f);
        }

        /// <summary>Finds the spec for a gameplay category; null when absent (caller falls back to baked defaults).</summary>
        public HapticTransientSpec FindGameplay(GameplaySFXCategory category)
        {
            for (int i = 0; i < gameplayTransients.Count; i++)
                if (gameplayTransients[i].category == category)
                    return gameplayTransients[i].spec;
            return null;
        }

        /// <summary>Finds the spec for a menu category; null when absent (caller falls back to baked defaults).</summary>
        public HapticTransientSpec FindMenu(MenuAudioCategory category)
        {
            for (int i = 0; i < menuTransients.Count; i++)
                if (menuTransients[i].category == category)
                    return menuTransients[i].spec;
            return null;
        }
    }
}
