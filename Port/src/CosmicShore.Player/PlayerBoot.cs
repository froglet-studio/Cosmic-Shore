using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using System.Reflection;
using CosmicShore.Content;
using CosmicShore.Engine;
using CosmicShore.Engine.SceneManagement;

namespace CosmicShore.Player
{
    /// <summary>
    /// Boots the port the way a Unity player boots: the content runtime over the project's
    /// Assets/ (scripts resolved against the REAL game assembly, CosmicShore.Live), the
    /// Reflex project scopes from Resources/ReflexSettings, then build scene 0 — Bootstrap —
    /// from which the game's own AppManager/SceneLoader drive every later scene.
    /// </summary>
    public sealed class PlayerBoot : IDisposable
    {
        public GameLoop Loop { get; private set; }
        public ContentRuntime Runtime { get; private set; }
        public LogCounter Log { get; } = new();

        public static Assembly GameAssembly => typeof(CosmicShore.Core.AppManager).Assembly;

        static void EnableLogChannels(string list)
        {
            if (string.IsNullOrWhiteSpace(list)) return;
            var type = GameAssembly.GetType("CosmicShore.Utility.CSLogChannel") ?? GameAssembly.GetType("CosmicShore.CSLogChannel");
            var debug = GameAssembly.GetType("CosmicShore.Utility.CSDebug") ?? GameAssembly.GetType("CosmicShore.CSDebug");
            var field = debug?.GetField("VerboseChannels");
            if (type == null || field == null) { Console.WriteLine("[player] log channels: CSDebug not found"); return; }
            long mask = Convert.ToInt64(field.GetValue(null));
            foreach (var name in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (Enum.TryParse(type, name, true, out var v)) mask |= Convert.ToInt64(v);
                else Console.WriteLine($"[player] unknown log channel '{name}'");
            field.SetValue(null, Enum.ToObject(type, mask));
            Console.WriteLine($"[player] verbose log channels: {field.GetValue(null)}");
        }

        static readonly object s_idLock = new();
        static string s_playerId;

        /// <summary>
        /// This install's anonymous player id: minted once and kept, like the real service's device
        /// identity. Read by the game's sign-in and by the online service's connector thread, so the
        /// first read mints it under a lock and every later one returns the same id.
        /// </summary>
        static string AnonymousPlayerId()
        {
            lock (s_idLock)
            {
                if (s_playerId != null) return s_playerId;
                var file = System.IO.Path.Combine(Application.persistentDataPath, "ugs-player-id");
                try
                {
                    if (System.IO.File.Exists(file))
                    {
                        var existing = System.IO.File.ReadAllText(file).Trim();
                        if (existing.Length > 0) return s_playerId = existing;
                    }
                    var id = Guid.NewGuid().ToString("N").Substring(0, 24);
                    System.IO.File.WriteAllText(file, id);
                    return s_playerId = id;
                }
                catch (Exception) { return "local-player"; }
            }
        }

        /// <summary>The online service when networking is on and sessions go through a server (not the shared folder).</summary>
        public CosmicShore.Engine.Networking.OnlineMultiplayerService Online { get; private set; }

        /// <summary>
        /// Picks the lobby + relay backend. COSMIC_SHORE_NET_SERVER (the launcher's SETTINGS > ONLINE),
        /// else the branch's shared server (Port/ProjectSettings/AmoebiusProject.json, Network.Server):
        /// "host[:port]" uses that online server (internet play); "dir" keeps every session in a folder
        /// on this machine (COSMIC_SHORE_NET_DIR, the pre-network backend - a phone build sets the
        /// folder, so it stays on it unless the branch names a server); empty or "auto" finds an
        /// Amoebius server on the LAN, or hosts one in this game.
        /// </summary>
        void InstallSessions()
        {
            var options = CosmicShore.Engine.Networking.OnlineClientOptions.FromEnvironment();
            options.PlayerId = AnonymousPlayerId;
            if (string.IsNullOrWhiteSpace(options.Server) && AssetDatabase.FindProjectRoot() is { } root)
            {
                var network = Amoebius.AmoebiusProjectSettings.Load(root).Network;
                options.Server = network.Server;
                if (string.IsNullOrEmpty(options.Key)) options.Key = network.Key;
            }
            var server = options.Server?.Trim();
            bool folder = string.Equals(server, "dir", StringComparison.OrdinalIgnoreCase)
                || (string.IsNullOrEmpty(server) && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_DIR")));
            if (folder)
            {
                var dir = CosmicShore.Engine.Networking.DirectoryMultiplayerService.DefaultDirectory;
                CosmicShore.Engine.Networking.MultiplayerService.Instance = new CosmicShore.Engine.Networking.DirectoryMultiplayerService(dir);
                Console.WriteLine($"[online] sessions in the folder {dir} (this machine only)");
                return;
            }
            Online = CosmicShore.Engine.Networking.OnlineMultiplayerService.Install(options);
            Console.WriteLine(options.Automatic
                ? "[online] looking for an Amoebius server on the network (this game hosts one if there is none)"
                : $"[online] online server {options.Server}");
        }

        /// <summary>
        /// The session backend (<see cref="InstallSessions"/>), then the network test tools on top of it
        /// (docs/MULTIPLAYER.md §6). With the session folder - the NET page, the party harness and MCP
        /// net_players run on it - the transport is Froglet's UDP unless COSMIC_SHORE_NET_TRANSPORT=tcp,
        /// and COSMIC_SHORE_RELAY sends sessions through a relay (§6.7-6.8). The online server carries
        /// match traffic itself, so those two do not apply to it. The network simulator
        /// (COSMIC_SHORE_NET_SIM, `do netsim`) and the session faults (COSMIC_SHORE_NET_FAULT,
        /// `do netfault`) wrap whichever backend is in place.
        /// </summary>
        void InstallNetwork(string root)
        {
            InstallSessions();
            if (Online == null)
            {
                var transport = CosmicShore.Engine.Networking.NetTransports.Select(Environment.GetEnvironmentVariable("COSMIC_SHORE_NET_TRANSPORT"));
                Console.WriteLine($"[net] transport: {transport}");
                if (UgsSetup.InstallRelay(root) is { } relayLine) Console.WriteLine(relayLine);
            }
            else
            {
                foreach (var name in new[] { "COSMIC_SHORE_NET_TRANSPORT", "COSMIC_SHORE_RELAY" })
                    if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } value)
                        Console.WriteLine($"[net] {name}={value} ignored: the online server carries match traffic (it applies with the session folder)");
            }
            CosmicShore.Engine.Networking.NetSimulator.Install();
            CosmicShore.Engine.Networking.NetFaults.Install();
        }

