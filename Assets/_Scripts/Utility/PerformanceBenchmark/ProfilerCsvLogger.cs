using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// Standalone per-frame profiler logger. Samples a small fixed set of
    /// <see cref="ProfilerRecorder"/> stats once per frame and writes them to a CSV under
    /// <c>persistentDataPath/ProfilerCaptures/</c>, plus a compact summary .txt at the end.
    ///
    /// Raw numbers only - no scoring/grading (unlike the benchmark Runner). Built for long
    /// (minutes-long) gameplay captures: the CSV lets you pinpoint WHICH frames spike and
    /// WHICH subsystem owns them (render vs GC vs CPU/physics), so you can then deep-profile
    /// just those frames.
    ///
    /// Cost: zero when not capturing (no recorders allocated). While capturing, the per-frame
    /// path only reads recorder values and appends to an in-memory buffer; the file is written
    /// on Stop (plus an optional periodic safety flush), so the logger barely perturbs what it
    /// measures.
    ///
    /// Access (any of):
    ///  - Editor menu: FrogletTools > Performance > Profiler CSV Logger> Start / Stop &amp; Save / Open Folder
    ///  - Add this component to a scene/bootstrap object with autoStartOnEnable = true (logs a
    ///    whole play session hands-free, writes on play-exit).
    ///  - Call ProfilerCsvLogger.StartCapture() / StopCapture() from code.
    /// </summary>
    public class ProfilerCsvLogger : MonoBehaviour
    {
        [Header("Capture")]
        [Tooltip("Begin capturing automatically when this component is enabled (e.g. on Play). " +
                 "Writes the file on disable / play-exit.")]
        [SerializeField] bool autoStartOnEnable;

        [Tooltip("Optional label baked into the file name (the active scene name is always included).")]
        [SerializeField] string captureLabel = "";

        [Tooltip("Append buffered rows to disk every N frames as a crash-safety flush. " +
                 "0 = only write on Stop (cleanest, but data is lost if the editor crashes).")]
        [SerializeField] int safetyFlushEveryFrames = 1800;

        [Header("Output")]
        [Tooltip("Subfolder inside Application.persistentDataPath for captures.")]
        [SerializeField] string outputFolder = "ProfilerCaptures";

        [Header("Script Markers (CPU attribution)")]
        [Tooltip("Named ProfilerMarkers to log as extra per-frame ms columns, so the CSV self-attributes " +
                 "main-thread CPU without a deep-profile capture. Default category is Scripts; prefix with " +
                 "'Category:' to override (e.g. 'Physics:Physics.Processing', 'Network:CSM.Net.SpawnDespawn'). " +
                 "A marker only logs once it has been hit at least once; otherwise its column is blank.")]
        [SerializeField] string[] scriptMarkers =
        {
            "ObjectiveIndicator.LateUpdate",
        };

        // ----- static access -------------------------------------------------
        public static ProfilerCsvLogger Instance { get; private set; }
        public static bool IsCapturing => Instance != null && Instance._capturing;

        /// <summary>Spawns a persistent host (if needed) and begins a capture.</summary>
        public static ProfilerCsvLogger StartCapture(string label = null)
        {
            if (Instance == null)
            {
                var go = new GameObject("[ProfilerCsvLogger]");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<ProfilerCsvLogger>();
            }
            Instance.Begin(label);
            return Instance;
        }

        /// <summary>Stops the active capture, writes CSV + summary, returns the CSV path (or null).</summary>
        public static string StopCapture() => Instance != null ? Instance.End() : null;

        // ----- recorders -----------------------------------------------------
        ProfilerRecorder _mainThread;        // ns
        ProfilerRecorder _drawCalls;
        ProfilerRecorder _setPass;
        ProfilerRecorder _triangles;
        ProfilerRecorder _vertices;
        ProfilerRecorder _gcAlloc;           // bytes / frame
        ProfilerRecorder _physicsSendEvents; // ns - may be unavailable on some Unity versions

        // Optional named script markers -> one extra ms column each.
        ProfilerRecorder[] _markerRecorders;
        string[] _markerColumns;
        List<float>[] _markerSamples;        // ms per marker, for the summary

        // ----- state ---------------------------------------------------------
        bool _capturing;
        int _frame;
        float _startTime;
        string _csvPath;
        string _sceneName;
        StringBuilder _buffer;

        // Parallel per-frame arrays kept for the end-of-run summary / correlation.
        readonly List<float> _frameMs = new(16384);
        readonly List<float> _timeS = new(16384);
        readonly List<long> _tris = new(16384);
        readonly List<int> _draws = new(16384);
        readonly List<long> _gc = new(16384);

        void OnEnable()
        {
            if (autoStartOnEnable && !_capturing)
                Begin(captureLabel);
        }

        void OnDisable()
        {
            // Covers play-mode exit (DontDestroyOnLoad host is disabled/destroyed) and
            // component removal - make sure an in-flight capture is saved.
            if (_capturing) End();
        }

        void OnApplicationQuit()
        {
            if (_capturing) End();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Begin(string label)
        {
            if (_capturing) return;

            _sceneName = SceneManager.GetActiveScene().name;
            string lbl = string.IsNullOrEmpty(label) ? captureLabel : label;

            _mainThread        = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _drawCalls         = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _setPass           = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            _triangles         = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            _vertices          = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
            _gcAlloc           = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _physicsSendEvents = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.SendEvents");

            StartMarkerRecorders();

            _buffer = new StringBuilder(1 << 20);
            var header = new StringBuilder(
                "frame,time_s,frameMs,mainThreadMs,physicsSendEventsMs,gcAllocKB,drawCalls,setPass,triangles,vertices");
            if (_markerColumns != null)
                foreach (var col in _markerColumns) header.Append(',').Append(col);
            _buffer.AppendLine(header.ToString());

            _frameMs.Clear(); _timeS.Clear(); _tris.Clear(); _draws.Clear(); _gc.Clear();
            _frame = 0;
            _startTime = Time.realtimeSinceStartup;

            string dir = Path.Combine(Application.persistentDataPath, outputFolder);
            Directory.CreateDirectory(dir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string namePart = Sanitize(string.IsNullOrEmpty(lbl) ? _sceneName : $"{_sceneName}_{lbl}");
            _csvPath = Path.Combine(dir, $"perf_{namePart}_{stamp}.csv");

            _capturing = true;
            Debug.Log($"[ProfilerCsvLogger] Capture STARTED - scene '{_sceneName}'. Writing to:\n{_csvPath}");
        }

        void Update()
        {
            if (!_capturing) return;

            float frameMs = Time.unscaledDeltaTime * 1000f;
            double mainMs = NsToMs(_mainThread);
            double physMs = NsToMs(_physicsSendEvents);
            long gcBytes = LastLong(_gcAlloc);
            int draws = LastInt(_drawCalls);
            int setpass = LastInt(_setPass);
            long tris = LastLong(_triangles);
            long verts = LastLong(_vertices);
            float t = Time.realtimeSinceStartup - _startTime;

            var ci = CultureInfo.InvariantCulture;
            _buffer.Append(_frame).Append(',')
                   .Append(t.ToString("F3", ci)).Append(',')
                   .Append(frameMs.ToString("F2", ci)).Append(',')
                   .Append(mainMs >= 0 ? mainMs.ToString("F2", ci) : "").Append(',')
                   .Append(physMs >= 0 ? physMs.ToString("F2", ci) : "").Append(',')
                   .Append((gcBytes / 1024f).ToString("F1", ci)).Append(',')
                   .Append(draws).Append(',')
                   .Append(setpass).Append(',')
                   .Append(tris).Append(',')
                   .Append(verts);

            if (_markerRecorders != null)
            {
                for (int i = 0; i < _markerRecorders.Length; i++)
                {
                    double ms = NsToMs(_markerRecorders[i]);
                    _buffer.Append(',').Append(ms >= 0 ? ms.ToString("F2", ci) : "");
                    if (ms >= 0) _markerSamples[i].Add((float)ms);
                }
            }
            _buffer.Append('\n');

            _frameMs.Add(frameMs);
            _timeS.Add(t);
            _tris.Add(tris);
            _draws.Add(draws);
            _gc.Add(gcBytes);

            _frame++;

            if (safetyFlushEveryFrames > 0 && _frame % safetyFlushEveryFrames == 0)
                Flush();
        }

        void StartMarkerRecorders()
        {
            _markerRecorders = null;
            _markerColumns = null;
            _markerSamples = null;
            if (scriptMarkers == null || scriptMarkers.Length == 0) return;

            var recs = new List<ProfilerRecorder>(scriptMarkers.Length);
            var cols = new List<string>(scriptMarkers.Length);
            var samples = new List<List<float>>(scriptMarkers.Length);

            foreach (var entry in scriptMarkers)
            {
                if (string.IsNullOrWhiteSpace(entry)) continue;

                // Optional "Category:MarkerName" prefix; default category is Scripts.
                var category = ProfilerCategory.Scripts;
                string markerName = entry.Trim();
                int colon = markerName.IndexOf(':');
                if (colon > 0)
                {
                    category = ParseCategory(markerName.Substring(0, colon));
                    markerName = markerName.Substring(colon + 1).Trim();
                }
                if (markerName.Length == 0) continue;

                recs.Add(ProfilerRecorder.StartNew(category, markerName));
                cols.Add(Sanitize(markerName) + "_ms");
                samples.Add(new List<float>(4096));
            }

            if (recs.Count == 0) return;
            _markerRecorders = recs.ToArray();
            _markerColumns = cols.ToArray();
            _markerSamples = samples.ToArray();
        }

        static ProfilerCategory ParseCategory(string token) => token.Trim().ToLowerInvariant() switch
        {
            "scripts" => ProfilerCategory.Scripts,
            "internal" => ProfilerCategory.Internal,
            "physics" => ProfilerCategory.Physics,
            "render" => ProfilerCategory.Render,
            "network" => ProfilerCategory.Network,
            "memory" => ProfilerCategory.Memory,
            _ => ProfilerCategory.Scripts,
        };

        void Flush()
        {
            if (_buffer == null || _buffer.Length == 0) return;
            try
            {
                File.AppendAllText(_csvPath, _buffer.ToString());
                _buffer.Clear();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProfilerCsvLogger] Flush failed: {e.Message}");
            }
        }

        string End()
        {
            if (!_capturing) return null;
            _capturing = false;

            Flush(); // write any remaining buffered rows

            _mainThread.Dispose();
            _drawCalls.Dispose();
            _setPass.Dispose();
            _triangles.Dispose();
            _vertices.Dispose();
            _gcAlloc.Dispose();
            _physicsSendEvents.Dispose();
            if (_markerRecorders != null)
                for (int i = 0; i < _markerRecorders.Length; i++) _markerRecorders[i].Dispose();

            string summary = BuildSummary();
            string summaryPath = Path.Combine(
                Path.GetDirectoryName(_csvPath) ?? ".",
                Path.GetFileNameWithoutExtension(_csvPath) + "_summary.txt");
            try { File.WriteAllText(summaryPath, summary); }
            catch (Exception e) { Debug.LogWarning($"[ProfilerCsvLogger] Summary write failed: {e.Message}"); }

            Debug.Log($"[ProfilerCsvLogger] Capture STOPPED - {_frameMs.Count} frames.\n" +
                      $"CSV:     {_csvPath}\nSummary: {summaryPath}\n\n{summary}");
            return _csvPath;
        }

        // --------------------------------------------------------------------
        // Summary
        // --------------------------------------------------------------------
        string BuildSummary()
        {
            int n = _frameMs.Count;
            var sb = new StringBuilder(2048);
            sb.AppendLine("=== Profiler CSV Capture Summary ===");
            sb.AppendLine($"scene        : {_sceneName}");
            sb.AppendLine($"frames       : {n}");
            if (n == 0) { sb.AppendLine("(no frames captured)"); return sb.ToString(); }

            float duration = _timeS[n - 1];
            sb.AppendLine($"duration_s   : {duration:F1}");
            sb.AppendLine($"avg_fps      : {(duration > 0 ? n / duration : 0):F1}");

            var sorted = new List<float>(_frameMs);
            sorted.Sort();
            float avg = 0f; for (int i = 0; i < n; i++) avg += _frameMs[i]; avg /= n;

            sb.AppendLine();
            sb.AppendLine("frame time (ms):");
            sb.AppendLine($"  avg={avg:F2}  p50={Pct(sorted, 0.50f):F2}  p95={Pct(sorted, 0.95f):F2}  " +
                          $"p99={Pct(sorted, 0.99f):F2}  max={sorted[n - 1]:F2}  min={sorted[0]:F2}");
            sb.AppendLine($"  frames >16.7ms (<60fps): {CountOver(16.7f)} ({100f * CountOver(16.7f) / n:F1}%)");
            sb.AppendLine($"  frames >33.3ms (<30fps): {CountOver(33.3f)} ({100f * CountOver(33.3f) / n:F1}%)");

            // Medians for render/GC, so the worst-frame list can be read in context.
            long medTris = MedianLong(_tris);
            int medDraws = MedianInt(_draws);
            sb.AppendLine();
            sb.AppendLine($"render median: tris={medTris:N0}  draws={medDraws:N0}");

            sb.AppendLine();
            sb.AppendLine("worst 15 frames (frame | time_s | frameMs | tris | draws | gcKB):");
            var idx = new int[n];
            for (int i = 0; i < n; i++) idx[i] = i;
            Array.Sort(idx, (a, b) => _frameMs[b].CompareTo(_frameMs[a]));
            int top = Math.Min(15, n);
            for (int i = 0; i < top; i++)
            {
                int f = idx[i];
                sb.AppendLine($"  {f,7} | {_timeS[f],7:F2} | {_frameMs[f],8:F2} | " +
                              $"{_tris[f],12:N0} | {_draws[f],6:N0} | {_gc[f] / 1024f,9:F1}");
            }

            if (_markerColumns != null && _markerColumns.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("script markers (ms - avg / p95 / max, % of avg frame):");
                for (int m = 0; m < _markerColumns.Length; m++)
                {
                    var s = _markerSamples[m];
                    if (s.Count == 0)
                    {
                        sb.AppendLine($"  {_markerColumns[m]}: (never hit / unavailable)");
                        continue;
                    }
                    var sc = new List<float>(s); sc.Sort();
                    float ma = 0f; for (int i = 0; i < s.Count; i++) ma += s[i]; ma /= s.Count;
                    float p95 = sc[Mathf.Clamp(Mathf.RoundToInt(0.95f * (sc.Count - 1)), 0, sc.Count - 1)];
                    sb.AppendLine($"  {_markerColumns[m]}: avg={ma:F2}  p95={p95:F2}  max={sc[sc.Count - 1]:F2}  " +
                                  $"({(avg > 0 ? 100f * ma / avg : 0):F1}% of avg frame)");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Read: a worst frame with tris/draws far above the render median => render/geometry-bound; " +
                          "high gcKB => GC; neither => CPU (scripts/physics) - check the script-marker columns / deep-profile that frame.");
            return sb.ToString();
        }

        float Pct(List<float> sortedAsc, float p)
        {
            if (sortedAsc.Count == 0) return 0f;
            int i = Mathf.Clamp(Mathf.RoundToInt(p * (sortedAsc.Count - 1)), 0, sortedAsc.Count - 1);
            return sortedAsc[i];
        }

        int CountOver(float ms)
        {
            int c = 0;
            for (int i = 0; i < _frameMs.Count; i++) if (_frameMs[i] > ms) c++;
            return c;
        }

        static long MedianLong(List<long> values)
        {
            if (values.Count == 0) return 0;
            var copy = new List<long>(values); copy.Sort();
            return copy[copy.Count / 2];
        }

        static int MedianInt(List<int> values)
        {
            if (values.Count == 0) return 0;
            var copy = new List<int>(values); copy.Sort();
            return copy[copy.Count / 2];
        }

        // --------------------------------------------------------------------
        static double NsToMs(ProfilerRecorder r) => r.Valid && r.Count > 0 ? r.LastValue * 1e-6 : -1.0;
        static long LastLong(ProfilerRecorder r) => r.Valid && r.Count > 0 ? r.LastValue : 0;
        static int LastInt(ProfilerRecorder r) => r.Valid && r.Count > 0 ? (int)r.LastValue : 0;

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "capture";
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0 || chars[i] == ' ')
                    chars[i] = '_';
            return new string(chars);
        }
    }
}
