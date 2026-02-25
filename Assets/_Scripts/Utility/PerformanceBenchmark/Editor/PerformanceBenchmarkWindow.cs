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
        enum Tab { Run, Reports, Compare }
        Tab activeTab = Tab.Run;

        // ── Run tab ─────────────────────────────────────
        BenchmarkConfigSO config;
        BenchmarkDataSO benchmarkData;
        PerformanceBenchmarkRunner activeRunner;
        string lastReportPath;

        // ── Reports tab ─────────────────────────────────
        Vector2 reportsScroll;
        List<ReportEntry> reportEntries = new();
        string reportsFolder;

        // ── Compare tab ─────────────────────────────────
        Vector2 compareScroll;
        int baselineIndex = -1;
        int currentIndex = -1;
        BenchmarkComparer.ComparisonResult comparisonResult;
        string comparisonText;

        struct ReportEntry
        {
            public string filePath;
            public string fileName;
            public BenchmarkReport report;
        }

        [MenuItem("FrogletTools/Performance Benchmark", false, 20)]
        public static void Open()
        {
            var window = GetWindow<PerformanceBenchmarkWindow>("Performance Benchmark");
            window.minSize = new Vector2(520, 400);
            window.Show();
        }

        void OnEnable()
        {
            RefreshReportsList();
        }

        void OnGUI()
        {
            DrawTabs();

            switch (activeTab)
            {
                case Tab.Run: DrawRunTab(); break;
                case Tab.Reports: DrawReportsTab(); break;
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
            if (GUILayout.Toggle(activeTab == Tab.Reports, "Reports", EditorStyles.toolbarButton))
                activeTab = Tab.Reports;
            if (GUILayout.Toggle(activeTab == Tab.Compare, "Compare", EditorStyles.toolbarButton))
                activeTab = Tab.Compare;
            EditorGUILayout.EndHorizontal();
        }

        // ── Run Tab ─────────────────────────────────────

        void DrawRunTab()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Run Benchmark", EditorStyles.boldLabel);

            config = (BenchmarkConfigSO)EditorGUILayout.ObjectField(
                "Config", config, typeof(BenchmarkConfigSO), false);

            benchmarkData = (BenchmarkDataSO)EditorGUILayout.ObjectField(
                "Data Container", benchmarkData, typeof(BenchmarkDataSO), false);

            if (config == null)
            {
                EditorGUILayout.HelpBox("Assign a BenchmarkConfigSO to get started.\nCreate one via: Assets > Create > CosmicShore > Tools > Benchmark Config", MessageType.Info);
                return;
            }

            if (benchmarkData == null)
            {
                EditorGUILayout.HelpBox("Assign a BenchmarkDataSO for SOAP event integration.\nCreate one via: Assets > Create > ScriptableObjects > DataContainers > Benchmark Data", MessageType.Warning);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Settings Preview", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Warmup", $"{config.WarmupDuration}s");
            EditorGUILayout.LabelField("Sample Duration", $"{config.SampleDuration}s");
            EditorGUILayout.LabelField("Label", string.IsNullOrEmpty(config.BenchmarkLabel) ? "(none)" : config.BenchmarkLabel);
            EditorGUILayout.LabelField("Rendering Stats", config.CaptureRenderingStats ? "Yes" : "No");
            EditorGUILayout.LabelField("Memory Stats", config.CaptureMemoryStats ? "Yes" : "No");
            EditorGUILayout.LabelField("Physics Stats", config.CapturePhysicsStats ? "Yes" : "No");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(8);

            bool isPlaying = Application.isPlaying;

            if (activeRunner != null && activeRunner.IsRunning)
            {
                float progress = activeRunner.Progress;
                var rect = EditorGUILayout.GetControlRect(false, 20);
                EditorGUI.ProgressBar(rect, progress, $"Benchmarking... {progress * 100:F0}%");

                // Live stats from SOAP data container
                if (benchmarkData != null && benchmarkData.IsSampling)
                {
                    EditorGUILayout.LabelField(
                        $"Frames: {benchmarkData.FramesCaptured}  |  Label: {benchmarkData.ActiveLabel}",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Stop Early"))
                {
                    activeRunner.StopBenchmark();
                }
            }
            else
            {
                if (!isPlaying)
                {
                    EditorGUILayout.HelpBox("Enter Play Mode to run a benchmark.", MessageType.Warning);
                }

                using (new EditorGUI.DisabledScope(!isPlaying))
                {
                    if (GUILayout.Button("Start Benchmark", GUILayout.Height(30)))
                    {
                        StartBenchmarkInPlayMode();
                    }
                }
            }

            if (!string.IsNullOrEmpty(lastReportPath))
            {
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Last Report", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(lastReportPath, EditorStyles.miniLabel, GUILayout.Height(16));

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Open in File Explorer"))
                {
                    EditorUtility.RevealInFinder(lastReportPath);
                }
                if (GUILayout.Button("Refresh Reports List"))
                {
                    RefreshReportsList();
                    activeTab = Tab.Reports;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        void StartBenchmarkInPlayMode()
        {
            // Find or create the runner in the scene
            activeRunner = FindFirstObjectByType<PerformanceBenchmarkRunner>();

            if (activeRunner == null)
            {
                var go = new GameObject("[PerformanceBenchmarkRunner]");
                activeRunner = go.AddComponent<PerformanceBenchmarkRunner>();
            }

            // Inject config and SOAP data container via serialized fields
            var so = new SerializedObject(activeRunner);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("benchmarkData").objectReferenceValue = benchmarkData;
            so.ApplyModifiedProperties();

            // Subscribe to SOAP completion event if available
            if (benchmarkData != null && benchmarkData.OnBenchmarkCompleted != null)
                benchmarkData.OnBenchmarkCompleted.OnRaised += OnRunFinishedSOAP;

            activeRunner.StartBenchmark();
        }

        void OnRunFinishedSOAP(BenchmarkStateData stateData)
        {
            lastReportPath = stateData.ReportFilePath;

            if (benchmarkData != null && benchmarkData.OnBenchmarkCompleted != null)
                benchmarkData.OnBenchmarkCompleted.OnRaised -= OnRunFinishedSOAP;

            RefreshReportsList();
            Repaint();
        }

        // ── Reports Tab ─────────────────────────────────

        void DrawReportsTab()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Saved Reports", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                RefreshReportsList();
            EditorGUILayout.EndHorizontal();

            if (reportEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No benchmark reports found.\nRun a benchmark first, or check the output folder.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Folder: {GetBenchmarksFolder()}", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            reportsScroll = EditorGUILayout.BeginScrollView(reportsScroll);

            for (int i = 0; i < reportEntries.Count; i++)
            {
                var entry = reportEntries[i];
                var r = entry.report;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{r.label}", EditorStyles.boldLabel, GUILayout.Width(160));
                EditorGUILayout.LabelField($"{r.gitBranch}/{r.gitCommitHash}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField($"Scene: {r.sceneName}  |  {r.timestamp}  |  {r.statistics?.totalFrames ?? 0} frames", EditorStyles.miniLabel);

                if (r.statistics != null)
                {
                    EditorGUILayout.LabelField(
                        $"FPS avg:{r.statistics.avgFps:F1} min:{r.statistics.minFps:F1} p1:{r.statistics.p1Fps:F1}  |  " +
                        $"Frame avg:{r.statistics.avgFrameTimeMs:F1}ms p95:{r.statistics.p95FrameTimeMs:F1}ms  |  " +
                        $"Draw:{r.statistics.avgDrawCalls:F0} Batch:{r.statistics.avgBatches:F0}",
                        EditorStyles.miniLabel);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Set as Baseline", GUILayout.Width(110)))
                {
                    baselineIndex = i;
                    TryCompare();
                    activeTab = Tab.Compare;
                }
                if (GUILayout.Button("Set as Current", GUILayout.Width(110)))
                {
                    currentIndex = i;
                    TryCompare();
                    activeTab = Tab.Compare;
                }
                if (GUILayout.Button("Open JSON", GUILayout.Width(80)))
                {
                    EditorUtility.RevealInFinder(entry.filePath);
                }
                if (GUILayout.Button("Delete", GUILayout.Width(55)))
                {
                    if (EditorUtility.DisplayDialog("Delete Report",
                        $"Delete benchmark report \"{entry.fileName}\"?", "Delete", "Cancel"))
                    {
                        File.Delete(entry.filePath);
                        RefreshReportsList();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Compare Tab ─────────────────────────────────

        void DrawCompareTab()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Compare Two Reports", EditorStyles.boldLabel);

            if (reportEntries.Count < 2)
            {
                EditorGUILayout.HelpBox("Need at least 2 reports to compare. Run benchmarks from the Run tab.", MessageType.Info);
                return;
            }

            string[] reportNames = reportEntries
                .Select((e, i) => $"[{i}] {e.report.label} ({e.report.gitBranch}/{e.report.gitCommitHash})")
                .ToArray();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Baseline (before):", GUILayout.Width(130));
            int newBaseline = EditorGUILayout.Popup(baselineIndex, reportNames);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Current (after):", GUILayout.Width(130));
            int newCurrent = EditorGUILayout.Popup(currentIndex, reportNames);
            EditorGUILayout.EndHorizontal();

            if (newBaseline != baselineIndex || newCurrent != currentIndex)
            {
                baselineIndex = newBaseline;
                currentIndex = newCurrent;
                TryCompare();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Compare", GUILayout.Height(24)))
            {
                TryCompare();
            }

            if (comparisonResult == null || string.IsNullOrEmpty(comparisonText))
            {
                EditorGUILayout.HelpBox("Select a baseline and current report, then click Compare.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(4);

            // Summary badges
            EditorGUILayout.BeginHorizontal();
            DrawBadge($"{comparisonResult.improvements} Improved", new Color(0.2f, 0.7f, 0.3f));
            DrawBadge($"{comparisonResult.neutral} Unchanged", new Color(0.6f, 0.6f, 0.6f));
            DrawBadge($"{comparisonResult.regressions} Regressed", new Color(0.85f, 0.25f, 0.25f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(130)))
            {
                GUIUtility.systemCopyBuffer = comparisonText;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            // Detailed table
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
            if (baselineIndex >= reportEntries.Count || currentIndex >= reportEntries.Count) return;
            if (baselineIndex == currentIndex) return;

            comparisonResult = BenchmarkComparer.Compare(
                reportEntries[baselineIndex].report,
                reportEntries[currentIndex].report);

            comparisonText = BenchmarkComparer.FormatAsText(comparisonResult);
        }

        // ── Report Discovery ────────────────────────────

        string GetBenchmarksFolder()
        {
            string folder = "Benchmarks";
            if (config != null && !string.IsNullOrEmpty(config.OutputFolder))
                folder = config.OutputFolder;
            return Path.Combine(Application.persistentDataPath, folder);
        }

        void RefreshReportsList()
        {
            reportEntries.Clear();
            reportsFolder = GetBenchmarksFolder();

            if (!Directory.Exists(reportsFolder))
                return;

            var files = Directory.GetFiles(reportsFolder, "*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            foreach (var file in files)
            {
                try
                {
                    var report = BenchmarkReport.LoadFromFile(file);
                    if (report != null)
                    {
                        reportEntries.Add(new ReportEntry
                        {
                            filePath = file,
                            fileName = Path.GetFileName(file),
                            report = report
                        });
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Benchmark] Failed to load report {file}: {e.Message}");
                }
            }

            // Reset comparison indices if out of range
            if (baselineIndex >= reportEntries.Count) baselineIndex = -1;
            if (currentIndex >= reportEntries.Count) currentIndex = -1;
        }
    }
}

#endif
