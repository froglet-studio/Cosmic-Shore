// ─────────────────────────────────────────────────────────────────────────────
// Fmod.cs — engine placeholder surface for the FMOD Studio runtime the ported
// AudioSystem + FMODOneShotVolumeHelper drive (original contracts:
// FMODUnity.EventReference / RuntimeManager / RuntimeUtils and
// FMOD.Studio.EventInstance / Bus). Grown per the CloudSaveSdk /
// MultiplayerSdk precedent so the audio unit ports FULLY LIVE:
//
//   - The BUS registry is honest local state — GetBus resolves a per-path
//     volume/mute record, so "the SFX slider drives the whole bank through
//     the bus" is a real, observable behavior (not a no-op).
//   - EventInstance models FMOD's handle-over-shared-state: the struct copies
//     share one state record, so setVolume-then-start on a local copy lands
//     in the started-one-shot log exactly like the wire.
//   - No sound is emitted; the STARTED log (path, volume, position, attach
//     target) is the observable output, mirroring the AudioSource seams.
//
// Test seams (public — the engine assembly exposes no internals):
// StartedInstances / FailBusResolution / ResetForTests. FailBusResolution
// models unloaded banks: GetBus throws (original contract:
// FMOD.Studio.BusNotFoundException), exercising the caller's catch lane and
// the per-instance volume fallback.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;

namespace CosmicShore.Engine.Audio.Fmod
{
    /// <summary>
    /// Inspector-wired handle to an FMOD event (original contract:
    /// FMODUnity.EventReference — Guid + Path with IsNull). The placeholder
    /// keys purely off <see cref="Path"/>.
    /// </summary>
    public struct EventReference
    {
        public GUID Guid;
        public string Path;

        public bool IsNull => string.IsNullOrEmpty(Path) && Guid.IsNull;

        public override string ToString() => IsNull ? "(null EventReference)" : Path;
    }

    /// <summary>3D playback attributes (original contract: FMOD.ATTRIBUTES_3D, position only).</summary>
    public struct ATTRIBUTES_3D
    {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 forward;
        public Vector3 up;
    }

    /// <summary>Conversion helpers (original contract: FMODUnity.RuntimeUtils).</summary>
    public static class RuntimeUtils
    {
        public static ATTRIBUTES_3D To3DAttributes(Vector3 position) => new() { position = position, forward = Vector3.forward, up = Vector3.up };

        public static ATTRIBUTES_3D To3DAttributes(Transform transform, Rigidbody rigidbody = null)
            => transform == null ? To3DAttributes(Vector3.zero) : new ATTRIBUTES_3D
            {
                position = transform.position,
                forward = transform.forward,
                up = transform.up,
                velocity = rigidbody != null ? rigidbody.linearVelocity : Vector3.zero,
            };

        public static ATTRIBUTES_3D To3DAttributes(GameObject gameObject, Rigidbody rigidbody = null)
            => To3DAttributes(gameObject != null ? gameObject.transform : null, rigidbody);
    }

    /// <summary>Shared state behind <see cref="EventInstance"/> handle copies.</summary>
    public sealed class EventInstanceState
    {
        public string Path;
        public float Volume = 1f;
        public Vector3 Position;
        public Transform AttachedTo;
        public bool Started;
        public bool Stopped;
        public bool Paused;
        public bool Released;
        static int s_nextId;
        public readonly int Id = System.Threading.Interlocked.Increment(ref s_nextId);
        public readonly Dictionary<PARAMETER_ID, float> Parameters = new();
    }

    /// <summary>
    /// Playable instance of an FMOD event (original contract:
    /// FMOD.Studio.EventInstance). A struct handle over shared state — the
    /// create → setVolume → start → release sequence the one-shot helpers
    /// perform lands in <see cref="RuntimeManager.StartedInstances"/>.
    /// </summary>
    public struct EventInstance
    {
        internal EventInstanceState State;

        public bool isValid() => State != null && !State.Released;

        /// <summary>The native handle (non-zero and unique per live instance; zero when invalid).</summary>
        public System.IntPtr handle => State == null ? System.IntPtr.Zero : (System.IntPtr)State.Id;

