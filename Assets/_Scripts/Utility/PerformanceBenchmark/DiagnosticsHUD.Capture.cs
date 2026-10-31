using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
#endif

namespace CosmicShore.Utility.PerformanceBenchmark
{
    /// <summary>
    /// <c>capture &lt;label&gt; [seconds]</c>: every measurement this effort reads, in one command and
    /// ONE file. It runs, in order, <c>burst</c> (is Burst compiling the game's jobs), <c>diag</c>
    /// (frame statistics, per-system marker timings, the run environment), <c>prof</c> (the Profiler
    /// hierarchy, Editor only), <c>renderers</c> (the renderer census) and <c>cells</c> (the ecology's
    /// counts), collects every warning and error the console logged meanwhile, and writes them as one
    /// <c>capture_*.json</c> beside the diag reports. <c>capture copy</c> puts that file's text on the
    /// clipboard, so a run is: type the command, wait, type <c>capture copy</c>, paste.
    ///
    /// <para>The point is that the person measuring never transcribes anything: the file holds the
    /// same diag and prof JSON the separate commands write (unchanged, embedded), plus the lines the
    /// text-only commands answer. <c>Tools/Build/perf_capture_report.py</c> reads it back.</para>
    /// </summary>
    public partial class DiagnosticsHUD
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public const string CaptureCommand = "capture";
        const int CaptureProfFrames = 180;
        const int CaptureProfDepth = 6;
        const int CaptureProfTop = 40;
        const int CaptureLogLimit = 80;

        /// <summary>The bundle the last <c>capture</c> wrote, or null.</summary>
        public static string LastCapturePath { get; private set; }

        /// <summary>True while a <c>capture</c> is driving diag and prof.</summary>
        public static bool CaptureRunning => _instance != null && _instance._captureRunning;

        bool _captureRunning;
        readonly Dictionary<string, int> _captureLog = new(StringComparer.Ordinal);
        readonly List<string> _captureLogOrder = new();

        /// <summary>Runs one console line from code (the Editor's capture window uses it).</summary>
        public static string RunConsole(string raw) =>
            _instance != null ? _instance.ExecuteCommand(raw) : "no DiagnosticsHUD in this scene (it spawns in Play mode)";

