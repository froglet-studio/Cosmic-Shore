using System;
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
        readonly Dictionary<string, float> _defaults = new();
        HashSet<string> _exposed;
        readonly List<AudioMixerGroup> _groups = new();
        readonly List<AudioMixerSnapshot> _snapshots = new();

        public AudioMixerGroup outputAudioMixerGroup { get; set; }
        public AudioMixerUpdateMode updateMode { get; set; } = AudioMixerUpdateMode.Normal;

        /// <summary>
        /// Declares the exposed parameters with their start-snapshot values (the importer does
        /// this from the .mixer asset). Once declared, Set/GetFloat on any other name returns
        /// false — the original's behaviour for a parameter that was never exposed.
        /// </summary>
        public void DeclareExposed(string name, float startValue)
        {
            (_exposed ??= new HashSet<string>(StringComparer.Ordinal)).Add(name);
            _defaults[name] = startValue;
        }

        public void AddGroup(AudioMixerGroup group) { group.audioMixer = this; _groups.Add(group); }
        public void AddSnapshot(AudioMixerSnapshot snapshot) { snapshot.audioMixer = this; _snapshots.Add(snapshot); }

        bool Known(string name) => _exposed == null || _exposed.Contains(name);

        public bool SetFloat(string name, float value)
        {
            if (!Known(name)) return false;
            _values[name] = value;
            return true;
        }

        public bool GetFloat(string name, out float value)
        {
            if (_values.TryGetValue(name, out value)) return true;
            if (_defaults.TryGetValue(name, out value)) return true;
            value = 0f;
            return false;
        }

        /// <summary>Returns control of the parameter to the snapshots (original contract).</summary>
        public bool ClearFloat(string name)
        {
            if (!Known(name)) return false;
            _values.Remove(name);
            return true;
        }

        /// <summary>Groups whose path ("Master/Music") contains the sub-path, case-insensitively.</summary>
        public AudioMixerGroup[] FindMatchingGroups(string subPath)
        {
            var result = new List<AudioMixerGroup>();
            foreach (var g in _groups)
                if (string.IsNullOrEmpty(subPath) || g.path.IndexOf(subPath, StringComparison.OrdinalIgnoreCase) >= 0) result.Add(g);
            return result.ToArray();
        }

        public AudioMixerSnapshot FindSnapshot(string name)
        {
            foreach (var s in _snapshots) if (string.Equals(s.name, name, StringComparison.Ordinal)) return s;
            return null;
        }

        public void TransitionToSnapshots(AudioMixerSnapshot[] snapshots, float[] weights, float timeToReach) { }
    }

    public enum AudioMixerUpdateMode { Normal = 0, UnscaledTime = 1 }

    /// <summary>A routing group inside a mixer (original contract: UnityEngine.Audio.AudioMixerGroup).</summary>
    public class AudioMixerGroup : Object
    {
        public AudioMixer audioMixer { get; internal set; }
        /// <summary>Slash-separated path from the master group, e.g. "Master/Music".</summary>
        public string path { get; set; } = string.Empty;
    }

    /// <summary>A stored mixer state (original contract: UnityEngine.Audio.AudioMixerSnapshot).</summary>
    public class AudioMixerSnapshot : Object
    {
        public AudioMixer audioMixer { get; internal set; }
        public void TransitionTo(float timeToReach) { }
    }
}
