using System.IO;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// One button for a performance measurement: in Play mode, runs the HUD's <c>capture</c> command
    /// (burst, diag, prof, renderers, cells → one <c>capture_*.json</c>), then puts that file's text
    /// on the clipboard so it can be pasted into a chat or an issue as-is. It opens the Profiler
    /// window first, because <c>prof</c> reads the Profiler's frames and needs it open. Reader: it
    /// writes nothing under Assets, so it needs no ship panel (Docs/TOOLING.md).
    /// <c>Docs/PERFORMANCE_OPTIMIZATION.md</c> §4.6; <c>Tools/Build/perf_capture_report.py</c> reads the file.
    /// </summary>
    public class PerfCaptureWindow : EditorWindow
    {
        [MenuItem("FrogletTools/Performance/Capture Performance (one file)")]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 5,
            Description = "In Play mode: burst + diag + prof + renderers + cells in one run, saved as one JSON " +
                          "and copied to the clipboard - the whole measurement to paste, nothing to transcribe.")]
        public static void Open()
        {
            var window = GetWindow<PerfCaptureWindow>(false, "Capture Performance");
            window.minSize = new Vector2(440f, 300f);
            window.Show();
        }

        string _label = "";
        int _seconds = 15;
        string _lastCopied;
        bool _wasRunning;

        void OnEnable() => EditorApplication.update += Tick;
        void OnDisable() => EditorApplication.update -= Tick;

        void Tick()
        {
            bool running = EditorApplication.isPlaying && DiagnosticsHUD.CaptureRunning;
            if (_wasRunning && !running)
            {
                // The capture just finished: copy without asking, so the next action is "paste".
                string path = DiagnosticsHUD.LastCapturePath;
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && _lastCopied != path)
                {
                    GUIUtility.systemCopyBuffer = File.ReadAllText(path);
                    _lastCopied = path;
                }
            }
            if (running != _wasRunning) Repaint();
            _wasRunning = running;
        }

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Capture Performance", "burst + diag + prof + renderers + cells, as one file on the clipboard",
                FrogletEditorPalette.Indigo);

            bool playing = EditorApplication.isPlaying;
            bool running = playing && DiagnosticsHUD.CaptureRunning;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            EditorGUILayout.Space(6f);
            if (!playing)
                EditorGUILayout.HelpBox("Enter Play mode, get into the scene and state to measure (the record names it: " +
                                        "S2 = Menu_Main with the Lattice cell at 8 min, S4 = Scurry intensity 4 ...), then capture.",
                                        MessageType.Info);

            using (new EditorGUI.DisabledScope(running))
            {
                _label = EditorGUILayout.TextField(new GUIContent("Label", "Tags the run: S2, S4, before, after ..."), _label);
                _seconds = EditorGUILayout.IntSlider(new GUIContent("diag seconds", "How long diag samples; prof then records 180 frames"), _seconds, 5, 120);
            }

            EditorGUILayout.Space(4f);
            var pill = GUILayoutUtility.GetRect(GUIContent.none, FrogletEditorPalette.Pill, GUILayout.Height(20f), GUILayout.ExpandWidth(true));
            string status = !playing ? "NOT PLAYING" : running ? "CAPTURING - hands off the editor" : "READY";
            FrogletEditorPalette.StatusPill(pill, $"{status} · scene {scene}", !playing ? FrogletEditorPalette.Warn : running ? FrogletEditorPalette.Info : FrogletEditorPalette.Ok);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("Capture", FrogletEditorPalette.Ok, 160f, 28f,
                        "Opens the Profiler, runs 'capture <label> <seconds>' in the HUD console, copies the file when done",
                        enabled: playing && !running))
                    StartCapture();
                if (FrogletEditorPalette.ColorButton("Copy last file", FrogletEditorPalette.Info, 140f, 28f,
                        "Puts the last capture_*.json text on the clipboard again",
                        enabled: !string.IsNullOrEmpty(DiagnosticsHUD.LastCapturePath)))
                {
                    GUIUtility.systemCopyBuffer = File.ReadAllText(DiagnosticsHUD.LastCapturePath);
                    _lastCopied = DiagnosticsHUD.LastCapturePath;
                }
                if (FrogletEditorPalette.ColorButton("Open folder", FrogletEditorPalette.Indigo, 110f, 28f,
                        DiagnosticsHUD.OutputDirectory))
                {
                    Directory.CreateDirectory(DiagnosticsHUD.OutputDirectory);
                    EditorUtility.RevealInFinder(DiagnosticsHUD.OutputDirectory);
                }
            }

            EditorGUILayout.Space(8f);
            string last = DiagnosticsHUD.LastCapturePath;
            if (!string.IsNullOrEmpty(last))
            {
                EditorGUILayout.LabelField("Last capture", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(last, EditorStyles.miniLabel, GUILayout.Height(16f));
                if (_lastCopied == last)
                    EditorGUILayout.HelpBox("On the clipboard. Paste it to Claude as-is (it is the whole measurement).", MessageType.None);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Before you capture", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Code Optimization: Release (the bug icon, bottom right). Debug runs C# ~5x slower.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Profiler: Deep Profile OFF. The window is opened for you.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("• Keep the Game view focused and do not touch the editor until the status says READY again.", EditorStyles.wordWrappedMiniLabel);
        }

        void StartCapture()
        {
            // prof reads frames from the Profiler window; without it open there are no frames to read.
            var profilerType = System.Type.GetType("UnityEditor.ProfilerWindow,UnityEditor");
            if (profilerType != null) GetWindow(profilerType, false, "Profiler", false);
            Focus();

            string label = string.IsNullOrWhiteSpace(_label) ? "" : _label.Trim();
            string answer = DiagnosticsHUD.RunConsole($"{DiagnosticsHUD.CaptureCommand} {label} {_seconds}".Replace("  ", " "));
            Debug.Log("[PerfCaptureWindow] " + answer);
            _wasRunning = DiagnosticsHUD.CaptureRunning;
        }
    }
}