        string HandleCaptureCommand(string[] args)
        {
            if (args is { Length: 1 } && args[0].Equals("copy", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(LastCapturePath) || !File.Exists(LastCapturePath)) return "no capture to copy yet";
                GUIUtility.systemCopyBuffer = File.ReadAllText(LastCapturePath);
                return $"copied {Path.GetFileName(LastCapturePath)} ({new FileInfo(LastCapturePath).Length / 1024} KB) to the clipboard";
            }
            if (args is { Length: 1 } && args[0].Equals("last", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrEmpty(LastCapturePath) ? "no capture yet" : LastCapturePath;

            if (_captureRunning) return "a capture is already running";
            if (_recording) return "a 'diag' recording is in progress — wait for it";
            if (_abRunning) return "an 'ab' run is in progress — wait for it, or 'ab stop'";
            if (_profRunning) return "a 'prof' capture is in progress — wait for it, or 'prof stop'";

            string label = "";
            int seconds = 15;
            foreach (string a in args)
            {
                if (int.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0) seconds = Mathf.Clamp(n, 5, 600);
                else if (!string.IsNullOrWhiteSpace(a)) label = a;
            }
            if (label.Length == 0) label = Sanitize(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

            StartCoroutine(RunCapture(label, seconds));
            return $"capture '{label}': burst, diag {seconds}s, prof {CaptureProfFrames} frames, renderers, cells → one file. Hands off the editor until it says saved.";
        }

        IEnumerator RunCapture(string label, int seconds)
        {
            _captureRunning = true;
            _captureLog.Clear();
            _captureLogOrder.Clear();
            Application.logMessageReceived += OnCaptureLog;
            var notes = new List<string>();
            string burst = "", diagPath = "", profPath = "", renderers = "", cells = "", prismPath = "", cameras = "", editor = "";
            try
            {
                SetStat("Capture", "run", "burst");
                burst = Safe(() => BurstProbe.Report(), notes, "burst");
                prismPath = Safe(() => CosmicShore.ECS.PrismRenderService.StatusLine(), notes, "prismpath");
                cameras = Safe(DescribeCameras, notes, "cameras");
                editor = Safe(DescribeEditor, notes, "editor");

                SetStat("Capture", "run", $"diag {seconds}s");
                string started = HandleDiagCommand(new[] { label, seconds.ToString(CultureInfo.InvariantCulture) });
                if (!_recording) { notes.Add("diag did not start: " + started); }
                while (_recording) yield return null;
                diagPath = _lastSavedPath;

#if UNITY_EDITOR
                SetStat("Capture", "run", $"prof {CaptureProfFrames} frames");
                string profStarted = HandleProfCommand(new[]
                {
                    label, CaptureProfFrames.ToString(CultureInfo.InvariantCulture),
                    "depth=" + CaptureProfDepth, "top=" + CaptureProfTop,
                });
                if (!_profRunning) notes.Add("prof did not start: " + profStarted);
                while (_profRunning) yield return null;
                if (_lastSavedPath != diagPath) profPath = _lastSavedPath;
#else
                notes.Add("prof is Editor-only; a build's capture has no Profiler hierarchy");
#endif

                SetStat("Capture", "run", "renderers, cells");
                renderers = Safe(() => HandleRenderersCommand(Array.Empty<string>()), notes, "renderers");
                cells = Safe(() => CellStateReport.Handle(Array.Empty<string>()), notes, "cells");
            }
            finally
            {
                Application.logMessageReceived -= OnCaptureLog;
                _captureRunning = false;
            }

            string path = WriteBundle(label, seconds, burst, prismPath, cameras, editor, diagPath, profPath, renderers, cells, notes);
            LastCapturePath = path;
            _lastSavedPath = path;
            _lastSavedShownAt = Time.unscaledTime;
            string line = $"capture saved: {path} — type 'capture copy' and paste it";
            SetStat("Capture", "run", "done");
            SetStat("Console", "›", line);
            Debug.Log("[DiagnosticsHUD] " + line);
            if (_visible) RefreshText();
        }

        void OnCaptureLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log) return;
            string key = type + ": " + (condition.Length > 300 ? condition.Substring(0, 300) : condition);
            if (_captureLog.TryGetValue(key, out int n)) { _captureLog[key] = n + 1; return; }
            if (_captureLogOrder.Count >= CaptureLogLimit) return;
            _captureLog[key] = 1;
            _captureLogOrder.Add(key);
        }

        static string Safe(Func<string> f, List<string> notes, string what)
        {
            try { return f() ?? ""; }
            catch (Exception e) { notes.Add($"{what} failed: {e.Message}"); return ""; }
        }

        /// <summary>
        /// The bundle is hand-assembled JSON: the diag and prof files are embedded VERBATIM as objects
        /// (JsonUtility cannot nest arbitrary documents), the text answers as strings.
        /// </summary>
        /// <summary>
        /// Every enabled camera and what it renders to. A second camera drawing the world (the Third Eye
        /// window, a preview, a Scene view) renders the scene again that frame, so a capture taken with one
        /// open measures two frames' worth of rendering; the reader warns when it sees more than one.
        /// </summary>
        static string DescribeCameras()
        {
            var cams = Camera.allCameras;
            var sb = new StringBuilder();
            sb.Append(cams.Length).Append(" enabled");
            for (int i = 0; i < cams.Length; i++)
            {
                var c = cams[i];
                if (!c) continue;
                sb.Append(" | ").Append(c.name).Append(' ')
                  .Append(c.targetTexture ? $"{c.targetTexture.width}x{c.targetTexture.height} rt" : "screen")
                  .Append(" depth ").Append(c.depth.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// The editor's own share of the frame, which no game camera shows: open Scene views (a visible
        /// one renders the world again every Play frame), the Game view's size, and whether the Profiler
        /// is deep-profiling or profiling the editor too (either multiplies the cost of every C# call).
        /// Read before the capture opens the Profiler, so it is the state the person was playing in.
        /// Empty in a build. The reader judges it (perf_capture_report.py, validity).
        /// </summary>
        static string DescribeEditor()
        {
#if UNITY_EDITOR
            return string.Format(CultureInfo.InvariantCulture,
                "sceneViews {0} | gameView {1}x{2} | deepProfile {3} | profileEditor {4} | profilerRecording {5}",
                UnityEditor.SceneView.sceneViews.Count, Screen.width, Screen.height,
                UnityEditorInternal.ProfilerDriver.deepProfiling ? 1 : 0,
                UnityEditorInternal.ProfilerDriver.profileEditor ? 1 : 0,
                UnityEditorInternal.ProfilerDriver.enabled ? 1 : 0);
#else
            return "";
#endif
        }

        static string s_captureGitCommit;
        static string s_captureGitBranch;

        string WriteBundle(string label, int seconds, string burst, string prismPath, string cameras, string editor, string diagPath, string profPath,
                           string renderers, string cells, List<string> notes)
        {
            var sb = new StringBuilder(64 * 1024);
            sb.Append("{\n");
            sb.Append("  \"kind\": \"perf-capture\",\n  \"version\": 1,\n");
            Field(sb, "label", label);
            Field(sb, "scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            Field(sb, "timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            // The commit the capture measured (Yash, 2026-10-30): the sweep groups and judges by it, so a regression
            // points at a commit range instead of a guess from the timestamp. Cached once per session (git subprocess).
            if (s_captureGitCommit == null)
            {
                s_captureGitCommit = BenchmarkReport.TryRunGit("rev-parse --short HEAD") ?? "";
                s_captureGitBranch = BenchmarkReport.TryRunGit("rev-parse --abbrev-ref HEAD") ?? "";
            }
            Field(sb, "gitCommitHash", s_captureGitCommit);
            Field(sb, "gitBranch", s_captureGitBranch);
            sb.Append("  \"diagSeconds\": ").Append(seconds).Append(",\n");
            Field(sb, "prismPath", prismPath);
            Field(sb, "cameras", cameras);
            Field(sb, "editor", editor);
            Field(sb, "burst", burst);
            Field(sb, "renderers", renderers);
            Field(sb, "cells", cells);
            Field(sb, "diagPath", diagPath);
            Field(sb, "profPath", profPath);
            sb.Append("  \"notes\": [");
            for (int i = 0; i < notes.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(Quote(notes[i])); }
            sb.Append("],\n");
            sb.Append("  \"log\": [");
            for (int i = 0; i < _captureLogOrder.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append("\n    {\"count\": ").Append(_captureLog[_captureLogOrder[i]]).Append(", \"message\": ").Append(Quote(_captureLogOrder[i])).Append('}');
            }
            sb.Append("\n  ],\n");
            sb.Append("  \"diag\": ").Append(ReadJsonOrNull(diagPath)).Append(",\n");
            sb.Append("  \"prof\": ").Append(ReadJsonOrNull(profPath)).Append('\n');
            sb.Append("}\n");

            try
            {
                string dir = OutputDirectory;
                Directory.CreateDirectory(dir);
                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                string path = Path.Combine(dir, $"capture_{Sanitize(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)}_{Sanitize(label)}_{stamp}.json");
                File.WriteAllText(path, sb.ToString());
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiagnosticsHUD] Could not save the capture: {e.Message}");
                return "(save failed: " + e.Message + ")";
            }
        }

        static string ReadJsonOrNull(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "null";
                string text = File.ReadAllText(path).Trim();
                return text.StartsWith("{") ? text : "null";
            }
            catch { return "null"; }
        }

        static void Field(StringBuilder sb, string name, string value) =>
            sb.Append("  ").Append(Quote(name)).Append(": ").Append(Quote(value ?? "")).Append(",\n");

        static string Quote(string s)
        {
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
#endif
    }
}
