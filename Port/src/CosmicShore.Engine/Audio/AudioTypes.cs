// ─────────────────────────────────────────────────────────────────────────────
// AudioTypes.cs — engine surface for the Unity audio types the ported
// AudioSystem's LEGACY lane drives (original contracts: UnityEngine.AudioClip,
// UnityEngine.AudioSource, UnityEngine.Audio.AudioMixer). Headless-honest
// semantics per the CloudSaveSdk / MultiplayerSdk placeholder precedent: the
// play/stop/volume STATE is real and observable (that state IS the behavior
// the game logic reads back — isPlaying, clip, volume), while no sample data
// is decoded or emitted. The interactive client's own AudioEngine remains the
// audible backend; these types model the scene-side routing the Unity build
// authors in the inspector.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// A named audio asset (original contract: UnityEngine.AudioClip). No
    /// sample data — headless callers only route and name clips.
    /// </summary>
    public class AudioClip : Object
    {
        /// <summary>Clip length in seconds (authoring-supplied; 0 when unknown).</summary>
        public float length;
    }

    /// <summary>
    /// Plays back an <see cref="AudioClip"/> (original contract:
    /// UnityEngine.AudioSource). Play/Stop toggle <see cref="isPlaying"/>;
    /// <see cref="PlayOneShot(AudioClip)"/> records the shot so behavior
    /// tests can observe the legacy SFX lane.
    /// </summary>
    public class AudioSource : Behaviour
    {
        public AudioClip clip;
        public float volume = 1f;
        public bool loop;
        public bool playOnAwake;

        public bool isPlaying { get; private set; }

        // Port-only observability for the one-shot lane (no Unity counterpart;
        // the engine assembly exposes no internals, so these are public like
        // the other placeholder seams).
        public AudioClip LastOneShotClip { get; private set; }
        public float LastOneShotVolumeScale { get; private set; } = 1f;
        public int OneShotCount { get; private set; }

        public void Play()
        {
            if (clip == null) return; // original contract: Play with no clip produces nothing
            isPlaying = true;
        }

        public void Stop() => isPlaying = false;

        public void Pause() => isPlaying = false;

        public void PlayOneShot(AudioClip oneShotClip) => PlayOneShot(oneShotClip, 1f);

        public void PlayOneShot(AudioClip oneShotClip, float volumeScale)
        {
            if (oneShotClip == null)
            {
                // Original contract: Unity logs and continues without throwing.
                Debug.LogWarning("PlayOneShot was called with a null AudioClip.");
                return;
            }

            LastOneShotClip = oneShotClip;
            LastOneShotVolumeScale = volumeScale;
            OneShotCount++;
        }
    }
}

namespace CosmicShore.Engine.Audio
{
    /// <summary>
    /// A named-parameter mixing console (original contract:
    /// UnityEngine.Audio.AudioMixer — the SetFloat/GetFloat exposed-parameter
    /// surface). In Unity it is an authored asset; here it is directly
    /// constructible so rigs can wire it like the inspector does.
    /// </summary>
    public class AudioMixer : Object
    {
        readonly Dictionary<string, float> _values = new();

        public bool SetFloat(string name, float value)
        {
            _values[name] = value;
            return true;
        }

        public bool GetFloat(string name, out float value) => _values.TryGetValue(name, out value);

        public bool ClearFloat(string name) => _values.Remove(name);
    }
}
