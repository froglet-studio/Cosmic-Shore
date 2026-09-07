using CosmicShore.Core;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Plays a single looping, spatialized FMOD ambient bed for one flora instance.
    /// The instance is created and started when the component is enabled, attached to
    /// this GameObject so it follows the flora through the world, and stopped + released
    /// when the flora is disabled or destroyed (its death) - so each living flora
    /// contributes one ambient voice and nothing lingers after it withers.
    ///
    /// Attach this to a flora prefab (e.g. BranchingFlora / AssembledFlora) and wire the
    /// looping ambient event into <see cref="ambientEvent"/>. The event should be authored
    /// as a 3D / spatialized looping event in FMOD Studio; consider setting a sensible
    /// max-instance / voice-stealing limit on it in FMOD, since many flora can be alive at
    /// once.
    ///
    /// Note: <c>STOP_MODE</c> is written fully qualified as <c>FMOD.Studio.STOP_MODE</c>
    /// because FMODUnity also defines a <c>STOP_MODE</c> - a bare reference is ambiguous
    /// (CS0104) when both namespaces are imported.
    /// </summary>
    [DisallowMultipleComponent]
    public class FloraAmbientAudioController : MonoBehaviour
    {
        [Header("FMOD Event")]
        [SerializeField, Tooltip(
            "Looping, spatialized FMOD ambient event played for the lifetime of this " +
            "flora. Created + started on enable, stopped + released on disable/destroy.")]
        EventReference ambientEvent;

        [Header("SFX Volume")]
        [SerializeField, Tooltip(
            "When true, the ambient instance volume is multiplied by GameSetting.SFXLevel " +
            "and muted by SFXEnabled, matching the rest of the gameplay SFX.")]
        bool tieVolumeToSFXSlider = true;

        [SerializeField, Range(0f, 2f), Tooltip(
            "Per-flora volume trim applied on top of the SFX slider. 1 = no change.")]
        float baseVolumeMultiplier = 1f;

        [Header("Debug")]
        [SerializeField] bool debugLog = false;

        EventInstance _instance;
        bool _instanceStarted;

        void OnEnable()
        {
            StartAmbient();

            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel += OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus += OnSFXEnabledChanged;
            }
        }

        void OnDisable()
        {
            if (tieVolumeToSFXSlider)
            {
                GameSetting.OnChangeSFXLevel -= OnSFXLevelChanged;
                GameSetting.OnChangeSFXEnabledStatus -= OnSFXEnabledChanged;
            }

            // The flora is withering / being torn down - let the bed fade out, then free it.
            StopAndRelease(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        }

        void OnDestroy()
        {
            StopAndRelease(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        void StartAmbient()
        {
            if (_instanceStarted) return;

            if (ambientEvent.IsNull)
            {
                Debug.LogError($"[FloraAmbientAudioController] '{name}' has no Ambient Event assigned.", this);
                return;
            }

            if (!FmodSafe.TryCreateInstance(ambientEvent, out _instance, this))
                return;

            // Spatialise: follow this flora through the world.
            FmodSafe.Attach(_instance, gameObject);
            ApplySFXVolume();

            var startResult = _instance.start();
            _instanceStarted = startResult == FMOD.RESULT.OK;
            if (!_instanceStarted)
            {
                Debug.LogError(
                    $"[FloraAmbientAudioController] '{name}' start() returned {startResult} on '{ambientEvent}'.",
                    this);
                _instance.release();
                _instance.clearHandle();
                return;
            }

            if (debugLog)
                Debug.Log($"[FloraAmbientAudioController] '{name}' ambient START.", this);
        }

        void StopAndRelease(FMOD.Studio.STOP_MODE stopMode)
        {
            FmodSafe.StopAndRelease(ref _instance, _instanceStarted, stopMode);
            _instanceStarted = false;
        }

        void ApplySFXVolume()
        {
            if (!_instance.isValid()) return;
            _instance.setVolume(ResolveSFXVolume());
        }

        float ResolveSFXVolume()
        {
            if (!tieVolumeToSFXSlider)
                return Mathf.Clamp(baseVolumeMultiplier, 0f, AudioVolumeMath.MaxBaseMultiplier);
            return AudioSystem.ResolveSfxInstanceVolume(baseVolumeMultiplier);
        }

        void OnSFXLevelChanged(float level) => ApplySFXVolume();
        void OnSFXEnabledChanged(bool enabled) => ApplySFXVolume();
    }
}
