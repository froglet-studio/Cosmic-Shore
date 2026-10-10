using System.Collections.Generic;

namespace CosmicShore.Engine.Audio.Fmod
{
    /// <summary>
    /// Where the FMOD surface's calls go besides its own local state. With no backend installed
    /// the surface is the honest-local-state model the tests exercise (nothing is heard); a player
    /// that has the vendor runtime installs <see cref="FmodNativeBackend"/> and every event,
    /// bus, VCA and global parameter is ALSO driven through the real FMOD Studio system.
    /// </summary>
    public interface IFmodBackend
    {
        void Create(EventInstanceState state, EventReference reference);
        void Start(EventInstanceState state);
        void Stop(EventInstanceState state, STOP_MODE mode);
        void Release(EventInstanceState state);
        void SetVolume(EventInstanceState state, float volume);
        void SetPaused(EventInstanceState state, bool paused);
        void Set3DAttributes(EventInstanceState state, in ATTRIBUTES_3D attributes);
        void SetParameter(EventInstanceState state, string name, float value, bool ignoreSeekSpeed);
        /// <summary>The live playback state; null when this instance has no native counterpart.</summary>
        PLAYBACK_STATE? GetPlaybackState(EventInstanceState state);
        void SetBus(string path, float volume, bool mute, bool paused);
        void SetVca(string path, float volume);
        void SetGlobalParameter(string name, float value);
        void SetListener(int index, in ATTRIBUTES_3D attributes);
        void Update();

        // An event's static description from the loaded banks; null when this backend cannot say
        // (the silent model then answers: a one-shot, a snapshot by path prefix, length 0).
        bool? IsOneshot(string path) => null;
        bool? IsSnapshot(string path) => null;
        int? GetLength(string path) => null;

        /// <summary>
        /// Whether a loaded bank carries the event, resolved as FMOD's RuntimeManager does: by GUID
        /// when the reference has one, else by path. Null when this backend cannot say (the silent
        /// model then asks the build's strings bank, FmodGuids.BankCarries).
        /// </summary>
        bool? HasEvent(EventReference reference) => null;
    }

    public static class FmodBackend
    {
        /// <summary>The installed backend, or null (silent local-state model).</summary>
        public static IFmodBackend Current { get; set; }

        static readonly Dictionary<PARAMETER_ID, string> s_names = new();

        /// <summary>Remembers which name an id was made from, so a by-id write can reach FMOD by name.</summary>
        internal static void RememberName(PARAMETER_ID id, string name)
        {
            lock (s_names) s_names[id] = name;
        }

        internal static string NameOf(PARAMETER_ID id)
        {
            lock (s_names) return s_names.TryGetValue(id, out var n) ? n : null;
        }
    }
}
