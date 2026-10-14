#if UNITY_EDITOR
using System;
using System.Globalization;
using CosmicShore.Editor.Froglet;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    /// <summary>
    /// FrogletTools > Performance > Export Profiler Frames to JSON — writes the frames the
    /// Profiler window currently holds as the same report the in-game <c>prof</c> command writes.
    ///
    /// <para>It exists because <c>prof</c> runs inside Play mode and can only read the Editor's own
    /// frames, while the frame target is judged in a DEVELOPMENT BUILD. Attach the Profiler to the
    /// build, record the scenario, stop, press Export: the build's frames become a JSON report
    /// with no screenshots. It reads a loaded <c>.data</c> capture the same way, so a recording
    /// saved from the Profiler window can be exported later.</para>
    ///
    /// <para>A READER tool (Docs/TOOLING.md): it writes report files to
    /// <c>Documents/CosmicShore Diagnostics</c> and never touches an asset, so it carries no ship
    /// panel.</para>
    /// </summary>
    public class ProfilerJsonExporterWindow : EditorWindow
    {
        [SerializeField] string label = "";
        [SerializeField] bool allFrames = true;
        [SerializeField] int lastFrames = 300;

        string _lastPath = "";
        string _lastSummary = "";

        const int MaxExportFrames = 2000;

        [MenuItem("FrogletTools/Performance/Export Profiler Frames to JSON", false, 21)]
        [FrogletTool(FrogletToolCategory.Performance, Importance = 4,
            Description = "Export what the Profiler holds (Play mode, a connected Development build, or a loaded .data file) as the prof JSON report.")]
        public static void Open()
        {
            var window = GetWindow<ProfilerJsonExporterWindow>("Profiler → JSON");
            window.minSize = new Vector2(480, 320);
            window.Show();
        }

        // The buffer grows while the Profiler records; keep the frame count on screen current.
        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            var accent = FrogletEditorPalette.ColorFor(FrogletToolCategory.Performance);
            FrogletEditorPalette.Banner("Profiler → JSON",
                "The frames the Profiler window holds, written as the prof report", accent);
            EditorGUILayout.Space(6);

            int first = ProfilerFrameReader.FirstFrame;
            int last = ProfilerFrameReader.LastFrame;
            int available = first >= 0 && last >= first ? last - first + 1 : 0;

            EditorGUILayout.LabelField(available > 0
                ? $"Frames in the Profiler: {available} ({first}–{last})"
                : "The Profiler holds no frames. Open Window > Analysis > Profiler, attach it to the " +
                  "build (or enter Play mode), press Record, run the scenario, then stop.",
                EditorStyles.wordWrappedLabel);

            if (ProfilerFrameReader.Recording)
                EditorGUILayout.HelpBox("Record is ON. Export turns it off while reading (a buffer " +
                                        "that keeps recording evicts the frames being read) and back on after.",
                                        MessageType.Info);
            if (ProfilerFrameReader.DeepProfiling)
                EditorGUILayout.HelpBox("Deep Profile is ON: every script call is instrumented and its time " +
                                        "is several times too large. Use this capture to find WHERE, never HOW MUCH.",
                                        MessageType.Warning);

            EditorGUILayout.Space(4);
            label = EditorGUILayout.TextField(
                new GUIContent("Label", "Name it after the scenario and the build, e.g. S5_Wildlife_devbuild."),
                label);
            allFrames = EditorGUILayout.Toggle(new GUIContent("All frames in the buffer",
                $"Every frame the Profiler holds, up to {MaxExportFrames}."), allFrames);
            using (new EditorGUI.DisabledScope(allFrames))
                lastFrames = EditorGUILayout.IntSlider("Last N frames", lastFrames,
                    ProfilerCapture.MinFrames, MaxExportFrames);

            EditorGUILayout.Space(8);
            if (FrogletEditorPalette.ColorButton("Export JSON", accent, 160f, 28f,
                    enabled: available > 0))
                Export(first, last);

            if (!string.IsNullOrEmpty(_lastSummary))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(_lastSummary, EditorStyles.wordWrappedLabel);
                if (!string.IsNullOrEmpty(_lastPath) && GUILayout.Button("Show in folder", GUILayout.Width(140)))
                    EditorUtility.RevealInFinder(_lastPath);
            }
        }

        void Export(int first, int last)
        {
            int count = allFrames ? MaxExportFrames : lastFrames;
            first = Math.Max(first, last - count + 1);

            bool wasRecording = ProfilerFrameReader.Recording;
            var options = new ProfilerCapture.Options
            {
                label = label ?? "",
                frames = last - first + 1,
            };
            var report = new ProfilerCapture.Report
            {
                // A connected build's scene is not something the Editor can ask it, so the label
                // stands in for it in the file name.
                scene = string.IsNullOrWhiteSpace(label) ? "ProfilerBuffer" : "ProfilerExport",
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                label = label ?? "",
                framesRequested = options.frames,
                profilerWasRecording = wasRecording,
                deepProfiling = ProfilerFrameReader.DeepProfiling,
            };
            report.notes.Add("Exported from the Profiler window's buffer (Play mode, a connected build, or a loaded .data file) - the report does not know which; the label says.");

            bool completed = true;
            try
            {
                if (wasRecording) ProfilerFrameReader.Recording = false;

                var acc = new ProfilerCapture.Accumulator();
                var threads = new ProfilerCapture.ThreadAccumulator();
                for (int f = first; f <= last; f++)
                {
                    if ((f - first) % 20 == 0 &&
                        EditorUtility.DisplayCancelableProgressBar("Profiler → JSON",
                            $"Reading frame {f - first + 1} of {last - first + 1}",
                            (float)(f - first) / Math.Max(1, last - first + 1)))
                    {
                        completed = false;
                        break;
                    }

                    ProfilerFrameReader.ReadMainThread(f, acc);
                    if ((f - first) % ProfilerCapture.ThreadSampleStride == 0)
                        ProfilerFrameReader.ReadThreads(f, threads);
                }

                ProfilerFrameReader.Finish(report, acc, threads, options, completed);
                _lastPath = ProfilerFrameReader.Save(report, DiagnosticsHUD.OutputDirectory);
                _lastSummary = ProfilerCapture.Summarize(report, _lastPath);
                Debug.Log($"[ProfilerJsonExporter] {_lastSummary}");
            }
            catch (Exception e)
            {
                _lastPath = "";
                _lastSummary = $"Export failed: {e.Message}";
                Debug.LogError($"[ProfilerJsonExporter] {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (wasRecording) ProfilerFrameReader.Recording = true;
            }
        }
    }
}
#endif