        public RESULT setVolume(float volume)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Volume = volume;
            FmodBackend.Current?.SetVolume(State, volume);
            return RESULT.OK;
        }

        public RESULT getVolume(out float volume) { volume = State?.Volume ?? 0f; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT getVolume(out float volume, out float finalvolume) { var r = getVolume(out volume); finalvolume = volume; return r; }

        public RESULT set3DAttributes(ATTRIBUTES_3D attributes)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Position = attributes.position;
            FmodBackend.Current?.Set3DAttributes(State, attributes);
            return RESULT.OK;
        }

        public RESULT start()
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            if (State.Started && !State.Stopped) return RESULT.OK;
            State.Started = true; State.Stopped = false;
            RuntimeManager.RecordStart(State);
            FmodBackend.Current?.Start(State);
            return RESULT.OK;
        }

        public RESULT stop(STOP_MODE mode)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Stopped = true;
            FmodBackend.Current?.Stop(State, mode);
            return RESULT.OK;
        }

        public RESULT setPaused(bool paused) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Paused = paused; FmodBackend.Current?.SetPaused(State, paused); return RESULT.OK; }
        public RESULT getPaused(out bool paused) { paused = State?.Paused ?? false; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }

        public RESULT getPlaybackState(out PLAYBACK_STATE state)
        {
            state = State == null || !State.Started || State.Stopped ? PLAYBACK_STATE.STOPPED : PLAYBACK_STATE.PLAYING;
            // The real runtime knows when a one-shot has finished; the local model does not.
            if (State != null && FmodBackend.Current?.GetPlaybackState(State) is { } live) state = live;
            return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
        }

        public RESULT getDescription(out EventDescription description)
        {
            description = State == null ? default : new EventDescription { Path = State.Path };
            return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
        }

        public RESULT setParameterByID(PARAMETER_ID id, float value, bool ignoreseekspeed = false)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Parameters[id] = value;
            FmodBackend.Current?.SetParameter(State, FmodBackend.NameOf(id), value, ignoreseekspeed);
            return RESULT.OK;
        }

        public RESULT getParameterByID(PARAMETER_ID id, out float value) => getParameterByID(id, out value, out _);

        public RESULT getParameterByID(PARAMETER_ID id, out float value, out float finalvalue)
        {
            value = 0f;
            if (State != null) State.Parameters.TryGetValue(id, out value);
            finalvalue = value;
            return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
        }

        public RESULT setParameterByName(string name, float value, bool ignoreseekspeed = false)
            => setParameterByID(PARAMETER_ID.FromName(name), value, ignoreseekspeed);

        public RESULT getParameterByName(string name, out float value) => getParameterByID(PARAMETER_ID.FromName(name), out value);

        public RESULT release()
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Released = true;
            FmodBackend.Current?.Release(State);
            return RESULT.OK;
        }

        public RESULT clearHandle() { State = null; return RESULT.OK; }
    }

    /// <summary>Shared state behind <see cref="Bus"/> handle copies.</summary>
    public sealed class BusState
    {
        public string Path;
        public float Volume = 1f;
        public bool Mute;
        public bool Paused;
    }

    /// <summary>
    /// A mixing bus (original contract: FMOD.Studio.Bus). Volume + mute are
    /// honest local state resolved per path by <see cref="RuntimeManager.GetBus"/>.
    /// </summary>
    public struct Bus
    {
        internal BusState State;

        public bool isValid() => State != null;

        public RESULT setVolume(float volume) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Volume = volume; Push(); return RESULT.OK; }
        public RESULT setMute(bool mute) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Mute = mute; Push(); return RESULT.OK; }
        public RESULT setPaused(bool paused) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Paused = paused; Push(); return RESULT.OK; }
        void Push() => FmodBackend.Current?.SetBus(State.Path, State.Volume, State.Mute || (State.Path == "bus:/" && RuntimeManager.IsMuted), State.Paused);
        public RESULT getVolume(out float volume) { volume = State?.Volume ?? 0f; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT getMute(out bool mute) { mute = State?.Mute ?? false; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }
        public RESULT stopAllEvents(STOP_MODE mode) => State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
    }

    /// <summary>Thrown when a bus cannot be resolved (original contract: FMOD.Studio bank/bus lookup failure).</summary>
    public class BusNotFoundException : System.Exception
    {
        public BusNotFoundException(string path) : base($"FMOD bus not found: '{path}'") { }
    }

    /// <summary>
    /// The FMOD runtime entry point (original contract: FMODUnity.RuntimeManager
    /// — the CreateInstance / GetBus / AttachInstanceToGameObject subset the
    /// ported audio unit uses).
    /// </summary>
    public static class RuntimeManager
    {
        static readonly Dictionary<string, BusState> Buses = new();

        /// <summary>
        /// The most recent one-shots that reached start(), oldest first (port-only
        /// observability). Bounded: a long headless run starts thousands of events per match,
        /// so the log keeps the last <see cref="StartedLogCapacity"/> and the full history lives
        /// in <see cref="StartedTotal"/> and <see cref="StartedByPath"/>.
        /// </summary>
        public static readonly List<EventInstanceState> StartedInstances = new();
        public const int StartedLogCapacity = 4096;
        /// <summary>Every start() since the last reset.</summary>
        public static long StartedTotal { get; private set; }
        /// <summary>start() count per event path since the last reset ("(unresolved)" when there is none).</summary>
        public static readonly Dictionary<string, long> StartedByPath = new();

        /// <summary>
        /// Test seam: when true, <see cref="GetBus"/> throws — models banks
        /// not yet loaded, driving callers onto their unresolved-bus fallback.
        /// </summary>
        public static bool FailBusResolution;

        public static EventInstance CreateInstance(EventReference reference)
        {
            if (reference.IsNull)
                throw new System.ArgumentException("EventReference is null.", nameof(reference));
            AudioStats.Created(reference.Path ?? reference.ToString());
            var state = new EventInstanceState { Path = reference.Path };
            FmodBackend.Current?.Create(state, reference);
            return new EventInstance { State = state };
        }

        public static Bus GetBus(string path)
        {
            if (FailBusResolution) throw new BusNotFoundException(path);
            if (!Buses.TryGetValue(path, out var state))
                Buses[path] = state = new BusState { Path = path };
            return new Bus { State = state };
        }

        public static void AttachInstanceToGameObject(EventInstance instance, Transform transform)
        {
            if (!instance.isValid()) return;
            instance.State.AttachedTo = transform;
            if (!Attached.Contains(instance.State)) Attached.Add(instance.State);
            if (transform != null) instance.set3DAttributes(RuntimeUtils.To3DAttributes(transform));
        }

        /// <summary>Instances following a transform (original: RuntimeManager's attached-instance list).</summary>
        static readonly List<EventInstanceState> Attached = new();

        /// <summary>
        /// Once per frame, after the game's LateUpdate (original: RuntimeManager's own update):
        /// attached instances follow their objects, the listener follows its StudioListener (or
        /// the main camera), and the Studio system runs its command queue.
        /// </summary>
        public static void Update()
        {
            var backend = FmodBackend.Current;
            if (backend == null) return;
            for (int i = Attached.Count - 1; i >= 0; i--)
            {
                var s = Attached[i];
                var t = s.AttachedTo;
                bool finished = s.Released && backend.GetPlaybackState(s) is null or PLAYBACK_STATE.STOPPED;
                if (t == null || finished) { Attached.RemoveAt(i); continue; }
                backend.Set3DAttributes(s, RuntimeUtils.To3DAttributes(t));
            }
            int n = 0;
            foreach (var l in StudioListener.Active)
                if (l != null && l.isActiveAndEnabled) backend.SetListener(n++, RuntimeUtils.To3DAttributes(l.transform));
            if (n == 0 && Camera.main != null) backend.SetListener(0, RuntimeUtils.To3DAttributes(Camera.main.transform));
            backend.Update();
        }

        public static void AttachInstanceToGameObject(EventInstance instance, GameObject gameObject)
            => AttachInstanceToGameObject(instance, gameObject != null ? gameObject.transform : null);

        public static void AttachInstanceToGameObject(EventInstance instance, Transform transform, bool nonRigidbodyVelocity)
            => AttachInstanceToGameObject(instance, transform);

        public static void AttachInstanceToGameObject(EventInstance instance, GameObject gameObject, Rigidbody rigidbody)
            => AttachInstanceToGameObject(instance, gameObject);

        public static void DetachInstanceFromGameObject(EventInstance instance)
        {
            if (!instance.isValid()) return;
            instance.State.AttachedTo = null;
            Attached.Remove(instance.State);
        }

        public static EventInstance CreateInstance(string path) => CreateInstance(new EventReference { Path = path });
        public static EventInstance CreateInstance(GUID guid) => CreateInstance(new EventReference { Guid = guid });

        public static void PlayOneShot(EventReference reference, Vector3 position = default)
        {
            if (reference.IsNull) { AudioStats.UnwiredOneShots++; return; }
            var i = CreateInstance(reference);
            i.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            i.start(); i.release();
        }

        public static void PlayOneShot(string path, Vector3 position = default) => PlayOneShot(new EventReference { Path = path }, position);

        public static void PlayOneShotAttached(EventReference reference, GameObject gameObject)
        {
            if (reference.IsNull) { AudioStats.UnwiredOneShots++; return; }
            var i = CreateInstance(reference);
            AttachInstanceToGameObject(i, gameObject);
            i.start(); i.release();
        }

        public static void PlayOneShotAttached(string path, GameObject gameObject) => PlayOneShotAttached(new EventReference { Path = path }, gameObject);

        static readonly Dictionary<string, VCAState> Vcas = new();

        public static VCA GetVCA(string path)
        {
            if (FailBusResolution) throw new VCANotFoundException(path);
            if (!Vcas.TryGetValue(path, out var state)) Vcas[path] = state = new VCAState { Path = path };
            return new VCA { State = state };
        }

        public static EventDescription GetEventDescription(EventReference reference) => new() { Path = reference.Path ?? string.Empty };
        public static EventDescription GetEventDescription(string path) => new() { Path = path };

        /// <summary>Studio system (global parameters, bus/VCA lookups).</summary>
        public static StudioSystem StudioSystem { get; } = new StudioSystem();

        /// <summary>No FMOD runtime exists offline; the port's is always "initialized" (it never tears down mid-quit).</summary>
        public static bool IsInitialized => true;
        public static bool HaveAllBanksLoaded => true;
        public static bool HasBankLoaded(string bankName) => true;
        public static bool IsMuted { get; private set; }
        public static void MuteAllEvents(bool muted) { IsMuted = muted; FmodBackend.Current?.SetBus("bus:/", 1f, muted, false); }
        public static void PauseAllEvents(bool paused) { FmodBackend.Current?.SetBus("bus:/", 1f, IsMuted, paused); }
        public static void LoadBank(string bankName, bool loadSamples = false) { }
        public static void UnloadBank(string bankName) { }
        public static void WaitForAllSampleLoading() { }

        /// <summary>Original: the RuntimeManager MonoBehaviour singleton. The port has no component; this is a stand-in handle.</summary>
        public static object Instance => StudioSystem;

        /// <summary>
        /// Engine-only probe: every event start, by path, in order (the parity harness's FMOD
        /// event channel). Not part of the FMOD API; null unless a harness is listening.
        /// </summary>
        public static System.Action<string> EventStarted;

        internal static void RecordStart(EventInstanceState state)
        {
            EventStarted?.Invoke(state.Path ?? "(unresolved)");
            StartedTotal++;
            string key = state.Path ?? "(unresolved)";
            StartedByPath[key] = StartedByPath.TryGetValue(key, out var n) ? n + 1 : 1;
            StartedInstances.Add(state);
            if (StartedInstances.Count > StartedLogCapacity) StartedInstances.RemoveRange(0, StartedLogCapacity / 2);
        }

        /// <summary>Clears buses, the started log, and the failure seam (test isolation).</summary>
        public static void ResetForTests()
        {
            Buses.Clear();
            Vcas.Clear();
            StudioSystem.Globals.Clear();
            StartedInstances.Clear();
            StartedByPath.Clear();
            StartedTotal = 0;
            Attached.Clear();
            FailBusResolution = false;
        }
    }
}
