// Device play-test logs (Docs/perf/DEVICE_LOGS.md). Development builds on an Android device only: the whole body is
// behind the guard, so a release player, the Editor (which has BugHuntAutoSend) and Amoebius get an empty class.
// Per session (one app launch) it keeps
//   - samples.jsonl: every 5 s, the last window's fps and frame times, CPU and GPU ms, memory, thermal status and headroom
//   - unity_log.txt: every line the game logged, as it happens
//   - session.json: device, per-scene frame-time summary, error signatures, the last 400 warnings and errors with stacks
//   - summary.txt: line one is what the phone's notification says
//   - one `capture` per scene 90 s in, when the diagnostics HUD is up
// A swiped-away app gets no goodbye, so nothing waits for a quit. It is all on disk within seconds, and each pause schedules
// DeviceLogJob (DeviceLogAndroid~/, added to the build by DeviceLogGradleHook), which outlives this process, sends a finished
// session to the bugledger-android/<user> branch with Android's exit record and the app's logcat, and posts a notification.
#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine.Android;
using UnityEngine.Profiling;
using UnityEngine.UI;
#endif
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>Captures device and game logs during development-build play tests on a phone.</summary>
    public static class DeviceLogCapture
    {
        public const string CommandName = "devlog";
#if DEVELOPMENT_BUILD && UNITY_ANDROID && !UNITY_EDITOR
        const string BridgeClass = "com.froglet.cosmicshore.devicelogs.DeviceLogBridge";
        const string NotificationPermission = "android.permission.POST_NOTIFICATIONS";
        const string UserKey = "CS.DeviceLog.User", DefaultUser = "yskha";
        const int RingSize = 400, FrameWindow = 1024, MaxLogBytes = 16 * 1024 * 1024;
        const float FlushEvery = 60f, SampleEvery = 5f, CaptureAfter = 90f, SceneSettleSeconds = 15f;
        const int CaptureSeconds = 15;
        const long ScheduleDelayMs = 15000;

        struct Entry { public double t; public LogType type; public string msg, stack, scene; }
        sealed class SceneStats
        {
            public string name; public int frames; public double total, max; public float enteredAt;
            public readonly int[] buckets = new int[8]; // <16.7, <25, <33.4, <50, <66.7, <100, <200, >=200 ms
            public int hitches; // > 50 ms
            public float lowestWindowFps = float.MaxValue;
        }

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly HashSet<string> NoCaptureScenes = new(StringComparer.OrdinalIgnoreCase) { "Bootstrap", "Authentication" };
        static readonly object Lock = new();
        static readonly Entry[] Ring = new Entry[RingSize];
        static int _ringHead, _ringCount, _errors, _warnings, _exceptions, _marks;
        static readonly Dictionary<string, int> Signatures = new();
        static readonly List<SceneStats> Scenes = new();
        static readonly HashSet<string> Captured = new(StringComparer.OrdinalIgnoreCase);
        static readonly float[] WindowFrames = new float[FrameWindow];
        static readonly float[] WindowSorted = new float[FrameWindow];
        static readonly FrameTiming[] Timings = new FrameTiming[64];
        static int _windowCount;
        static SceneStats _scene;
        static string _sessionId, _sessionDir, _root, _previousSession;
        static bool _previousCrashed;
        static float _nextFlush, _nextSample;
        static string _currentScene = "";
        static DateTime _startedUtc;
        static StreamWriter _samples, _log;
        static long _logBytes;
        static AndroidJavaClass _bridge;
        static AndroidJavaObject _context;

        public static string Root => _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            _root = Path.Combine(Application.persistentDataPath, "CosmicShore DeviceLogs");
            _startedUtc = DateTime.UtcNow;
            _sessionId = _startedUtc.ToString("yyyyMMdd_HHmmss", Inv) + "_" + SystemInfo.deviceUniqueIdentifier.GetHashCode().ToString("x8", Inv);
            _sessionDir = Path.Combine(_root, "pending", _sessionId);
            try
            {
                Directory.CreateDirectory(_sessionDir);
                _samples = new StreamWriter(Path.Combine(_sessionDir, "samples.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
                _log = new StreamWriter(Path.Combine(_sessionDir, "unity_log.txt"), false, new UTF8Encoding(false));
            }
            catch (Exception e) { CSDebug.LogWarning("[DeviceLog] " + e.Message); return; }

            try
            {
                _bridge = new AndroidJavaClass(BridgeClass);
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                _context = player.GetStatic<AndroidJavaObject>("currentActivity");
                _bridge.CallStatic("setDataRoot", _context, Application.persistentDataPath);
                File.WriteAllText(Path.Combine(_sessionDir, "pid"), _bridge.CallStatic<int>("myPid").ToString(Inv));
            }
            catch (Exception e)
            {
                // DeviceLogGradleHook adds the Java half; a build without it still records, and adb ingest still works.
                _bridge = null;
                CSDebug.LogWarning("[DeviceLog] no Android bridge: " + e.Message);
            }

            CheckPreviousSession();
            WriteAlive("alive");
            Application.logMessageReceivedThreaded += OnLog;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            var go = new GameObject("[DeviceLogCapture]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Pump>();
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
            Schedule(ScheduleDelayMs); // anything an earlier launch left unsent; this session stays out of it while this process lives
            CSDebug.Log($"[DeviceLog] session {_sessionId} -> {_sessionDir}");
        }

        static void Schedule(long delayMs)
        {
            try { _bridge?.CallStatic("schedule", _context, delayMs); }
            catch (Exception e) { CSDebug.LogWarning("[DeviceLog] schedule failed: " + e.Message); }
        }

        // ── previous session ──
        static string AlivePath => Path.Combine(_root, "session_alive.txt");
        static void CheckPreviousSession()
        {
            try
            {
                if (!File.Exists(AlivePath)) return;
                var lines = File.ReadAllLines(AlivePath);
                if (lines.Length >= 2) { _previousSession = lines[0]; _previousCrashed = lines[1] == "alive"; }
            }
            catch { }
        }
        static void WriteAlive(string state)
        {
            try { File.WriteAllText(AlivePath, _sessionId + "\n" + state + "\n" + DateTime.UtcNow.ToString("o", Inv)); } catch { }
        }

        // ── logs (any thread) ──
        static void OnLog(string msg, string stack, LogType type)
        {
            lock (Lock)
            {
                if (_log != null && _logBytes < MaxLogBytes)
                {
                    string line = DateTime.UtcNow.ToString("HH:mm:ss.fff", Inv) + " " + type + " " + msg;
                    _log.WriteLine(line);
                    _logBytes += line.Length + 1;
                    if (type != LogType.Log && !string.IsNullOrEmpty(stack)) { _log.WriteLine(stack); _logBytes += stack.Length + 1; }
                    if (_logBytes >= MaxLogBytes) _log.WriteLine("(log full: the rest of this session is in logcat_app.txt.gz)");
                }
                if (type == LogType.Log) return;
                Ring[_ringHead] = new Entry { t = DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds, type = type, msg = msg, stack = stack, scene = _currentScene };
                _ringHead = (_ringHead + 1) % RingSize;
                if (_ringCount < RingSize) _ringCount++;
                if (type == LogType.Warning) _warnings++; else if (type == LogType.Exception) _exceptions++; else _errors++;
                string sig = type + "|" + Signature(msg, stack);
                Signatures.TryGetValue(sig, out int n);
                Signatures[sig] = n + 1;
            }
        }
        static string Signature(string msg, string stack)
        {
            string first = msg ?? "";
            int nl = first.IndexOf('\n'); if (nl > 0) first = first.Substring(0, nl);
            if (first.Length > 160) first = first.Substring(0, 160);
            string top = "";
            if (!string.IsNullOrEmpty(stack)) { int s = stack.IndexOf('\n'); top = s > 0 ? stack.Substring(0, s) : stack; }
            return first + " @ " + top;
        }

        // ── frames (main thread, no allocation) ──
        static void OnSceneChanged(UnityEngine.SceneManagement.Scene from, UnityEngine.SceneManagement.Scene to)
        {
            _currentScene = to.name;
            _scene = null;
            for (int i = 0; i < Scenes.Count; i++) if (Scenes[i].name == to.name) _scene = Scenes[i];
            if (_scene == null) { _scene = new SceneStats { name = to.name }; Scenes.Add(_scene); }
            _scene.enteredAt = Time.realtimeSinceStartup;
            Flush(); // leaving a game scene is the match end on every mode
        }
        static void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            float ms = dt * 1000f;
            if (dt > 0f)
            {
                if (_windowCount < FrameWindow) WindowFrames[_windowCount++] = ms;
                if (_scene != null)
                {
                    _scene.frames++; _scene.total += ms; if (ms > _scene.max) _scene.max = ms;
                    int b = ms < 16.7f ? 0 : ms < 25f ? 1 : ms < 33.4f ? 2 : ms < 50f ? 3 : ms < 66.7f ? 4 : ms < 100f ? 5 : ms < 200f ? 6 : 7;
                    _scene.buckets[b]++; if (ms > 50f) _scene.hitches++;
                }
            }
            FrameTimingManager.CaptureFrameTimings();

            float now = Time.realtimeSinceStartup;
            if (now >= _nextSample)
            {
                _nextSample = now + SampleEvery;
                SampleDevice();
                MaybeCapture(now);
                lock (Lock) { try { _log?.Flush(); } catch { } }
            }
            if (now >= _nextFlush) { _nextFlush = now + FlushEvery; Flush(); }
        }

        // The editor's PerfAutoCapture, on the phone: the same one file, so perf_hunt.py and perf_capture_report.py read it as they are.
        static void MaybeCapture(float now)
        {
            if (_scene == null || !DevDiagnostics.Enabled || DiagnosticsHUD.CaptureRunning) return;
            if (NoCaptureScenes.Contains(_scene.name) || Captured.Contains(_scene.name)) return;
            if (now - _scene.enteredAt < CaptureAfter) return;
            Captured.Add(_scene.name);
            DiagnosticsHUD.RunConsole($"{DiagnosticsHUD.CaptureCommand} {DeviceLogFormat.SafeName(_scene.name, "scene")} {CaptureSeconds}");
        }

        // ── device ──
        static void SampleDevice()
        {
            int n = _windowCount;
            _windowCount = 0;
            if (n == 0 || _samples == null) return;

            Array.Copy(WindowFrames, WindowSorted, n);
            Array.Sort(WindowSorted, 0, n);
            float sum = 0f;
            for (int i = 0; i < n; i++) sum += WindowSorted[i];
            float avg = sum / n;
            float fps = avg > 0f ? 1000f / avg : 0f;
            bool capturing = DiagnosticsHUD.CaptureRunning;
            // A scene's load is not its frame rate: the first seconds, and the boot scenes, stay out of its lowest fps.
            bool settled = _scene != null && !NoCaptureScenes.Contains(_scene.name) && Time.realtimeSinceStartup - _scene.enteredAt >= SceneSettleSeconds;
            if (settled && !capturing && fps < _scene.lowestWindowFps) _scene.lowestWindowFps = fps;

            uint got = FrameTimingManager.GetLatestTimings((uint)Timings.Length, Timings);
            double cpu = 0, gpu = 0;
            for (int i = 0; i < got; i++) { cpu += Timings[i].cpuFrameTime; gpu += Timings[i].gpuFrameTime; }
            if (got > 0) { cpu /= got; gpu /= got; }

            long rssKb = -1; int thermal = -1; float headroom = -1f, batteryC = -1f;
            try
            {
                if (_bridge != null)
                {
                    rssKb = _bridge.CallStatic<long>("rssKb");
                    thermal = _bridge.CallStatic<int>("thermalStatus", _context);
                    headroom = _bridge.CallStatic<float>("thermalHeadroom", _context);
                    batteryC = _bridge.CallStatic<float>("batteryTempC", _context);
                    if (float.IsNaN(headroom)) headroom = -1f;
                }
            }
            catch (Exception e) { CSDebug.LogWarning("[DeviceLog] sample failed: " + e.Message); }

            const long MB = 1048576;
            try
            {
                _samples.WriteLine(FormattableString.Invariant(
                    $"{{\"t\":{Time.realtimeSinceStartup:F0},\"scene\":\"{Esc(_currentScene)}\",\"fps\":{fps:F1},\"avgMs\":{avg:F1},\"p50Ms\":{DeviceLogFormat.Percentile(WindowSorted, n, .5f):F1},\"p95Ms\":{DeviceLogFormat.Percentile(WindowSorted, n, .95f):F1},\"p99Ms\":{DeviceLogFormat.Percentile(WindowSorted, n, .99f):F1},\"maxMs\":{WindowSorted[n - 1]:F1},\"cpuMs\":{cpu:F1},\"gpuMs\":{gpu:F1},\"rssMB\":{(rssKb < 0 ? -1 : rssKb / 1024)},\"allocMB\":{Profiler.GetTotalAllocatedMemoryLong() / MB},\"reservedMB\":{Profiler.GetTotalReservedMemoryLong() / MB},\"monoMB\":{Profiler.GetMonoUsedSizeLong() / MB},\"gfxMB\":{Profiler.GetAllocatedMemoryForGraphicsDriver() / MB},\"thermalStatus\":{thermal},\"headroom\":{headroom:F2},\"batteryTempC\":{batteryC:F1},\"capturing\":{(capturing ? 1 : 0)}}}"));
            }
            catch (Exception e) { CSDebug.LogWarning("[DeviceLog] sample write failed: " + e.Message); }
        }

        /// <summary>"That felt bad, right now": a timestamped line in the samples, for whoever reads the report.</summary>
        static int Mark(string note)
        {
            _marks++;
            try
            {
                _samples?.WriteLine(FormattableString.Invariant($"{{\"t\":{Time.realtimeSinceStartup:F0},\"scene\":\"{Esc(_currentScene)}\",\"mark\":{_marks},\"note\":\"{Esc(note)}\"}}"));
            }
            catch (Exception e) { CSDebug.LogWarning("[DeviceLog] mark failed: " + e.Message); }
            return _marks;
        }

        static string DeviceJson()
        {
            var sb = new StringBuilder(1024);
            sb.Append("{\"model\":\"").Append(Esc(SystemInfo.deviceModel)).Append("\",\"os\":\"").Append(Esc(SystemInfo.operatingSystem))
              .Append("\",\"gpu\":\"").Append(Esc(SystemInfo.graphicsDeviceName)).Append("\",\"gpuApi\":\"").Append(SystemInfo.graphicsDeviceType)
              .Append("\",\"gpuMemMB\":").Append(SystemInfo.graphicsMemorySize).Append(",\"ramMB\":").Append(SystemInfo.systemMemorySize)
              .Append(",\"cpu\":\"").Append(Esc(SystemInfo.processorType)).Append("\",\"cores\":").Append(SystemInfo.processorCount)
              .Append(",\"resolution\":\"").Append(Screen.width).Append('x').Append(Screen.height).Append("\",\"refreshHz\":").Append(Screen.currentResolution.refreshRateRatio.value.ToString("F0", Inv))
              .Append(",\"targetFps\":").Append(Application.targetFrameRate).Append(",\"quality\":\"").Append(Esc(QualitySettings.names[QualitySettings.GetQualityLevel()]))
              .Append("\",\"appVersion\":\"").Append(Esc(Application.version)).Append("\",\"unity\":\"").Append(Application.unityVersion)
              .Append("\",\"buildGuid\":\"").Append(Application.buildGUID).Append("\"}");
            return sb.ToString();
        }

        // ── bundle ──
        static void Flush()
        {
            if (string.IsNullOrEmpty(_sessionDir)) return;
            try
            {
                var sb = new StringBuilder(64 * 1024);
                sb.Append("{\"schema\":2,\"session\":\"").Append(_sessionId).Append("\",\"user\":\"").Append(Esc(User()))
                  .Append("\",\"writtenUtc\":\"").Append(DateTime.UtcNow.ToString("o", Inv)).Append("\",\"uptimeS\":").Append(Time.realtimeSinceStartup.ToString("F0", Inv))
                  .Append(",\"previousSession\":\"").Append(Esc(_previousSession)).Append("\",\"previousSessionCrashed\":").Append(_previousCrashed ? "true" : "false")
                  .Append(",\"marks\":").Append(_marks)
                  .Append(",\"device\":").Append(DeviceJson()).Append(",\"scenes\":[");
                for (int i = 0; i < Scenes.Count; i++)
                {
                    var s = Scenes[i]; if (i > 0) sb.Append(',');
                    sb.Append("{\"name\":\"").Append(Esc(s.name)).Append("\",\"frames\":").Append(s.frames)
                      .Append(",\"avgMs\":").Append(s.frames > 0 ? (s.total / s.frames).ToString("F2", Inv) : "0")
                      .Append(",\"maxMs\":").Append(s.max.ToString("F1", Inv)).Append(",\"hitches50ms\":").Append(s.hitches)
                      .Append(",\"lowestWindowFps\":").Append((s.lowestWindowFps == float.MaxValue ? 0f : s.lowestWindowFps).ToString("F1", Inv)).Append(",\"buckets\":[");
                    for (int b = 0; b < s.buckets.Length; b++) { if (b > 0) sb.Append(','); sb.Append(s.buckets[b]); }
                    sb.Append("]}");
                }
                sb.Append(']');
                int errorKinds;
                lock (Lock)
                {
                    errorKinds = Signatures.Count;
                    try { _log?.Flush(); } catch { }
                    sb.Append(",\"counts\":{\"errors\":").Append(_errors).Append(",\"warnings\":").Append(_warnings).Append(",\"exceptions\":").Append(_exceptions).Append("},\"signatures\":[");
                    bool first = true;
                    foreach (var kv in Signatures)
                    {
                        if (!first) sb.Append(','); first = false;
                        sb.Append("{\"sig\":\"").Append(Esc(kv.Key)).Append("\",\"count\":").Append(kv.Value).Append('}');
                    }
                    sb.Append("],\"recent\":[");
                    int start = (_ringHead - _ringCount + RingSize) % RingSize;
                    for (int i = 0; i < _ringCount; i++)
                    {
                        var e = Ring[(start + i) % RingSize]; if (i > 0) sb.Append(',');
                        sb.Append("{\"t\":").Append(e.t.ToString("F1", Inv)).Append(",\"type\":\"").Append(e.type).Append("\",\"scene\":\"").Append(Esc(e.scene))
                          .Append("\",\"msg\":\"").Append(Esc(e.msg)).Append("\",\"stack\":\"").Append(Esc(e.stack)).Append("\"}");
                    }
                }
                sb.Append("]}");
                // Through a temp file: a process killed mid-write must not leave half a session.json behind.
                string path = Path.Combine(_sessionDir, "session.json"), temp = path + ".tmp";
                File.WriteAllText(temp, sb.ToString());
                File.Copy(temp, path, true);
                File.Delete(temp);
                WriteSummary(errorKinds);
            }
            catch (Exception e) { CSDebug.LogWarning("[DeviceLog] flush failed: " + e.Message); }
        }

        static void WriteSummary(int errorKinds)
        {
            float worstFps = float.MaxValue;
            string worstScene = "";
            for (int i = 0; i < Scenes.Count; i++)
                if (Scenes[i].lowestWindowFps < worstFps) { worstFps = Scenes[i].lowestWindowFps; worstScene = Scenes[i].name; }
            double minutes = (DateTime.UtcNow - _startedUtc).TotalMinutes;
            File.WriteAllText(Path.Combine(_sessionDir, "summary.txt"),
                DeviceLogFormat.Headline(minutes, Scenes.Count, worstFps, worstScene, errorKinds, _marks) + "\n");
        }

        static string User() => PlayerPrefs.GetString(UserKey, DefaultUser);

        // ── console ──
        static string Handle(string[] args)
        {
            string sub = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "token":
                    return "never type a token here (the console logs what it runs): copy it, then run devlog paste";
                case "paste":
                    return PasteToken();
                case "user":
                    if (args.Length < 2) return "devlog user <name>";
                    string name = DeviceLogFormat.SafeName(args[1], DefaultUser);
                    PlayerPrefs.SetString(UserKey, name); PlayerPrefs.Save();
                    _bridge?.CallStatic("setUser", _context, name);
                    return "devlog: reports go to bugledger-android/" + name;
                case "clear":
                    _bridge?.CallStatic("forget", _context);
                    return "devlog: token removed from this phone; reports stay here until a new one is set";
                case "mark":
                    return "devlog: mark " + Mark(args.Length > 1 ? string.Join(" ", args, 1, args.Length - 1) : "");
                case "send":
                    Flush();
                    Schedule(0);
                    return "devlog: finished sessions go now; this one goes after the app closes";
                default:
                    bool hasToken = _bridge != null && _bridge.CallStatic<bool>("hasToken", _context);
                    return $"devlog: session {_sessionId}, {_errors} err / {_exceptions} exc / {_warnings} warn, {_marks} marked, token {(hasToken ? "set" : "none")}, prev crashed {_previousCrashed}, dir {_sessionDir}";
            }
        }

        // From the clipboard, never typed: the console logs every line it runs, and logs get uploaded.
        static string PasteToken()
        {
            if (_bridge == null) return "devlog: this build has no Android bridge";
            string token = GUIUtility.systemCopyBuffer;
            if (!DeviceLogFormat.LooksLikeToken(token)) return "devlog: the clipboard does not hold a GitHub token; copy it first";
            string user = User();
            _bridge.CallStatic("setIdentity", _context, token.Trim(), user);
            GUIUtility.systemCopyBuffer = "";
            if (!Permission.HasUserAuthorizedPermission(NotificationPermission)) Permission.RequestUserPermission(NotificationPermission);
            _bridge.CallStatic("verify", _context);
            return "devlog: token saved, clipboard cleared; a notification will say whether GitHub accepts it";
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append(' '); else sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        sealed class Pump : MonoBehaviour
        {
            const float MarkLabelSeconds = 1.5f;

            GameObject _canvas;
            Text _markLabel;
            float _restoreMarkLabelAt;

            void Awake() { _nextFlush = Time.realtimeSinceStartup + FlushEvery; }

            void Start()
            {
                if (_scene == null)
                    OnSceneChanged(default, UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                BuildPanel();
                DevDiagnostics.Changed += OnDiagnosticsChanged;
            }

            void OnDestroy() => DevDiagnostics.Changed -= OnDiagnosticsChanged;

            void Update()
            {
                Tick();
                if (_restoreMarkLabelAt > 0f && Time.unscaledTime >= _restoreMarkLabelAt)
                {
                    _restoreMarkLabelAt = 0f;
                    _markLabel.text = "Mark";
                }
            }

            void OnApplicationPause(bool paused)
            {
                if (paused) { WriteAlive("clean"); Flush(); Schedule(ScheduleDelayMs); }
                else WriteAlive("alive");
            }

            void OnApplicationQuit() { WriteAlive("clean"); Flush(); Schedule(ScheduleDelayMs); }

            void OnDiagnosticsChanged(bool on) { if (_canvas) _canvas.SetActive(on); }

            // ── the Mark button. Logs leave over USB (android_logs_report.py ingest); a token for uploading is set with `devlog paste` ──
            void BuildPanel()
            {
                _canvas = new GameObject("DeviceLogCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                DontDestroyOnLoad(_canvas);
                var canvas = _canvas.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32761;
                var scaler = _canvas.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;

                _markLabel = CreateButton("Mark", new Vector2(-16f, -240f), OnMarkPressed).GetComponentInChildren<Text>();
                _canvas.SetActive(DevDiagnostics.Enabled);
            }

            GameObject CreateButton(string label, Vector2 fromTopRight, UnityEngine.Events.UnityAction onClick)
            {
                var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(_canvas.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = fromTopRight;
                rt.sizeDelta = new Vector2(150f, 64f);
                go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
                go.GetComponent<Button>().onClick.AddListener(onClick);

                var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var trt = (RectTransform)textGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                var text = textGo.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 26;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.raycastTarget = false;
                text.text = label;
                return go;
            }

            void OnMarkPressed()
            {
                _markLabel.text = "Marked " + Mark("");
                _restoreMarkLabelAt = Time.unscaledTime + MarkLabelSeconds;
            }
        }
#endif
    }
}
