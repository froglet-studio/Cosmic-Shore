using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CosmicShore.Engine.Audio.Fmod;

namespace CosmicShore.Player
{
    /// <summary>
    /// Brings up real audio: the FMOD Studio runtime the project ships (fetched out of Git LFS by
    /// Port/tools/fetch_native.py), the banks the project's FMOD Studio build wrote, and the header
    /// version the project's FMOD integration was built against — all read from the project, so
    /// nothing here is a copy of anything it could drift from.
    ///
    /// COSMIC_SHORE_AUDIO: unset = the default output (falling back to no-sound where there is no
    /// device), "off" = no runtime at all (the silent local-state model), "wav:PATH" = record the
    /// mix to a WAV file (FMOD's WAV writer) — how a machine with no speakers verifies it, "nrt" =
    /// no output, mixed only when the engine ticks (FMOD's non-real-time no-sound output with a
    /// synchronous Studio update): the parity harness's mode, deterministic and device-free, with
    /// every event description (one-shot, snapshot, length) answered by the banks.
    ///
    /// The build's GUIDs.txt is read in every mode, so a GUID-only EventReference has its path.
    /// </summary>
    static class PlayerAudio
    {
        static string s_libraryPath;

        /// <summary>What answered the FMOD calls this run: "native" (the runtime and banks) or "silent" (the local-state model).</summary>
        public static string Mode { get; private set; } = "silent";

        public static FmodNativeBackend Start(string projectRoot, bool headless)
        {
            string banks = BankDirectory(projectRoot);
            FmodGuids.Load(Path.Combine(Path.GetDirectoryName(banks) ?? string.Empty, "GUIDs.txt"));
            LoadStringsBank(banks);
            var backend = StartRuntime(projectRoot, headless);
            Mode = backend == null ? "silent" : "native";
            return backend;
        }

        /// <summary>
        /// The strings bank's GUID index is what the loaded banks carry (FmodGuids.BankCarries):
        /// read in every mode, so the silent model refuses a stale EventReference as the runtime
        /// would. GUIDs.txt is the cross-check: an index that misses most of the file's entries
        /// was not read correctly (a format change) and is dropped, with a line saying so, rather
        /// than silencing the game.
        /// </summary>
        static void LoadStringsBank(string bankDirectory)
        {
            if (!Directory.Exists(bankDirectory)) return;
            foreach (var file in Directory.GetFiles(bankDirectory, "*.strings.bank"))
            {
                if (FmodGuids.LoadStringsBank(file) == 0) { Console.Error.WriteLine($"[fmod] no GUID index found in {Path.GetFileName(file)}; stale event references are not detected"); continue; }
                var (listed, carried) = FmodGuids.StringsBankCoverage();
                if (listed > 0 && carried * 2 < listed)
                {
                    Console.Error.WriteLine($"[fmod] {Path.GetFileName(file)} indexes {carried} of GUIDs.txt's {listed} entries; the index was not read correctly and is ignored");
                    FmodGuids.ClearStringsBank();
                }
                return;
            }
        }

        static FmodNativeBackend StartRuntime(string projectRoot, bool headless)
        {
            string mode = Environment.GetEnvironmentVariable("COSMIC_SHORE_AUDIO");
            if (string.Equals(mode, "off", StringComparison.OrdinalIgnoreCase)) return null;
            if (headless && string.IsNullOrEmpty(mode)) return null;   // -nographics is silent by default too

            s_libraryPath = FindLibrary(projectRoot);
            if (s_libraryPath == null)
            {
                Console.Error.WriteLine("[fmod] FMOD Studio runtime not found — audio is silent. Run: python3 Port/tools/fetch_native.py");
                return null;
            }
            NativeLibrary.SetDllImportResolver(typeof(FmodNativeBackend).Assembly, Resolve);

            uint version = HeaderVersion(projectRoot);
            string banks = BankDirectory(projectRoot);
            int output = FmodNativeBackend.OutputAutodetect;
            string wav = null;
            if (mode != null && mode.StartsWith("wav:", StringComparison.OrdinalIgnoreCase))
            {
                output = FmodNativeBackend.OutputWavWriter;
                wav = Path.GetFullPath(mode.Substring(4));
            }
            else if (string.Equals(mode, "nrt", StringComparison.OrdinalIgnoreCase)) output = FmodNativeBackend.OutputNoSoundNrt;
            var backend = FmodNativeBackend.TryCreate(version, banks, output, wav);
            if (backend == null) return null;
            FmodBackend.Current = backend;
            string outName = backend.Output switch
            {
                FmodNativeBackend.OutputNoSound => "no-sound (no audio device)",
                FmodNativeBackend.OutputWavWriter => "WAV writer → " + wav,
                FmodNativeBackend.OutputNoSoundNrt => "no-sound, non-real-time (parity)",
                _ => "default device",
            };
            Console.WriteLine($"[fmod] FMOD Studio 0x{version:X8} up, output {outName}, banks: {string.Join(", ", backend.LoadedBanks)}");
            return backend;
        }

