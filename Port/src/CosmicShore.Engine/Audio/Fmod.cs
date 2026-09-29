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
    }

    /// <summary>Conversion helpers (original contract: FMODUnity.RuntimeUtils).</summary>
    public static class RuntimeUtils
    {
        public static ATTRIBUTES_3D To3DAttributes(Vector3 position) => new() { position = position };
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

        public bool isValid() => State != null;

        public RESULT setVolume(float volume)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Volume = volume; return RESULT.OK;
        }

        public RESULT getVolume(out float volume) { volume = State?.Volume ?? 0f; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }

        public RESULT set3DAttributes(ATTRIBUTES_3D attributes)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Position = attributes.position; return RESULT.OK;
        }

        public RESULT start()
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            if (State.Started && !State.Stopped) return RESULT.OK;
            State.Started = true; State.Stopped = false;
            RuntimeManager.RecordStart(State);
            return RESULT.OK;
        }

        public RESULT stop(STOP_MODE mode)
        {
            if (State == null) return RESULT.ERR_INVALID_HANDLE;
            State.Stopped = true; return RESULT.OK;
        }

        public RESULT setPaused(bool paused) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Paused = paused; return RESULT.OK; }
        public RESULT getPaused(out bool paused) { paused = State?.Paused ?? false; return State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK; }

        public RESULT getPlaybackState(out PLAYBACK_STATE state)
        {
            state = State == null || !State.Started || State.Stopped ? PLAYBACK_STATE.STOPPED : PLAYBACK_STATE.PLAYING;
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
            State.Parameters[id] = value; return RESULT.OK;
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
            State.Released = true; return RESULT.OK;
        }

        public RESULT clearHandle() { State = null; return RESULT.OK; }
    }

    /// <summary>Shared state behind <see cref="Bus"/> handle copies.</summary>
    public sealed class BusState
    {
        public string Path;
        public float Volume = 1f;
        public bool Mute;
    }

    /// <summary>
    /// A mixing bus (original contract: FMOD.Studio.Bus). Volume + mute are
    /// honest local state resolved per path by <see cref="RuntimeManager.GetBus"/>.
    /// </summary>
    public struct Bus
    {
        internal BusState State;

        public bool isValid() => State != null;

        public RESULT setVolume(float volume) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Volume = volume; return RESULT.OK; }
        public RESULT setMute(bool mute) { if (State == null) return RESULT.ERR_INVALID_HANDLE; State.Mute = mute; return RESULT.OK; }
        public RESULT setPaused(bool paused) => State == null ? RESULT.ERR_INVALID_HANDLE : RESULT.OK;
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

        /// <summary>Every one-shot that reached start(), oldest first (port-only observability).</summary>
        public static readonly List<EventInstanceState> StartedInstances = new();

        /// <summary>
        /// Test seam: when true, <see cref="GetBus"/> throws — models banks
        /// not yet loaded, driving callers onto their unresolved-bus fallback.
        /// </summary>
        public static bool FailBusResolution;

        public static EventInstance CreateInstance(EventReference reference)
        {
            if (reference.IsNull)
                throw new System.ArgumentException("EventReference is null.", nameof(reference));
            return new EventInstance { State = new EventInstanceState { Path = reference.Path } };
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
            if (instance.isValid()) instance.State.AttachedTo = transform;
        }

        public static void AttachInstanceToGameObject(EventInstance instance, GameObject gameObject)
            => AttachInstanceToGameObject(instance, gameObject != null ? gameObject.transform : null);

        public static void AttachInstanceToGameObject(EventInstance instance, Transform transform, bool nonRigidbodyVelocity)
            => AttachInstanceToGameObject(instance, transform);

        public static void AttachInstanceToGameObject(EventInstance instance, GameObject gameObject, Rigidbody rigidbody)
            => AttachInstanceToGameObject(instance, gameObject);

        public static void DetachInstanceFromGameObject(EventInstance instance)
        {
            if (instance.isValid()) instance.State.AttachedTo = null;
        }

        public static EventInstance CreateInstance(string path) => CreateInstance(new EventReference { Path = path });
        public static EventInstance CreateInstance(GUID guid) => CreateInstance(new EventReference { Guid = guid });

        public static void PlayOneShot(EventReference reference, Vector3 position = default)
        {
            if (reference.IsNull) return;
            var i = CreateInstance(reference);
            i.set3DAttributes(RuntimeUtils.To3DAttributes(position));
            i.start(); i.release();
        }

        public static void PlayOneShot(string path, Vector3 position = default) => PlayOneShot(new EventReference { Path = path }, position);

        public static void PlayOneShotAttached(EventReference reference, GameObject gameObject)
        {
            if (reference.IsNull) return;
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
        public static void MuteAllEvents(bool muted) => IsMuted = muted;
        public static void PauseAllEvents(bool paused) { }
        public static void LoadBank(string bankName, bool loadSamples = false) { }
        public static void UnloadBank(string bankName) { }
        public static void WaitForAllSampleLoading() { }

        /// <summary>Original: the RuntimeManager MonoBehaviour singleton. The port has no component; this is a stand-in handle.</summary>
        public static object Instance => StudioSystem;

        internal static void RecordStart(EventInstanceState state) => StartedInstances.Add(state);

        /// <summary>Clears buses, the started log, and the failure seam (test isolation).</summary>
        public static void ResetForTests()
        {
            Buses.Clear();
            Vcas.Clear();
            StudioSystem.Globals.Clear();
            StartedInstances.Clear();
            FailBusResolution = false;
        }
    }
}
