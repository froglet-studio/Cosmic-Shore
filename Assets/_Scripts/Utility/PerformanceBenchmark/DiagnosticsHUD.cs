using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Profiling;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// In-build diagnostics overlay (uGUI). Auto-spawns in the Editor and Development builds only
    /// (stripped from Release). Shows live performance data and can record a "diagnostic" - a
    /// timed spike capture written to the user's Documents folder as JSON + a readable .txt.
    ///
    /// • Normal mode: FPS + Frame Time (ms) + CPU/GPU split (busy CPU work vs GPU time) and a
    ///   live bound verdict (CPU-bound / GPU-bound / Balanced / Capped) via FrameTimingManager.
    /// • Advanced mode: + CPU thread breakdown (total, main thread, present wait, render thread),
    ///   draw calls / batches / triangles / SetPass, memory (GC per frame, managed heap, Unity
    ///   allocated, reserved vs device RAM, graphics driver, device RAM/VRAM), and network
    ///   (RTT/ping, NetVars dirty, RPCs, bytes per frame).
    /// • Run Diagnostic: records spikes for the selected seconds (works in editor and build),
    ///   then saves Documents/CosmicShore Diagnostics/diag_*.json (+ .txt).
    ///
    /// Buttons drive everything; keyboard fallbacks exist in case the scene's EventSystem can't
    /// route clicks: F7 toggle · F6 advanced · F5 run diagnostic.
    /// </summary>
    public class DiagnosticsHUD : MonoBehaviour
    {
        // ── external stats API ────────────────────────────────────────────
        // Any system (stress harness, perf probes, debug injectors) publishes rows here
        // instead of drawing its own OnGUI overlay — one diagnostics surface. Sections
        // render in registration order below the core rows, in simple AND advanced view.
        // The methods exist in all builds but no-op outside editor/dev (the HUD itself
        // only exists there).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly List<string> s_statSectionOrder = new();
        static readonly Dictionary<string, List<KeyValuePair<string, string>>> s_customStats = new();
        static readonly Dictionary<string, System.Func<string[], string>> s_commands = new();
        // Who registered each name, so a collision can NAME both sides. Two owners claiming
        // one name is not theoretical: PrismStressInjector (a render-only ECS cloud) and
        // PrismGridExplosionHarness (a lattice of REAL prisms) both claimed "prisms", and
        // registration order is not guaranteed — so `prisms 50000` gave you whichever ran
        // last, and the two measure completely different things.
        static readonly Dictionary<string, string> s_commandOwners = new();

        // Command handlers are closures over play-mode components; an owner that misses
        // UnregisterCommand in OnDestroy would otherwise ghost into the next session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistries()
        {
            s_statSectionOrder.Clear();
            s_customStats.Clear();
            s_commands.Clear();
            s_commandOwners.Clear();
        }
#endif

        /// <summary>Adds or updates one row under a titled section of the diagnostics overlay.</summary>
        public static void SetStat(string section, string label, string value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!s_customStats.TryGetValue(section, out var rows))
            {
                rows = new List<KeyValuePair<string, string>>();
                s_customStats[section] = rows;
                s_statSectionOrder.Add(section);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key != label) continue;
                rows[i] = new KeyValuePair<string, string>(label, value);
                return;
            }
            rows.Add(new KeyValuePair<string, string>(label, value));
#endif
        }

        /// <summary>Removes an entire section published via <see cref="SetStat"/> (call from the owner's OnDestroy).</summary>
        public static void ClearStats(string section)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            s_customStats.Remove(section);
            s_statSectionOrder.Remove(section);
#endif
        }

        /// <summary>
        /// Registers a console command for the HUD's input field (e.g. "prisms"). The handler
        /// receives the whitespace-split arguments after the command name and returns a
        /// one-line result shown on the overlay. Re-registering a name replaces the handler.
        /// </summary>
        public static void RegisterCommand(string name, System.Func<string[], string> handler)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (string.IsNullOrEmpty(name) || handler == null) return;

            string key = name.ToLowerInvariant();
            string owner = handler.Target?.GetType().Name
                           ?? handler.Method.DeclaringType?.Name
                           ?? "<static>";

            // Loud, because the failure mode is silent and total: the command still works, it
            // just belongs to somebody else, and a measurement taken through it is a
            // measurement of the wrong thing. Re-registering from the SAME owner (a component
            // re-enabled) is normal and says nothing.
            if (s_commandOwners.TryGetValue(key, out string previous) && previous != owner)
                Debug.LogWarning(
                    $"[DiagnosticsHUD] Console command '{key}' re-registered by {owner}, " +
                    $"replacing {previous}. Registration order is NOT guaranteed, so this " +
                    $"command is now whichever component happened to start last. Give one of " +
                    $"them a distinct name.");

            s_commands[key] = handler;
            s_commandOwners[key] = owner;
