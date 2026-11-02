// Device play-test logs (Docs/perf/DEVICE_LOGS.md). Development builds on a device only: the whole body is
// #if DEVELOPMENT_BUILD && !UNITY_EDITOR, so a release player (and the Editor, which has BugHuntAutoSend) gets
// an empty class. Per session it keeps
//   - a ring buffer of the last 400 warnings/errors/exceptions/asserts with stack traces, plus per-signature counts
//   - device info (model, OS, GPU, RAM, resolution, quality tier, battery, thermal status)
//   - a per-scene frame-time summary (fixed-bucket histogram, no per-frame allocation)
//   - the app's own logcat (`logcat -d --pid`) and the crash buffer, read at upload time only
//   - whether the PREVIOUS session ended without a clean pause/quit (crash, ANR or OS kill)
// It writes the bundle to persistentDataPath/CosmicShore DeviceLogs/pending/<session>/ every 60 s, on every scene
// change, on pause and on quit, and uploads pending bundles to the bugledger-android/<user> branch through the
// GitHub contents API when a token was set with the `devlog token <pat>` console command. Without a token the bundles
// stay on the phone and Tools/Perf/android_logs_ingest.py pulls them over adb and commits them to the same branch.
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>Captures device and game logs during development-build play tests on a phone.</summary>
    public static class DeviceLogCapture
    {
        public const string CommandName = "devlog";
        public const string Branch = "bugledger-android/yskha";
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        const string Repo = "froglet-studio/Cosmic-Shore";
        const string TokenKey = "CS.DeviceLog.Token", UserKey = "CS.DeviceLog.User";
        const int RingSize = 400;
        const float FlushEvery = 60f, DeviceSampleEvery = 10f;

        struct Entry { public double t; public LogType type; public string msg, stack, scene; }
        sealed class SceneStats
        {
            public string name; public int frames; public double total, max; public float enteredAt;
            public readonly int[] buckets = new int[8]; // <16.7, <25, <33.4, <50, <66.7, <100, <200, >=200 ms
            public int hitches; // > 50 ms
        }

        static readonly object Lock = new();
        static readonly Entry[] Ring = new Entry[RingSize];
        static int _ringHead, _ringCount, _errors, _warnings, _exceptions;
        static readonly System.Collections.Generic.Dictionary<string, int> Signatures = new();
        static readonly System.Collections.Generic.List<SceneStats> Scenes = new();
        static readonly System.Collections.Generic.List<string> DeviceSamples = new();
        static SceneStats _scene;
        static string _sessionId, _sessionDir, _root, _previousSession;
        static bool _previousCrashed, _uploading;
        static float _nextFlush, _nextSample;
        static string _currentScene = "";

        public static string Root => _root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Application.platform != RuntimePlatform.Android) return; // phones only; the Editor has BugHuntAutoSend
            _root = System.IO.Path.Combine(Application.persistentDataPath, "CosmicShore DeviceLogs");
            _sessionId = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + SystemInfo.deviceUniqueIdentifier.GetHashCode().ToString("x8");
            _sessionDir = System.IO.Path.Combine(_root, "pending", _sessionId);
            try { System.IO.Directory.CreateDirectory(_sessionDir); } catch (System.Exception e) { CSDebug.LogWarning("[DeviceLog] " + e.Message); return; }
            CheckPreviousSession();
            WriteAlive("alive");
            Application.logMessageReceivedThreaded += OnLog;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
            var go = new GameObject("[DeviceLogCapture]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Pump>();
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
            CSDebug.Log($"[DeviceLog] session {_sessionId} -> {_sessionDir}");
        }

        // ── previous session ──
        static string AlivePath => System.IO.Path.Combine(_root, "session_alive.txt");
        static void CheckPreviousSession()
        {
            try
            {
                if (!System.IO.File.Exists(AlivePath)) return;
                var lines = System.IO.File.ReadAllLines(AlivePath);
                if (lines.Length >= 2) { _previousSession = lines[0]; _previousCrashed = lines[1] == "alive"; }
            }
            catch { }
        }
        static void WriteAlive(string state)
        {
            try { System.IO.File.WriteAllText(AlivePath, _sessionId + "\n" + state + "\n" + System.DateTime.UtcNow.ToString("o")); } catch { }
        }

        // ── logs (any thread) ──
        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Log) return;
            lock (Lock)
            {
                Ring[_ringHead] = new Entry { t = System.DateTime.UtcNow.Subtract(System.DateTime.UnixEpoch).TotalSeconds, type = type, msg = msg, stack = stack, scene = _currentScene };
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
            if (_scene == null) { _scene = new SceneStats { name = to.name, enteredAt = Time.realtimeSinceStartup }; Scenes.Add(_scene); }
            Flush(false); // leaving a game scene is the match end on every mode
        }
        static void Tick()
        {
            float dt = Time.unscaledDeltaTime;
            if (_scene != null && dt > 0f)
            {
                double ms = dt * 1000.0;
                _scene.frames++; _scene.total += ms; if (ms > _scene.max) _scene.max = ms;
                int b = ms < 16.7 ? 0 : ms < 25 ? 1 : ms < 33.4 ? 2 : ms < 50 ? 3 : ms < 66.7 ? 4 : ms < 100 ? 5 : ms < 200 ? 6 : 7;
                _scene.buckets[b]++; if (ms > 50) _scene.hitches++;
            }
            float now = Time.realtimeSinceStartup;
            if (now >= _nextSample) { _nextSample = now + DeviceSampleEvery; SampleDevice(); }
            if (now >= _nextFlush) { _nextFlush = now + FlushEvery; Flush(false); }
        }

        // ── device ──
        static void SampleDevice()
        {
            float fps = _scene != null && _scene.frames > 0 ? (float)(1000.0 * _scene.frames / _scene.total) : 0f;
            string line = $"{{\"t\":{Time.realtimeSinceStartup:F0},\"scene\":\"{Esc(_currentScene)}\",\"fpsAvg\":{fps:F1},\"battery\":{SystemInfo.batteryLevel:F2},\"batteryTempC\":{BatteryTempC():F1},\"thermalStatus\":{ThermalStatus()},\"monoMB\":{UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576},\"allocMB\":{UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576}}}";
            if (DeviceSamples.Count >= 2000) DeviceSamples.RemoveAt(0);
            DeviceSamples.Add(line);
        }
        static AndroidJavaObject Activity()
        {
            using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            return up.GetStatic<AndroidJavaObject>("currentActivity");
        }
        static float BatteryTempC()
        {
            try
            {
                using var act = Activity();
                using var filter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED");
                using var intent = act.Call<AndroidJavaObject>("registerReceiver", null, filter);
                return intent == null ? -1f : intent.Call<int>("getIntExtra", "temperature", -10) / 10f;
            }
            catch { return -1f; }
        }
        /// <summary>PowerManager thermal status 0 none .. 6 shutdown (API 29+), -1 unknown.</summary>
        static int ThermalStatus()
        {
            try
            {
                using var ver = new AndroidJavaClass("android.os.Build$VERSION");
                if (ver.GetStatic<int>("SDK_INT") < 29) return -1;
                using var act = Activity();
                using var pm = act.Call<AndroidJavaObject>("getSystemService", "power");
                return pm.Call<int>("getCurrentThermalStatus");
            }
            catch { return -1; }
        }
        static string DeviceJson()
        {
            var sb = new System.Text.StringBuilder(1024);
            sb.Append("{\"model\":\"").Append(Esc(SystemInfo.deviceModel)).Append("\",\"os\":\"").Append(Esc(SystemInfo.operatingSystem))
              .Append("\",\"gpu\":\"").Append(Esc(SystemInfo.graphicsDeviceName)).Append("\",\"gpuApi\":\"").Append(SystemInfo.graphicsDeviceType)
              .Append("\",\"gpuMemMB\":").Append(SystemInfo.graphicsMemorySize).Append(",\"ramMB\":").Append(SystemInfo.systemMemorySize)
              .Append(",\"cpu\":\"").Append(Esc(SystemInfo.processorType)).Append("\",\"cores\":").Append(SystemInfo.processorCount)
              .Append(",\"resolution\":\"").Append(Screen.width).Append('x').Append(Screen.height).Append("\",\"refreshHz\":").Append(Screen.currentResolution.refreshRateRatio.value.ToString("F0"))
              .Append(",\"targetFps\":").Append(Application.targetFrameRate).Append(",\"quality\":\"").Append(Esc(QualitySettings.names[QualitySettings.GetQualityLevel()]))
              .Append("\",\"appVersion\":\"").Append(Esc(Application.version)).Append("\",\"unity\":\"").Append(Application.unityVersion)
              .Append("\",\"buildGuid\":\"").Append(Application.buildGUID).Append("\"}");
            return sb.ToString();
        }

        // ── bundle ──
        static void Flush(bool withLogcat)
        {
            if (string.IsNullOrEmpty(_sessionDir)) return;
            try
            {
                var sb = new System.Text.StringBuilder(64 * 1024);
                sb.Append("{\"schema\":1,\"session\":\"").Append(_sessionId).Append("\",\"user\":\"").Append(Esc(User()))
                  .Append("\",\"writtenUtc\":\"").Append(System.DateTime.UtcNow.ToString("o")).Append("\",\"uptimeS\":").Append(Time.realtimeSinceStartup.ToString("F0"))
                  .Append(",\"previousSession\":\"").Append(Esc(_previousSession)).Append("\",\"previousSessionCrashed\":").Append(_previousCrashed ? "true" : "false")
                  .Append(",\"device\":").Append(DeviceJson()).Append(",\"scenes\":[");
                for (int i = 0; i < Scenes.Count; i++)
                {
                    var s = Scenes[i]; if (i > 0) sb.Append(',');
                    sb.Append("{\"name\":\"").Append(Esc(s.name)).Append("\",\"frames\":").Append(s.frames)
                      .Append(",\"avgMs\":").Append(s.frames > 0 ? (s.total / s.frames).ToString("F2") : "0")
                      .Append(",\"maxMs\":").Append(s.max.ToString("F1")).Append(",\"hitches50ms\":").Append(s.hitches).Append(",\"buckets\":[");
                    for (int b = 0; b < s.buckets.Length; b++) { if (b > 0) sb.Append(','); sb.Append(s.buckets[b]); }
                    sb.Append("]}");
                }
                sb.Append("],\"samples\":[").Append(string.Join(",", DeviceSamples)).Append("]");
                lock (Lock)
                {
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
                        sb.Append("{\"t\":").Append(e.t.ToString("F1")).Append(",\"type\":\"").Append(e.type).Append("\",\"scene\":\"").Append(Esc(e.scene))
                          .Append("\",\"msg\":\"").Append(Esc(e.msg)).Append("\",\"stack\":\"").Append(Esc(e.stack)).Append("\"}");
                    }
                }
                sb.Append("]}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(_sessionDir, "session.json"), sb.ToString());
                if (withLogcat) DumpLogcat();
            }
            catch (System.Exception e) { CSDebug.LogWarning("[DeviceLog] flush failed: " + e.Message); }
        }
        static void DumpLogcat()
        {
            try
            {
                using var proc = new AndroidJavaClass("android.os.Process");
                int pid = proc.CallStatic<int>("myPid");
                using var rt = new AndroidJavaClass("java.lang.Runtime");
                using var runtime = rt.CallStatic<AndroidJavaObject>("getRuntime");
                string app = System.IO.Path.Combine(_sessionDir, "logcat_app.txt");
                string crash = System.IO.Path.Combine(_sessionDir, "logcat_crash.txt");
                using (var p1 = runtime.Call<AndroidJavaObject>("exec", $"logcat -d --pid={pid} -t 4000 -f {app}")) p1.Call<int>("waitFor");
                using (var p2 = runtime.Call<AndroidJavaObject>("exec", $"logcat -d -b crash -t 300 -f {crash}")) p2.Call<int>("waitFor");
            }
            catch (System.Exception e) { CSDebug.LogWarning("[DeviceLog] logcat failed: " + e.Message); }
        }

        // ── upload ──
        static string Token => PlayerPrefs.GetString(TokenKey, "");
        static string User() => PlayerPrefs.GetString(UserKey, "yskha");
        static void UploadPending(MonoBehaviour host)
        {
            if (_uploading || string.IsNullOrEmpty(Token)) return;
            host.StartCoroutine(UploadRoutine());
        }
        static System.Collections.IEnumerator UploadRoutine()
        {
            _uploading = true;
            string pending = System.IO.Path.Combine(_root, "pending");
            string[] dirs;
            try { dirs = System.IO.Directory.GetDirectories(pending); } catch { _uploading = false; yield break; }
            foreach (var dir in dirs)
            {
                string id = System.IO.Path.GetFileName(dir);
                bool ok = true;
                foreach (var file in System.IO.Directory.GetFiles(dir))
                {
                    string path = $"devicelogs/{User()}/{id}/{System.IO.Path.GetFileName(file)}";
                    byte[] bytes; try { bytes = System.IO.File.ReadAllBytes(file); } catch { ok = false; continue; }
                    string sha = null;
                    using (var get = UnityEngine.Networking.UnityWebRequest.Get($"https://api.github.com/repos/{Repo}/contents/{path}?ref={Branch}"))
                    {
                        Auth(get); yield return get.SendWebRequest();
                        if (get.responseCode == 200) { var m = System.Text.RegularExpressions.Regex.Match(get.downloadHandler.text, "\"sha\"\\s*:\\s*\"([0-9a-f]+)\""); if (m.Success) sha = m.Groups[1].Value; }
                    }
                    string body = "{\"message\":\"devicelogs: " + id + " " + System.IO.Path.GetFileName(file) + "\",\"branch\":\"" + Branch + "\",\"content\":\"" + System.Convert.ToBase64String(bytes) + "\"" + (sha != null ? ",\"sha\":\"" + sha + "\"" : "") + "}";
                    using var put = new UnityEngine.Networking.UnityWebRequest($"https://api.github.com/repos/{Repo}/contents/{path}", "PUT",
                        new UnityEngine.Networking.DownloadHandlerBuffer(), new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body)));
                    Auth(put); put.SetRequestHeader("Content-Type", "application/json");
                    yield return put.SendWebRequest();
                    if (put.responseCode != 200 && put.responseCode != 201) { ok = false; CSDebug.LogWarning($"[DeviceLog] upload {path} failed: {put.responseCode} {put.error}"); }
                }
                // The live session stays pending so later flushes are uploaded too; older ones move to sent.
                if (ok && id != _sessionId)
                    try { string sent = System.IO.Path.Combine(_root, "sent", id); System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(sent)); System.IO.Directory.Move(dir, sent); } catch { }
            }
            _uploading = false;
        }
        static void Auth(UnityEngine.Networking.UnityWebRequest r)
        {
            r.SetRequestHeader("Authorization", "Bearer " + Token);
            r.SetRequestHeader("Accept", "application/vnd.github+json");
            r.SetRequestHeader("User-Agent", "CosmicShore-DeviceLog");
        }

        // ── console ──
        static string Handle(string[] args)
        {
            string sub = args is { Length: > 0 } ? args[0].ToLowerInvariant() : "";
            switch (sub)
            {
                case "token":
                    if (args.Length < 2) return "devlog token <github fine-grained PAT with contents:write on " + Repo + ">";
                    PlayerPrefs.SetString(TokenKey, args[1]); PlayerPrefs.Save(); return "devlog: token saved; uploads to " + Branch;
                case "user":
                    if (args.Length < 2) return "devlog user <slug>";
                    PlayerPrefs.SetString(UserKey, args[1]); PlayerPrefs.Save(); return "devlog: user " + args[1];
                case "clear": PlayerPrefs.DeleteKey(TokenKey); PlayerPrefs.Save(); return "devlog: token cleared";
                case "send": Flush(true); if (Pump.Instance) UploadPending(Pump.Instance); return "devlog: flushed" + (string.IsNullOrEmpty(Token) ? " (no token: pull with adb)" : ", uploading");
                default:
                    return $"devlog: session {_sessionId}, {_errors} err / {_exceptions} exc / {_warnings} warn, token {(string.IsNullOrEmpty(Token) ? "none" : "set")}, prev crashed {_previousCrashed}, dir {_sessionDir}";
            }
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length + 16);
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
            public static Pump Instance;
            void Awake() { Instance = this; _nextFlush = Time.realtimeSinceStartup + FlushEvery; }
            void Start() => UploadPending(this); // unsent bundles from earlier sessions
            void Update() => Tick();
            void OnApplicationPause(bool paused)
            {
                if (paused) { WriteAlive("clean"); Flush(true); UploadPending(this); }
                else WriteAlive("alive");
            }
            void OnApplicationQuit() { WriteAlive("clean"); Flush(true); }
        }
#endif
    }
}