        public void Start(string firstScene)
        {
            var root = AssetDatabase.FindProjectRoot()
                ?? throw new InvalidOperationException("Unity project not found — run from inside the repository (or set COSMIC_SHORE_PROJECT).");

            Debug.Sink = Log;
            _audio = PlayerAudio.Start(root, Headless);
            CosmicShore.Engine.Networking.NetworkManager.EmulateNetcodeLifecycle = true;
            // A real transport between players (TCP; COSMIC_SHORE_NET=off keeps one process).
            CosmicShore.Engine.Networking.NetDriver.Enabled = Environment.GetEnvironmentVariable("COSMIC_SHORE_NET") != "off";
            CosmicShore.Engine.Services.AuthenticationService.AnonymousIdProvider = AnonymousPlayerId;
            // Sessions (lobby + relay) and the network test tools on top of them (InstallNetwork).
            if (CosmicShore.Engine.Networking.NetDriver.Enabled) InstallNetwork(root);
            // The game's own verbose log channels (CSDebug.VerboseChannels - the Froglet Toolbox
            // Logging tab in the editor): COSMIC_SHORE_LOG_CHANNELS=Party,Boot. Only a Debug
            // (development) build compiles LogVerbose in.
            EnableLogChannels(Environment.GetEnvironmentVariable("COSMIC_SHORE_LOG_CHANNELS"));
            GameObject.EnforceRequireComponent = true;
            // The player's Cloud Save backend: a store that survives relaunches, as UGS does.
            CosmicShore.Engine.Services.CloudSaveService.Instance =
                new CosmicShore.Engine.Services.LocalCloudSaveService(
                    System.IO.Path.Combine(Application.persistentDataPath, "ugs-cloudsave.json"));
            // The player's Friends backend: an account with no friends yet, as a fresh UGS login reads.
            CosmicShore.Engine.Services.Friends.FriendsService.Instance ??=
                new CosmicShore.Engine.Services.Friends.LocalFriendsService();
            EnableVerboseChannels(Environment.GetEnvironmentVariable("CS_VERBOSE"));
            var sw = Stopwatch.StartNew();
            Loop = new GameLoop("Boot");
            Runtime = new ContentRuntime(root, new[] { GameAssembly, typeof(DG.Tweening.DOTween).Assembly });
            Runtime.Install();

            if (NoScene)
            {
                // --view-model: content only - no game code runs, nothing else is in the picture.
                Console.WriteLine($"[player] content ready (no scene) in {sw.ElapsedMilliseconds} ms");
                return;
            }
            // A player build runs RuntimeInitializeOnLoadMethod(BeforeSceneLoad) before the first scene.
            RuntimeInitialize.Run(GameAssembly, RuntimeInitializeLoadType.SubsystemRegistration);
            RuntimeInitialize.Run(GameAssembly, RuntimeInitializeLoadType.AfterAssembliesLoaded);
            RuntimeInitialize.Run(GameAssembly, RuntimeInitializeLoadType.BeforeSplashScreen);
            RuntimeInitialize.Run(GameAssembly, RuntimeInitializeLoadType.BeforeSceneLoad);

            Runtime.BootRootScopes();
            string scene = firstScene ?? (Runtime.BuildScenes.Count > 0 ? System.IO.Path.GetFileNameWithoutExtension(Runtime.BuildScenes[0].path) : "Bootstrap");
            SceneManager.LoadScene(scene);
            RuntimeInitialize.Run(GameAssembly, RuntimeInitializeLoadType.AfterSceneLoad);
            Console.WriteLine($"[player] booted into '{scene}' in {sw.ElapsedMilliseconds} ms");
        }

