using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CosmicShore.Engine.Audio.Fmod
{
    /// <summary>
    /// The FMOD Studio runtime, driven through its documented C API (the vendor library the
    /// project ships under Assets/Plugins/FMOD — the same binary a Unity build loads). The
    /// bindings here are written against the published API reference; no vendor source is used.
    /// </summary>
    public sealed class FmodNativeBackend : IFmodBackend, IDisposable
    {
        public const string LibraryName = "fmodstudio";

        // ── Output types (FMOD_OUTPUTTYPE) the port uses ──
        public const int OutputAutodetect = 0, OutputNoSound = 2, OutputWavWriter = 3, OutputNoSoundNrt = 4;

        // FMOD_STUDIO_INIT_SYNCHRONOUS_UPDATE: no Studio thread, so a non-real-time run is deterministic.
        const uint StudioInitSynchronousUpdate = 0x4;

        [StructLayout(LayoutKind.Sequential)] struct Vec { public float x, y, z; }
        [StructLayout(LayoutKind.Sequential)] struct Attr3D { public Vec position, velocity, forward, up; }
        [StructLayout(LayoutKind.Sequential)] struct Guid16 { public int d1, d2, d3, d4; }

        static class N
        {
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_Create(out IntPtr system, uint headerVersion);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_GetCoreSystem(IntPtr system, out IntPtr core);
            [DllImport(LibraryName)] public static extern int FMOD_System_SetOutput(IntPtr core, int output);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_Initialize(IntPtr system, int maxChannels, uint studioFlags, uint flags, IntPtr extraDriverData);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_Release(IntPtr system);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_Update(IntPtr system);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_FlushCommands(IntPtr system);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_LoadBankFile(IntPtr system, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, uint flags, out IntPtr bank);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_GetEvent(IntPtr system, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr description);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_GetEventByID(IntPtr system, ref Guid16 id, out IntPtr description);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_GetBus(IntPtr system, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr bus);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_GetVCA(IntPtr system, [MarshalAs(UnmanagedType.LPUTF8Str)] string path, out IntPtr vca);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_SetNumListeners(IntPtr system, int count);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_SetListenerAttributes(IntPtr system, int listener, ref Attr3D attributes, IntPtr attenuationPosition);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_System_SetParameterByName(IntPtr system, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, float value, int ignoreSeekSpeed);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventDescription_CreateInstance(IntPtr description, out IntPtr instance);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventDescription_GetPath(IntPtr description, byte[] path, int size, out int retrieved);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventDescription_IsOneshot(IntPtr description, out int oneshot);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventDescription_IsSnapshot(IntPtr description, out int snapshot);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventDescription_GetLength(IntPtr description, out int length);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_Start(IntPtr instance);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_Stop(IntPtr instance, int mode);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_Release(IntPtr instance);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_SetVolume(IntPtr instance, float volume);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_SetPaused(IntPtr instance, int paused);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_Set3DAttributes(IntPtr instance, ref Attr3D attributes);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_SetParameterByName(IntPtr instance, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, float value, int ignoreSeekSpeed);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_EventInstance_GetPlaybackState(IntPtr instance, out int state);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_Bus_SetVolume(IntPtr bus, float volume);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_Bus_SetMute(IntPtr bus, int mute);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_Bus_SetPaused(IntPtr bus, int paused);
            [DllImport(LibraryName)] public static extern int FMOD_Studio_VCA_SetVolume(IntPtr vca, float volume);
        }

        IntPtr _system;
        IntPtr _wavPath;
        readonly Dictionary<EventInstanceState, IntPtr> _instances = new();
        readonly Dictionary<string, IntPtr> _buses = new(StringComparer.Ordinal);
        readonly Dictionary<string, IntPtr> _vcas = new(StringComparer.Ordinal);
        readonly Dictionary<string, IntPtr> _eventsByPath = new(StringComparer.Ordinal);
        readonly HashSet<string> _reported = new(StringComparer.Ordinal);

        public IReadOnlyList<string> LoadedBanks => _banks;
        readonly List<string> _banks = new();
        public int Output { get; private set; }
        public int LiveInstanceCount => _instances.Count;

        /// <summary>Everything that stopped the backend from starting; empty when it is running.</summary>
        public string Error { get; private set; } = string.Empty;

        FmodNativeBackend() { }

        /// <summary>
        /// Creates and initialises the Studio system and loads every bank in
        /// <paramref name="bankDirectory"/> (strings banks first, as the Unity integration does).
        /// <paramref name="output"/> picks the output: autodetect falls back to no-sound on a
        /// machine with no audio device; the WAV writer records the mix to <paramref name="wavPath"/>.
        /// Returns null (with a reason on stderr) when the runtime cannot start.
        /// </summary>
        public static FmodNativeBackend TryCreate(uint headerVersion, string bankDirectory, int output = OutputAutodetect, string wavPath = null)
        {
            var b = new FmodNativeBackend();
            try
            {
                if (!b.Init(headerVersion, output, wavPath) && output == OutputAutodetect)
                {
                    // No audio device (a server, a container): keep the simulation honest by
                    // running the mix with no output rather than not running it at all.
                    b.Shutdown();
                    if (!b.Init(headerVersion, OutputNoSound, null)) { Console.Error.WriteLine($"[fmod] {b.Error}"); return null; }
                }
                else if (b._system == IntPtr.Zero) { Console.Error.WriteLine($"[fmod] {b.Error}"); return null; }
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
            {
                Console.Error.WriteLine($"[fmod] runtime unavailable: {e.Message}");
                return null;
            }

            if (Directory.Exists(bankDirectory))
            {
                var files = new List<string>(Directory.GetFiles(bankDirectory, "*.bank"));
                files.Sort((x, y) =>
                {
                    bool sx = x.EndsWith(".strings.bank", StringComparison.OrdinalIgnoreCase);
                    bool sy = y.EndsWith(".strings.bank", StringComparison.OrdinalIgnoreCase);
                    return sx != sy ? (sx ? -1 : 1) : string.CompareOrdinal(x, y);
                });
                foreach (var f in files)
                {
                    int r = N.FMOD_Studio_System_LoadBankFile(b._system, f, 0, out _);
                    if (r == 0) b._banks.Add(Path.GetFileName(f));
                    else Console.Error.WriteLine($"[fmod] bank {Path.GetFileName(f)} failed to load (FMOD_RESULT {r})");
                }
            }
            else Console.Error.WriteLine($"[fmod] bank directory not found: {bankDirectory}");
            return b;
        }

        bool Init(uint headerVersion, int output, string wavPath)
        {
            int r = N.FMOD_Studio_System_Create(out _system, headerVersion);
            if (r != 0) { Error = $"FMOD_Studio_System_Create failed (FMOD_RESULT {r}; header version 0x{headerVersion:X8})"; _system = IntPtr.Zero; return false; }
            N.FMOD_Studio_System_GetCoreSystem(_system, out var core);
            if (output != OutputAutodetect) N.FMOD_System_SetOutput(core, output);
            IntPtr extra = IntPtr.Zero;
            if (output == OutputWavWriter && !string.IsNullOrEmpty(wavPath))
                extra = _wavPath = Marshal.StringToHGlobalAnsi(wavPath);
            // 1024 virtual channels (the Unity integration's default), studio + core flags normal.
            r = N.FMOD_Studio_System_Initialize(_system, 1024, output == OutputNoSoundNrt ? StudioInitSynchronousUpdate : 0, 0, extra);
            if (r != 0) { Error = $"FMOD_Studio_System_Initialize failed (FMOD_RESULT {r}, output {output})"; return false; }
            Output = output;
            return true;
        }

        void Shutdown()
        {
            if (_system != IntPtr.Zero) N.FMOD_Studio_System_Release(_system);
            _system = IntPtr.Zero;
            if (_wavPath != IntPtr.Zero) { Marshal.FreeHGlobal(_wavPath); _wavPath = IntPtr.Zero; }
        }

        public void Dispose()
        {
            _instances.Clear();
            _released.Clear();
            Shutdown();
        }

        IntPtr Description(EventReference reference)
        {
            IntPtr d;
            if (!reference.Guid.IsNull)
            {
                var g = new Guid16 { d1 = reference.Guid.Data1, d2 = reference.Guid.Data2, d3 = reference.Guid.Data3, d4 = reference.Guid.Data4 };
                if (N.FMOD_Studio_System_GetEventByID(_system, ref g, out d) == 0) return d;
            }
            if (string.IsNullOrEmpty(reference.Path)) return IntPtr.Zero;
            if (_eventsByPath.TryGetValue(reference.Path, out d)) return d;
            if (N.FMOD_Studio_System_GetEvent(_system, reference.Path, out d) != 0) d = IntPtr.Zero;
            _eventsByPath[reference.Path] = d;
            return d;
        }

        /// <summary>The banks' own answer (FMOD_Studio_System_GetEventByID, or GetEvent for a path-only reference).</summary>
        public bool? HasEvent(EventReference reference)
        {
            if (_system == IntPtr.Zero) return null;
            if (!reference.Guid.IsNull)
            {
                var g = new Guid16 { d1 = reference.Guid.Data1, d2 = reference.Guid.Data2, d3 = reference.Guid.Data3, d4 = reference.Guid.Data4 };
                return N.FMOD_Studio_System_GetEventByID(_system, ref g, out var byId) == 0 && byId != IntPtr.Zero;
            }
            return !string.IsNullOrEmpty(reference.Path) && Description(reference) != IntPtr.Zero;
        }

        public void Create(EventInstanceState state, EventReference reference)
        {
            var d = Description(reference);
            if (d == IntPtr.Zero)
            {
                if (_reported.Add(reference.ToString())) Console.Error.WriteLine($"[fmod] event not found in the loaded banks: {reference}");
                AudioStats.Missing.Add(reference.ToString());
                return;
            }
            if (string.IsNullOrEmpty(state.Path)) state.Path = PathOf(d);
            if (N.FMOD_Studio_EventDescription_CreateInstance(d, out var i) == 0 && i != IntPtr.Zero)
                _instances[state] = i;
        }

        readonly Dictionary<IntPtr, string> _paths = new();
        readonly byte[] _pathBuffer = new byte[512];

        /// <summary>The event's path, from the loaded strings bank (a GUID-only reference has none of its own).</summary>
        string PathOf(IntPtr description)
        {
            if (_paths.TryGetValue(description, out var p)) return p;
            p = N.FMOD_Studio_EventDescription_GetPath(description, _pathBuffer, _pathBuffer.Length, out int n) == 0 && n > 1
                ? System.Text.Encoding.UTF8.GetString(_pathBuffer, 0, n - 1) : null;
            return _paths[description] = p;
        }

        bool Get(EventInstanceState s, out IntPtr i) => _instances.TryGetValue(s, out i);

        IntPtr DescriptionOf(string path) => string.IsNullOrEmpty(path) ? IntPtr.Zero : Description(new EventReference { Path = path });

        public bool? IsOneshot(string path)
        {
            var d = DescriptionOf(path);
            return d != IntPtr.Zero && N.FMOD_Studio_EventDescription_IsOneshot(d, out int v) == 0 ? v != 0 : null;
        }

        public bool? IsSnapshot(string path)
        {
            var d = DescriptionOf(path);
            return d != IntPtr.Zero && N.FMOD_Studio_EventDescription_IsSnapshot(d, out int v) == 0 ? v != 0 : null;
        }

        public int? GetLength(string path)
        {
            var d = DescriptionOf(path);
            return d != IntPtr.Zero && N.FMOD_Studio_EventDescription_GetLength(d, out int v) == 0 ? v : null;
        }

        public void Start(EventInstanceState s) { if (Get(s, out var i)) N.FMOD_Studio_EventInstance_Start(i); }
        public void Stop(EventInstanceState s, STOP_MODE mode) { if (Get(s, out var i)) N.FMOD_Studio_EventInstance_Stop(i, (int)mode); }
        public void SetVolume(EventInstanceState s, float v) { if (Get(s, out var i)) N.FMOD_Studio_EventInstance_SetVolume(i, v); }
        public void SetPaused(EventInstanceState s, bool p) { if (Get(s, out var i)) N.FMOD_Studio_EventInstance_SetPaused(i, p ? 1 : 0); }

        readonly List<EventInstanceState> _released = new();

        public void Release(EventInstanceState s)
        {
            // FMOD keeps a released instance alive until it finishes playing, and an attached
            // one-shot is released the moment it starts — so the handle stays until it stops.
            if (!Get(s, out var i)) return;
            N.FMOD_Studio_EventInstance_Release(i);
            _released.Add(s);
        }

        public void Set3DAttributes(EventInstanceState s, in ATTRIBUTES_3D a)
        {
            if (!Get(s, out var i)) return;
            var n = ToNative(a);
            N.FMOD_Studio_EventInstance_Set3DAttributes(i, ref n);
        }

        public void SetParameter(EventInstanceState s, string name, float value, bool ignoreSeekSpeed)
        {
            if (name != null && Get(s, out var i)) N.FMOD_Studio_EventInstance_SetParameterByName(i, name, value, ignoreSeekSpeed ? 1 : 0);
        }

        public PLAYBACK_STATE? GetPlaybackState(EventInstanceState s)
        {
            if (!Get(s, out var i) || N.FMOD_Studio_EventInstance_GetPlaybackState(i, out int st) != 0) return null;
            return (PLAYBACK_STATE)st;
        }

        public void SetBus(string path, float volume, bool mute, bool paused)
        {
            if (!_buses.TryGetValue(path, out var b))
            {
                if (N.FMOD_Studio_System_GetBus(_system, path, out b) != 0) b = IntPtr.Zero;
                _buses[path] = b;
            }
            if (b == IntPtr.Zero) return;
            N.FMOD_Studio_Bus_SetVolume(b, volume);
            N.FMOD_Studio_Bus_SetMute(b, mute ? 1 : 0);
            N.FMOD_Studio_Bus_SetPaused(b, paused ? 1 : 0);
        }

        public void SetVca(string path, float volume)
        {
            if (!_vcas.TryGetValue(path, out var v))
            {
                if (N.FMOD_Studio_System_GetVCA(_system, path, out v) != 0) v = IntPtr.Zero;
                _vcas[path] = v;
            }
            if (v != IntPtr.Zero) N.FMOD_Studio_VCA_SetVolume(v, volume);
        }

        public void SetGlobalParameter(string name, float value)
        {
            if (name != null) N.FMOD_Studio_System_SetParameterByName(_system, name, value, 0);
        }

        int _listeners = 1;

        public void SetListener(int index, in ATTRIBUTES_3D a)
        {
            if (index + 1 > _listeners) { _listeners = index + 1; N.FMOD_Studio_System_SetNumListeners(_system, _listeners); }
            var n = ToNative(a);
            N.FMOD_Studio_System_SetListenerAttributes(_system, index, ref n, IntPtr.Zero);
        }

        public void Update()
        {
            N.FMOD_Studio_System_Update(_system);
            // Drop the handles of released instances that have finished (or that FMOD has freed).
            for (int k = _released.Count - 1; k >= 0; k--)
            {
                var s = _released[k];
                if (Get(s, out var i) && N.FMOD_Studio_EventInstance_GetPlaybackState(i, out int st) == 0 && st != (int)PLAYBACK_STATE.STOPPED)
                    continue;
                _instances.Remove(s);
                _released.RemoveAt(k);
            }
        }

        /// <summary>Blocks until every queued command has executed (for a deterministic capture).</summary>
        public void Flush() => N.FMOD_Studio_System_FlushCommands(_system);

        static Attr3D ToNative(in ATTRIBUTES_3D a)
        {
            // A zero forward/up is invalid to FMOD; fall back to the identity basis.
            var fwd = a.forward.sqrMagnitude > 1e-8f ? a.forward : Vector3.forward;
            var up = a.up.sqrMagnitude > 1e-8f ? a.up : Vector3.up;
            return new Attr3D
            {
                position = V(a.position), velocity = V(a.velocity), forward = V(fwd.normalized), up = V(up.normalized),
            };
        }

        static Vec V(Vector3 v) => new() { x = v.x, y = v.y, z = v.z };
    }
}
