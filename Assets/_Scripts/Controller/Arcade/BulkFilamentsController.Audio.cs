using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public partial class BulkFilamentsController
    {
        const string GrappleFireResourcePath = "Audio/BulkFilaments/BulkGrappleFire";
        const string LatchSurgeResourcePath = "Audio/BulkFilaments/BulkLatchSurge";
        const string PowerCrystalResourcePath = "Audio/BulkFilaments/BulkPowerCrystal";

        [Header("Bulk Audio")]
        [SerializeField, Range(0f, 1f)] float bulkMusicMaxVolume = 1f;

        AudioSource _sfxSource;
        AudioClip _grappleFireClip;
        AudioClip _latchSurgeClip;
        AudioClip _latchMissClip;
        AudioClip _powerCrystalClip;
        AudioClip _nanitePopClip;
        AudioClip _pulseGateClip;
        readonly List<AudioSourceSnapshot> _bulkAudioSnapshots = new();
        bool _audioStartupLogged;
        bool _bulkMixApplied;
        bool _bulkMusicSettingsSubscribed;
        float _nextBulkMixEnforceTime;

        void StartMusic()
        {
            EnsureBulkAudioSources();
            if (!_musicSource)
                return;

            if (!_musicSource.clip)
                _musicSource.clip = Resources.Load<AudioClip>(MusicResourcePath);

            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.pitch = 1f;
            _musicSource.spatialBlend = 0f;
            _musicSource.ignoreListenerPause = true;
            _musicSource.mute = false;
            _musicSource.outputAudioMixerGroup = null;

            AudioListener.pause = false;
            EnsureSingleAudioListener();
            SubscribeBulkMusicSettings();
            ApplyBulkMusicVolume();
            ApplyBulkAudioMix();
            ApplyBulkMusicVolume();

            if (_musicSource.clip)
            {
                if (!_musicSource.isPlaying)
                    _musicSource.Play();
                CSDebug.Log($"[BulkFilaments] Music playing: {_musicSource.clip.name}, length={_musicSource.clip.length:0.0}s, volume={_musicSource.volume:0.00}.");
            }
            else
            {
                CSDebug.LogWarning($"[BulkFilaments] Missing music resource at Resources/{MusicResourcePath}.");
            }
        }

        void PlayLatchFireSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_grappleFireClip, 1.2f, "grapple_fire");
        }

        void PlayLatchLockSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_latchSurgeClip, 0.78f, "front_lock");
        }

        void PlayLatchSurgeSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_latchSurgeClip, 1.15f, "transfer_surge");
        }

        void PlayLatchMissSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_latchMissClip, 0.8f, "latch_miss");
        }

        void PlayPowerCrystalSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_powerCrystalClip, 1.05f, "power_crystal");
        }

        void PlayNanitePopSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_nanitePopClip, 0.74f, "nanite_pop");
        }

        void PlayPulseGateSound()
        {
            EnsureBulkAudioSources();
            PlayOneShotClip(_pulseGateClip, 1.05f, "pulse_gate");
        }

        void PlayOneShotClip(AudioClip clip, float volumeScale, string eventName)
        {
            if (!_sfxSource || !clip)
            {
                CSDebug.LogWarning($"[BulkFilamentsAudio] Missing SFX for {eventName}: source={_sfxSource}, clip={clip}.");
                return;
            }

            AudioListener.pause = false;
            _sfxSource.PlayOneShot(clip, volumeScale);
        }

        void SubscribeBulkMusicSettings()
        {
            if (_bulkMusicSettingsSubscribed)
                return;

            _bulkMusicSettingsSubscribed = true;
            GameSetting.OnChangeMusicEnabledStatus += OnBulkMusicEnabledChanged;
            GameSetting.OnChangeMusicLevel += OnBulkMusicLevelChanged;
        }

        void UnsubscribeBulkMusicSettings()
        {
            if (!_bulkMusicSettingsSubscribed)
                return;

            _bulkMusicSettingsSubscribed = false;
            GameSetting.OnChangeMusicEnabledStatus -= OnBulkMusicEnabledChanged;
            GameSetting.OnChangeMusicLevel -= OnBulkMusicLevelChanged;
        }

        void OnBulkMusicEnabledChanged(bool _) => ApplyBulkMusicVolume();

        void OnBulkMusicLevelChanged(float _) => ApplyBulkMusicVolume();

        void ApplyBulkMusicVolume()
        {
            if (!_musicSource)
                return;

            bool enabled = true;
            float level = 1f;
            var setting = GameSetting.Instance;
            if (setting)
            {
                enabled = setting.MusicEnabled;
                level = setting.MusicLevel;
            }
            else
            {
                enabled = PlayerPrefs.GetInt(nameof(GameSetting.PlayerPrefKeys.MusicEnabled), 1) == 1;
                level = PlayerPrefs.GetFloat(nameof(GameSetting.PlayerPrefKeys.MusicLevel), 1f);
            }

            // Dopamine is the featured track for this mode, so keep it tied to
            // the same slider but do not inherit AudioSystem's quiet /5 legacy cap.
            _musicSource.volume = enabled ? Mathf.Clamp01(level) * bulkMusicMaxVolume : 0f;
            _musicSource.mute = !enabled;
        }

        float BeatPulse()
        {
            float sourceTime = _musicSource && _musicSource.isPlaying ? _musicSource.time : Time.time;
            float beat = sourceTime * (musicBpm / 60f);
            float phase = beat - Mathf.Floor(beat);
            return Mathf.Pow(1f - phase, 5f);
        }

        void EnsureBulkAudioSources()
        {
            if (!_runtimeRoot)
                return;

            if (!_musicSource)
            {
                var musicObject = new GameObject("Bulk Filaments Music");
                musicObject.transform.SetParent(_runtimeRoot.transform, false);
                _musicSource = musicObject.AddComponent<AudioSource>();
            }

            if (!_sfxSource)
            {
                var sfxObject = new GameObject("Bulk Filaments SFX");
                sfxObject.transform.SetParent(_runtimeRoot.transform, false);
                _sfxSource = sfxObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f;
                _sfxSource.ignoreListenerPause = true;
                _sfxSource.volume = 1f;
                _sfxSource.mute = false;
                _sfxSource.bypassEffects = true;
                _sfxSource.bypassListenerEffects = true;
                _sfxSource.bypassReverbZones = true;
                _sfxSource.outputAudioMixerGroup = null;
            }

            _grappleFireClip ??= Resources.Load<AudioClip>(GrappleFireResourcePath) ??
                MakeProceduralClip("Bulk Grapple Fire", 0.24f, 1120f, 370f, 0.74f, 0.22f);
            _latchSurgeClip ??= Resources.Load<AudioClip>(LatchSurgeResourcePath) ??
                MakeProceduralClip("Bulk Latch Surge", 0.58f, 160f, 920f, 0.82f, 0.08f);
            _latchMissClip ??= MakeProceduralClip("Bulk Latch Miss", 0.18f, 260f, 120f, 0.38f, 0.3f);
            _powerCrystalClip ??= Resources.Load<AudioClip>(PowerCrystalResourcePath) ??
                MakeProceduralClip("Bulk Power Crystal", 0.42f, 420f, 1540f, 0.8f, 0.12f);
            _nanitePopClip ??= MakeProceduralClip("Bulk Nanite Pop", 0.16f, 2100f, 420f, 0.48f, 0.34f);
            _pulseGateClip ??= MakeProceduralClip("Bulk Pulse Gate", 0.5f, 120f, 1380f, 0.72f, 0.06f);

            if (!_audioStartupLogged)
            {
                _audioStartupLogged = true;
                CSDebug.Log(
                    $"[BulkFilamentsAudio] clips fire={_grappleFireClip?.name} surge={_latchSurgeClip?.name} miss={_latchMissClip?.name} power={_powerCrystalClip?.name} " +
                    $"nanite={_nanitePopClip?.name} gate={_pulseGateClip?.name} " +
                    $"listenerPause={AudioListener.pause} listenerVolume={AudioListener.volume:0.00}.");
            }
        }

        void ApplyBulkAudioMix()
        {
            if (_bulkMixApplied)
                return;

            _bulkMixApplied = true;
            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var source in sources)
            {
                if (!source || IsBulkSource(source))
                    continue;

                if (!IsBackgroundAudioLayer(source))
                    continue;

                _bulkAudioSnapshots.Add(new AudioSourceSnapshot(source, source.volume, source.mute, source.isPlaying));
                source.volume = 0f;
                source.mute = true;
                if (source.isPlaying)
                    source.Pause();
            }
        }

        void RestoreBulkAudioMix()
        {
            if (!_bulkMixApplied)
                return;

            foreach (var snapshot in _bulkAudioSnapshots)
            {
                if (!snapshot.Source)
                    continue;

                snapshot.Source.volume = snapshot.Volume;
                snapshot.Source.mute = snapshot.WasMuted;
                if (snapshot.WasPlaying)
                    snapshot.Source.UnPause();
            }

            _bulkAudioSnapshots.Clear();
            _bulkMixApplied = false;
            UnsubscribeBulkMusicSettings();
        }

        void EnforceBulkAudioMix()
        {
            if (!_bulkMixApplied || Time.unscaledTime < _nextBulkMixEnforceTime)
                return;

            _nextBulkMixEnforceTime = Time.unscaledTime + 0.75f;
            ApplyBulkMusicVolume();

            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var source in sources)
            {
                if (!source || IsBulkSource(source) || !IsBackgroundAudioLayer(source))
                    continue;

                if (!_bulkAudioSnapshots.Exists(snapshot => snapshot.Source == source))
                    _bulkAudioSnapshots.Add(new AudioSourceSnapshot(source, source.volume, source.mute, source.isPlaying));

                source.volume = 0f;
                source.mute = true;
                if (source.isPlaying)
                    source.Pause();
            }
        }

        bool IsBulkSource(AudioSource source)
        {
            return source == _musicSource || source == _sfxSource ||
                   source.name.IndexOf("Bulk Filaments", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsBackgroundAudioLayer(AudioSource source)
        {
            return IsDefaultMusicSource(source) || source.loop || IsNamedBackgroundLayer(source);
        }

        bool IsDefaultMusicSource(AudioSource source)
        {
            var audioSystem = AudioSystem.Instance;
            if (audioSystem && (source == audioSystem.MusicSource1 || source == audioSystem.MusicSource2))
                return true;

            string sourceName = source.name ?? string.Empty;
            string clipName = source.clip ? source.clip.name : string.Empty;
            return sourceName.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   sourceName.IndexOf("jukebox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   clipName.IndexOf("music", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        bool IsNamedBackgroundLayer(AudioSource source)
        {
            string sourceName = source.name ?? string.Empty;
            string clipName = source.clip ? source.clip.name : string.Empty;
            return ContainsBackgroundAudioName(sourceName) || ContainsBackgroundAudioName(clipName);
        }

        static bool ContainsBackgroundAudioName(string value)
        {
            return value.IndexOf("ambient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("ambience", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("background", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("wind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("drone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("loop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("bed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static AudioClip MakeProceduralClip(string clipName, float duration, float startFrequency, float endFrequency, float gain, float noise)
        {
            const int sampleRate = 44100;
            int samples = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[samples];
            var random = new System.Random(clipName.GetHashCode());
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)Mathf.Max(1, samples - 1);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float frequency = Mathf.Lerp(startFrequency, endFrequency, Mathf.SmoothStep(0f, 1f, t));
                phase += frequency / sampleRate;
                float tone = Mathf.Sin(phase * Mathf.PI * 2f);
                float grit = ((float)random.NextDouble() * 2f - 1f) * noise;
                data[i] = (tone * (1f - noise) + grit) * envelope * gain;
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void EnsureSingleAudioListener()
        {
            AudioListener keeper = FindEnabledAudioListener();
            if (!keeper)
            {
                if (!_mainCamera)
                    _mainCamera = Camera.main;

                if (_mainCamera)
                {
                    keeper = _mainCamera.GetComponent<AudioListener>();
                    if (!keeper)
                        keeper = _mainCamera.gameObject.AddComponent<AudioListener>();
                }
                else if (_runtimeRoot)
                {
                    keeper = _runtimeRoot.GetComponent<AudioListener>();
                    if (!keeper)
                        keeper = _runtimeRoot.AddComponent<AudioListener>();
                }

                if (keeper)
                    keeper.enabled = true;
            }

            if (!keeper)
                return;

            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (listener && listener != keeper && listener.enabled)
                    listener.enabled = false;
            }
        }

        static AudioListener FindEnabledAudioListener()
        {
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var listener in listeners)
            {
                if (listener && listener.enabled)
                    return listener;
            }

            return null;
        }

        readonly struct AudioSourceSnapshot
        {
            public readonly AudioSource Source;
            public readonly float Volume;
            public readonly bool WasMuted;
            public readonly bool WasPlaying;

            public AudioSourceSnapshot(AudioSource source, float volume, bool wasMuted, bool wasPlaying)
            {
                Source = source;
                Volume = volume;
                WasMuted = wasMuted;
                WasPlaying = wasPlaying;
            }
        }
    }
}
