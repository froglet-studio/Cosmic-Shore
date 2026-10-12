using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Audio.Fmod
{
    // Original contracts: FMOD.RESULT / FMOD.GUID / FMOD.Studio.* enums + handles and the
    // FMODUnity StudioListener / StudioEventEmitter components. The port has no FMOD runtime,
    // so every handle is honest local state and every call answers RESULT.OK on a valid handle.

    public enum RESULT
    {
        OK = 0,
        ERR_BADCOMMAND,
        ERR_CHANNEL_ALLOC,
        ERR_EVENT_NOTFOUND = 74,
        ERR_INVALID_HANDLE = 30,
        ERR_INVALID_PARAM = 31,
        ERR_NOTREADY = 46,
        ERR_NOT_LOADED = 76,
        ERR_INTERNAL = 28,
    }

    public enum STOP_MODE { ALLOWFADEOUT = 0, IMMEDIATE = 1 }

    public enum PLAYBACK_STATE { PLAYING = 0, SUSTAINING, STOPPED, STARTING, STOPPING }

    [Flags]
    public enum PARAMETER_FLAGS : uint
    {
        READONLY = 0x01, AUTOMATIC = 0x02, GLOBAL = 0x04, DISCRETE = 0x08, LABELED = 0x10,
    }

    public enum PARAMETER_TYPE { GAME_CONTROLLED = 0 }

    [Serializable]
    public struct GUID : IEquatable<GUID>
    {
        public int Data1, Data2, Data3, Data4;
        public bool IsNull => Data1 == 0 && Data2 == 0 && Data3 == 0 && Data4 == 0;
        public bool Equals(GUID o) => Data1 == o.Data1 && Data2 == o.Data2 && Data3 == o.Data3 && Data4 == o.Data4;
        public override bool Equals(object obj) => obj is GUID g && Equals(g);
        public override int GetHashCode() => HashCode.Combine(Data1, Data2, Data3, Data4);
        public static bool operator ==(GUID a, GUID b) => a.Equals(b);
        public static bool operator !=(GUID a, GUID b) => !a.Equals(b);
    }

    public struct PARAMETER_ID : IEquatable<PARAMETER_ID>
    {
        public uint data1, data2;

        /// <summary>Port-only: a stable id per parameter NAME (no banks to look ids up in).</summary>
        public static PARAMETER_ID FromName(string name)
        {
            uint h1 = 2166136261, h2 = 16777619;
            foreach (char c in name ?? string.Empty) { h1 = (h1 ^ c) * 16777619; h2 = (h2 ^ c) * 2166136261; }
            var id = new PARAMETER_ID { data1 = h1, data2 = h2 };
            FmodBackend.RememberName(id, name);
            return id;
        }

        public bool Equals(PARAMETER_ID o) => data1 == o.data1 && data2 == o.data2;
        public override bool Equals(object obj) => obj is PARAMETER_ID p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(data1, data2);
    }

    public struct PARAMETER_DESCRIPTION
    {
        public string name;
        public PARAMETER_ID id;
        public float minimum, maximum, defaultvalue;
        public PARAMETER_TYPE type;
        public PARAMETER_FLAGS flags;
        public GUID guid;
    }

    /// <summary>An event's static description. Without banks every parameter name resolves (0..1, default 0).</summary>
    public struct EventDescription
    {
        internal string Path;

        public bool isValid() => Path != null;

        public RESULT getPath(out string path) { path = Path; return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        // With a runtime installed these come from the banks (a looping event is NOT a one-shot,
        // which FMODOneShotVolumeHelper checks); without one, a one-shot of length 0.
        public RESULT isOneshot(out bool oneshot) { oneshot = FmodBackend.Current?.IsOneshot(Path) ?? true; return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT is3D(out bool is3D) { is3D = true; return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT isSnapshot(out bool snapshot) { snapshot = RuntimeManager.IsSnapshot(Path); return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT getLength(out int length) { length = FmodBackend.Current?.GetLength(Path) ?? 0; return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT loadSampleData() => RESULT.OK;
        public RESULT unloadSampleData() => RESULT.OK;

        public RESULT getParameterDescriptionByName(string name, out PARAMETER_DESCRIPTION parameter)
        {
            parameter = new PARAMETER_DESCRIPTION { name = name, id = PARAMETER_ID.FromName(name), minimum = 0f, maximum = 1f };
            return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
        }

        public RESULT getParameterDescriptionCount(out int count) { count = 0; return RESULT.OK; }

        public RESULT createInstance(out EventInstance instance)
        {
            instance = Path == null ? default : RuntimeManager.CreateInstance(Path);
            return Path == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
        }
    }

    public sealed class VCAState { public string Path; public float Volume = 1f; }

    public struct VCA
    {
        internal VCAState State;
        public bool isValid() => State != null;
        public RESULT setVolume(float volume) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Volume = volume; FmodBackend.Current?.SetVca(State.Path, volume); return RESULT.OK; }
        public RESULT getVolume(out float volume) { volume = State?.Volume ?? 0f; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT getVolume(out float volume, out float finalvolume) { var r = getVolume(out volume); finalvolume = volume; return r; }
        public RESULT getPath(out string path) { path = State?.Path; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
    }

    public class VCANotFoundException : Exception
    {
        public VCANotFoundException(string path) : base($"FMOD VCA not found: '{path}'") { }
    }

    /// <summary>Original: FMOD.Studio.System — the global-parameter + lookup half the game reads.</summary>
    public sealed class StudioSystem
    {
        internal readonly Dictionary<PARAMETER_ID, float> Globals = new();

        public bool isValid() => true;
        public RESULT setParameterByID(PARAMETER_ID id, float value, bool ignoreseekspeed = false) { Globals[id] = value; FmodBackend.Current?.SetGlobalParameter(FmodBackend.NameOf(id), value); return RESULT.OK; }
        public RESULT getParameterByID(PARAMETER_ID id, out float value) { Globals.TryGetValue(id, out value); return RESULT.OK; }
        public RESULT getParameterByID(PARAMETER_ID id, out float value, out float finalvalue) { Globals.TryGetValue(id, out value); finalvalue = value; return RESULT.OK; }
        public RESULT setParameterByName(string name, float value, bool ignoreseekspeed = false) => setParameterByID(PARAMETER_ID.FromName(name), value);
        public RESULT getParameterByName(string name, out float value) => getParameterByID(PARAMETER_ID.FromName(name), out value);

        public RESULT getParameterDescriptionByName(string name, out PARAMETER_DESCRIPTION parameter)
        {
            parameter = new PARAMETER_DESCRIPTION { name = name, id = PARAMETER_ID.FromName(name), maximum = 1f, flags = PARAMETER_FLAGS.GLOBAL };
            return RESULT.OK;
        }

        public RESULT getBus(string path, out Bus bus)
        {
            try { bus = RuntimeManager.GetBus(path); return RESULT.OK; }
            catch (BusNotFoundException) { bus = default; return RESULT.ERR_EVENT_NOTFOUND; }
        }

        public RESULT getVCA(string path, out VCA vca)
        {
            try { vca = RuntimeManager.GetVCA(path); return RESULT.OK; }
            catch (VCANotFoundException) { vca = default; return RESULT.ERR_EVENT_NOTFOUND; }
        }

        public RESULT getEvent(string path, out EventDescription description)
        {
            description = new EventDescription { Path = path };
            return RESULT.OK;
        }

        public RESULT update() => RESULT.OK;
        public RESULT flushCommands() => RESULT.OK;
    }

    /// <summary>
    /// Original: FMODUnity.StudioListener — marks the object whose pose is the 3D listener. Every
    /// enabled listener is tracked (FMOD treats each as a distinct listener).
    /// </summary>
    public class StudioListener : MonoBehaviour
    {
        public static readonly List<StudioListener> Active = new();

        [SerializeField] GameObject attenuationObject;
        public int ListenerNumber = -1;

        public GameObject AttenuationObject { get => attenuationObject; set => attenuationObject = value; }

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); ListenerNumber = Active.IndexOf(this); }
        void OnDisable() { Active.Remove(this); ListenerNumber = -1; }
    }

    public enum EmitterGameEvent { None = 0, ObjectStart, ObjectDestroy, TriggerEnter, TriggerExit, TriggerEnter2D, TriggerExit2D, CollisionEnter, CollisionExit, CollisionEnter2D, CollisionExit2D, ObjectEnable, ObjectDisable, ObjectMouseEnter, ObjectMouseExit, ObjectMouseDown, ObjectMouseUp, UIMouseEnter, UIMouseExit, UIMouseDown, UIMouseUp }

    [Serializable]
    public struct ParamRef { public string Name; public float Value; public PARAMETER_ID ID; }

    /// <summary>Original: FMODUnity.StudioEventEmitter — plays its <see cref="EventReference"/> on its game event.</summary>
    public class StudioEventEmitter : MonoBehaviour
    {
        public EventReference EventReference;
        // FMOD 2.02's names (what the project's prefabs serialize: EventPlayTrigger/EventStopTrigger);
        // data written under the older names still loads.
        [CosmicShore.Engine.Serialization.FormerlySerializedAs("PlayEvent")]
        public EmitterGameEvent EventPlayTrigger = EmitterGameEvent.None;
        [CosmicShore.Engine.Serialization.FormerlySerializedAs("StopEvent")]
        public EmitterGameEvent EventStopTrigger = EmitterGameEvent.None;
        /// <summary>The pre-2.02 names, kept as the original keeps them.</summary>
        public EmitterGameEvent PlayEvent { get => EventPlayTrigger; set => EventPlayTrigger = value; }
        public EmitterGameEvent StopEvent { get => EventStopTrigger; set => EventStopTrigger = value; }
        public bool AllowFadeout = true;
        public bool TriggerOnce;
        public bool Preload;
        public bool NonRigidbodyVelocity;
        public ParamRef[] Params = Array.Empty<ParamRef>();
        public bool OverrideAttenuation;
        public float OverrideMinDistance = -1f;
        public float OverrideMaxDistance = -1f;

        EventInstance _instance;
        bool _hasTriggered;

        public EventInstance EventInstance => _instance;
        public EventDescription EventDescription => RuntimeManager.GetEventDescription(EventReference);

        protected virtual void Start() { HandleGameEvent(EmitterGameEvent.ObjectStart); }
        protected virtual void OnEnable() { HandleGameEvent(EmitterGameEvent.ObjectEnable); }
        protected virtual void OnDisable() { HandleGameEvent(EmitterGameEvent.ObjectDisable); }
        // As the original: the destroy trigger runs, then the instance is only detached (a looping
        // event with no stop trigger plays on) and a one-shot's handle is released.
        protected virtual void OnDestroy()
        {
            HandleGameEvent(EmitterGameEvent.ObjectDestroy);
            if (!_instance.isValid()) return;
            RuntimeManager.DetachInstanceFromGameObject(_instance);
            if (EventDescription.isOneshot(out bool oneshot) == RESULT.OK && oneshot) { _instance.release(); _instance.clearHandle(); }
        }

        protected void HandleGameEvent(EmitterGameEvent gameEvent)
        {
            if (EventPlayTrigger == gameEvent && gameEvent != EmitterGameEvent.None) Play();
            if (EventStopTrigger == gameEvent && gameEvent != EmitterGameEvent.None) Stop();
        }

        public void Play()
        {
            if (TriggerOnce && _hasTriggered) return;
            if (EventReference.IsNull) return;
            if (!_instance.isValid()) _instance = RuntimeManager.CreateInstance(EventReference);
            foreach (var p in Params) _instance.setParameterByName(p.Name, p.Value);
            RuntimeManager.AttachInstanceToGameObject(_instance, transform);
            _instance.start();
            _hasTriggered = true;
        }

        public void Stop()
        {
            if (!_instance.isValid()) return;
            _instance.stop(AllowFadeout ? STOP_MODE.ALLOWFADEOUT : STOP_MODE.IMMEDIATE);
            _instance.release();
            _instance.clearHandle();
        }

        public bool IsPlaying()
        {
            if (!_instance.isValid()) return false;
            _instance.getPlaybackState(out var state);
            return state != PLAYBACK_STATE.STOPPED;
        }

        public void SetParameter(string name, float value, bool ignoreseekspeed = false)
        {
            if (_instance.isValid()) _instance.setParameterByName(name, value, ignoreseekspeed);
        }

        public void SetParameter(PARAMETER_ID id, float value, bool ignoreseekspeed = false)
        {
            if (_instance.isValid()) _instance.setParameterByID(id, value, ignoreseekspeed);
        }

        public void Lookup() { }
    }
}
