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
        public string Path;

        public bool IsNull => string.IsNullOrEmpty(Path);

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
        public bool Released;
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

        public void setVolume(float volume)
        {
            if (State != null) State.Volume = volume;
        }

        public void set3DAttributes(ATTRIBUTES_3D attributes)
        {
            if (State != null) State.Position = attributes.position;
        }

        public void start()
        {
            if (State == null || State.Started) return;
            State.Started = true;
            RuntimeManager.RecordStart(State);
        }

        public void release()
        {
            if (State != null) State.Released = true;
        }
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

        public void setVolume(float volume)
        {
            if (State != null) State.Volume = volume;
        }

        public void setMute(bool mute)
        {
            if (State != null) State.Mute = mute;
        }

        public void getVolume(out float volume) => volume = State?.Volume ?? 0f;

        public void getMute(out bool mute) => mute = State?.Mute ?? false;
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

        internal static void RecordStart(EventInstanceState state) => StartedInstances.Add(state);

        /// <summary>Clears buses, the started log, and the failure seam (test isolation).</summary>
        public static void ResetForTests()
        {
            Buses.Clear();
            StartedInstances.Clear();
            FailBusResolution = false;
        }
    }
}
