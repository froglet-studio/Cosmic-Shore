using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Lofelt.NiceVibrations;
using Unity.Profiling;
using UnityEngine;
using LofeltController = Lofelt.NiceVibrations.HapticController;

namespace CosmicShore.Core
{
    /// <summary>
    /// The single owner of the device's haptic channel, translating the game's
    /// audio into touch. Two cooperating layers:
    ///
    ///  1. TRANSIENTS — event-synchronized haptic clips whose envelopes were
    ///     measured from each sound's actual source waveform
    ///     (AudioHapticsBakedDefaults / AudioHapticsConfigSO). Fired by
    ///     AudioSystem at the same call that starts the FMOD one-shot, scaled by
    ///     the same category gain and by listener distance for spatialized
    ///     events.
    ///
    ///  2. CONTINUOUS BED — an envelope follower on real-time FMOD bus metering
    ///     (RMS + spectral centroid via FmodSfxBusMeter). Whatever the SFX mix
    ///     is doing — engine hum rising with speed, drift layers, boost swells,
    ///     one-shot tails — the actuator follows it, with zero per-emitter
    ///     wiring. On iOS and gamepads the bed modulates a looping clip in real
    ///     time (clipLevel / clipFrequencyShift); Android has no realtime
    ///     modulation, so the bed re-issues a short constant clip at a fixed
    ///     cadence with the level applied per Play().
    ///
    /// Because the Lofelt runtime holds exactly ONE clip (every Load() evicts
    /// the previous one), HapticTransientArbiter decides which transient may
    /// take the channel, and the bed automatically yields while a transient
    /// plays, resuming afterwards to carry the sound's measured tail.
    ///
    /// Created by AudioSystem on its own persistent GameObject — zero scene
    /// wiring. All tunables live in AudioHapticsConfigSO; user gating comes
    /// from GameSetting.HapticsEnabled / HapticsLevel (level maps to Lofelt
    /// outputLevel, the global haptic volume knob).
    /// </summary>
    public class AudioHapticsOrchestrator : MonoBehaviour
    {
        public static AudioHapticsOrchestrator Instance { get; private set; }

        static readonly ProfilerMarker s_updateMarker = new ProfilerMarker("CosmicShore.AudioHaptics.Update");

        sealed class CompiledPattern
        {
            public byte[] Json;
            public GamepadRumble Rumble;
            public float Duration;
        }

        enum BedMode
        {
            None,       // no output device that can carry a continuous layer
            Modulated,  // iOS / gamepad: looping clip + realtime clipLevel writes
            Chunked,    // Android: re-Play a short constant clip at a fixed cadence
        }

        // Category-key domains for the arbiter (one flat int space).
        const int MenuKeyOffset = 1000;
        const int SkimPulseKey = 3000;
        const int LegacyClipKeyOffset = unchecked((int)0x80000000);


        AudioHapticsConfigSO _config;
        GameSetting _gameSetting;

        readonly HapticTransientArbiter _arbiter = new();
        readonly Dictionary<HapticTransientSpec, CompiledPattern> _compiledSpecs = new();
        readonly Dictionary<AudioClip, CompiledPattern> _legacyClipPatterns = new();

        FmodSfxBusMeter _meter;
        HapticEnvelopeFollower _follower;

        bool _hapticsEnabled = true;
        float _hapticsLevel = 1f;
        bool _lofeltInitialized;
        bool _deviceMeetsAdvanced;

        // Bed state
        BedMode _lastBedMode = BedMode.None;
        CompiledPattern _bedLoopPattern;
        CompiledPattern _bedChunkPattern;
        bool _bedClipLoaded;
        bool _bedPlaying;
        float _bedFrequency = 0.5f;
        float _bedSilenceSince = float.NegativeInfinity;
        float _bedRestartDue = float.PositiveInfinity;
        float _chunkDue;

        // External continuous drive (PlayConstant compatibility — skim scaling,
        // elemental debuffs). Multiple slots so a strong per-frame driver (skim
        // proximity) can't starve a weaker concurrent pulse (debuff buzz).
        struct Pulse
        {
            public float Level;
            public float Frequency;
            public float Until;
        }

        readonly Pulse[] _pulses = new Pulse[4];

        // Rate limiter for the no-bed-mode pulse fallback (old devices without
        // clip playback still get their legacy constant vibration).
        const float FallbackPulseInterval = 0.1f;
        float _fallbackPulseDue;

