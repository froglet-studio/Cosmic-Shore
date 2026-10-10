using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Editor.Froglet;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CosmicShore.Editor
{
    /// <summary>
    /// FrogletTools > Parity > Capture Goldens: the Unity half of the parity harness
    /// (<c>Port/parity/README.md</c> § The Unity PR). For every case in
    /// <c>Port/parity/manifest.json</c> it enters Play mode on the replay's scene with the
    /// deterministic session and the replay armed through the same environment hook a player
    /// build or Prisma uses (<see cref="DeterministicSession"/>), plays the replay's <c>do</c>
    /// stream frame by frame (device verbs through Input System state events, the inspector
    /// verb <c>arcade</c> through the game API Prisma's Inspector calls) and lets
    /// <see cref="ParityProbe"/> write <c>Port/parity/goldens/&lt;case&gt;/</c>. Before the first
    /// case it writes <c>goldens/random/random_&lt;seed&gt;.json</c> for every manifest seed.
    ///
    /// <para>READER-style tool: its output lands OUTSIDE <c>Assets/</c> (under <c>Port/parity/</c>,
    /// ordinary committable project data the Port side owns), it touches no scene, prefab or SO,
    /// so it records nothing in <c>FrogletToolChangeLedger</c> and draws no ship panel.</para>
    ///
    /// <para>CLI: <see cref="CaptureAll"/> is the public static entry the <c>unity</c> binary's
    /// command surface would call. The <c>[CliCommand]</c> attribute from <c>com.unity.pipeline</c>
    /// is deliberately not on it yet: the package is experimental, this checkout's offline
    /// reference compile cannot resolve it, and CLAUDE.md records that no wrapper exists in the
    /// repo to copy the attribute's shape from. Adding it is one attribute on this method once an
    /// editor proves it compiles; nothing else changes.</para>
    ///
    /// <para>Case runs survive the domain reload Play mode causes: the queue lives in
    /// <see cref="SessionState"/> and <see cref="Hook"/> re-arms the driver after every reload.
    /// Keep the editor focused while it runs: InputController skips unfocused frames on Windows.</para>
    /// </summary>
    public static class ParityCapture
    {
        const string QueueKey = "CosmicShore.ParityCapture.Queue";
        const string KeyboardName = "ParityCaptureKeyboard";
        const string MouseName = "ParityCaptureMouse";
        const int BootGraceFrames = 600;

        [Serializable] class Manifest { public int[] seeds = Array.Empty<int>(); public ManifestCase[] cases = Array.Empty<ManifestCase>(); }
        [Serializable] class ManifestCase { public string name; public string replay; }
        [Serializable] class Queue { public string parityDir; public ManifestCase[] cases = Array.Empty<ManifestCase>(); public int index; public int failures; }

        static ReplayFile s_replay;
        static string s_caseName;
        static readonly SortedDictionary<int, List<string>> s_steps = new();
        static readonly List<(int frame, Action release)> s_releases = new();
        static readonly HashSet<Key> s_heldKeys = new();
        static Keyboard s_keyboard;
        static Mouse s_mouse;
        static Vector2 s_mousePosition;
        static bool s_mouseLeft;
        static int s_idleFrames;

        public static string ParityDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Port", "parity"));

        [MenuItem("FrogletTools/Parity/Capture Goldens")]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 3,
            Description = "Play every Port/parity manifest case in Play mode and write the Unity goldens the engine parity diff compares against.",
            DocPath = "Assets/_Scripts/Utility/Replay/README.md")]
        public static void CaptureGoldensMenu() => CaptureAll();

        /// <summary>
        /// Writes the random goldens, then queues every manifest case and starts the first. The
        /// public static entry a CLI wrapper calls; returns false when nothing could be started.
        /// </summary>
        public static bool CaptureAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                CSDebug.LogWarning("[ParityCapture] leave Play mode first");
                return false;
            }
            string parityDir = ParityDirectory;
            string manifestPath = Path.Combine(parityDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                CSDebug.LogError($"[ParityCapture] no manifest at {manifestPath}");
                return false;
            }
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath)) ?? new Manifest();

            string randomDir = Path.Combine(parityDir, "goldens", "random");
            foreach (int seed in manifest.seeds ?? Array.Empty<int>())
                ParityProbe.WriteRandomGolden(randomDir, seed);
            CSDebug.Log($"[ParityCapture] random goldens for {manifest.seeds?.Length ?? 0} seed(s) -> {randomDir}");

            var queue = new Queue { parityDir = parityDir, cases = manifest.cases ?? Array.Empty<ManifestCase>() };
            SessionState.SetString(QueueKey, JsonUtility.ToJson(queue));
            StartNextCase();
            return true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        static Queue LoadQueue()
        {
            string json = SessionState.GetString(QueueKey, "");
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Queue>(json);
        }

        static void SaveQueue(Queue queue) => SessionState.SetString(QueueKey, JsonUtility.ToJson(queue));

        static void StartNextCase()
        {
            var queue = LoadQueue();
            if (queue == null) return;
            if (queue.index >= queue.cases.Length)
            {
                Finish(queue);
                return;
            }
            var entry = queue.cases[queue.index];
            string replayPath = Path.Combine(queue.parityDir, entry.replay ?? "");
            if (!File.Exists(replayPath))
            {
                CSDebug.LogError($"[ParityCapture] case '{entry.name}': replay '{replayPath}' not found; skipped");
                queue.index++; queue.failures++;
                SaveQueue(queue);
                StartNextCase();
                return;
            }
            var replay = ReplayFile.Load(replayPath);
            string outDir = Path.Combine(queue.parityDir, "goldens", entry.name);
            Directory.CreateDirectory(outDir);

            var sceneAsset = FindSceneAsset(replay.scene);
            if (sceneAsset == null)
            {
                CSDebug.LogError($"[ParityCapture] case '{entry.name}': scene '{replay.scene}' is not in the build settings; skipped");
                queue.index++; queue.failures++;
                SaveQueue(queue);
                StartNextCase();
                return;
            }

            Environment.SetEnvironmentVariable(DeterministicSession.ReplayEnvironmentVariable, replayPath);
            Environment.SetEnvironmentVariable(DeterministicSession.ParityOutEnvironmentVariable, outDir);
            EditorSceneManager.playModeStartScene = sceneAsset;
            CSDebug.Log($"[ParityCapture] case {queue.index + 1}/{queue.cases.Length} '{entry.name}': {replay.frames} frames from {replay.scene}, seed {replay.seed} -> {outDir}");
            EditorApplication.EnterPlaymode();
        }

        static void Finish(Queue queue)
        {
            SessionState.EraseString(QueueKey);
            ClearEnvironment();
            CSDebug.Log($"[ParityCapture] done: {queue.cases.Length - queue.failures}/{queue.cases.Length} case(s) written under {Path.Combine(queue.parityDir, "goldens")}");
        }

        static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(DeterministicSession.ReplayEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(DeterministicSession.ParityOutEnvironmentVariable, null);
            EditorSceneManager.playModeStartScene = null;
        }

        static SceneAsset FindSceneAsset(string sceneName)
        {
            foreach (var scene in EditorBuildSettings.scenes)
                if (Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                    return AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            foreach (var guid in AssetDatabase.FindAssets("t:SceneAsset " + sceneName))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                    return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            }
            return null;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            var queue = LoadQueue();
            if (queue == null) return;
            ResetCaseState();
            queue.index++;
            SaveQueue(queue);
            EditorApplication.delayCall += StartNextCase;
        }

        // ── The in-play driver ─────────────────────────────────────────────────────────

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            var queue = LoadQueue();
            if (queue == null || queue.index >= queue.cases.Length) return;
            if (s_replay == null && !LoadCase(queue)) return;

            if (!ParityProbe.Active)
            {
                // The environment hook arms the probe before the first scene; a run that never
                // gets there is reported, not waited on forever.
                if (++s_idleFrames > BootGraceFrames)
                {
                    CSDebug.LogError($"[ParityCapture] case '{s_caseName}': ParityProbe never began (is {DeterministicSession.ParityOutEnvironmentVariable} readable by the hook?); aborting");
                    queue.failures++;
                    SaveQueue(queue);
                    EditorApplication.ExitPlaymode();
                }
                return;
            }

            int frame = ParityProbe.Frame;
            for (int i = s_releases.Count - 1; i >= 0; i--)
                if (s_releases[i].frame <= frame) { s_releases[i].release(); s_releases.RemoveAt(i); }

            while (s_steps.Count > 0)
            {
                int due = -1;
                foreach (var key in s_steps.Keys) { due = key; break; }
                if (due > frame) break;
                var list = s_steps[due];
                s_steps.Remove(due);
                foreach (var step in list) Run(step, frame);
            }

            if (frame >= s_replay.frames)
            {
                CSDebug.Log($"[ParityCapture] case '{s_caseName}' complete at frame {frame}");
                EditorApplication.ExitPlaymode();
            }
        }

        static bool LoadCase(Queue queue)
        {
            var entry = queue.cases[queue.index];
            string replayPath = Path.Combine(queue.parityDir, entry.replay ?? "");
            if (!File.Exists(replayPath)) return false;
            s_replay = ReplayFile.Load(replayPath);
            s_caseName = entry.name;
            s_steps.Clear();
            foreach (var spec in s_replay.Do)
            {
                int colon = spec.IndexOf(':');
                if (colon <= 0 || !int.TryParse(spec.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out int at))
                {
                    CSDebug.LogWarning($"[ParityCapture] do step '{spec}' is not FRAME:ACTION; skipped");
                    continue;
                }
                if (!s_steps.TryGetValue(at, out var list)) s_steps[at] = list = new List<string>();
                list.Add(spec.Substring(colon + 1).Trim());
            }
            s_idleFrames = 0;
            if (s_steps.Count > 0) EnsureDevices();
            return true;
        }

        static void ResetCaseState()
        {
            s_replay = null;
            s_caseName = null;
            s_steps.Clear();
            s_releases.Clear();
            s_heldKeys.Clear();
            s_mouseLeft = false;
            RemoveDevice(KeyboardName);
            RemoveDevice(MouseName);
            s_keyboard = null;
            s_mouse = null;
        }

        /// <summary>One step of the do stream, in Prisma's InputScript grammar (verbs it cannot map are named).</summary>
        static void Run(string step, int frame)
        {
            int sp = step.IndexOf(' ');
            string verb = sp < 0 ? step : step.Substring(0, sp);
            string arg = sp < 0 ? "" : step.Substring(sp + 1).Trim();
            switch (verb)
            {
                case "arcade": Arcade(arg); break;
                case "key": TapKey(ParseKey(arg), frame, 2); break;
                case "hold":
                {
                    var parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    TapKey(ParseKey(parts[0]), frame, parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 2);
                    break;
                }
                case "click":
                    s_mousePosition = Point(arg);
                    s_mouseLeft = true;
                    SyncMouse();
                    s_releases.Add((frame + 2, () => { s_mouseLeft = false; SyncMouse(); }));
                    break;
                case "move":
                    s_mousePosition = Point(arg);
                    SyncMouse();
                    break;
                case "type":
                    foreach (char c in arg) InputSystem.QueueTextEvent(s_keyboard, c);
                    break;
                default:
                    // pad, score, domain, party, timescale and the engine's inspection verbs have
                    // no game-side equivalent here; the report lists them.
                    CSDebug.LogWarning($"[ParityCapture] frame {frame}: do verb '{verb}' is not played in Unity; skipped");
                    break;
            }
        }

        /// <summary>The arcade inspector verb, through the same game API Prisma's Inspector.Arcade calls.</summary>
        static void Arcade(string arg)
        {
            if (arg == "start")
            {
                foreach (var modal in UnityEngine.Object.FindObjectsByType<ArcadeGameConfigureModal>(FindObjectsSortMode.None))
                    if (modal.isActiveAndEnabled) modal.OnStartGameClicked();
                return;
            }
            if (arg.StartsWith("intensity ", StringComparison.Ordinal)
                && int.TryParse(arg.Substring(10).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int intensity))
            {
                // The modal's intensity-button handler is private; Prisma reaches it the same way.
                var select = typeof(ArcadeGameConfigureModal).GetMethod("HandleIntensitySelected",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var modal in UnityEngine.Object.FindObjectsByType<ArcadeGameConfigureModal>(FindObjectsSortMode.None))
                    if (modal.isActiveAndEnabled) select?.Invoke(modal, new object[] { intensity });
                return;
            }
            if (arg == "ready")
            {
                foreach (var controller in UnityEngine.Object.FindObjectsByType<MiniGameControllerBase>(FindObjectsSortMode.None))
                    controller.OnReadyClicked();
                return;
            }
            if (!Enum.TryParse(arg, true, out GameModes mode))
            {
                CSDebug.LogWarning($"[ParityCapture] arcade: no mode '{arg}'");
                return;
            }
            foreach (var view in UnityEngine.Object.FindObjectsByType<ArcadeExploreView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var game = view.FindGameByMode(mode);
                if (game == null) continue;
                view.SelectGame(game);
                return;
            }
            CSDebug.LogWarning($"[ParityCapture] arcade: no card for {mode}");
        }

        // ── Input System injection ─────────────────────────────────────────────────────

        static void EnsureDevices()
        {
            s_keyboard ??= FindDevice<Keyboard>(KeyboardName) ?? InputSystem.AddDevice<Keyboard>(KeyboardName);
            s_mouse ??= FindDevice<Mouse>(MouseName) ?? InputSystem.AddDevice<Mouse>(MouseName);
        }

        static T FindDevice<T>(string name) where T : InputDevice
        {
            foreach (var device in InputSystem.devices)
                if (device is T match && device.name == name) return match;
            return null;
        }

        static void RemoveDevice(string name)
        {
            var device = FindDevice<InputDevice>(name);
            if (device != null) InputSystem.RemoveDevice(device);
        }

        static Key ParseKey(string name) => (Key)Enum.Parse(typeof(Key), name, true);

        static void TapKey(Key key, int frame, int frames)
        {
            EnsureDevices();
            s_heldKeys.Add(key);
            SyncKeyboard();
            s_releases.Add((frame + frames, () => { s_heldKeys.Remove(key); SyncKeyboard(); }));
        }

        static void SyncKeyboard()
        {
            if (s_keyboard == null) return;
            var state = new KeyboardState();
            foreach (var key in s_heldKeys) state.Set(key, true);
            InputSystem.QueueStateEvent(s_keyboard, state);
        }

        static void SyncMouse()
        {
            EnsureDevices();
            var state = new MouseState { position = s_mousePosition }.WithButton(MouseButton.Left, s_mouseLeft);
            InputSystem.QueueStateEvent(s_mouse, state);
        }

        /// <summary>Screenshot pixels, top-left origin (the engine's convention) to Unity's bottom-left mouse space.</summary>
        static Vector2 Point(string arg)
        {
            var parts = arg.Split(',');
            float x = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
            float y = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
            return new Vector2(x, Screen.height - y);
        }
    }
}
