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
    ///
    /// <para>Every way out of a case leaves the editor clean: the driver ends a case by exiting
    /// Play itself (it reached the replay's frame count, or it aborted), and only then does the
    /// next case start. Pressing Stop mid-case, or <c>FrogletTools > Parity > Cancel Capture</c>,
    /// cancels the whole run instead; the interrupted case's <c>run.json</c> (the probe's
    /// finished-run marker) is removed and the console names the partial directory. A case whose
    /// replay will not load, or whose scene is not in the build, is counted as failed and
    /// skipped. Each exit runs <see cref="DeterministicSession.Reset"/> and clears the two
    /// environment variables, so a normal Play afterwards gets the devices back.</para>
    ///
    /// <para>Three Play-mode entries record and replay by hand, outside any capture:
    /// <c>Record Input</c>, <c>Replay Recording</c> (saves under the gitignored
    /// <c>Logs/Parity/</c> and plays the saved file back) and <c>Stop Replay</c>.</para>
    /// </summary>
    public static class ParityCapture
    {
        const string QueueKey = "CosmicShore.ParityCapture.Queue";
        const string KeyboardName = "ParityCaptureKeyboard";
        const string MouseName = "ParityCaptureMouse";
        const int BootGraceFrames = 600;

        [Serializable] class Manifest { public int[] seeds = Array.Empty<int>(); public ManifestCase[] cases = Array.Empty<ManifestCase>(); }
        [Serializable] class ManifestCase { public string name; public string replay; }
        // ended: the driver itself exited Play for the current case (complete or aborted). Kept in
        // the queue, not a static, so it survives a domain reload like the rest of the run.
        [Serializable] class Queue { public string parityDir; public ManifestCase[] cases = Array.Empty<ManifestCase>(); public int index; public int failures; public bool ended; }

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
        static bool s_caseFaulted;
        static bool s_probeSeen;

        public static string ParityDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Port", "parity"));

        [MenuItem("FrogletTools/Parity/Capture Goldens")]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 3,
            Description = "Play every Port/parity manifest case in Play mode and write the Unity goldens the engine parity diff compares against.",
            DocPath = "Assets/_Scripts/Utility/Replay/README.md")]
        public static void CaptureGoldensMenu() => CaptureAll();

        [MenuItem("FrogletTools/Parity/Cancel Capture")]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 2,
            Description = "Stop a Capture Goldens run: clear its queue and environment, leave Play mode and hand input back to the devices.",
            DocPath = "Assets/_Scripts/Utility/Replay/README.md")]
        public static void CancelCaptureMenu() => Cancel();

        /// <summary>
        /// Cancels the queued run, wherever it is: in a case's Play session, between cases, or
        /// stuck after a failure. Safe with nothing queued; it still clears the environment
        /// variables and the replay statics. Returns true when a run was cancelled.
        /// </summary>
        public static bool Cancel()
        {
            var queue = LoadQueue();
            // Erase the queue first, so the EnteredEditMode that leaving Play causes, and a
            // delayCall already waiting to start the next case, both find nothing to run.
            SessionState.EraseString(QueueKey);
            bool playing = EditorApplication.isPlaying;
            // A case the driver has already ended (its exit is pending) finished; leave it be.
            if (queue != null && playing && !queue.ended && queue.index < queue.cases.Length && (s_probeSeen || ParityProbe.Active))
            {
                // Close the channels now, so the run.json the probe writes on close is the one
                // MarkPartial removes rather than one written after it.
                ParityProbe.End();
                MarkPartial(queue);
            }
            ResetCaseState();
            DeterministicSession.Reset();
            ClearEnvironment();
            if (playing) EditorApplication.ExitPlaymode();
            if (queue == null)
            {
                CSDebug.Log("[ParityCapture] no capture was queued; environment and replay state cleared");
                return false;
            }
            CSDebug.LogWarning($"[ParityCapture] cancelled at case {Math.Min(queue.index + 1, queue.cases.Length)}/{queue.cases.Length}; cases after it were not captured");
            return true;
        }

        // ── Record and play back by hand (Play mode) ───────────────────────────────────
        // A tester records the local pilot's input and plays it straight back from the saved
        // file, to see a replay drive a hull in the editor (a single-stick hull turns only if the
        // replay carries its stick vectors). The file lands under the gitignored Logs/Parity/,
        // never Assets/, so these entries record nothing in the tool ledger either. Outside Play,
        // and while a capture is queued, the menu greys them out and each one refuses on its own
        // too, because the Froglet board falls back to a direct call when a validate says no.

        const string RecordInputMenuPath = "FrogletTools/Parity/Record Input";
        const string ReplayRecordingMenuPath = "FrogletTools/Parity/Replay Recording";
        const string StopReplayMenuPath = "FrogletTools/Parity/Stop Replay";

        /// <summary>Where <see cref="ReplayRecording"/> saves (gitignored, outside <c>Assets/</c>).</summary>
        public static string RecordingDirectory => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Parity"));

        static bool CanRecordInput => HandReplayAllowed && !ReplayRecorder.Recording && !ReplayPlayer.Active;
        static bool CanReplayRecording => HandReplayAllowed && ReplayRecorder.Recording;
        static bool CanStopReplay => HandReplayAllowed && ReplayPlayer.Active;
        static bool HandReplayAllowed => EditorApplication.isPlaying && LoadQueue() == null;

        [MenuItem(RecordInputMenuPath)]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 1,
            Description = "Play mode: record the local pilot's input (the IInputStatus floats, stick vectors and button edges) as a parity replay. Replay Recording plays it back.",
            DocPath = "Assets/_Scripts/Utility/Replay/README.md")]
        public static void RecordInputMenu()
        {
            if (!CanRecordInput)
            {
                CSDebug.LogWarning("[ParityCapture] Record Input needs Play mode with no capture queued, no recording running and no replay playing");
                return;
            }
            ReplayRecorder.Start();
            CSDebug.Log("[ParityCapture] recording input; FrogletTools > Parity > Replay Recording saves it and plays it back");
        }

        [MenuItem(RecordInputMenuPath, true)]
        static bool RecordInputMenuValidate() => CanRecordInput;

        [MenuItem(ReplayRecordingMenuPath)]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 1,
            Description = "Play mode: stop the Record Input recording, save it under Logs/Parity/ and replay it from that file. The replay owns input until Stop Replay or the end of Play.",
            DocPath = "Assets/_Scripts/Utility/Replay/README.md")]
        public static void ReplayRecordingMenu()
        {
            if (!CanReplayRecording)
            {
                CSDebug.LogWarning("[ParityCapture] Replay Recording needs Play mode with no capture queued and a Record Input recording running");
                return;
            }
            ReplayRecording();
        }

        [MenuItem(ReplayRecordingMenuPath, true)]
        static bool ReplayRecordingMenuValidate() => CanReplayRecording;

        [MenuItem(StopReplayMenuPath)]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 1,
            Description = "Play mode: stop a replay started by Replay Recording and hand input back to the devices.",
            DocPath = "Assets/_Scripts/Utility/Replay/README.md")]
        public static void StopReplayMenu()
        {
            if (!CanStopReplay)
            {
                CSDebug.LogWarning("[ParityCapture] Stop Replay needs Play mode with no capture queued and a replay playing");
                return;
            }
            ReplayPlayer.Stop();
            CSDebug.Log("[ParityCapture] replay stopped; input is back on the devices");
        }

        [MenuItem(StopReplayMenuPath, true)]
        static bool StopReplayMenuValidate() => CanStopReplay;

        /// <summary>
        /// Stops the running recording, saves it under <see cref="RecordingDirectory"/> and plays
        /// it back through <see cref="ReplayPlayer.StartFromFile"/>, so the saved file is what
        /// drives the vessel. Returns the saved path, or null when nothing was recording or no
        /// frame was recorded.
        /// </summary>
        public static string ReplayRecording()
        {
            string path = Path.Combine(RecordingDirectory, $"recording-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var file = ReplayRecorder.StopAndSave(path);
            if (file == null || file.Status.Length == 0)
            {
                CSDebug.LogWarning(file == null
                    ? "[ParityCapture] nothing was recording; nothing to replay"
                    : "[ParityCapture] no input frame was recorded (no local pilot's InputController ran while recording); nothing to replay");
                return null;
            }
            ReplayPlayer.StartFromFile(path);
            CSDebug.Log($"[ParityCapture] replaying {file.Status.Length} recorded frame(s) from {path}; the last frame is held until Stop Replay or the end of Play");
            return path;
        }

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

        /// <summary>
        /// Starts the queue's current case, skipping (and counting as failed) every case that
        /// cannot start: a missing replay, a replay that does not parse, a scene not in the build.
        /// A loop, not recursion, and nothing it throws escapes into the delayCall that runs it,
        /// so one bad case can neither leave the queue stuck nor leave the last case's
        /// environment armed for the developer's next Play.
        /// </summary>
        static void StartNextCase()
        {
            var queue = LoadQueue();
            if (queue == null) return;
            while (queue.index < queue.cases.Length)
            {
                var entry = queue.cases[queue.index];
                try
                {
                    if (ArmCase(queue, entry))
                    {
                        queue.ended = false;
                        SaveQueue(queue);
                        EditorApplication.EnterPlaymode();
                        return;
                    }
                }
                catch (Exception e)
                {
                    CSDebug.LogError($"[ParityCapture] case '{entry.name}': {e.GetType().Name}: {e.Message}; skipped");
                }
                ClearEnvironment();
                queue.index++; queue.failures++;
                SaveQueue(queue);
            }
            Finish(queue);
        }

        /// <summary>Loads the case's replay and arms the environment hook and start scene; false (with the reason logged) when the case cannot run.</summary>
        static bool ArmCase(Queue queue, ManifestCase entry)
        {
            string replayPath = Path.Combine(queue.parityDir, entry.replay ?? "");
            if (!File.Exists(replayPath))
            {
                CSDebug.LogError($"[ParityCapture] case '{entry.name}': replay '{replayPath}' not found; skipped");
                return false;
            }
            var replay = ReplayFile.Load(replayPath);
            var sceneAsset = FindSceneAsset(replay.scene);
            if (sceneAsset == null)
            {
                CSDebug.LogError($"[ParityCapture] case '{entry.name}': scene '{replay.scene}' is not in the build settings; skipped");
                return false;
            }
            string outDir = CaseOutputDirectory(queue, entry);
            Directory.CreateDirectory(outDir);

            Environment.SetEnvironmentVariable(DeterministicSession.ReplayEnvironmentVariable, replayPath);
            Environment.SetEnvironmentVariable(DeterministicSession.ParityOutEnvironmentVariable, outDir);
            EditorSceneManager.playModeStartScene = sceneAsset;
            CSDebug.Log($"[ParityCapture] case {queue.index + 1}/{queue.cases.Length} '{entry.name}': {replay.frames} frames from {replay.scene}, seed {replay.seed} -> {outDir}");
            return true;
        }

        static string CaseOutputDirectory(Queue queue, ManifestCase entry) =>
            Path.Combine(queue.parityDir, "goldens", entry.name ?? "");

        static void Finish(Queue queue)
        {
            SessionState.EraseString(QueueKey);
            DeterministicSession.Reset();
            ClearEnvironment();
            CSDebug.Log($"[ParityCapture] done: {queue.cases.Length - queue.failures}/{queue.cases.Length} case(s) written under {Path.Combine(queue.parityDir, "goldens")}");
        }

        static void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable(DeterministicSession.ReplayEnvironmentVariable, null);
            Environment.SetEnvironmentVariable(DeterministicSession.ParityOutEnvironmentVariable, null);
            EditorSceneManager.playModeStartScene = null;
        }

        /// <summary>
        /// The current case was interrupted: its channels hold part of a run, and the probe wrote
        /// <c>run.json</c> when it closed them. That file is the mark of a run the probe finished,
        /// so removing it leaves the directory reading as unfinished. engine_parity does not look
        /// for it and would still diff the partial channels, which is why the warning says to
        /// recapture or restore the case before committing.
        /// </summary>
        static void MarkPartial(Queue queue)
        {
            var entry = queue.cases[queue.index];
            string outDir = CaseOutputDirectory(queue, entry);
            string runJson = Path.Combine(outDir, "run.json");
            if (File.Exists(runJson)) File.Delete(runJson);
            CSDebug.LogWarning($"[ParityCapture] case '{entry.name}' was interrupted; {outDir} holds a partial run (run.json removed). Recapture it, or restore the committed goldens with git checkout.");
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
            bool faulted = s_caseFaulted, probeSeen = s_probeSeen;
            ResetCaseState();
            // The case's replay and session must not reach the next case, or a normal Play.
            DeterministicSession.Reset();
            if (!queue.ended)
            {
                // Play was stopped by hand, not by the driver: that cancels the run. Only a case
                // whose probe began has touched its goldens; one stopped earlier left them as
                // they were. Every case from this one on counts as not written.
                if (queue.index < queue.cases.Length && probeSeen) MarkPartial(queue);
                CSDebug.LogWarning("[ParityCapture] Play was stopped before the case finished; capture cancelled");
                int written = queue.index - (queue.failures - (faulted ? 1 : 0));
                queue.failures = queue.cases.Length - written;
                Finish(queue);
                return;
            }
            queue.index++;
            queue.ended = false;
            SaveQueue(queue);
            EditorApplication.delayCall += StartNextCase;
        }

        /// <summary>The driver ends the current case: records why in the queue, then leaves Play.</summary>
        static void EndCase(Queue queue, bool failed)
        {
            if (failed && !s_caseFaulted) queue.failures++;
            s_caseFaulted |= failed;
            queue.ended = true;
            SaveQueue(queue);
            EditorApplication.ExitPlaymode();
        }

        /// <summary>A case that ran but hit a fault (a do step that threw) still finishes, and is counted once as failed.</summary>
        static void MarkCaseFaulted(Queue queue)
        {
            if (s_caseFaulted) return;
            s_caseFaulted = true;
            queue.failures++;
            SaveQueue(queue);
        }

        // ── The in-play driver ─────────────────────────────────────────────────────────

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            var queue = LoadQueue();
            if (queue == null || queue.ended || queue.index >= queue.cases.Length) return;
            if (s_replay == null)
            {
                string loadError;
                try { loadError = LoadCase(queue); }
                catch (Exception e) { loadError = $"{e.GetType().Name}: {e.Message}"; }
                if (loadError != null)
                {
                    CSDebug.LogError($"[ParityCapture] case '{queue.cases[queue.index].name}': {loadError}; aborting");
                    EndCase(queue, failed: true);
                    return;
                }
            }

            s_probeSeen |= ParityProbe.Active;
            if (!ParityProbe.Active)
            {
                // The environment hook arms the probe before the first scene; a run that never
                // gets there is reported, not waited on forever.
                if (++s_idleFrames > BootGraceFrames)
                {
                    CSDebug.LogError($"[ParityCapture] case '{s_caseName}': ParityProbe never began (is {DeterministicSession.ParityOutEnvironmentVariable} readable by the hook?); aborting");
                    EndCase(queue, failed: true);
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
                foreach (var step in list)
                {
                    try { Run(step, frame); }
                    catch (Exception e)
                    {
                        CSDebug.LogError($"[ParityCapture] case '{s_caseName}', frame {frame}: do step '{step}' failed ({e.GetType().Name}: {e.Message}); the case is counted as failed");
                        MarkCaseFaulted(queue);
                    }
                }
            }

            if (frame >= s_replay.frames)
            {
                CSDebug.Log($"[ParityCapture] case '{s_caseName}' {(s_caseFaulted ? "finished with a failed step" : "complete")} at frame {frame}");
                EndCase(queue, failed: false);
            }
        }

        /// <summary>Loads the running case's replay and do steps; returns why it could not, or null.</summary>
        static string LoadCase(Queue queue)
        {
            var entry = queue.cases[queue.index];
            string replayPath = Path.Combine(queue.parityDir, entry.replay ?? "");
            if (!File.Exists(replayPath)) return $"replay '{replayPath}' not found";
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
            return null;
        }

        static void ResetCaseState()
        {
            s_replay = null;
            s_caseName = null;
            s_idleFrames = 0;
            s_caseFaulted = false;
            s_probeSeen = false;
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
