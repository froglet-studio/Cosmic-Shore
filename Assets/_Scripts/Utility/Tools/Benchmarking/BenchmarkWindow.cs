#if UNITY_EDITOR

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Utility.Tools.Benchmarking
{
    public class BenchmarkWindow : EditorWindow
    {
        // ── Config ──────────────────────────────────────────────────────────
        string _label = "Benchmark";
        float _warmupSeconds = 2f;
        float _durationSeconds = 10f;

        // ── State ───────────────────────────────────────────────────────────
        PerformanceSampler _activeSampler;
        BenchmarkReport _lastReport;
        BenchmarkReport _baselineReport;
        Vector2 _scrollPos;
        int _selectedTab;

        // ── Report browser ──────────────────────────────────────────────────
        string[] _savedPaths = Array.Empty<string>();
        string[] _savedNames = Array.Empty<string>();
        int _selectedBaselineIdx = -1;
        int _selectedCurrentIdx = -1;

        // ── Comparison cache ────────────────────────────────────────────────
        List<ComparisonLine> _comparisonLines;
        BenchmarkReport _comparedBaseline;
        BenchmarkReport _comparedCurrent;

        // ── Palette (consistent with LogControlWindow) ──────────────────────
        static readonly Color BannerBg       = new(0.22f, 0.20f, 0.30f, 1f);
        static readonly Color AccentColor    = new(0.55f, 0.82f, 0.65f, 1f);
        static readonly Color SectionHeader  = new(0.20f, 0.19f, 0.26f, 1f);
        static readonly Color TextMuted      = new(0.58f, 0.56f, 0.65f, 1f);
        static readonly Color FooterBg       = new(0.14f, 0.13f, 0.18f, 1f);
        static readonly Color TabInactive    = new(0.18f, 0.17f, 0.22f, 1f);
        static readonly Color GoodColor      = new(0.35f, 0.78f, 0.48f, 1f);
        static readonly Color BadColor       = new(0.88f, 0.38f, 0.35f, 1f);
        static readonly Color NeutralColor   = new(0.72f, 0.72f, 0.72f, 1f);

        // ── Styles ──────────────────────────────────────────────────────────
        [NonSerialized] GUIStyle _bannerStyle;
        [NonSerialized] GUIStyle _sectionStyle;
        [NonSerialized] GUIStyle _mutedLabel;
        [NonSerialized] GUIStyle _tabStyle;
        [NonSerialized] GUIStyle _valueStyle;
        [NonSerialized] GUIStyle _metricLabel;
        [NonSerialized] bool _stylesBuilt;

        static readonly string[] TabLabels = { "Run", "Results", "Compare", "History" };
        static readonly Color[] TabColors =
        {
            new(0.55f, 0.82f, 0.65f, 1f),
            new(0.65f, 0.65f, 0.88f, 1f),
            new(0.88f, 0.72f, 0.55f, 1f),
            new(0.72f, 0.55f, 0.82f, 1f),
        };

        [MenuItem("FrogletTools/Benchmark", false, 10)]
        static void Open()
        {
            var w = GetWindow<BenchmarkWindow>("Benchmark");
            w.minSize = new Vector2(400, 500);
        }

        void OnEnable() => RefreshSavedReports();

        void OnInspectorUpdate()
        {
            if (_activeSampler != null)
                Repaint();
        }

        void BuildStyles()
        {
            if (_stylesBuilt) return;

            _bannerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 6, 6)
            };
            _bannerStyle.normal.textColor = AccentColor;

            _sectionStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                padding = new RectOffset(8, 0, 4, 4)
            };
            _sectionStyle.normal.textColor = AccentColor;

            _mutedLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 10 };
            _mutedLabel.normal.textColor = TextMuted;

            _tabStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _valueStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight
            };

            _metricLabel = new GUIStyle(EditorStyles.label) { fontSize = 11 };

            _stylesBuilt = true;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  MAIN GUI
        // ═════════════════════════════════════════════════════════════════════

        void OnGUI()
        {
            BuildStyles();

            // Banner
            var bannerRect = GUILayoutUtility.GetRect(0, 34, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bannerRect, BannerBg);
            GUI.Label(bannerRect, "Performance Benchmark", _bannerStyle);

            var lineRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(lineRect, AccentColor * 0.6f);

            // Tabs
            DrawTabBar();

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            GUILayout.Space(6);

            switch (_selectedTab)
            {
                case 0: DrawRunTab(); break;
                case 1: DrawResultsTab(); break;
                case 2: DrawCompareTab(); break;
                case 3: DrawHistoryTab(); break;
            }

            GUILayout.Space(8);
            EditorGUILayout.EndScrollView();

            // Footer
            var footerRect = GUILayoutUtility.GetRect(0, 18, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(footerRect, FooterBg);
            GUI.Label(footerRect, "Froglet Inc. — Cosmic Shore — Benchmark Tool", _mutedLabel);
        }

        void DrawTabBar()
        {
            const float tabHeight = 28;
            const float gap = 2;
            const float pad = 4;

            var barRect = GUILayoutUtility.GetRect(0, tabHeight + pad * 2, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(barRect, new Color(0.16f, 0.15f, 0.20f, 1f));

            float w = (barRect.width - pad * 2 - gap * (TabLabels.Length - 1)) / TabLabels.Length;

            for (int i = 0; i < TabLabels.Length; i++)
            {
                var rect = new Rect(barRect.x + pad + i * (w + gap), barRect.y + pad, w, tabHeight);
                bool selected = i == _selectedTab;
                bool hover = rect.Contains(Event.current.mousePosition);

                Color bg = selected ? TabColors[i] : hover ? Color.Lerp(TabInactive, TabColors[i], 0.45f) : Color.Lerp(TabInactive, TabColors[i], 0.15f);
                EditorGUI.DrawRect(rect, bg);

                if (selected)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 3, rect.width, 3), Color.white * 0.9f);

                _tabStyle.normal.textColor = selected ? Color.white : new Color(0.88f, 0.86f, 0.94f);
                GUI.Label(rect, TabLabels[i], _tabStyle);

                if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
                {
                    _selectedTab = i;
                    _scrollPos = Vector2.zero;
                    Event.current.Use();
                    Repaint();
                }
            }

            if (Event.current.type == EventType.MouseMove) Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: RUN
        // ═════════════════════════════════════════════════════════════════════

        void DrawRunTab()
        {
            DrawSection("Configuration");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Label", GUILayout.Width(80));
            _label = EditorGUILayout.TextField(_label);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Warmup (s)", GUILayout.Width(80));
            _warmupSeconds = EditorGUILayout.FloatField(_warmupSeconds, GUILayout.Width(60));
            GUILayout.Space(16);
            GUILayout.Label("Duration (s)", GUILayout.Width(80));
            _durationSeconds = EditorGUILayout.FloatField(_durationSeconds, GUILayout.Width(60));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);

            bool isPlaying = Application.isPlaying;
            bool isSampling = _activeSampler != null && (_activeSampler.IsSampling || _activeSampler.IsWarming);

            if (!isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to run a benchmark.", MessageType.Info);
                return;
            }

            // Status
            if (isSampling)
            {
                GUILayout.Space(4);
                DrawSection("Status");

                string status;
                float progress;
                if (_activeSampler.IsWarming)
                {
                    status = $"Warming up... {_activeSampler.WarmupRemaining:F1}s remaining";
                    progress = 1f - (_activeSampler.WarmupRemaining / _warmupSeconds);
                }
                else
                {
                    float elapsed = _activeSampler.ElapsedSampleTime;
                    status = _durationSeconds > 0
                        ? $"Sampling... {elapsed:F1}s / {_durationSeconds:F0}s  ({_activeSampler.FramesCaptured} frames)"
                        : $"Sampling... {elapsed:F1}s  ({_activeSampler.FramesCaptured} frames)";
                    progress = _durationSeconds > 0 ? elapsed / _durationSeconds : -1f;
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label(status);
                EditorGUILayout.EndHorizontal();

                if (progress >= 0f)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(12);
                    var rect = GUILayoutUtility.GetRect(0, 16, GUILayout.ExpandWidth(true));
                    rect.width -= 12;
                    EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.2f));
                    var fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(progress), rect.height);
                    EditorGUI.DrawRect(fill, AccentColor);
                    EditorGUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(8);

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUI.enabled = !isSampling;
            if (GUILayout.Button("Start Benchmark", GUILayout.Height(30)))
            {
                StartBenchmark();
            }
            GUI.enabled = isSampling;
            if (GUILayout.Button("Stop", GUILayout.Height(30), GUILayout.Width(60)))
            {
                var report = _activeSampler.StopSampling();
                HandleReport(report);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: RESULTS
        // ═════════════════════════════════════════════════════════════════════

        void DrawResultsTab()
        {
            if (_lastReport == null)
            {
                EditorGUILayout.HelpBox("No benchmark results yet. Run a benchmark first.", MessageType.Info);
                return;
            }

            DrawSection($"Results — {_lastReport.Label}");
            DrawReportDetails(_lastReport);

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (GUILayout.Button("Save Report"))
            {
                string path = _lastReport.Save();
                CSDebug.Log($"[Benchmark] Saved to {path}");
                EditorUtility.DisplayDialog("Benchmark Saved", $"Report saved to:\n{path}", "OK");
                RefreshSavedReports();
            }
            if (GUILayout.Button("Set as Baseline"))
            {
                _baselineReport = _lastReport;
                CSDebug.Log("[Benchmark] Current result set as baseline.");
            }
            EditorGUILayout.EndHorizontal();

            // Quick comparison with baseline
            if (_baselineReport != null && _baselineReport != _lastReport)
            {
                GUILayout.Space(12);
                DrawSection("Quick Comparison vs Baseline");
                DrawComparison(_baselineReport, _lastReport);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: COMPARE
        // ═════════════════════════════════════════════════════════════════════

        void DrawCompareTab()
        {
            DrawSection("Compare Two Reports");

            if (_savedPaths.Length == 0)
            {
                EditorGUILayout.HelpBox("No saved reports found. Run and save a benchmark first.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Baseline", GUILayout.Width(60));
            _selectedBaselineIdx = EditorGUILayout.Popup(_selectedBaselineIdx, _savedNames);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("Current", GUILayout.Width(60));
            _selectedCurrentIdx = EditorGUILayout.Popup(_selectedCurrentIdx, _savedNames);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (GUILayout.Button("Compare", GUILayout.Height(28)))
            {
                if (_selectedBaselineIdx >= 0 && _selectedCurrentIdx >= 0)
                {
                    _comparedBaseline = BenchmarkReport.Load(_savedPaths[_selectedBaselineIdx]);
                    _comparedCurrent = BenchmarkReport.Load(_savedPaths[_selectedCurrentIdx]);
                    _comparisonLines = BenchmarkReport.Compare(_comparedBaseline, _comparedCurrent);
                }
            }
            if (GUILayout.Button("Refresh", GUILayout.Width(60), GUILayout.Height(28)))
                RefreshSavedReports();
            EditorGUILayout.EndHorizontal();

            if (_comparisonLines != null && _comparedBaseline != null && _comparedCurrent != null)
            {
                GUILayout.Space(8);
                DrawSection($"{_comparedBaseline.Label} vs {_comparedCurrent.Label}");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"Baseline: {_comparedBaseline.Timestamp}  ({_comparedBaseline.GitCommit})", _mutedLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label($"Current:  {_comparedCurrent.Timestamp}  ({_comparedCurrent.GitCommit})", _mutedLabel);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(4);
                DrawComparisonLines(_comparisonLines);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  TAB: HISTORY
        // ═════════════════════════════════════════════════════════════════════

        void DrawHistoryTab()
        {
            DrawSection("Saved Reports");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            if (GUILayout.Button("Refresh"))
                RefreshSavedReports();
            if (GUILayout.Button("Open Folder") && Directory.Exists(ReportsDir))
                EditorUtility.RevealInFinder(ReportsDir);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            if (_savedPaths.Length == 0)
            {
                EditorGUILayout.HelpBox("No saved reports. Reports are saved to /BenchmarkReports/ in the project root.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _savedPaths.Length; i++)
            {
                var report = BenchmarkReport.Load(_savedPaths[i]);
                if (report == null) continue;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);

                GUILayout.Label($"{report.Label}", EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label($"{report.Timestamp}", GUILayout.Width(140));
                GUILayout.Label($"{report.SceneName}", GUILayout.Width(100));
                GUILayout.Label($"{report.AvgFps:F1} fps", GUILayout.Width(70));
                GUILayout.Label($"({report.GitCommit})", _mutedLabel, GUILayout.Width(70));

                if (GUILayout.Button("Load", GUILayout.Width(45)))
                {
                    _lastReport = report;
                    _selectedTab = 1;
                    _scrollPos = Vector2.zero;
                }
                if (GUILayout.Button("Base", GUILayout.Width(40)))
                {
                    _baselineReport = report;
                }
                if (GUILayout.Button("Del", GUILayout.Width(35)))
                {
                    if (EditorUtility.DisplayDialog("Delete Report", $"Delete {_savedNames[i]}?", "Delete", "Cancel"))
                    {
                        File.Delete(_savedPaths[i]);
                        RefreshSavedReports();
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  DRAWING HELPERS
        // ═════════════════════════════════════════════════════════════════════

        void DrawSection(string title)
        {
            var rect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, SectionHeader);
            var accent = new Rect(rect.x, rect.y, 4, rect.height);
            EditorGUI.DrawRect(accent, AccentColor);
            var labelRect = new Rect(rect.x + 12, rect.y, rect.width - 12, rect.height);
            _sectionStyle.normal.textColor = AccentColor;
            GUI.Label(labelRect, title, _sectionStyle);
            GUILayout.Space(4);
        }

        void DrawReportDetails(BenchmarkReport r)
        {
            DrawMetric("Scene", r.SceneName);
            DrawMetric("Duration", $"{r.DurationSeconds:F1}s ({r.TotalFrames} frames)");
            DrawMetric("Git Commit", r.GitCommit);

            GUILayout.Space(6);
            DrawSubSection("Frame Time");
            DrawMetric("Average", $"{r.AvgFrameTimeMs:F2} ms");
            DrawMetric("Median", $"{r.MedianFrameTimeMs:F2} ms");
            DrawMetric("Min / Max", $"{r.MinFrameTimeMs:F2} / {r.MaxFrameTimeMs:F2} ms");
            DrawMetric("P1 / P5", $"{r.P1FrameTimeMs:F2} / {r.P5FrameTimeMs:F2} ms");
            DrawMetric("P95 / P99", $"{r.P95FrameTimeMs:F2} / {r.P99FrameTimeMs:F2} ms");
            DrawMetric("Std Dev", $"{r.StdDevFrameTimeMs:F2} ms");

            GUILayout.Space(6);
            DrawSubSection("FPS");
            DrawMetric("Average", $"{r.AvgFps:F1}");
            DrawMetric("P1 (worst 1%)", $"{r.P1Fps:F1}");
            DrawMetric("P5 (worst 5%)", $"{r.P5Fps:F1}");
            DrawMetric("Jank %", $"{r.JankPercent:F1}%");

            if (r.AvgGpuTimeMs >= 0)
            {
                GUILayout.Space(6);
                DrawSubSection("GPU");
                DrawMetric("Avg GPU Time", $"{r.AvgGpuTimeMs:F2} ms");
            }

            GUILayout.Space(6);
            DrawSubSection("Memory / GC");
            DrawMetric("GC Alloc Total", $"{r.TotalGcAllocBytes / 1024f:F0} KB");
            DrawMetric("GC Collections", $"{r.GcCollectCount}");
            DrawMetric("Peak Used Memory", $"{r.PeakUsedMemoryBytes / (1024f * 1024f):F1} MB");

            if (r.AvgDrawCalls >= 0)
            {
                GUILayout.Space(6);
                DrawSubSection("Rendering");
                DrawMetric("Avg Draw Calls", $"{r.AvgDrawCalls}");
                DrawMetric("Avg SetPass Calls", $"{r.AvgSetPassCalls}");
                DrawMetric("Avg Triangles", $"{r.AvgTriangles / 1000f:F1}K");
                DrawMetric("Avg Vertices", $"{r.AvgVertices / 1000f:F1}K");
            }
        }

        void DrawMetric(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(label, _metricLabel, GUILayout.Width(160));
            GUILayout.Label(value, _valueStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        void DrawSubSection(string title)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 };
            style.normal.textColor = Color.Lerp(AccentColor, Color.white, 0.4f);
            GUILayout.Label("— " + title, style);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2);
        }

        void DrawComparison(BenchmarkReport baseline, BenchmarkReport current)
        {
            var lines = BenchmarkReport.Compare(baseline, current);
            DrawComparisonLines(lines);
        }

        void DrawComparisonLines(List<ComparisonLine> lines)
        {
            // Header row
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label("Metric", EditorStyles.boldLabel, GUILayout.Width(140));
            GUILayout.Label("Baseline", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Current", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Delta", EditorStyles.boldLabel, GUILayout.Width(80));
            GUILayout.Label("Change", EditorStyles.boldLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            foreach (var line in lines)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16);

                GUILayout.Label(line.Label, _metricLabel, GUILayout.Width(140));
                GUILayout.Label($"{line.Baseline:F2}{line.Unit}", GUILayout.Width(80));
                GUILayout.Label($"{line.Current:F2}{line.Unit}", GUILayout.Width(80));

                // Delta with color
                Color deltaColor = line.Improved ? GoodColor : line.Regressed ? BadColor : NeutralColor;
                var deltaStyle = new GUIStyle(EditorStyles.label) { fontSize = 11 };
                deltaStyle.normal.textColor = deltaColor;

                string sign = line.Delta > 0 ? "+" : "";
                GUILayout.Label($"{sign}{line.Delta:F2}", deltaStyle, GUILayout.Width(80));
                GUILayout.Label($"{sign}{line.DeltaPercent:F1}%", deltaStyle, GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  LOGIC
        // ═════════════════════════════════════════════════════════════════════

        void StartBenchmark()
        {
            if (_activeSampler != null)
                DestroyImmediate(_activeSampler.gameObject);

            var go = new GameObject("[Benchmark Sampler]");
            go.hideFlags = HideFlags.DontSave;
            _activeSampler = go.AddComponent<PerformanceSampler>();
            _activeSampler.Configure(_label, _warmupSeconds, _durationSeconds);
            _activeSampler.OnSamplingComplete += HandleReport;
            _activeSampler.StartSampling();
        }

        void HandleReport(BenchmarkReport report)
        {
            if (report == null) return;
            _lastReport = report;
            _selectedTab = 1;
            _scrollPos = Vector2.zero;
            Repaint();

            CSDebug.Log($"[Benchmark] Complete — {report.AvgFps:F1} avg FPS, {report.P99FrameTimeMs:F2}ms P99, {report.TotalFrames} frames in {report.DurationSeconds:F1}s");
        }

        static readonly string ReportsDir = Path.Combine(Application.dataPath, "..", "BenchmarkReports");

        void RefreshSavedReports()
        {
            _savedPaths = BenchmarkReport.GetSavedReportPaths();
            Array.Sort(_savedPaths);
            Array.Reverse(_savedPaths); // newest first
            _savedNames = _savedPaths.Select(p => Path.GetFileNameWithoutExtension(p)).ToArray();
            _selectedBaselineIdx = Mathf.Clamp(_selectedBaselineIdx, -1, _savedPaths.Length - 1);
            _selectedCurrentIdx = Mathf.Clamp(_selectedCurrentIdx, -1, _savedPaths.Length - 1);
        }
    }
}

#endif
