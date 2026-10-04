using System;
using CosmicShore.Core;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay.Audio
{
    /// <summary>
    /// Ties every FMOD <see cref="StudioEventEmitter"/> under this GameObject to the in-game SFX
    /// slider / toggle.
    ///
    /// <para><b>Why this exists.</b> A <c>StudioEventEmitter</c> creates and starts its own
    /// <c>EventInstance</c> and never calls <c>setVolume()</c> on it. Cosmic Shore applies the SFX
    /// slider per instance (the FMOD project has no SFX bus/VCA driving the level yet - see
    /// <see cref="AudioVolumeMath"/>), so any emitter-driven loop - the brittlestar's
    /// <c>event:/SFX/Loops/mass birttle</c>, the shark and tadpole loops - played at full volume
    /// no matter where the slider sat.</para>
    ///
    /// <para><b>Instances are replaced behind our back.</b> With FMOD's "Stop Events Outside Max
    /// Distance" setting the emitter stops its instance when the listener leaves range and creates a
    /// NEW one when it comes back. So the handle is watched every LateUpdate (one IntPtr compare per
    /// emitter, no native call unless it changed) and the volume is re-applied to each new instance.
    /// </para>
    ///
    /// <para>Execution order runs this after the emitters' own <c>Start</c> (ObjectStart play), so the
    /// first instance gets its volume before FMOD's next studio update mixes it.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class EmitterSfxVolumeBinder : MonoBehaviour
    {
        [SerializeField, Range(0f, AudioVolumeMath.MaxBaseMultiplier), Tooltip(
            "Per-object volume trim applied on top of the SFX slider. 1 = no change.")]
        float baseVolumeMultiplier = 1f;

        StudioEventEmitter[] _emitters = Array.Empty<StudioEventEmitter>();
        IntPtr[] _appliedHandles = Array.Empty<IntPtr>();
        float _volume = 1f;

        /// <summary>Re-scan children for emitters (call after adding/removing emitters at runtime).</summary>
        public void RefreshEmitters()
        {
            _emitters = GetComponentsInChildren<StudioEventEmitter>(true);
            _appliedHandles = new IntPtr[_emitters.Length];
        }

        void Awake() => RefreshEmitters();

        void OnEnable()
        {
            GameSetting.OnChangeSFXLevel += OnSFXLevelChanged;
            GameSetting.OnChangeSFXEnabledStatus += OnSFXEnabledChanged;
            ResolveAndForceReapply();
        }

        void OnDisable()
        {
            GameSetting.OnChangeSFXLevel -= OnSFXLevelChanged;
            GameSetting.OnChangeSFXEnabledStatus -= OnSFXEnabledChanged;
        }

        void Start() => ApplyToNewInstances();

        void LateUpdate() => ApplyToNewInstances();

        void ApplyToNewInstances()
        {
            for (int i = 0; i < _emitters.Length; i++)
            {
                var emitter = _emitters[i];
                if (!emitter) continue;

                var instance = emitter.EventInstance;
                if (instance.handle == IntPtr.Zero || instance.handle == _appliedHandles[i]) continue;

                instance.setVolume(_volume);
                _appliedHandles[i] = instance.handle;
            }
        }

        void ResolveAndForceReapply()
        {
            _volume = AudioSystem.ResolveSfxInstanceVolume(baseVolumeMultiplier);
            Array.Clear(_appliedHandles, 0, _appliedHandles.Length);
            ApplyToNewInstances();
        }

        void OnSFXLevelChanged(float level) => ResolveAndForceReapply();
        void OnSFXEnabledChanged(bool enabled) => ResolveAndForceReapply();
    }
}