        /// <summary>Boot content only: no RuntimeInitialize, no root scopes, no first scene (the model viewer).</summary>
        public bool NoScene { get; set; }

        /// <summary>A headless run is silent unless COSMIC_SHORE_AUDIO asks otherwise (Unity's -nographics).</summary>
        public bool Headless { get; set; }

        CosmicShore.Engine.Audio.Fmod.FmodNativeBackend _audio;

        /// <summary>One frame: the game loop, then the audio runtime (after LateUpdate, as FMOD's RuntimeManager runs).</summary>
        public void Tick(float step)
        {
            Loop.Tick(step);
            CosmicShore.Engine.Audio.Fmod.RuntimeManager.Update();
        }

        public void Dispose()
        {
            // Leaves the online server (and stops the one this game hosted, if any).
            Online?.Dispose();
            Online = null;
            Loop?.Dispose();
            PlayerAudio.Stop(_audio);
            _audio = null;
        }

        /// <summary>
        /// CS_VERBOSE=All (or a comma list of CSLogChannel names) switches on the game's own
        /// verbose log channels — the same toggles FrogletTools > Toolbox > Logging flips in the editor.
        /// </summary>
        static void EnableVerboseChannels(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return;
            var debugType = GameAssembly.GetTypes().FirstOrDefault(t => t.Name == "CSDebug");
            var field = debugType?.GetField("VerboseChannels", BindingFlags.Public | BindingFlags.Static);
            if (field == null) return;
            var enumType = field.FieldType;
            long bits = 0;
            foreach (var name in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (name.Equals("All", StringComparison.OrdinalIgnoreCase)) { bits = ~0L; break; }
                if (Enum.TryParse(enumType, name, ignoreCase: true, out var v)) bits |= Convert.ToInt64(v);
            }
            field.SetValue(null, Enum.ToObject(enumType, bits));
        }
    }

    /// <summary>Invokes a game assembly's [RuntimeInitializeOnLoadMethod] hooks of one phase.</summary>
    public static class RuntimeInitialize
    {
        public static void Run(Assembly assembly, RuntimeInitializeLoadType phase)
        {
            foreach (var type in SafeTypes(assembly))
            foreach (var m in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
                if (attr == null || attr.loadType != phase || m.GetParameters().Length != 0) continue;
                try { m.Invoke(null, null); }
                catch (TargetInvocationException e) { Debug.LogException(e.InnerException ?? e); }
            }
        }

        static IEnumerable<Type> SafeTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
        }
    }

    /// <summary>Console sink that also counts and de-duplicates warnings/errors for the run summary.</summary>
    public sealed class LogCounter : ILogSink
    {
        readonly ConsoleLogSink _console = new();
        public int Errors, Warnings, Exceptions;
        public readonly Dictionary<string, int> Unique = new();
        public bool Quiet;

        public void Write(LogType type, string message, CosmicShore.Engine.Object context, Exception exception = null)
        {
            switch (type)
            {
                case LogType.Error: case LogType.Assert: Errors++; break;
                case LogType.Warning: Warnings++; break;
                case LogType.Exception: Exceptions++; break;
            }
            if (type != LogType.Log)
            {
                string site = exception?.StackTrace?.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
                string key = $"{type}: {(exception != null ? exception.GetType().Name + ": " : "")}{message} {site}";
                if (key.Length > 400) key = key[..400];
                Unique.TryGetValue(key, out int n);
                Unique[key] = n + 1;
                if (n > 0) return; // print each distinct problem once
            }
            if (Quiet && type == LogType.Log) return;
            _console.Write(type, message, context, exception);
        }

        public void PrintSummary()
        {
            Console.WriteLine($"[player] log: {Errors} errors, {Exceptions} exceptions, {Warnings} warnings ({Unique.Count} distinct)");
        }
    }
}
