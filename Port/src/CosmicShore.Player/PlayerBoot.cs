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

        public void Start(string firstScene)
        {
            var root = AssetDatabase.FindProjectRoot()
                ?? throw new InvalidOperationException("Unity project not found — run from inside the repository (or set COSMIC_SHORE_PROJECT).");

            Debug.Sink = Log;
            _audio = PlayerAudio.Start(root, Headless);
            CosmicShore.Engine.Networking.NetworkManager.EmulateNetcodeLifecycle = true;
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