        Transform _listener;
        float _nextListenerSearchTime;

        static float Now => Time.unscaledTime;

        /// <summary>User + config gate. When false, no haptic work happens at all.</summary>
        public bool HapticsActive =>
            _config != null && _config.systemEnabled && _hapticsEnabled && _hapticsLevel > 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StopAllHaptics();
            _meter?.Dispose();
            _meter = null;
        }

        void OnEnable()
        {
            GameSetting.OnChangeHapticsEnabledStatus += HandleHapticsEnabledChanged;
            GameSetting.OnChangeHapticsLevel += HandleHapticsLevelChanged;
        }

        void OnDisable()
        {
            GameSetting.OnChangeHapticsEnabledStatus -= HandleHapticsEnabledChanged;
            GameSetting.OnChangeHapticsLevel -= HandleHapticsLevelChanged;
            StopAllHaptics();
        }

        /// <summary>
        /// Called by AudioSystem right after creation. Config may be null — the
        /// baked code defaults then cover every category via a runtime instance.
        /// </summary>
        public void Configure(AudioHapticsConfigSO config, GameSetting gameSetting)
        {
            _config = config != null ? config : Resources.Load<AudioHapticsConfigSO>(AudioHapticsConfigSO.ResourcesPath);
            if (_config == null)
                _config = ScriptableObject.CreateInstance<AudioHapticsConfigSO>();

            _gameSetting = gameSetting;
            if (_gameSetting != null)
            {
                _hapticsEnabled = _gameSetting.HapticsEnabled;
                _hapticsLevel = _gameSetting.HapticsLevel;
            }

            EnsureLofeltInitialized();
            ApplyOutputLevel();
        }

        void EnsureLofeltInitialized()
        {
            if (_lofeltInitialized) return;
            _lofeltInitialized = true;
            _deviceMeetsAdvanced = LofeltController.Init();
        }

        void ApplyOutputLevel() => LofeltController.outputLevel = Mathf.Clamp01(_hapticsLevel);

        void HandleHapticsEnabledChanged(bool enabledStatus)
        {
            _hapticsEnabled = enabledStatus;
            if (!enabledStatus) StopAllHaptics();
        }