#endif
        }

        /// <summary>Removes a console command (call from the owner's OnDestroy).</summary>
        public static void UnregisterCommand(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (string.IsNullOrEmpty(name)) return;
            string key = name.ToLowerInvariant();
            s_commands.Remove(key);
            s_commandOwners.Remove(key);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static DiagnosticsHUD _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[DiagnosticsHUD]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<DiagnosticsHUD>();
        }

        // ── config ──
        const Key ToggleKey = Key.F7, AdvancedKey = Key.F6, DiagnosticKey = Key.F5;
        const string OutputFolderName = "CosmicShore Diagnostics";

        // ── state ──
        bool _visible = true, _advanced;
        // Minimized: the panel shrinks to a single FPS row (no buttons, no console).
        // Entered via the "Min" button; exited by clicking anywhere on the panel.
        bool _minimized;
        int _diagSeconds = 10;

        float _smoothedMs, _refreshTimer;
        float _displayFps, _displayMs;

        // CPU/GPU split via FrameTimingManager (always active in editor + dev builds; GPU time
        // is 0 on platforms without GPU timing support). Raw = latest frame, _sm* = smoothed.
        readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        float _rawCpuMs, _rawGpuMs, _rawMainMs, _rawWaitMs, _rawRenderMs;
        float _smCpuMs, _smGpuMs, _smMainMs, _smWaitMs, _smRenderMs;

        // recording
        bool _recording;
        float _recStart, _recEnd, _recRunningSum;
        float _recCpuSum, _recBusyCpuSum, _recGpuSum;
        int _recFrames, _recTimedFrames;

        // Render + GC accumulators. The recorders behind these have been started since
        // StartRecorders() but were only ever read for SPIKE records and the live rows, so a
        // saved report carried one INSTANTANEOUS draw count and no GC figure at all — which is
        // precisely the number an A/B of the prism render path is chasing. Averaged over the
        // whole run they are comparable between arms; a single sample is not.
        double _recDrawSum, _recBatchSum, _recSetPassSum, _recGcKbSum;

        /// <summary>Operator-supplied tag for the run in flight ("pathOn" / "pathOff").</summary>
        string _recLabel = string.Empty;
        readonly List<float> _recFrameMs = new(8192);
        readonly List<DiagSpike> _recSpikes = new(256);
        string _lastSavedPath = "";
        float _lastSavedShownAt = -100f;

        // recorders
        ProfilerRecorder _drawCalls, _setPass, _batches, _triangles, _vertices, _gcAlloc;
        ProfilerRecorder _rpcs, _netVars, _netBytes;

        // ui - two side-by-side blocks, each a label sub-column + value sub-column
        Text _labelA, _valueA, _labelB, _valueB, _advBtnLabel, _diagBtnLabel;
        RectTransform _panel, _labelART, _valueART, _labelBRT, _valueBRT, _buttonRow, _commandRow;
        InputField _cmdInput;
        GameObject _canvasGO;
        Font _font;

        // cached once - local machine region + UTC offset (UGS auto-picks the Relay region and
        // doesn't surface it, so we report the client's OS region; ping gives latency to host).
        string _regionCache, _utcCache;

        void Awake()
        {
            _instance = this;
            StartRecorders();
            BuildUI();
            RegisterCommand(FrameCapCommand, HandleFrameCapCommand);
            RegisterCommand(DiagCommand, HandleDiagCommand);
            RegisterCommand(RenderersCommand, HandleRenderersCommand);
            RegisterCommand(FreezeCommand, EcologyFreezeSwitch.Handle);
            RegisterCommand(ABComparison.CommandName, HandleABCommand);
        }

        void OnDestroy()
        {
            RestoreFrameCap();
            UnregisterCommand(FrameCapCommand);
            UnregisterCommand(DiagCommand);
            UnregisterCommand(RenderersCommand);
            UnregisterCommand(FreezeCommand);
            UnregisterCommand(ABComparison.CommandName);
            _abStopRequested = true;
            _abAcc = null;
            RendererHideSwitch.ShowIfHidden();
            EcologyFreezeSwitch.ReleaseIfFrozen();
            DisposeRecorders();
            if (_instance == this) _instance = null;
        }

        // ── frame cap ─────────────────────────────────────────────────────
        // A capped frame cannot MEASURE: idle time absorbs any change smaller than itself,
        // so an A/B run under a cap reports "no difference" from a test that could not have
        // shown one. This is the one knob that has to be reachable without leaving play mode.
        const string FrameCapCommand = "fps";
        bool _frameCapOverridden;
        int _savedVSync, _savedTargetFrameRate;

        const string DiagCommand = "diag";

        // ── renderer census ──────────────────────────────────────────────
        // On demand only: see RendererCensus for why it is never sampled on a timer.
        const string RenderersCommand = "renderers";
        RendererCensus _lastCensus;

        // renderers                  → census
        // renderers hide <prefix>      → switch off every renderer on a material named <prefix>*
        // renderers show               → switch exactly those back on
        string HandleRenderersCommand(string[] args)
        {
            string verb = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            if (verb == "hide") return RendererHideSwitch.Hide(args.Length > 1 ? args[1] : null);
            if (verb == "show") return RendererHideSwitch.Show();

            _lastCensus = RendererCensus.Take();
            string hidden = RendererHideSwitch.HiddenCount > 0
                ? $" [{RendererHideSwitch.HiddenCount:N0} hidden by 'renderers hide']"
                : "";
            return _lastCensus.Describe() + hidden;
        }

        /// <summary>
        /// <c>diag [label] [seconds]</c> — start a timed recording, TAGGED. The label is what
        /// makes two saved reports diffable as an A/B; F5 leaves them anonymous, and a pair of
        /// anonymous JSONs an hour apart is exactly how an arm gets misattributed.
        /// </summary>
        string HandleDiagCommand(string[] args)
        {
            if (_recording) return $"already recording ({_recFrames} frames so far) — wait, or press Stop";
            if (_abRunning) return "an 'ab' run is in progress — wait for it, or 'ab stop'";

            string label = string.Empty;
            for (int i = 0; i < args.Length; i++)
            {
                if (int.TryParse(args[i], out int seconds) && seconds > 0)
                {
                    _diagSeconds = Mathf.Clamp(seconds, 1, 600);
                    continue;
                }
                label = args[i];
            }

            _recLabel = label;
            StartDiagnostic();
            return $"recording {_diagSeconds}s" +
                   (string.IsNullOrEmpty(label) ? "" : $" as '{label}'") +
                   $" · path {CosmicShore.ECS.PrismRenderService.StatusLine()}";
        }

        string HandleFrameCapCommand(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            switch (mode)
            {
                case "uncap":
                    if (!_frameCapOverridden)
                    {
                        // Captured at OVERRIDE time, not at Awake: DisplayGraphicsSettings
                        // applies the player's saved settings at AfterSceneLoad, which can be
                        // after this component exists, so an Awake snapshot is the wrong value.
                        _savedVSync = QualitySettings.vSyncCount;
                        _savedTargetFrameRate = Application.targetFrameRate;
                        _frameCapOverridden = true;
                    }
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    return $"frame cap removed (was vsync {_savedVSync}, target " +
                           $"{(_savedTargetFrameRate > 0 ? _savedTargetFrameRate.ToString() : "uncapped")}) " +
                           "— `fps restore` puts it back";

                case "restore":
                    if (!_frameCapOverridden) return "frame cap was never overridden here";
                    RestoreFrameCap();
                    return $"frame cap restored: vsync {QualitySettings.vSyncCount}, target " +
                           $"{(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "uncapped")}";

                case "":
                    return $"vsync {QualitySettings.vSyncCount}, target " +
                           $"{(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "uncapped")}" +
                           $"{(_frameCapOverridden ? " (overridden by `fps uncap`)" : "")} " +
                           $"| usage: {FrameCapCommand} uncap | restore";

                default:
                    return $"usage: {FrameCapCommand} uncap | restore";
            }
        }

        // Restoring on teardown matters because both of these are PROCESS-wide and survive a
        // scene load: an override left behind would silently uncap the next scene the player
        // entered, which is a measurement setting escaping into the game.
        void RestoreFrameCap()
        {
            if (!_frameCapOverridden) return;
            QualitySettings.vSyncCount = _savedVSync;
            Application.targetFrameRate = _savedTargetFrameRate;
            _frameCapOverridden = false;
        }

        // ── ecology freeze ────────────────────────────────────────────────
        // Production gating for a same-state A/B; the switch owns the hold and its release.
        const string FreezeCommand = "freeze";

        // ── A/B ───────────────────────────────────────────────────────────
        // `ab "<command A>" "<command B>" [seconds] [rounds]` runs two console commands as the
        // two arms of one comparison: counterbalanced rounds (A B | B A | ...), a settle after
        // every command, a recording per arm, then ONE line of paired deltas and one saved
        // JSON holding both arms. The statistics are ABComparison's (pure, tested); this class
        // owns only the clock and the per-frame sampling, which already runs in Update.
        bool _abRunning, _abStopRequested;
        ABComparison.ArmAccumulator _abAcc;
        const string ABSection = "A/B";

        string HandleABCommand(string[] args)
        {
            if (args is { Length: 1 } && args[0].Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                if (!_abRunning) return "no 'ab' run in progress";
                _abStopRequested = true;
                return "stopping after the current step - the world is put back in arm B's state";
            }

            if (_abRunning) return "an 'ab' run is already in progress — 'ab stop' to cancel it";
            if (_recording) return "a 'diag' recording is in progress — wait for it first";
            if (!ABComparison.TryParse(args, out var request, out string error)) return error;

            foreach (string command in new[] { request.CommandA, request.CommandB })
            {
                string name = ABComparison.CommandNameOf(command);
                // A run inside a run would sample into one accumulator from two clocks; a diag
                // inside a run would record over the arm it was meant to measure.
                if (name == ABComparison.CommandName || name == DiagCommand)
                    return $"'{name}' cannot be an arm of an A/B";
                if (!s_commands.ContainsKey(name))
                    return $"unknown command '{name}' in an arm — commands: {string.Join(", ", s_commands.Keys)}";
            }

            StartCoroutine(RunAB(request));
            float perArm = ABComparison.SettleSeconds + ABComparison.PostCensusGapSeconds + request.Seconds;
            return $"A/B started: {request.Rounds} rounds x 2 arms x ~{perArm:F0}s = ~{request.Rounds * 2 * perArm:F0}s. " +
                   $"Hands off; do not change focus. Frozen: {(EcologyFreezeSwitch.IsFrozen ? "yes" : "NO")}";
        }

        IEnumerator RunAB(ABComparison.Request request)
        {
            _abRunning = true;
            _abStopRequested = false;

            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var report = new ABComparison.Report
            {
                scene = scene,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                commandA = request.CommandA,
                commandB = request.CommandB,
                seconds = request.Seconds,
                rounds = request.Rounds,
                settleSeconds = ABComparison.SettleSeconds,
                frozenAtStart = EcologyFreezeSwitch.IsFrozen,
                prismPath = CosmicShore.ECS.PrismRenderService.StatusLine(),
            };

            var schedule = ABComparison.Schedule(request.Rounds);
            ABComparison.Arm lastArmRun = ABComparison.Arm.B;
            bool completed = true;

            for (int i = 0; i < schedule.Length; i++)
            {
                if (_abStopRequested || !SameScene(scene)) { completed = false; break; }

                var arm = schedule[i];
                int round = i / 2;
                string status = $"round {round + 1}/{request.Rounds} · arm {arm}";

                string result = ExecuteCommand(arm == ABComparison.Arm.A ? request.CommandA : request.CommandB);
                lastArmRun = arm;

                SetStat(ABSection, "run", status + " · settling");
                yield return new WaitForSecondsRealtime(ABComparison.SettleSeconds);
                if (_abStopRequested || !SameScene(scene)) { completed = false; break; }

                // Census before AND after, both outside the recorded window: FindObjectsByType
                // over tens of thousands of objects is a spike of its own.
                int renderersStart = RendererCensus.Take().enabled;
                yield return new WaitForSecondsRealtime(ABComparison.PostCensusGapSeconds);

                int entsStart = CosmicShore.ECS.PrismRenderService.LiveEntityCount;
                _abAcc = new ABComparison.ArmAccumulator();
                float end = Time.unscaledTime + request.Seconds;
                while (Time.unscaledTime < end)
                {
                    if (_abStopRequested || !SameScene(scene)) break;
                    SetStat(ABSection, "run", $"{status} · recording {Time.unscaledTime - (end - request.Seconds):F0}/{request.Seconds}s");
                    yield return null;
                }

                var acc = _abAcc;
                _abAcc = null;
                if (acc == null || _abStopRequested || !SameScene(scene)) { completed = false; break; }

                var rec = acc.ToRecord(arm, round);
                rec.prismEntsStart = entsStart;
                rec.prismEntsEnd = CosmicShore.ECS.PrismRenderService.LiveEntityCount;
                rec.renderersStart = renderersStart;
                rec.renderersEnd = RendererCensus.Take().enabled;
                rec.commandResult = result;

                float namedCap = FrameBoundness.TargetFpsCap();
                float avgFps = rec.avgFrameMs > 0.0001f ? 1000f / rec.avgFrameMs : 0f;
                var limit = FrameBoundness.ClassifyFrameLimit(
                    rec.avgFrameMs, avgFps, rec.avgBusyCpuMs, rec.avgGpuMs,
                    FrameBoundness.IsFrameCapConfigured(), namedCap, out float idleMs);
                rec.frameLimit = limit.ToString();
                rec.frameTrustworthy = ABComparison.IsFrameTrustworthy(limit);
                rec.idleMs = idleMs;

                report.recordings.Add(rec);
            }

            // Leave the world in arm B's state, whatever order the schedule ended in, and even
            // when stopped - B is the "put it back" arm (e.g. renderers show). Not after a scene
            // change: the command would act on a world the run never saw.
            if (lastArmRun != ABComparison.Arm.B && SameScene(scene))
                ExecuteCommand(request.CommandB);

            report.completed = completed;
            report.frozenAtEnd = EcologyFreezeSwitch.IsFrozen;
            string line = ABComparison.Summarize(report);
            string path = SaveABReport(report);

            if (report.warnings.Count > 0)
                Debug.LogWarning($"[DiagnosticsHUD] {line}\n  " + string.Join("\n  ", report.warnings) + $"\n  saved: {path}");
            else
                Debug.Log($"[DiagnosticsHUD] {line}\n  saved: {path}");

            SetStat(ABSection, "run", completed ? "done" : "stopped");
            SetStat(ABSection, "result", line);
            SetStat("Console", "›", line);
            _lastSavedPath = path;
            _lastSavedShownAt = Time.unscaledTime;
            if (_visible) RefreshText();

            _abRunning = false;
            _abStopRequested = false;
        }

        static bool SameScene(string scene) =>
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == scene;

        string SaveABReport(ABComparison.Report r)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs)) docs = Application.persistentDataPath;
                string dir = Path.Combine(docs, OutputFolderName);
                Directory.CreateDirectory(dir);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                string baseName = $"ab_{Sanitize(r.scene)}_{stamp}";
                File.WriteAllText(Path.Combine(dir, baseName + ".json"), JsonUtility.ToJson(r, true));
                File.WriteAllText(Path.Combine(dir, baseName + ".txt"), ABComparison.BuildText(r));
                return Path.Combine(dir, baseName + ".json");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiagnosticsHUD] Could not save A/B report: {e.Message}");
                return "(save failed: " + e.Message + ")";
            }
        }

        // ── recorders ─────────────────────────────────────────────────────
        void StartRecorders()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            _triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _rpcs = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM RPCs Sent");
            _netVars = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM NetVars Dirty");
            _netBytes = ProfilerRecorder.StartNew(ProfilerCategory.Network, "CSM Bytes Sent");
        }

        void DisposeRecorders()
        {
            _drawCalls.Dispose(); _setPass.Dispose(); _batches.Dispose();
            _triangles.Dispose(); _vertices.Dispose(); _gcAlloc.Dispose();
            _rpcs.Dispose(); _netVars.Dispose(); _netBytes.Dispose();
        }

        static int RInt(ProfilerRecorder r) => r.Valid && r.Count > 0 ? (int)r.LastValue : 0;
        static long RLong(ProfilerRecorder r) => r.Valid && r.Count > 0 ? r.LastValue : 0;

        double Rtt()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return -1;
            if (nm.NetworkConfig?.NetworkTransport is UnityTransport utp)
            {
                ulong target = nm.IsServer && !nm.IsClient
                    ? (nm.ConnectedClientsIds.Count > 0 ? nm.ConnectedClientsIds[0] : nm.LocalClientId)
                    : NetworkManager.ServerClientId;
                return utp.GetCurrentRtt(target);
            }
            return -1;
        }

        // ── loop ──────────────────────────────────────────────────────────
        void Update()
        {
            var kb = Keyboard.current;
            // While the command input has focus, letters must reach the field, not the hotkeys.
            bool typingCommand = _cmdInput != null && _cmdInput.isFocused;
            if (kb != null && !typingCommand)
            {
                if (kb[ToggleKey].wasPressedThisFrame) SetVisible(!_visible);
                if (kb[AdvancedKey].wasPressedThisFrame) ToggleAdvanced();
                if (kb[DiagnosticKey].wasPressedThisFrame) ToggleDiagnostic();
            }

            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedMs = _smoothedMs <= 0f ? ms : Mathf.Lerp(_smoothedMs, ms, 0.1f);

            // Latest CPU/GPU frame timings (data lags ~4 frames behind by design).
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
            {
                _rawCpuMs = (float)_frameTimings[0].cpuFrameTime;
                // Sanitize BEFORE Smooth: one garbage sample poisons the EMA for many
                // frames, and a 7.7e10 ms reading makes Bound read GPU-bound on an idle GPU.
                _rawGpuMs = FrameBoundness.SanitizeGpuMs((float)_frameTimings[0].gpuFrameTime);
                _rawMainMs = (float)_frameTimings[0].cpuMainThreadFrameTime;
                _rawWaitMs = (float)_frameTimings[0].cpuMainThreadPresentWaitTime;
                _rawRenderMs = (float)_frameTimings[0].cpuRenderThreadFrameTime;
                Smooth(ref _smCpuMs, _rawCpuMs);
                Smooth(ref _smGpuMs, _rawGpuMs);
                Smooth(ref _smMainMs, _rawMainMs);
                Smooth(ref _smWaitMs, _rawWaitMs);
                Smooth(ref _smRenderMs, _rawRenderMs);
            }

            if (_recording) SampleRecording(ms);
            if (_abAcc != null)
                _abAcc.Add(ms, _rawCpuMs,
                    FrameBoundness.BusyCpuMs(_rawCpuMs, _rawMainMs, _rawWaitMs, _rawRenderMs),
                    _rawGpuMs, RInt(_drawCalls), RInt(_batches), RInt(_setPass), RLong(_gcAlloc) / 1024.0);

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 0.25f)
            {
                _refreshTimer = 0f;
                _displayMs = _smoothedMs;
                _displayFps = _displayMs > 0.0001f ? 1000f / _displayMs : 0f;
                if (_visible) RefreshText();
            }
        }

        // palette
        const string Label = "#8b97a8", White = "#ffffff", Good = "#5fe07a", Warn = "#ffd066",
                     Bad = "#ff6b6b", Accent = "#7cc2ff", Dim = "#6b7686";

        // layout
        const float Pad = 10f, BtnH = 22f, RowGap = 10f, TopY = 8f, LblValGap = 8f, ColGap = 26f;

        static string Col(string hex, string body) => "<color=" + hex + ">" + body + "</color>";
        static string FpsColor(float fps) => fps >= 55f ? Good : fps >= 30f ? Warn : Bad;
        static string MsColor(float ms) => ms <= 17f ? Good : ms <= 33.4f ? Warn : Bad;

        static void Row(StringBuilder l, StringBuilder v, string label, string value)
        {
            l.Append(Col(Label, label)).Append('\n');
            v.Append(value).Append('\n');
        }

        static void Header(StringBuilder l, StringBuilder v, string title)
        {
            l.Append(Col(Accent, title)).Append('\n');
            v.Append('\n');
        }

        void RefreshText()
        {
            if (_labelA == null) return;
            // Left block: most-important live data + render. Right block: memory / network / region.
            var la = new StringBuilder(256);
            var va = new StringBuilder(256);
            var lb = new StringBuilder(256);
            var vb = new StringBuilder(256);

            // Minimized: FPS only - no sections, no advanced block, no saved-file note.
            // Click anywhere on the panel to restore the detailed view.
            if (_minimized)
            {
                Row(la, va, "FPS", Col(FpsColor(_displayFps), _displayFps.ToString("F0")));
                _labelA.text = la.ToString();
                _valueA.text = va.ToString();
                _labelB.text = "";
                _valueB.text = "";
                Relayout();
                return;
            }

            if (_recording)
            {
                float left = Mathf.Max(0f, _recEnd - Time.unscaledTime);
                Row(la, va, "● Recording", Col(Warn, left.ToString("F0") + "s left"));
                Row(la, va, "Captured", Col(Dim, _recFrames + " f · " + _recSpikes.Count + " spikes"));
            }

            Row(la, va, "FPS", Col(FpsColor(_displayFps), _displayFps.ToString("F0")));
            Row(la, va, "Frame Time", Col(MsColor(_displayMs), _displayMs.ToString("F1") + " ms"));

            // CPU vs GPU dependence - busy CPU (work minus present wait) against GPU time,
            // plus the verdict for which side limits the frame.
            float busyCpuMs = FrameBoundness.BusyCpuMs(_smCpuMs, _smMainMs, _smWaitMs, _smRenderMs);
            Row(la, va, "CPU (busy)", MsValue(busyCpuMs));
            Row(la, va, "GPU", MsValue(_smGpuMs));
            Row(la, va, "Bound", BoundValue(busyCpuMs));
            Row(la, va, "Frame cap", FrameCapValue());

            // External sections published via SetStat (stress harness, probes, injectors).
            foreach (var section in s_statSectionOrder)
            {
                Header(la, va, section);
                foreach (var row in s_customStats[section])
                    Row(la, va, row.Key, Col(White, row.Value));
            }

            if (_advanced)
            {
                // Left block - local frame cost (cpu/gpu threads + render + memory).
                Header(la, va, "CPU / GPU");
                Row(la, va, "CPU Total", MsValue(_smCpuMs));
                Row(la, va, "Main Thread", MsValue(_smMainMs));
                Row(la, va, "Wait (present)", _smWaitMs > 0.001f ? Col(Dim, _smWaitMs.ToString("F1") + " ms") : Col(Dim, "n/a"));
                Row(la, va, "Render Thread", MsValue(_smRenderMs));

                Header(la, va, "Render");
                Row(la, va, "Draw Calls", Col(White, RInt(_drawCalls).ToString()));
                Row(la, va, "Batches", Col(White, RInt(_batches).ToString()));
                Row(la, va, "SetPass", Col(White, RInt(_setPass).ToString()));
                Row(la, va, "Triangles", Col(White, RLong(_triangles).ToString("N0")));
                Row(la, va, "Vertices", Col(White, RLong(_vertices).ToString("N0")));
                Row(la, va, "Renderers", _lastCensus == null
                    ? Col(Dim, "type 'renderers'")
                    : Col(White, _lastCensus.Summary()) +
                      Col(Dim, $" ({Time.unscaledTime - _lastCensus.takenAt:F0}s ago)"));

                // Instanced prism path (Entities Graphics): ON ⇒ draw calls should decouple
                // from prism count; OFF (reason) explains why they don't. See PrismRenderService.
                string prismPath = CosmicShore.ECS.PrismRenderService.StatusLine();
                Row(la, va, "Prism Path", Col(prismPath.StartsWith("ON") ? Good : Warn, prismPath));

                Header(la, va, "Memory");
                float gcKB = RLong(_gcAlloc) / 1024f;
                Row(la, va, "GC / frame", Col(gcKB > 4f ? Warn : Good, gcKB.ToString("F1") + " KB"));
                Row(la, va, "Managed", Col(White, Mb(Profiler.GetMonoUsedSizeLong())));
                Row(la, va, "Unity Alloc", Col(White, Mb(Profiler.GetTotalAllocatedMemoryLong())));
                Row(la, va, "Reserved", ReservedRamValue(Profiler.GetTotalReservedMemoryLong()));
                long gfxBytes = Profiler.GetAllocatedMemoryForGraphicsDriver();
                Row(la, va, "Gfx Driver", gfxBytes > 0 ? Col(White, Mb(gfxBytes)) : Col(Dim, "n/a"));
                Row(la, va, "Device", Col(Dim, DeviceMemory()));

                // Right block - connection (network + region).
                Header(lb, vb, "Network");
                double rtt = Rtt();
                Row(lb, vb, "Ping", rtt >= 0
                    ? Col(rtt <= 80 ? Good : rtt <= 160 ? Warn : Bad, rtt.ToString("F0") + " ms")
                    : Col(Dim, "offline"));
                Row(lb, vb, "NetVars", Col(White, RInt(_netVars).ToString()));
                Row(lb, vb, "RPCs", Col(White, RInt(_rpcs).ToString()));
                Row(lb, vb, "Bytes / f", Col(White, RLong(_netBytes).ToString("N0")));

                Header(lb, vb, "Region");
                Row(lb, vb, "Location", Col(White, RegionName()));
                Row(lb, vb, "UTC", Col(White, UtcOffset()));
            }

            if (Time.unscaledTime - _lastSavedShownAt < 6f && !string.IsNullOrEmpty(_lastSavedPath))
            {
                Header(lb, vb, "Saved");
                Row(lb, vb, "File", Col(Dim, Path.GetFileName(_lastSavedPath)));
            }

            _labelA.text = la.ToString();
            _valueA.text = va.ToString();
            _labelB.text = lb.ToString();
            _valueB.text = vb.ToString();
            Relayout();
        }

        string RegionName()
        {
            if (_regionCache != null) return _regionCache;
            try { _regionCache = System.Globalization.RegionInfo.CurrentRegion.DisplayName; }
            catch { _regionCache = "Unknown"; }
            return _regionCache;
        }

        string UtcOffset()
        {
            if (_utcCache != null) return _utcCache;
            TimeSpan off;
            try { off = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now); }
            catch { off = TimeSpan.Zero; }
            string sign = off < TimeSpan.Zero ? "-" : "+";
            _utcCache = $"UTC{sign}{Math.Abs(off.Hours):D2}:{Math.Abs(off.Minutes):D2}";
            return _utcCache;
        }

        // ── cpu/gpu + memory helpers ──────────────────────────────────────
        static void Smooth(ref float current, float sample) =>
            current = current <= 0f ? sample : Mathf.Lerp(current, sample, 0.1f);

        static string MsValue(float msVal) =>
            msVal > 0.001f ? Col(MsColor(msVal), msVal.ToString("F1") + " ms") : Col(Dim, "n/a");

        string BoundValue(float busyCpuMs)
        {
            // ONE decision, shared with BuildReport — see FrameBoundness.ClassifyFrameLimit.
            // The two used to walk different ladders and the saved report was the one that
            // lied: "CPU-bound" on a 76.6%-idle empty scene.
            float namedCap = FrameBoundness.TargetFpsCap();
            var limit = FrameBoundness.ClassifyFrameLimit(
                _displayMs, _displayFps, busyCpuMs, _smGpuMs,
                FrameBoundness.IsFrameCapConfigured(), namedCap, out float idleMs);

            switch (limit)
            {
                case FrameBoundness.FrameLimit.Unknown:
                    return Col(Dim, "n/a");
                case FrameBoundness.FrameLimit.AtNamedCap:
                    return Col(Good, $"Capped @{namedCap:F0}");
                case FrameBoundness.FrameLimit.CappedByIdle:
                    return Col(Warn, $"Capped — {idleMs:F1} ms idle");
                case FrameBoundness.FrameLimit.Stalled:
                    return Col(Bad, $"Stalled — {idleMs:F1} ms unattributed");
                case FrameBoundness.FrameLimit.IdleNoCap:
                    return Col(Dim, $"Idle {idleMs:F1} ms — no cap set");
                default:
                    return Col(FpsColor(_displayFps),
                        FrameBoundness.DescribeFrameLimit(limit, idleMs, namedCap));
            }
        }

        /// <summary>
        /// The live frame-rate cap, always on the overlay so it can never be the invisible
        /// reason an A/B showed no difference. Reads QualitySettings/Application directly
        /// rather than any cached setting: DisplayGraphicsSettings re-applies the player's
        /// saved graphics settings at AfterSceneLoad, AFTER AppManager.ConfigurePlatform has
        /// set BootstrapConfig's values, so the config asset does not say what is in force.
        /// </summary>
        static string FrameCapValue()
        {
            int vsync = QualitySettings.vSyncCount;
            int target = Application.targetFrameRate;
            bool capped = FrameBoundness.IsFrameCapConfigured(vsync, target);
            string text = vsync > 0 ? $"vsync {vsync}" : "vsync off";
            text += target > 0 ? $" · target {target}" : " · target uncapped";
            return Col(capped ? Warn : Good, text);
        }

        static string Mb(long bytes) => (bytes / (1024f * 1024f)).ToString("F0") + " MB";

        // Reserved (Unity's total footprint) against the device's physical RAM - the number
        // that predicts OS kills on mobile.
        static string ReservedRamValue(long reservedBytes)
        {
            float reservedMb = reservedBytes / (1024f * 1024f);
            int sysMb = SystemInfo.systemMemorySize;
            if (sysMb <= 0) return Col(White, reservedMb.ToString("F0") + " MB");
            float pct = reservedMb / sysMb * 100f;
            string color = pct < 25f ? Good : pct < 50f ? Warn : Bad;
            return Col(color, $"{reservedMb:F0} MB ({pct:F0}%)");
        }

        string DeviceMemory() =>
            _deviceMemCache ??= SystemInfo.systemMemorySize + " MB RAM · " +
                                SystemInfo.graphicsMemorySize + " MB VRAM";
        string _deviceMemCache;

        // Position the two blocks side by side, each sub-column sized to its widest row, then fit
        // the panel and slide the button row up under whichever block is taller.
        void Relayout()
        {
            if (_panel == null || _labelA == null) return;

            float aLblW = _labelA.preferredWidth;
            float aValW = _valueA.preferredWidth;
            bool hasB = _labelB.text.Length > 0;
            float bLblW = hasB ? _labelB.preferredWidth : 0f;
            float bValW = hasB ? _valueB.preferredWidth : 0f;

            float textH = Mathf.Max(_labelA.preferredHeight, hasB ? _labelB.preferredHeight : 0f);

            // X positions of each sub-column.
            float valAX = Pad + aLblW + LblValGap;
            float blockBX = valAX + aValW + ColGap;
            float valBX = blockBX + bLblW + LblValGap;

            float dataRight = hasB ? valBX + bValW : valAX + aValW;
            // Min width so the five buttons (run to ~304px) always fit. Minimized mode has
            // no buttons or console row, so it hugs the FPS text instead.
            float panelW = _minimized ? dataRight + Pad : Mathf.Max(dataRight + Pad, 322f);
            float panelH = _minimized
                ? TopY + textH + Pad
                : TopY + textH + RowGap + BtnH + RowGap + BtnH + Pad;
            _panel.sizeDelta = new Vector2(panelW, panelH);

            Place(_labelART, Pad, textH, aLblW);
            Place(_valueART, valAX, textH, aValW);
            Place(_labelBRT, blockBX, textH, bLblW);
            Place(_valueBRT, valBX, textH, bValW);

            if (_buttonRow != null) _buttonRow.anchoredPosition = new Vector2(Pad, -(TopY + textH + RowGap));
            if (_commandRow != null) _commandRow.anchoredPosition = new Vector2(Pad, -(TopY + textH + RowGap + BtnH + RowGap));
        }

        static void Place(RectTransform rt, float x, float height, float width)
        {
            if (rt == null) return;
            rt.anchoredPosition = new Vector2(x, -TopY);
            rt.sizeDelta = new Vector2(Mathf.Max(1f, width), height);
        }

        void ToggleAdvanced()
        {
            _advanced = !_advanced;
            if (_advBtnLabel != null) _advBtnLabel.text = _advanced ? "Simple" : "Advanced";
            if (_visible) RefreshText();
        }

        void SetMinimized(bool minimized)
        {
            _minimized = minimized;
            if (_buttonRow != null) _buttonRow.gameObject.SetActive(!minimized);
            if (_commandRow != null) _commandRow.gameObject.SetActive(!minimized);
            if (_visible) RefreshText();
        }

        // Panel background click: only meaningful while minimized - restores the detailed
        // view. Expanded mode keeps all interaction on the explicit buttons.
        void OnPanelClicked()
        {
            if (_minimized) SetMinimized(false);
        }

        // ── diagnostic recording ──────────────────────────────────────────
        void ToggleDiagnostic()
        {
            if (_recording) FinishDiagnostic();
            else if (_abRunning)
            {
                SetStat("Console", "›", "an 'ab' run is in progress — wait for it, or 'ab stop'");
            }
            else
            {
                // An F5/button run is ANONYMOUS. Clearing here rather than in StartDiagnostic
                // is what lets `diag <label>` set the label before starting — and stops an
                // untagged run inheriting the previous arm's label, which would put two
                // different populations in two files that claim to be the same arm.
                _recLabel = string.Empty;
                StartDiagnostic();
            }
        }

        void StartDiagnostic()
        {
            _recording = true;
            _recStart = Time.unscaledTime;
            _recEnd = _recStart + _diagSeconds;
            _recFrames = 0;
            _recTimedFrames = 0;
            _recRunningSum = 0f;
            _recCpuSum = _recBusyCpuSum = _recGpuSum = 0f;
            _recDrawSum = _recBatchSum = _recSetPassSum = _recGcKbSum = 0;
            _recFrameMs.Clear();
            _recSpikes.Clear();
            UpdateDiagButtonLabel();
        }

        void SampleRecording(float frameMs)
        {
            _recFrames++;
            _recRunningSum += frameMs;
            _recFrameMs.Add(frameMs);

            _recDrawSum += RInt(_drawCalls);
            _recBatchSum += RInt(_batches);
            _recSetPassSum += RInt(_setPass);
            _recGcKbSum += RLong(_gcAlloc) / 1024.0;

            if (_rawCpuMs > 0.001f || _rawGpuMs > 0.001f)
            {
                _recTimedFrames++;
                _recCpuSum += _rawCpuMs;
                _recBusyCpuSum += FrameBoundness.BusyCpuMs(_rawCpuMs, _rawMainMs, _rawWaitMs, _rawRenderMs);
                _recGpuSum += _rawGpuMs;
            }

            float mean = _recFrames > 0 ? _recRunningSum / _recFrames : frameMs;
            float threshold = Mathf.Max(33.3f, 1.75f * mean);
            if (frameMs >= threshold && _recSpikes.Count < 256)
            {
                _recSpikes.Add(new DiagSpike
                {
                    t = Time.unscaledTime - _recStart,
                    ms = frameMs,
                    fps = frameMs > 0.0001f ? 1000f / frameMs : 0f,
                    draws = RInt(_drawCalls),
                    tris = RLong(_triangles),
                    gcKB = RLong(_gcAlloc) / 1024f,
                    rttMs = Rtt(),
                    // Frame-timing data lags ~4 frames, so this attributes the spike approximately.
                    cpuMs = _rawCpuMs,
                    gpuMs = _rawGpuMs,
                });
            }

            if (Time.unscaledTime >= _recEnd)
                FinishDiagnostic();
        }

        void FinishDiagnostic()
        {
            _recording = false;
            UpdateDiagButtonLabel();

            var report = BuildReport();
            _lastSavedPath = SaveReport(report);
            _lastSavedShownAt = Time.unscaledTime;
            Debug.Log($"[DiagnosticsHUD] Diagnostic saved: {_lastSavedPath}");
        }

        DiagReport BuildReport()
        {
            int n = _recFrameMs.Count;
            var r = new DiagReport
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                durationSec = _diagSeconds,
                frames = n,
                draws = RInt(_drawCalls),
                tris = RLong(_triangles),
                rttMs = Rtt(),
                label = _recLabel,
                prismPath = CosmicShore.ECS.PrismRenderService.StatusLine(),
                prismEnts = CosmicShore.ECS.PrismRenderService.LiveEntityCount,
                ecologyFrozen = EcologyFreezeSwitch.IsFrozen,
                spikes = new List<DiagSpike>(_recSpikes),
            };
            if (_recTimedFrames > 0)
            {
                r.avgCpuMs = _recCpuSum / _recTimedFrames;
                r.avgCpuBusyMs = _recBusyCpuSum / _recTimedFrames;
                r.avgGpuMs = _recGpuSum / _recTimedFrames;
            }
            // Computed AFTER avgFrameMs/avgFps are filled in below? No — they are filled in the
            // `if (n > 0)` block further down, so the verdict is assigned there instead. See
            // the ordering note at that site.
            // Taken AFTER sampling stopped, so its own cost can never land in the run's frames.
            r.renderers = _lastCensus = RendererCensus.Take();
            r.frameCapVSync = QualitySettings.vSyncCount;
            r.frameCapTarget = Application.targetFrameRate;
            r.allocMB = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            r.reservedMB = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            r.systemMB = SystemInfo.systemMemorySize;
            if (n > 0)
            {
                r.avgDraws = (float)(_recDrawSum / n);
                r.avgBatches = (float)(_recBatchSum / n);
                r.avgSetPass = (float)(_recSetPassSum / n);
                r.avgGcKbPerFrame = (float)(_recGcKbSum / n);

                var sorted = new List<float>(_recFrameMs); sorted.Sort();
                float sum = 0f; for (int i = 0; i < n; i++) sum += _recFrameMs[i];
                r.avgFrameMs = sum / n;
                r.avgFps = r.avgFrameMs > 0.0001f ? 1000f / r.avgFrameMs : 0f;

                // ORDERING: the frame-limit verdict needs avgFrameMs and avgFps, so it cannot
                // be assigned in the object initializer above. Assigning it there against a
                // still-zero frame time is what a bare Classify() call hid — it needed neither,
                // and answered "CPU-bound" for a frame it had never looked at.
                float namedCap = FrameBoundness.TargetFpsCap();
                var limit = FrameBoundness.ClassifyFrameLimit(
                    r.avgFrameMs, r.avgFps, r.avgCpuBusyMs, r.avgGpuMs,
                    FrameBoundness.IsFrameCapConfigured(r.frameCapVSync, r.frameCapTarget),
                    namedCap, out float idleMs);
                r.idleMs = idleMs;
                r.boundVerdict = FrameBoundness.DescribeFrameLimit(limit, idleMs, namedCap);
                r.p99FrameMs = sorted[Mathf.Clamp(Mathf.RoundToInt(0.99f * (n - 1)), 0, n - 1)];
                r.maxFrameMs = sorted[n - 1];
            }
            return r;
        }

        string SaveReport(DiagReport r)
        {
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs)) docs = Application.persistentDataPath;
                string dir = Path.Combine(docs, OutputFolderName);
                Directory.CreateDirectory(dir);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseName = $"diag_{Sanitize(r.scene)}_{stamp}";
                File.WriteAllText(Path.Combine(dir, baseName + ".json"), JsonUtility.ToJson(r, true));
                File.WriteAllText(Path.Combine(dir, baseName + ".txt"), BuildTxt(r));
                return Path.Combine(dir, baseName + ".json");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiagnosticsHUD] Could not save diagnostic: {e.Message}");
                return "(save failed: " + e.Message + ")";
            }
        }

        static string BuildTxt(DiagReport r)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine($"Cosmic Shore diagnostic - {r.scene}   {r.timestamp}" +
                          (string.IsNullOrEmpty(r.label) ? "" : $"   [{r.label}]"));
            sb.AppendLine($"duration {r.durationSec}s · {r.frames} frames · avg {r.avgFps:F1} fps " +
                          $"({r.avgFrameMs:F1} ms) · p99 {r.p99FrameMs:F1} ms · max {r.maxFrameMs:F1} ms");
            sb.AppendLine($"draws avg {r.avgDraws:F0} (sample {r.draws}) · batches avg {r.avgBatches:F0} · " +
                          $"setpass avg {r.avgSetPass:F0} · tris {r.tris:N0} · " +
                          $"RTT {(r.rttMs >= 0 ? r.rttMs.ToString("F0") + " ms" : "n/a")}");
            sb.AppendLine($"GC {r.avgGcKbPerFrame:F1} KB/frame · prism path {r.prismPath} · " +
                          $"ecology {(r.ecologyFrozen ? "FROZEN" : "running")}");
            sb.AppendLine($"frame cap: vsync {r.frameCapVSync} · target " +
                          $"{(r.frameCapTarget > 0 ? r.frameCapTarget.ToString() : "uncapped")}" +
                          $" · idle {r.idleMs:F1} ms of {r.avgFrameMs:F1} ms");
            sb.AppendLine($"cpu {r.avgCpuMs:F1} ms (busy {r.avgCpuBusyMs:F1}) · " +
                          $"gpu {(r.avgGpuMs > 0.001f ? r.avgGpuMs.ToString("F1") + " ms" : "n/a")} · {r.boundVerdict} · " +
                          $"mem {r.allocMB}/{r.reservedMB} MB (device {r.systemMB} MB)");
            if (r.renderers != null) sb.AppendLine($"renderers {r.renderers.Describe()}");
            sb.AppendLine($"spikes ({r.spikes?.Count ?? 0}):");
            if (r.spikes != null)
                foreach (var s in r.spikes)
                    sb.AppendLine($"  [{s.t:F1}s] {s.ms:F1} ms ({s.fps:F0} fps) · draws {s.draws} · " +
                                  $"tris {s.tris:N0} · GC {s.gcKB:F1} KB" +
                                  (s.cpuMs > 0.001f || s.gpuMs > 0.001f ? $" · cpu {s.cpuMs:F1} / gpu {s.gpuMs:F1} ms" : "") +
                                  (s.rttMs >= 0 ? $" · RTT {s.rttMs:F0} ms" : ""));
            return sb.ToString();
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "scene";
            foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        // ── UI construction (uGUI) ────────────────────────────────────────
        void SetVisible(bool v)
        {
            _visible = v;
            if (_canvasGO != null) _canvasGO.SetActive(v);
        }

        void BuildUI()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            EnsureEventSystem();

            _canvasGO = new GameObject("DiagnosticsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasGO.transform.SetParent(transform, false);
            var canvas = _canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = _canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // Panel (top-left). Size is recomputed every refresh by Relayout().
            _panel = CreateRect("Panel", _canvasGO.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(8, -8), new Vector2(300, 80));
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            // Whole-panel click target: expands the HUD out of minimized (FPS-only) mode.
            // No transition so the background never tints; child buttons still get their
            // clicks first, so this is inert in the expanded view.
            var panelButton = _panel.gameObject.AddComponent<Button>();
            panelButton.transition = Selectable.Transition.None;
            panelButton.targetGraphic = bg;
            panelButton.onClick.AddListener(OnPanelClicked);

            // Two side-by-side blocks; each is a label sub-column + value sub-column. Exact x
            // positions and the panel size are computed every refresh by Relayout().
            _labelART = CreateRect("LabelsA", _panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad, -TopY), new Vector2(90, 40));
            _labelA = MakeColumn(_labelART, TextAnchor.UpperLeft);
            _labelA.text = "FPS";

            _valueART = CreateRect("ValuesA", _panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(100, -TopY), new Vector2(90, 40));
            _valueA = MakeColumn(_valueART, TextAnchor.UpperLeft);

            _labelBRT = CreateRect("LabelsB", _panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(220, -TopY), new Vector2(90, 40));
            _labelB = MakeColumn(_labelBRT, TextAnchor.UpperLeft);

            _valueBRT = CreateRect("ValuesB", _panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(310, -TopY), new Vector2(90, 40));
            _valueB = MakeColumn(_valueBRT, TextAnchor.UpperLeft);

            // Button row - a container Relayout() slides up to sit just below the table.
            _buttonRow = CreateRect("ButtonRow", _panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(Pad, -52), new Vector2(0, BtnH));
            _advBtnLabel = CreateButton("Advanced", _buttonRow, 0, 92, ToggleAdvanced);
            _diagBtnLabel = CreateButton("Run 10s", _buttonRow, 98, 84, ToggleDiagnostic);
            CreateButton("-", _buttonRow, 188, 30, () => { _diagSeconds = Mathf.Max(1, _diagSeconds - 5); UpdateDiagButtonLabel(); });
            CreateButton("+", _buttonRow, 224, 30, () => { _diagSeconds = Mathf.Min(600, _diagSeconds + 5); UpdateDiagButtonLabel(); });
            // Collapse to the FPS-only strip; click the strip itself to expand again.
            CreateButton("Min", _buttonRow, 260, 44, () => SetMinimized(true));

            // Command console row — type a registered command (e.g. "prisms 50000") and press
            // Enter or Run. Systems add commands via DiagnosticsHUD.RegisterCommand.
            _commandRow = CreateRect("CommandRow", _panel, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(Pad, -84), new Vector2(0, BtnH));

            var inputRT = CreateRect("CmdInput", _commandRow, new Vector2(0, 1), new Vector2(0, 1),
                Vector2.zero, new Vector2(196, BtnH));
            var inputBg = inputRT.gameObject.AddComponent<Image>();
            inputBg.color = new Color(0.12f, 0.15f, 0.2f, 0.95f);
            _cmdInput = inputRT.gameObject.AddComponent<InputField>();
            _cmdInput.targetGraphic = inputBg;
            _cmdInput.lineType = InputField.LineType.SingleLine;

            var cmdTextRT = CreateRect("Text", inputRT, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(6, 0), new Vector2(-12, 0));
            var cmdText = cmdTextRT.gameObject.AddComponent<Text>();
            cmdText.font = _font;
            cmdText.fontSize = 13;
            cmdText.color = Color.white;
            cmdText.alignment = TextAnchor.MiddleLeft;
            cmdText.supportRichText = false;

            var placeholderRT = CreateRect("Placeholder", inputRT, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(6, 0), new Vector2(-12, 0));
            var placeholder = placeholderRT.gameObject.AddComponent<Text>();
            placeholder.font = _font;
            placeholder.fontSize = 13;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.text = "command…  e.g. prisms 50000";

            _cmdInput.textComponent = cmdText;
            _cmdInput.placeholder = placeholder;
            _cmdInput.onEndEdit.AddListener(OnCommandEndEdit);

            CreateButton("Run", _commandRow, 202, 52, RunCommandFromInput);

            SetVisible(_visible);
            RefreshText();
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(es);
        }

        RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return rt;
        }

        Text MakeColumn(RectTransform rt, TextAnchor anchor)
        {
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 14;
            t.color = Color.white;
            t.supportRichText = true;
            t.alignment = anchor;
            t.lineSpacing = 1.1f;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Text CreateButton(string label, Transform parent, float x, float width, Action onClick)
        {
            var rt = CreateRect("Btn_" + label, parent, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, 0), new Vector2(width, 22));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.4f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var labelRt = CreateRect("Label", rt, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            labelRt.anchoredPosition = Vector2.zero;
            labelRt.sizeDelta = Vector2.zero;
            var t = labelRt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = 13;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.text = label;
            return t;
        }

        void UpdateDiagButtonLabel()
        {
            if (_diagBtnLabel != null)
                _diagBtnLabel.text = _recording ? "Stop" : $"Run {_diagSeconds}s";
        }

        // ── command console ───────────────────────────────────────────────
        void OnCommandEndEdit(string _)
        {
            // onEndEdit also fires on focus loss — only execute on an actual Enter press.
            var kb = Keyboard.current;
            bool submitted = kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
            if (submitted) RunCommandFromInput();
        }

        void RunCommandFromInput()
        {
            if (_cmdInput == null) return;
            string raw = _cmdInput.text;
            _cmdInput.text = "";
            ExecuteCommand(raw);
            _cmdInput.ActivateInputField(); // keep focus for repeated commands
        }

        /// <summary>Runs one console line and returns what the handler answered (also shown on the overlay).</summary>
        string ExecuteCommand(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string[] tokens = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string name = tokens[0].ToLowerInvariant();

            string result;
            if (s_commands.TryGetValue(name, out var handler))
            {
                var args = new string[tokens.Length - 1];
                Array.Copy(tokens, 1, args, 0, args.Length);
                try { result = handler(args) ?? "done"; }
                catch (Exception e) { result = "error: " + e.Message; }
            }
            else
            {
                result = s_commands.Count > 0
                    ? $"unknown '{name}' — commands: {string.Join(", ", s_commands.Keys)}"
                    : "no commands registered";
            }

            SetStat("Console", "›", result);
            Debug.Log($"[DiagnosticsHUD] {raw} → {result}");
            if (_visible) RefreshText();
            return result;
        }

        // ── serializable report ───────────────────────────────────────────
        [Serializable]
        class DiagSpike
        {
            public float t, ms, fps, gcKB;
            public float cpuMs, gpuMs;
            public int draws;
            public long tris;
            public double rttMs;
        }

        [Serializable]
        class DiagReport
        {
            public string scene, timestamp;
            public float durationSec, avgFps, avgFrameMs, p99FrameMs, maxFrameMs;
            public float avgCpuMs, avgCpuBusyMs, avgGpuMs;
            public string boundVerdict;
            public long allocMB, reservedMB;
            public int systemMB;
            public int frames, draws;
            public long tris;
            public double rttMs;

            /// <summary>Operator tag for the arm ("pathOn" / "pathOff"), so two files can be diffed.</summary>
            public string label;

            /// <summary>
            /// Run averages. `draws` above is a single instantaneous sample taken as the report is
            /// built; these are the comparable numbers. `avgGcKbPerFrame` is the one this whole
            /// exercise is chasing and was not recorded at all before.
            /// </summary>
            public float avgDraws, avgBatches, avgSetPass, avgGcKbPerFrame;

            /// <summary>
            /// <c>PrismRenderService.StatusLine()</c> captured with the run, so a report can never
            /// misattribute its own arm — the failure mode of hand-labelled A/B captures.
            /// </summary>
            public string prismPath;
            public int prismEnts;

            /// <summary>
            /// Whether ecology production was held (<c>freeze on</c>) when the run ended. A frozen
            /// world and a growing one are different populations, so two reports only compare
            /// when this matches.
            /// </summary>
            public bool ecologyFrozen;

            /// <summary>
            /// The frame-rate cap IN FORCE during the run, and how much of the average frame
            /// was idle. Recorded because a capped run's frame time and fps carry no
            /// information — three reports in one batch sat within 0.06 ms of the 120 Hz
            /// budget while reading "CPU-bound" — and a reader a week later has no other way
            /// to tell. `frameCapVSync > 0 || frameCapTarget > 0` means a cap was set.
            /// </summary>
            public int frameCapVSync, frameCapTarget;
            public float idleMs;

            /// <summary>
            /// The culling population at the end of the run. Draw calls turned out to cost
            /// ~0.2 ms of a 57 ms frame; renderer COUNT is what culling and the render-job
            /// wait scale with, and no report recorded it.
            /// </summary>
            public RendererCensus renderers;

            public List<DiagSpike> spikes;
        }
#endif
    }
}
