using System;
using System.IO;
using System.Text;
using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.AI
{
    /// <summary>
    /// File-based remote control for the Skim Race AI benchmark, so a headless driver (a CI job,
    /// a terminal session, an agent) can start runs and read progress from an OPEN editor without
    /// a GUI and without depending on any particular editor-automation package.
    ///
    /// Drop <c>Library/SkimRaceAIRemote/command.json</c> containing
    /// <c>{"op":"bench","races":10,"intensity":4,"players":2,"limit":70,"timeout":150}</c>
    /// (or <c>{"op":"stop"}</c>); the editor consumes it within a second.
    /// <c>Library/SkimRaceAIRemote/status.json</c> is rewritten every second with play state,
    /// compile state and runner progress, and <c>errors.log</c> collects every console error and
    /// exception. Inert when no command file exists. Library/ is not version-controlled.
    /// </summary>
    [InitializeOnLoad]
    public static class SkimRaceBenchmarkRemote
    {
        [Serializable]
        class Command
        {
            public string op = "";
            public int races = 10;
            public int intensity = 4;
            public int players = 2;
            public float limit = 70f;
            public float timeout = 150f;
            public string filter = "";
            public int quality = -1;
        }

        /// <summary>Runs edit-mode tests matching <paramref name="filter"/> and writes tests.json.</summary>
        static void RunTests(string filter)
        {
            var api = UnityEngine.ScriptableObject.CreateInstance<UnityEditor.TestTools.TestRunner.Api.TestRunnerApi>();
            api.RegisterCallbacks(new TestCollector());
            api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(
                new UnityEditor.TestTools.TestRunner.Api.Filter
                {
                    testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
                    groupNames = new[] { filter },
                }));
        }

        class TestCollector : UnityEditor.TestTools.TestRunner.Api.ICallbacks
        {
            readonly StringBuilder _sb = new();
            int _pass, _fail;
            public void RunStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor t) { }
            public void TestStarted(UnityEditor.TestTools.TestRunner.Api.ITestAdaptor t) { }
            public void TestFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                bool ok = r.TestStatus == UnityEditor.TestTools.TestRunner.Api.TestStatus.Passed;
                if (ok) _pass++; else _fail++;
                _sb.AppendLine($"{r.TestStatus} {r.FullName} {(ok ? "" : r.Message)}");
            }
            public void RunFinished(UnityEditor.TestTools.TestRunner.Api.ITestResultAdaptor r)
            {
                File.WriteAllText(Path.Combine(Dir, "tests.txt"), $"passed={_pass} failed={_fail}\n" + _sb);
            }
        }

        [Serializable]
        class Status
        {
            public string utc;
            public bool playing;
            public bool compiling;
            public bool compileFailed;
            public string scene;
            public bool runnerActive;
            public bool runnerDone;
            public int completed;
            public int successes;
            public string resultsFile;
            public string lastCommand;
        }

        static readonly string Dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "SkimRaceAIRemote");
        static double _next;
        static string _lastCommand = "";

        static SkimRaceBenchmarkRemote()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            try
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(Path.Combine(Dir, "errors.log"),
                    $"[{DateTime.UtcNow:O}] {type}: {condition}\n{stack}\n", Encoding.UTF8);
            }
            catch { /* logging must never throw */ }
        }

        static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _next) return;
            _next = EditorApplication.timeSinceStartup + 1.0;
            try
            {
                Directory.CreateDirectory(Dir);
                string cmdPath = Path.Combine(Dir, "command.json");
                if (File.Exists(cmdPath))
                {
                    string json = File.ReadAllText(cmdPath);
                    File.Delete(cmdPath);
                    Execute(JsonUtility.FromJson<Command>(json));
                    _lastCommand = json;
                }

                var r = SkimRaceBenchmarkRunner.Active;
                var st = new Status
                {
                    utc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    playing = EditorApplication.isPlaying,
                    compiling = EditorApplication.isCompiling,
                    compileFailed = EditorUtility.scriptCompilationFailed,
                    scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                    runnerActive = r != null,
                    runnerDone = r != null && r.Done,
                    completed = r != null ? r.Completed : 0,
                    successes = r != null ? r.Successes : 0,
                    resultsFile = r != null ? r.ResultsFile : "",
                    lastCommand = _lastCommand,
                };
                File.WriteAllText(Path.Combine(Dir, "status.json"), JsonUtility.ToJson(st, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SkimRaceBenchmarkRemote] {e.Message}");
            }
        }

        // What the editor is spending its frame on: render resolution, quality, open views.
        // A benchmark frame rate that is GPU-bound by the editor's own windows is not the game's.
        static void WriteDiagnostics()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"screen {Screen.width}x{Screen.height} dpi={Screen.dpi}");
            sb.AppendLine($"quality {QualitySettings.GetQualityLevel()} {QualitySettings.names[QualitySettings.GetQualityLevel()]} vsync={QualitySettings.vSyncCount} aa={QualitySettings.antiAliasing}");
            sb.AppendLine($"targetFrameRate {Application.targetFrameRate} timeScale {Time.timeScale}");
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                sb.AppendLine($"window {w.GetType().Name} {w.position.width}x{w.position.height} focused={w == EditorWindow.focusedWindow}");
            foreach (var cam in Camera.allCameras)
                sb.AppendLine($"camera {cam.name} enabled={cam.enabled} target={(cam.targetTexture ? cam.targetTexture.width + "x" + cam.targetTexture.height : "screen")} hdr={cam.allowHDR}");
            File.WriteAllText(Path.Combine(Dir, "diag.txt"), sb.ToString());
        }

        static void Execute(Command c)
        {
            if (c == null) return;
            switch (c.op)
            {
                case "bench":
                    if (EditorApplication.isPlaying)
                    {
                        // Leave Play mode first; the command is re-queued for the next tick.
                        EditorApplication.isPlaying = false;
                        File.WriteAllText(Path.Combine(Dir, "command.json"), JsonUtility.ToJson(c));
                        break;
                    }
                    SkimRaceBenchmarkWindow.StartFromEditor(c.races, c.intensity, c.players, c.limit, c.timeout,
                        promptToSave: false, qualityLevel: c.quality);
                    break;
                case "stop":
                    EditorApplication.isPlaying = false;
                    break;
                case "refresh":
                    AssetDatabase.Refresh();
                    break;
                case "tests":
                    RunTests(string.IsNullOrEmpty(c.filter) ? "CosmicShore.Tests.SkimRaceAITests" : c.filter);
                    break;
                case "diag":
                    WriteDiagnostics();
                    break;
            }
        }
    }
}