        public static void Stop(FmodNativeBackend backend)
        {
            if (backend == null) return;
            var started = RuntimeManager.StartedByPath;
            Console.WriteLine($"[fmod] {RuntimeManager.StartedTotal} event start(s), {started.Count} distinct:");
            foreach (var kv in System.Linq.Enumerable.OrderByDescending(started, kv => kv.Value))
                Console.WriteLine($"[fmod]   {kv.Value,5}  {kv.Key}");
            if (ReferenceEquals(FmodBackend.Current, backend)) FmodBackend.Current = null;
            backend.Dispose();
        }

        /// <summary>The FMOD runtime is linked INTO the app (iOS: static libraries, as Unity links them).</summary>
        const string MainProgram = "<main program>";

        static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? path)
        {
            if (name != FmodNativeBackend.LibraryName || s_libraryPath == null) return IntPtr.Zero;
            if (s_libraryPath == MainProgram) return NativeLibrary.GetMainProgramHandle();
            return NativeLibrary.Load(s_libraryPath);
        }

        static string FindLibrary(string projectRoot)
        {
            // Mobile: the platform build packs the runtime with the app (Android: lib/<abi>/ in the
            // APK, which the loader finds by file name; iOS: linked into the executable).
            if (OperatingSystem.IsIOS() || OperatingSystem.IsTvOS()) return MainProgram;
            if (OperatingSystem.IsAndroid())
                return NativeLibrary.TryLoad("libfmodstudio.so", out var handle) && handle != IntPtr.Zero ? "libfmodstudio.so" : null;

            bool win = OperatingSystem.IsWindows();
            string file = win ? "fmodstudio.dll" : "libfmodstudio.so";
            string rid = win ? "win-x64" : "linux-x64";
            var candidates = new[]
            {
                Environment.GetEnvironmentVariable("COSMIC_SHORE_FMOD_LIB"),
                Path.Combine(AppContext.BaseDirectory, file),
                Path.Combine(projectRoot, "Port", ".native", rid, file),
                Path.Combine(projectRoot, "Assets", "Plugins", "FMOD", "platforms", win ? "win" : "linux", "lib", "x86_64", file),
            };
            foreach (var c in candidates)
                if (!string.IsNullOrEmpty(c) && File.Exists(c) && new FileInfo(c).Length > 4096) // not an LFS pointer
                    return c;
            return null;
        }

        /// <summary>The FMOD header version the project's integration declares (FMOD.VERSION.number).</summary>
        static uint HeaderVersion(string projectRoot)
        {
            // Packaged player data carries no source: the build recorded the version.
            var manifest = Path.Combine(projectRoot, "PlayerData.json");
            if (File.Exists(manifest))
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
                    if (doc.RootElement.TryGetProperty("fmodHeaderVersion", out var v) && v.TryGetUInt32(out var n) && n != 0) return n;
                }
                catch (System.Text.Json.JsonException) { }

            var src = Path.Combine(projectRoot, "Assets", "Plugins", "FMOD", "src", "fmod.cs");
            if (File.Exists(src))
            {
                var m = Regex.Match(File.ReadAllText(src), @"number\s*=\s*0x([0-9A-Fa-f]{8})");
                if (m.Success) return Convert.ToUInt32(m.Groups[1].Value, 16);
            }
            return 0x00020313;
        }

        /// <summary>The FMOD Studio build output the settings asset names (sourceBankPath) for the desktop platform.</summary>
        static string BankDirectory(string projectRoot)
        {
            string rel = "Cosmic Shore/Build";
            var settings = Path.Combine(projectRoot, "Assets", "Plugins", "FMOD", "Resources", "FMODStudioSettings.asset");
            if (File.Exists(settings))
            {
                var m = Regex.Match(File.ReadAllText(settings), @"(?m)^\s*sourceBankPath:\s*(.+?)\s*$");
                if (m.Success && m.Groups[1].Value.Length > 0) rel = m.Groups[1].Value;
            }
            return Path.Combine(projectRoot, rel, "Desktop");
        }
    }
}