        void HandleHapticsLevelChanged(float level)
        {
            _hapticsLevel = level;
            ApplyOutputLevel();
            if (level <= 0f) StopAllHaptics();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            LofeltController.ProcessApplicationFocus(hasFocus);
            if (!hasFocus) ResetChannelState();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                LofeltController.Stop();
                ResetChannelState();
            }
        }

        // ------------------------------------------------------------------
        // Public API — transients
        // ------------------------------------------------------------------

        /// <summary>Plays the audio-matched haptic for a gameplay SFX category (2D — full strength). For explicit, gameplay-attributed calls (HapticSpec / HapticController).</summary>
        public void PlayGameplayTransient(GameplaySFXCategory category, float gain = 1f)
            => PlayTransient(ResolveGameplaySpec(category), (int)category, gain, null);

        /// <summary>Plays the audio-matched haptic for a spatialized gameplay SFX, attenuated by listener distance like the sound itself.</summary>
        public void PlayGameplayTransient(GameplaySFXCategory category, Vector3 worldPosition, float gain = 1f)
            => PlayTransient(ResolveGameplaySpec(category), (int)category, gain, worldPosition);

        /// <summary>
        /// Entry point for AudioSystem's automatic one-shot hook. Applies the
        /// spec's audioEventGain on top: categories owned by a local-player-gated
        /// effect SO set it to 0 so an AI crashing into a prism next to you
        /// doesn't buzz your device just because you heard it.
        /// </summary>
        public void PlayGameplayFromAudioEvent(GameplaySFXCategory category, float gain, Vector3? worldPosition)
        {
            var spec = ResolveGameplaySpec(category);
            if (spec == null || spec.audioEventGain <= 0f) return;
            PlayTransient(spec, (int)category, gain * spec.audioEventGain, worldPosition);
        }

        /// <summary>Plays the audio-matched haptic for a menu/UI audio category.</summary>
        public void PlayMenuTransient(MenuAudioCategory category, float gain = 1f)
            => PlayTransient(ResolveMenuSpec(category), MenuKeyOffset + (int)category, gain, null);

        /// <summary>
        /// THE hero haptic: one bright per-prism skim pulse. Fired by the
        /// skimmer's prism-enter effects; riding a dense trail chains pulses
        /// into a continuous rewarding train (the spec's 30 ms cooldown is the
        /// train's max rate). <paramref name="strength01"/> scales the pulse —
        /// proximity-aware callers pass how deep in the sweet spot the prism is.
        /// </summary>
        public void PlaySkimPulse(float strength01 = 1f)
        {
            var spec = _config != null && _config.skimPulse != null && _config.skimPulse.HasEnvelope
                ? _config.skimPulse
                : AudioHapticsBakedDefaults.SkimPulse();
            PlayTransient(spec, SkimPulseKey, strength01, null);
        }

        /// <summary>
        /// Runtime-analyzed haptic for the legacy Unity AudioClip path: the
        /// clip's waveform is converted to a haptic envelope on first play and
        /// cached, so unmigrated sounds still get matched haptics.
        /// </summary>
        public void PlayLegacyClip(AudioClip clip, float gain = 1f)
        {
            if (clip == null || _config == null || !_config.analyzeLegacyClips || !HapticsActive)
                return;

            if (!_legacyClipPatterns.TryGetValue(clip, out var pattern))
            {
                // Still streaming in (Load In Background)? Retry on a later play
                // instead of permanently caching a failed analysis.
                if (clip.loadState == AudioDataLoadState.Loading)
                    return;

                pattern = AnalyzeLegacyClip(clip);
                _legacyClipPatterns[clip] = pattern; // null cached too — structurally ineligible clips are never retried
            }
            if (pattern == null) return;

            var request = new HapticTransientArbiter.Request
            {
                CategoryKey = LegacyClipKeyOffset | (clip.GetInstanceID() & 0x7FFFFFFF),
                Priority = _config.legacyClipPriority,
                Strength = Mathf.Clamp01(gain * _config.legacyClipGain * _config.transientMasterGain),
                Duration = pattern.Duration,
                CooldownSeconds = _config.legacyClipCooldown,
            };
            AdmitAndPlay(request, pattern);
        }

        /// <summary>
        /// Continuous external drive mixed into the bed (compatibility surface
        /// for the old PlayConstant API — skimmer proximity scaling, elemental
        /// debuff buzzes). Unlike the old path this does NOT thrash the channel
        /// with per-frame clip loads; it simply lifts the bed level. Runs even
        /// when the audio-following bed layer is disabled in config.
        /// </summary>
        public void PulseContinuous(float amplitude, float frequency, float duration)
        {
            if (!HapticsActive) return;

            amplitude = Mathf.Clamp01(amplitude);
            frequency = Mathf.Clamp01(frequency);

            // Android's chunked bed only samples pulses at chunk ticks — a pulse
            // shorter than the cadence could expire unheard between ticks.
            float minDuration = ResolveBedMode() == BedMode.Chunked
                ? _config.androidChunkIntervalSeconds + 0.02f
                : 0f;
            float until = Now + Mathf.Max(minDuration, Mathf.Max(0f, duration));

            // Prefer an expired slot; otherwise replace the weakest active slot
            // this pulse outranks. Weaker than all four actives → dropped.
            int slot = -1;
            int weakestIndex = 0;
            for (int i = 0; i < _pulses.Length; i++)
            {
                if (Now >= _pulses[i].Until)
                {
                    slot = i;
                    break;
                }
                if (_pulses[i].Level < _pulses[weakestIndex].Level)
                    weakestIndex = i;
            }
            if (slot < 0 && _pulses[weakestIndex].Level < amplitude)
                slot = weakestIndex;
            if (slot < 0) return;

            _pulses[slot] = new Pulse { Level = amplitude, Frequency = frequency, Until = until };
        }

        // ------------------------------------------------------------------
        // Spec resolution + compilation
        // ------------------------------------------------------------------

        HapticTransientSpec ResolveGameplaySpec(GameplaySFXCategory category)
        {
            var spec = _config != null ? _config.FindGameplay(category) : null;
            return spec != null && spec.HasEnvelope ? spec : AudioHapticsBakedDefaults.ForGameplay(category);
        }

        HapticTransientSpec ResolveMenuSpec(MenuAudioCategory category)
        {
            var spec = _config != null ? _config.FindMenu(category) : null;
            return spec != null && spec.HasEnvelope ? spec : AudioHapticsBakedDefaults.ForMenu(category);
        }

        CompiledPattern GetCompiled(HapticTransientSpec spec)
        {
            if (spec == null || !spec.HasEnvelope) return null;
            if (_compiledSpecs.TryGetValue(spec, out var compiled)) return compiled;

            // Duration must reflect what actually plays: sanitize can pad a
            // degenerate envelope and the rumble rounds up to whole steps —
            // claiming the channel for less would let the bed evict a tail.
            var sanitized = HapticPatternBuilder.Sanitize(spec.envelope);
            // Short clips (skim pulse, punish thud) need finer rumble steps than
            // the default 50 ms or the gamepad renders them as a single mushy cell.
            int rumbleStepMs = HapticPatternBuilder.Duration(sanitized) < 0.25f ? 16 : HapticPatternBuilder.RumbleStepMs;
            var rumble = HapticPatternBuilder.RenderRumble(sanitized, rumbleStepMs);
            compiled = new CompiledPattern
            {
                Json = HapticPatternBuilder.RenderJsonBytes(sanitized),
                Rumble = rumble,
                Duration = Mathf.Max(HapticPatternBuilder.Duration(sanitized), rumble.totalDurationMs / 1000f),
            };
            _compiledSpecs[spec] = compiled;
            return compiled;
        }

        CompiledPattern AnalyzeLegacyClip(AudioClip clip)
        {
            // Streaming / compressed-in-memory clips can't surrender samples.
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.samples <= 0)
                return null;

            // One-time per clip; legacy-path clips (UI stingers, countdown) are short.
            var interleaved = new float[clip.samples * clip.channels];
            if (!clip.GetData(interleaved, 0))
                return null;

            var settings = HapticWaveformAnalyzer.Settings.Default;
            settings.MaxDurationSeconds = _config.legacyClipMaxSeconds;
            var envelope = HapticWaveformAnalyzer.Analyze(
                HapticWaveformAnalyzer.InterleavedToMono(interleaved, clip.channels),
                clip.frequency, settings);
            if (envelope.Count == 0) return null;

            return new CompiledPattern
            {
                Json = HapticPatternBuilder.RenderJsonBytes(envelope),
                Rumble = HapticPatternBuilder.RenderRumble(envelope),
                Duration = HapticPatternBuilder.Duration(envelope),
            };
        }

        // ------------------------------------------------------------------
        // Transient playback
        // ------------------------------------------------------------------

        void PlayTransient(HapticTransientSpec spec, int categoryKey, float gain, Vector3? worldPosition)
        {
            if (!HapticsActive || spec == null) return;

            float strength = Mathf.Clamp01(gain) * spec.gain * _config.transientMasterGain;
            if (worldPosition.HasValue)
                strength *= SpatialAttenuation(worldPosition.Value);
            if (strength <= 0f) return;

            var compiled = GetCompiled(spec);
            if (compiled == null) return;

            var request = new HapticTransientArbiter.Request
            {
                CategoryKey = categoryKey,
                Priority = spec.priority,
                Strength = strength,
                Duration = compiled.Duration,
                CooldownSeconds = spec.cooldownSeconds,
            };
            AdmitAndPlay(request, compiled);
        }

        void AdmitAndPlay(in HapticTransientArbiter.Request request, CompiledPattern pattern)
        {
            EnsureLofeltInitialized();

            if (!_arbiter.TryAdmit(request, Now, _config.ArbiterSettings))
                return;

            // The Load below evicts whatever the bed had on the channel.
            _bedClipLoaded = false;
            _bedPlaying = false;
            _bedRestartDue = float.PositiveInfinity;

            // Old Androids / old iPhones can't play clips — degrade to a preset that matches the punch.
            LofeltController.fallbackPreset = FallbackPresetFor(request.Strength);
            LofeltController.Load(pattern.Json, pattern.Rumble);
            LofeltController.Loop(false);
            LofeltController.clipLevel = request.Strength;
            LofeltController.Play();
        }

        static HapticPatterns.PresetType FallbackPresetFor(float strength)
        {
            if (strength > 0.66f) return HapticPatterns.PresetType.HeavyImpact;
            if (strength > 0.33f) return HapticPatterns.PresetType.MediumImpact;
            return HapticPatterns.PresetType.LightImpact;
        }

        float SpatialAttenuation(Vector3 worldPosition)
        {
            var listener = ResolveListener();
            if (listener == null) return 1f;

            float distance = Vector3.Distance(listener.position, worldPosition);
            float full = _config.spatialFullStrengthDistance;
            float cutoff = _config.spatialCutoffDistance;
            if (distance <= full) return 1f;
            if (distance >= cutoff) return 0f;

            float t = 1f - (distance - full) / (cutoff - full);
            return t * t; // quadratic falloff, mirroring audio attenuation
        }

        Transform ResolveListener()
        {
            if (_listener != null) return _listener;
            if (Now < _nextListenerSearchTime) return null;
            _nextListenerSearchTime = Now + 1f;

            // The FMOD StudioListener rides the active camera; Camera.main is
            // the cheap, always-present proxy for "where the player hears from".
            var mainCamera = Camera.main;
            if (mainCamera != null) _listener = mainCamera.transform;
            return _listener;
        }

        // ------------------------------------------------------------------
        // Continuous bed
        // ------------------------------------------------------------------

        void Update()
        {
            using (s_updateMarker.Auto())
            {
                if (!HapticsActive)
                {
                    if (_bedPlaying) StopBed();
                    return;
                }

                UpdateContinuous(Time.unscaledDeltaTime);
            }
        }

        BedMode ResolveBedMode()
        {
            if (GamepadRumbler.IsConnected())
                return BedMode.Modulated;
            if (_deviceMeetsAdvanced)
                return DeviceCapabilities.hasAmplitudeModulation ? BedMode.Modulated : BedMode.Chunked;
            return BedMode.None;
        }

        void UpdateContinuous(float deltaSeconds)
        {
            var mode = ResolveBedMode();
            if (mode != _lastBedMode)
            {
                // Output route changed (gamepad connected/disconnected): drop the
                // playing bed so the new mode loads its own clip shape.
                // (_bedPlaying == true implies the bed owns the channel, so this
                // can never stop a transient.)
                if (_bedPlaying) StopBed();
                _lastBedMode = mode;
            }

            // --- Level: measured bus loudness through the follower (only when the
            // audio-following layer is enabled), external pulses mixed on top.
            // Pulses actuate regardless of bedEnabled — PlayConstant callers
            // (skim scaling, debuff buzz) must not go dark when a designer turns
            // off the bus-following layer. ---
            float meterNorm = 0f;
            float centroidHz = 0f;
            if (_config.bedEnabled && mode != BedMode.None)
            {
                EnsureMeter();
                if (_meter != null && _meter.TryRead(out float rms, out centroidHz))
                    meterNorm = rms / Mathf.Max(0.01f, _config.bedInputReference);
            }

            float followerLevel = _follower.Step(meterNorm, deltaSeconds, _config.bedFollower);

            float pulseLevel = 0f;
            float pulseFrequency = 0.5f;
            for (int i = 0; i < _pulses.Length; i++)
            {
                if (Now >= _pulses[i].Until || _pulses[i].Level <= pulseLevel) continue;
                pulseLevel = _pulses[i].Level;
                pulseFrequency = _pulses[i].Frequency;
            }

            float level = Mathf.Max(followerLevel, pulseLevel);

            // --- Frequency: spectral centroid (slewed), or the pulse's explicit frequency ---
            float frequencyTarget = 0.5f;
            if (pulseLevel > 0f && pulseLevel >= followerLevel)
                frequencyTarget = pulseFrequency;
            else if (_config.bedTrackSpectralCentroid && centroidHz > 0f)
                frequencyTarget = HapticEnvelopeFollower.NormalizeFrequency(centroidHz, _config.bedMinFrequencyHz, _config.bedMaxFrequencyHz);
            _bedFrequency = HapticEnvelopeFollower.SlewTowards(_bedFrequency, frequencyTarget, _config.bedFrequencySlewPerSecond, deltaSeconds);

            // --- A playing transient owns the channel; touching clipLevel would modulate IT. ---
            if (_arbiter.IsPlaying(Now))
            {
                _bedSilenceSince = Now; // restart silence bookkeeping when we get the channel back
                return;
            }

            if (mode == BedMode.None)
            {
                // No clip playback machinery (old devices, editor without a
                // gamepad). Pulses still deserve output: the legacy constant
                // pattern, rate-limited so per-frame callers can't spam it.
                if (pulseLevel > 0f && Now >= _fallbackPulseDue)
                {
                    _fallbackPulseDue = Now + FallbackPulseInterval;
                    HapticPatterns.PlayConstant(pulseLevel, pulseFrequency, FallbackPulseInterval + 0.02f);
                }
                return;
            }

            if (level <= 0f)
            {
                if (_bedPlaying)
                {
                    // Silence the still-looping clip immediately (it would otherwise
                    // ring at its last level), then hard-stop once the silence holds.
                    if (mode == BedMode.Modulated)
                        LofeltController.clipLevel = 0f;
                    if (Now - _bedSilenceSince >= _config.bedIdleStopSeconds)
                        StopBed();
                }
                return;
            }
            _bedSilenceSince = Now;

            switch (mode)
            {
                case BedMode.Modulated:
                    DriveModulatedBed(level);
                    break;
                case BedMode.Chunked:
                    DriveChunkedBed(level);
                    break;
            }
        }

        void EnsureMeter()
        {
            if (_meter != null || _config == null) return;

            string busPath = _config.bedBusPathOverride;
            if (string.IsNullOrEmpty(busPath) && AudioSystem.Instance != null)
                busPath = AudioSystem.Instance.SfxBusPath;
            _meter = new FmodSfxBusMeter(busPath, _config.bedTrackSpectralCentroid, _config.bedFftWindowSize);
        }

        void DriveModulatedBed(float level)
        {
            if (!_bedPlaying)
            {
                _bedLoopPattern ??= CompileConstant(_config.bedLoopClipSeconds);
                LofeltController.Load(_bedLoopPattern.Json, _bedLoopPattern.Rumble);
                LofeltController.Loop(true);
                LofeltController.clipLevel = level;
                LofeltController.clipFrequencyShift = _bedFrequency - 0.5f;
                LofeltController.Play();
                _bedClipLoaded = true;
                _bedPlaying = true;

                // Gamepads (and any platform that can't loop) need periodic
                // re-Play; iOS loops natively so the restart never comes due.
                _bedRestartDue = DeviceCapabilities.canLoop
                    ? float.PositiveInfinity
                    : Now + _bedLoopPattern.Duration * 0.9f;
                return;
            }

            LofeltController.clipLevel = level;
            LofeltController.clipFrequencyShift = _bedFrequency - 0.5f;

            if (Now >= _bedRestartDue)
            {
                LofeltController.Play();
                _bedRestartDue += _bedLoopPattern.Duration * 0.9f; // re-arm from due time so hitches don't de-phase the loop
                if (_bedRestartDue < Now) _bedRestartDue = Now + _bedLoopPattern.Duration * 0.9f;
            }
        }

        void DriveChunkedBed(float level)
        {
            if (Now < _chunkDue) return;
            _chunkDue += _config.androidChunkIntervalSeconds;
            if (_chunkDue < Now) _chunkDue = Now + _config.androidChunkIntervalSeconds;

            if (!_bedClipLoaded)
            {
                _bedChunkPattern ??= CompileConstant(_config.androidChunkClipSeconds);
                LofeltController.Load(_bedChunkPattern.Json, _bedChunkPattern.Rumble);
                LofeltController.Loop(false);
                _bedClipLoaded = true;
            }

            // Android applies clipLevel on the next Play() — which is exactly what follows.
            LofeltController.clipLevel = level;
            LofeltController.Play();
            _bedPlaying = true;
        }

        static CompiledPattern CompileConstant(float duration)
        {
            var envelope = HapticPatternBuilder.BuildConstantBed(duration);
            return new CompiledPattern
            {
                Json = HapticPatternBuilder.RenderJsonBytes(envelope),
                Rumble = HapticPatternBuilder.RenderRumble(envelope, 16),
                Duration = HapticPatternBuilder.Duration(envelope),
            };
        }

        void StopBed()
        {
            LofeltController.Stop();
            _bedPlaying = false;
            _bedClipLoaded = false;
            _bedRestartDue = float.PositiveInfinity;
        }

        void ResetChannelState()
        {
            _arbiter.NotifyStopped();
            _bedPlaying = false;
            _bedClipLoaded = false;
            _bedRestartDue = float.PositiveInfinity;
            _follower.Reset();
            for (int i = 0; i < _pulses.Length; i++)
                _pulses[i] = default;
        }

        void StopAllHaptics()
        {
            if (_lofeltInitialized)
                LofeltController.Stop();
            ResetChannelState();
        }
    }
}
