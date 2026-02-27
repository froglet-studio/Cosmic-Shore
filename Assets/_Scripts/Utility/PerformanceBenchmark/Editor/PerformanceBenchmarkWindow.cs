#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Soap;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.PerformanceBenchmark.Editor
{
    public class PerformanceBenchmarkWindow : EditorWindow
    {
        // ── Tab state ───────────────────────────────────
        enum Tab { Run, History, Compare }
        Tab activeTab = Tab.Run;

        // ── Run tab ─────────────────────────────────────
        BenchmarkConfigSO config;
        BenchmarkDataSO benchmarkData;
        PerformanceBenchmarkRunner activeRunner;
        BenchmarkReport lastReport;
        string lastReportPath;
        bool showSettingsFoldout;

        // ── History tab ─────────────────────────────────
        Vector2 historyScroll;
        List<BenchmarkHistory.IndexEntry> historyEntries = new();
        string tagEditId;
        string tagEditValue = "";

        // ── Compare tab ─────────────────────────────────
        Vector2 compareScroll;
        int baselineIndex = -1;
        int currentIndex = -1;
        BenchmarkComparer.ComparisonResult comparisonResult;
        string comparisonText;

        [MenuItem("FrogletTools/Performance Benchmark", false, 20)]
        public static void Open()
        {
            var window = GetWindow<PerformanceBenchmarkWindow>("Performance Benchmark");
            window.minSize = new Vector2(560, 420);
            window.Show();
        }

        void OnEnable()
        {
            RefreshHistory();
        }

        void OnGUI()
        {
            DrawTabs();

            switch (activeTab)
            {
                case Tab.Run: DrawRunTab(); break;
                case Tab.History: DrawHistoryTab(); break;
                case Tab.Compare: DrawCompareTab(); break;
            }
        }

        void Update()
        {
            if (activeRunner != null && activeRunner.IsRunning)
                Repaint();
        }

        // ── Tabs ────────────────────────────────────────

        void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Toggle(activeTab == Tab.Run, "Run", EditorStyles.toolbarButton))
                activeTab = Tab.Run;
            if (GUILayout.Toggle(activeTab == Tab.History, $"History ({historyEntries.Count})", EditorStyles.toolbarButton))
                activeTab = Tab.History;
            if (GUILayout.Toggle(activeTab == Tab.Compare, "Compare", EditorStyles.toolbarButton))
                activeTab = Tab.Compare;
            EditorGUILayout.EndHorizontal();
        }

        // ════════════════════════════════════════════════
        // ── Run Tab ─────────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawRunTab()
        {
            EditorGUILayout.Space(8);

            // ── Config assignment ──────────────────────
            config = (BenchmarkConfigSO)EditorGUILayout.ObjectField(
                "Config", config, typeof(BenchmarkConfigSO), false);

            benchmarkData = (BenchmarkDataSO)EditorGUILayout.ObjectField(
                "Data Container (optional)", benchmarkData, typeof(BenchmarkDataSO), false);

            if (config == null)
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.HelpBox(
                    "Getting started:\n" +
                    "1. Right-click in Project > Create > CosmicShore > Tools > Benchmark Config\n" +
                    "2. Drag the new asset into the Config slot above\n" +
                    "3. Enter Play Mode and click 'Start Benchmark'\n\n" +
                    "Every run is saved automatically. You can compare any two runs in the History tab.",
                    MessageType.Info);
                return;
            }

            // ── Settings foldout ───────────────────────
            showSettingsFoldout = EditorGUILayout.Foldout(showSettingsFoldout, "Benchmark Settings", true);
            if (showSettingsFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Warmup", $"{config.WarmupDuration}s  (scene stabilizes before measurement)");
                EditorGUILayout.LabelField("Sample Duration", $"{config.SampleDuration}s  (how long to record)");
                EditorGUILayout.LabelField("Label", string.IsNullOrEmpty(config.BenchmarkLabel) ? "(none — set one to identify this run)" : config.BenchmarkLabel);

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Capturing:", EditorStyles.miniBoldLabel);
                DrawCaptureToggle("Rendering (draw calls, batches, triangles)", config.CaptureRenderingStats);
                DrawCaptureToggle("Memory (heap size, GC allocations)", config.CaptureMemoryStats);
                DrawCaptureToggle("Physics (active rigidbodies)", config.CapturePhysicsStats);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);

            // ── Run / Progress ─────────────────────────
            bool isPlaying = Application.isPlaying;

            if (activeRunner != null && activeRunner.IsRunning)
            {
                float progress = activeRunner.Progress;
                var rect = EditorGUILayout.GetControlRect(false, 22);
                EditorGUI.ProgressBar(rect, progress, $"Benchmarking... {progress * 100:F0}%");

                if (benchmarkData != null && benchmarkData.IsSampling)
                {
                    EditorGUILayout.LabelField(
                        $"  Frames captured: {benchmarkData.FramesCaptured}",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Stop Early"))
                    activeRunner.StopBenchmark();
            }
            else
            {
                if (!isPlaying)
                {
                    EditorGUILayout.HelpBox(
                        "Enter Play Mode to run a benchmark.\n" +
                        "Tip: Open Unity's Profiler window (Window > Analysis > Profiler) alongside " +
                        "this tool to see real-time counters under 'Scripts' module while the benchmark runs.",
                        MessageType.Warning);
                }

                using (new EditorGUI.DisabledScope(!isPlaying))
                {
                    if (GUILayout.Button("Start Benchmark", GUILayout.Height(32)))
                        StartBenchmarkInPlayMode();
                }
            }

            // ── Last result summary ────────────────────
            if (lastReport?.statistics != null)
            {
                EditorGUILayout.Space(12);
                DrawHealthGrade(lastReport.statistics);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Last Run Results", EditorStyles.boldLabel);
                DrawStatsSummary(lastReport.statistics);

                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("View in History"))
                {
                    RefreshHistory();
                    activeTab = Tab.History;
                }
                if (!string.IsNullOrEmpty(lastReportPath) && GUILayout.Button("Open JSON File"))
                    EditorUtility.RevealInFinder(lastReportPath);
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawCaptureToggle(string label, bool enabled)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(enabled ? "  [x]" : "  [ ]", GUILayout.Width(30));
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawHealthGrade(BenchmarkStatistics stats)
        {
            // Simple health grading: A/B/C/D/F based on avg FPS and frame time stability
            string grade;
            string explanation;
            Color gradeColor;

            if (stats.avgFps >= 55 && stats.p99FrameTimeMs < 25 && stats.stdDevFrameTimeMs < 5)
            {
                grade = "A"; explanation = "Excellent — smooth and stable"; gradeColor = new Color(0.2f, 0.8f, 0.3f);
            }
            else if (stats.avgFps >= 45 && stats.p99FrameTimeMs < 35)
            {
                grade = "B"; explanation = "Good — playable with minor hitches"; gradeColor = new Color(0.5f, 0.8f, 0.2f);
            }
            else if (stats.avgFps >= 30 && stats.p99FrameTimeMs < 50)
            {
                grade = "C"; explanation = "Acceptable — noticeable frame drops"; gradeColor = new Color(0.9f, 0.75f, 0.1f);
            }
            else if (stats.avgFps >= 20)
            {
                grade = "D"; explanation = "Poor — frequent stutters, needs optimization"; gradeColor = new Color(0.9f, 0.4f, 0.1f);
            }
            else
            {
                grade = "F"; explanation = "Critical — not playable"; gradeColor = new Color(0.85f, 0.2f, 0.2f);
            }

            var rect = EditorGUILayout.GetControlRect(false, 36);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.15f));

            var gradeStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            gradeStyle.normal.textColor = gradeColor;
            EditorGUI.LabelField(new Rect(rect.x + 8, rect.y, 40, rect.height), grade, gradeStyle);

            var explStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(new Rect(rect.x + 48, rect.y, rect.width - 56, rect.height), explanation, explStyle);
        }

        void DrawStatsSummary(BenchmarkStatistics stats)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawStatRow("Avg FPS", $"{stats.avgFps:F1}", "Higher is better. Target: 60 for mobile.");
            DrawStatRow("Worst 1% FPS", $"{stats.p1Fps:F1}", "FPS during the worst spikes. Below 30 = visible stutter.");
            DrawStatRow("Avg Frame Time", $"{stats.avgFrameTimeMs:F2} ms", "Time per frame. 16.7ms = 60fps, 33.3ms = 30fps.");
            DrawStatRow("Worst 1% Frame Time", $"{stats.p99FrameTimeMs:F2} ms", "Frame time during the worst spikes.");
            DrawStatRow("Stability (StdDev)", $"{stats.stdDevFrameTimeMs:F2} ms", "Lower = more consistent. Above 5ms = noticeable hitching.");

            if (stats.avgDrawCalls > 0)
            {
                EditorGUILayout.Space(2);
                DrawStatRow("Draw Calls", $"{stats.avgDrawCalls:F0}", "GPU commands per frame. Lower is better.");
                DrawStatRow("Batches", $"{stats.avgBatches:F0}", "Grouped draw calls. Lower = better batching.");
                DrawStatRow("Triangles", $"{stats.avgTriangles:F0}", "Total scene geometry per frame.");
            }

            if (stats.peakAllocatedMemory > 0)
            {
                EditorGUILayout.Space(2);
                DrawStatRow("Peak Memory", $"{stats.peakAllocatedMemory / (1024f * 1024f):F1} MB", "Maximum memory used during the run.");
                DrawStatRow("GC Allocations", $"{stats.totalGcAllocated / (1024f * 1024f):F2} MB", "Total garbage created. Causes stutter when collected.");
            }

            EditorGUILayout.EndVertical();
        }

        void DrawStatRow(string label, string value, string tooltip)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), GUILayout.Width(160));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();
        }

        void StartBenchmarkInPlayMode()
        {
            activeRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();

            if (activeRunner == null)
            {
                var go = new GameObject("[PerformanceBenchmarkRunner]");
                activeRunner = go.AddComponent<PerformanceBenchmarkRunner>();
            }

            var so = new SerializedObject(activeRunner);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("benchmarkData").objectReferenceValue = benchmarkData;
            so.ApplyModifiedProperties();

            if (benchmarkData != null && benchmarkData.OnBenchmarkCompleted != null)
                benchmarkData.OnBenchmarkCompleted.OnRaised += OnRunFinishedSOAP;

            activeRunner.StartBenchmark();
        }

        void OnRunFinishedSOAP(BenchmarkStateData stateData)
        {
            lastReportPath = stateData.ReportFilePath;

            if (!string.IsNullOrEmpty(lastReportPath))
                lastReport = BenchmarkReport.LoadFromFile(lastReportPath);

            if (benchmarkData != null && benchmarkData.OnBenchmarkCompleted != null)
                benchmarkData.OnBenchmarkCompleted.OnRaised -= OnRunFinishedSOAP;

            RefreshHistory();
            Repaint();
        }

        // ════════════════════════════════════════════════
        // ── History Tab ─────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawHistoryTab()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Benchmark History", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
                RefreshHistory();
            if (GUILayout.Button("Rebuild Index", GUILayout.Width(95)))
            {
                int count = BenchmarkHistory.RebuildIndex(GetOutputFolder());
                RefreshHistory();
                Debug.Log($"[Benchmark] Index rebuilt: {count} reports found.");
            }
            EditorGUILayout.EndHorizontal();

            if (historyEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No benchmark snapshots yet.\n\n" +
                    "Every time you run a benchmark from the Run tab, the results are automatically " +
                    "saved here. You can then tag runs (e.g., 'baseline', 'after-optimization') " +
                    "and compare any two runs side-by-side in the Compare tab.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"{historyEntries.Count} snapshots saved", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            historyScroll = EditorGUILayout.BeginScrollView(historyScroll);

            for (int i = 0; i < historyEntries.Count; i++)
            {
                var e = historyEntries[i];
                DrawHistoryEntry(e, i);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawHistoryEntry(BenchmarkHistory.IndexEntry e, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // ── Header row: label + tag + branch ──────
            EditorGUILayout.BeginHorizontal();

            string displayLabel = string.IsNullOrEmpty(e.label) ? "(untitled)" : e.label;
            EditorGUILayout.LabelField(displayLabel, EditorStyles.boldLabel, GUILayout.Width(140));

            if (!string.IsNullOrEmpty(e.tag))
            {
                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.6f, 1f);
                GUILayout.Label(e.tag, EditorStyles.miniButton, GUILayout.Width(80));
                GUI.backgroundColor = prevBg;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{e.gitBranch}/{e.gitCommitHash}", EditorStyles.miniLabel, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();

            // ── Stats row ─────────────────────────────
            string date = e.timestamp?.Length > 19 ? e.timestamp[..19].Replace("T", " ") : e.timestamp ?? "?";
            EditorGUILayout.LabelField(
                $"{date}  |  {e.sceneName}  |  {e.totalFrames} frames  |  " +
                $"FPS: {e.avgFps:F1} (p1: {e.p1Fps:F1})  |  Frame: {e.avgFrameTimeMs:F1}ms (p99: {e.p99FrameTimeMs:F1}ms)",
                EditorStyles.miniLabel);

            // ── Action row ────────────────────────────
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Baseline", GUILayout.Width(65)))
            {
                baselineIndex = index;
                TryCompare();
                activeTab = Tab.Compare;
            }
            if (GUILayout.Button("Current", GUILayout.Width(60)))
            {
                currentIndex = index;
                TryCompare();
                activeTab = Tab.Compare;
            }

            // Tag editing
            if (tagEditId == e.reportId)
            {
                tagEditValue = EditorGUILayout.TextField(tagEditValue, GUILayout.Width(80));
                if (GUILayout.Button("Save", GUILayout.Width(40)))
                {
                    BenchmarkHistory.TagReport(e.reportId, tagEditValue, GetOutputFolder());
                    tagEditId = null;
                    RefreshHistory();
                }
                if (GUILayout.Button("X", GUILayout.Width(20)))
                    tagEditId = null;
            }
            else
            {
                if (GUILayout.Button("Tag", GUILayout.Width(35)))
                {
                    tagEditId = e.reportId;
                    tagEditValue = e.tag ?? "";
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("JSON", GUILayout.Width(40)))
            {
                if (File.Exists(e.filePath))
                    EditorUtility.RevealInFinder(e.filePath);
            }
            if (GUILayout.Button("Del", GUILayout.Width(30)))
            {
                if (EditorUtility.DisplayDialog("Delete Snapshot",
                    $"Delete benchmark snapshot \"{e.label}\" ({e.timestamp})?", "Delete", "Cancel"))
                {
                    BenchmarkHistory.RemoveEntry(e.reportId, GetOutputFolder());
                    RefreshHistory();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // ════════════════════════════════════════════════
        // ── Compare Tab ─────────────────────────────────
        // ════════════════════════════════════════════════

        void DrawCompareTab()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Compare Two Snapshots", EditorStyles.boldLabel);

            if (historyEntries.Count < 2)
            {
                EditorGUILayout.HelpBox(
                    "Run at least 2 benchmarks to compare them.\n\n" +
                    "Typical workflow:\n" +
                    "1. Run a benchmark before making changes (tag it 'baseline')\n" +
                    "2. Make your optimization changes\n" +
                    "3. Run another benchmark\n" +
                    "4. Compare the two to see what improved or regressed",
                    MessageType.Info);
                return;
            }

            string[] names = historyEntries
                .Select((e, i) =>
                {
                    string tag = string.IsNullOrEmpty(e.tag) ? "" : $" [{e.tag}]";
                    string date = e.timestamp?.Length > 10 ? e.timestamp[..10] : "?";
                    return $"{e.label}{tag} ({e.gitBranch} {date})";
                })
                .ToArray();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Baseline (before):", GUILayout.Width(130));
            int newBaseline = EditorGUILayout.Popup(baselineIndex, names);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current (after):", GUILayout.Width(130));
            int newCurrent = EditorGUILayout.Popup(currentIndex, names);
            EditorGUILayout.EndHorizontal();

            if (newBaseline != baselineIndex || newCurrent != currentIndex)
            {
                baselineIndex = newBaseline;
                currentIndex = newCurrent;
                TryCompare();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Compare", GUILayout.Height(24)))
                TryCompare();

            if (comparisonResult == null || string.IsNullOrEmpty(comparisonText))
            {
                EditorGUILayout.HelpBox(
                    "Pick a 'Baseline' and a 'Current' snapshot from the dropdowns above, " +
                    "then click Compare.\n\nTip: You can also set these from the History tab by " +
                    "clicking the Baseline/Current buttons on any snapshot.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            // ── Summary badges ─────────────────────────
            EditorGUILayout.BeginHorizontal();
            DrawBadge($"{comparisonResult.improvements} Improved", new Color(0.2f, 0.7f, 0.3f));
            DrawBadge($"{comparisonResult.neutral} Unchanged", new Color(0.6f, 0.6f, 0.6f));
            DrawBadge($"{comparisonResult.regressions} Regressed", new Color(0.85f, 0.25f, 0.25f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy Text Report", GUILayout.Width(120)))
                GUIUtility.systemCopyBuffer = comparisonText;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // ── Detailed comparison table ──────────────
            compareScroll = EditorGUILayout.BeginScrollView(compareScroll);

            var headerStyle = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Metric", headerStyle, GUILayout.Width(200));
            EditorGUILayout.LabelField("Baseline", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("Current", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("Delta", headerStyle, GUILayout.Width(80));
            EditorGUILayout.LabelField("%", headerStyle, GUILayout.Width(60));
            EditorGUILayout.LabelField("Verdict", headerStyle, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

            foreach (var d in comparisonResult.deltas)
            {
                Color rowColor = d.verdict switch
                {
                    MetricDelta.Verdict.Improved => new Color(0.2f, 0.8f, 0.3f, 0.15f),
                    MetricDelta.Verdict.Regressed => new Color(0.9f, 0.2f, 0.2f, 0.15f),
                    _ => Color.clear
                };

                var rect = EditorGUILayout.BeginHorizontal();
                if (rowColor != Color.clear)
                    EditorGUI.DrawRect(rect, rowColor);

                EditorGUILayout.LabelField(d.metricName, EditorStyles.miniLabel, GUILayout.Width(200));
                EditorGUILayout.LabelField(d.baselineValue.ToString("F2"), EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField(d.currentValue.ToString("F2"), EditorStyles.miniLabel, GUILayout.Width(80));
                string sign = d.absoluteDelta >= 0 ? "+" : "";
                EditorGUILayout.LabelField($"{sign}{d.absoluteDelta:F2}", EditorStyles.miniLabel, GUILayout.Width(80));
                EditorGUILayout.LabelField($"{sign}{d.percentDelta:F1}%", EditorStyles.miniLabel, GUILayout.Width(60));

                string verdictLabel = d.verdict switch
                {
                    MetricDelta.Verdict.Improved => "BETTER",
                    MetricDelta.Verdict.Regressed => "WORSE",
                    _ => "~"
                };
                EditorGUILayout.LabelField(verdictLabel, EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawBadge(string text, Color color)
        {
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, EditorStyles.miniButton, GUILayout.Width(110));
            GUI.backgroundColor = prevBg;
        }

        void TryCompare()
        {
            if (baselineIndex < 0 || currentIndex < 0) return;
            if (baselineIndex >= historyEntries.Count || currentIndex >= historyEntries.Count) return;
            if (baselineIndex == currentIndex) return;

            var baselineReport = BenchmarkHistory.LoadReport(historyEntries[baselineIndex]);
            var currentReport = BenchmarkHistory.LoadReport(historyEntries[currentIndex]);

            if (baselineReport == null || currentReport == null)
            {
                comparisonResult = null;
                comparisonText = null;
                return;
            }

            comparisonResult = BenchmarkComparer.Compare(baselineReport, currentReport);
            comparisonText = BenchmarkComparer.FormatAsText(comparisonResult);
        }

        // ── Data Access ─────────────────────────────────

        string GetOutputFolder()
        {
            if (config != null && !string.IsNullOrEmpty(config.OutputFolder))
                return config.OutputFolder;
            return "Benchmarks";
        }

        void RefreshHistory()
        {
            historyEntries = BenchmarkHistory.GetAll(GetOutputFolder());

            if (baselineIndex >= historyEntries.Count) baselineIndex = -1;
            if (currentIndex >= historyEntries.Count) currentIndex = -1;
        }
    }
}

#endif
